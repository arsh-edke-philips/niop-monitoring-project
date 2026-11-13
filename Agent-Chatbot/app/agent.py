import logging
from collections.abc import Callable
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

from app.config import Settings
from app.dotnet_client import DotnetClient
from app.prompt import build_system_prompt
from app.tools import build_tools

log = logging.getLogger("ops_agent.agent")

TOOL_NAMES = ["query_entity", "find_by_identifier", "count_entity"]
TURN_TIMEOUT_SECONDS = 120


class OpsAgent:
    def __init__(self, settings: Settings) -> None:
        self._settings = settings
        self._dotnet = DotnetClient(settings)
        self._client: CopilotClient | None = None
        self._session = None
        self._tools: list = []
        self._prompt = ""

    async def start(self) -> None:
        catalog = await self._dotnet.get_catalog()
        entity_names = [e["name"] for e in catalog["entities"]]
        log.info("catalog loaded: %s", ", ".join(entity_names))

        self._tools = build_tools(self._dotnet, entity_names)
        self._prompt = build_system_prompt(catalog)

        home = Path(self._settings.copilot_home).resolve()
        home.mkdir(exist_ok=True)
        token = self._settings.github_token.get_secret_value() if self._settings.github_token else None
        self._client = CopilotClient(mode="empty", base_directory=str(home), github_token=token)
        await self._client.start()

        await self.reset()

    async def reset(self) -> None:
        if self._session:
            await self._session.disconnect()
        self._session = await self._client.create_session(
            model=self._settings.copilot_model,
            tools=self._tools,
            available_tools=[f"custom:{name}" for name in TOOL_NAMES],
            system_message={"mode": "append", "content": self._prompt},
            streaming=True,
            on_permission_request=PermissionHandler.approve_all,
        )
        self._session.on(self._log_event)
        log.info("session %s started with model %s", self._session.session_id, self._settings.copilot_model)

    async def ask(
        self,
        question: str,
        on_text: Callable[[str], None] | None = None,
        on_tool: Callable[[str, dict[str, Any]], None] | None = None,
    ) -> str:
        def listener(event) -> None:
            data = event.data
            if on_text and isinstance(data, AssistantMessageDeltaData):
                on_text(data.delta_content)
            elif on_tool and isinstance(data, ToolExecutionStartData):
                on_tool(data.tool_name, data.arguments or {})

        unsubscribe = self._session.on(listener)
        log.info("user: %s", question)
        try:
            reply = await self._session.send_and_wait(question, timeout=TURN_TIMEOUT_SECONDS)
        finally:
            unsubscribe()

        answer = reply.data.content if reply else ""
        log.info("assistant: %s", answer)
        return answer

    async def stop(self) -> None:
        if self._session:
            await self._session.disconnect()
        if self._client:
            await self._client.stop()
        await self._dotnet.aclose()

    def _log_event(self, event) -> None:
        data = event.data
        if isinstance(data, ToolExecutionStartData):
            log.info("tool call %s %s", data.tool_name, data.arguments)
        elif isinstance(data, ToolExecutionCompleteData):
            log.info("tool done %s success=%s", data.tool_call_id, data.success)
        elif isinstance(data, SessionErrorData):
            log.error("session error %s: %s", data.error_type, data.message)