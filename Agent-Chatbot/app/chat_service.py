import asyncio
import json
import logging
from collections import OrderedDict
from collections.abc import AsyncIterator
from dataclasses import dataclass, field
from pathlib import Path
from typing import Any

from copilot import CopilotClient
from copilot.session import PermissionHandler
from copilot.session_events import (
    AssistantMessageDeltaData,
    SessionErrorData,
    ToolExecutionCompleteData,
    ToolExecutionStartData,
)

from app.agent import TOOL_NAMES, TURN_TIMEOUT_SECONDS
from app.config import Settings
from app.dotnet_client import DotnetClient
from app.prompt import build_system_prompt
from app.tools import build_tools

log = logging.getLogger("ops_agent.chat")

_END = "__end__"


class SessionNotFound(Exception):
    pass


class SessionBusy(Exception):
    pass


@dataclass
class _Conversation:
    session: Any
    busy: bool = False


@dataclass
class Turn:
    session_id: str
    queue: asyncio.Queue = field(default_factory=asyncio.Queue)

    async def events(self) -> AsyncIterator[tuple[str, dict]]:
        while True:
            kind, payload = await self.queue.get()
            if kind == _END:
                return
            yield kind, payload


class ChatService:
    def __init__(self, settings: Settings) -> None:
        self._settings = settings
        self._dotnet = DotnetClient(settings)
        self._client: CopilotClient | None = None
        self._catalog: dict = {}
        self._tools: list = []
        self._open: OrderedDict[str, _Conversation] = OrderedDict()
        self._open_lock = asyncio.Lock()

    async def start(self) -> None:
        self._catalog = await self._dotnet.get_catalog()
        names = [e["name"] for e in self._catalog["entities"]]
        log.info("catalog loaded: %s", ", ".join(names))
        self._tools = build_tools(self._dotnet, names)

        home = Path(self._settings.copilot_home).resolve()
        home.mkdir(exist_ok=True)
        token = self._settings.github_token.get_secret_value() if self._settings.github_token else None
        self._client = CopilotClient(mode="empty", base_directory=str(home), github_token=token)
        await self._client.start()

    async def stop(self) -> None:
        for conv in self._open.values():
            await conv.session.disconnect()
        self._open.clear()
        if self._client:
            await self._client.stop()
        await self._dotnet.aclose()

    def _options(self) -> dict[str, Any]:
        return {
            "model": self._settings.copilot_model,
            "tools": self._tools,
            "available_tools": [f"custom:{name}" for name in TOOL_NAMES],
            "system_message": {"mode": "append", "content": build_system_prompt(self._catalog)},
            "streaming": True,
            "on_permission_request": PermissionHandler.approve_all,
        }

    async def _get(self, session_id: str | None) -> tuple[str, _Conversation]:
        async with self._open_lock:
            if session_id in self._open:
                self._open.move_to_end(session_id)
                return session_id, self._open[session_id]

            if session_id is None:
                session = await self._client.create_session(**self._options())
                log.info("new session %s", session.session_id)
            else:
                try:
                    session = await self._client.resume_session(session_id, **self._options())
                except Exception as ex:
                    log.warning("resume %s failed: %s", session_id, ex)
                    raise SessionNotFound(session_id) from ex
                log.info("resumed session %s", session_id)

            session.on(self._log_event)
            conv = _Conversation(session)
            self._open[session.session_id] = conv
            await self._evict_idle()
            return session.session_id, conv

    async def _evict_idle(self) -> None:
        for sid in list(self._open):
            if len(self._open) <= self._settings.max_open_sessions:
                return
            conv = self._open[sid]
            if not conv.busy:
                del self._open[sid]
                await conv.session.disconnect()
                log.info("closed idle session %s", sid)

    async def begin(self, session_id: str | None, message: str) -> Turn:
        sid, conv = await self._get(session_id)
        if conv.busy:
            raise SessionBusy(sid)
        conv.busy = True

        turn = Turn(sid)
        asyncio.create_task(self._run_turn(conv, turn, message))
        return turn

    async def _run_turn(self, conv: _Conversation, turn: Turn, message: str) -> None:
        loop = asyncio.get_running_loop()
        calls: dict[str, dict] = {}

        def emit(kind: str, payload: dict) -> None:
            loop.call_soon_threadsafe(turn.queue.put_nowait, (kind, payload))

        def listener(event) -> None:
            data = event.data
            if isinstance(data, AssistantMessageDeltaData):
                emit("delta", {"text": data.delta_content})
            elif isinstance(data, ToolExecutionStartData):
                args = data.arguments or {}
                calls[data.tool_call_id] = {"tool": data.tool_name, "args": args}
                emit("tool", {"name": data.tool_name, "arguments": args})
            elif isinstance(data, ToolExecutionCompleteData):
                table = _table_from(calls.get(data.tool_call_id), data)
                if table:
                    emit("table", table)

        unsubscribe = conv.session.on(listener)
        log.info("[%s] user: %s", turn.session_id, message)
        try:
            reply = await conv.session.send_and_wait(message, timeout=TURN_TIMEOUT_SECONDS)
            answer = reply.data.content if reply else ""
            log.info("[%s] assistant: %s", turn.session_id, answer)
            emit("done", {"session_id": turn.session_id, "answer": answer})
        except Exception:
            log.exception("[%s] turn failed", turn.session_id)
            emit("error", {"message": "The assistant could not answer. Please try again."})
        finally:
            unsubscribe()
            conv.busy = False
            emit(_END, {})

    def _log_event(self, event) -> None:
        data = event.data
        if isinstance(data, ToolExecutionStartData):
            log.info("tool call %s %s", data.tool_name, data.arguments)
        elif isinstance(data, ToolExecutionCompleteData):
            log.info("tool done %s success=%s", data.tool_call_id, data.success)
        elif isinstance(data, SessionErrorData):
            log.error("session error %s: %s", data.error_type, data.message)


def _table_from(call: dict | None, data: ToolExecutionCompleteData) -> dict | None:
    if not call or call["tool"] != "query_entity" or not data.success or not data.result:
        return None
    try:
        body = json.loads(data.result.content)
    except (TypeError, ValueError):
        return None
    if not isinstance(body, dict) or not body.get("rows"):
        return None
    return {"entity": body.get("entity"), "rows": body["rows"], "truncated": body.get("truncated", False)}