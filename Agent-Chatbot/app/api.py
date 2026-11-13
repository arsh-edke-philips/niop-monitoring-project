import json
import logging
import secrets
from contextlib import asynccontextmanager

from fastapi import Depends, FastAPI, Header, HTTPException
from fastapi.responses import StreamingResponse
from pydantic import BaseModel, Field

from app.chat_service import ChatService, SessionBusy, SessionNotFound
from app.config import get_settings
from cli import setup_logging

settings = get_settings()
log = logging.getLogger("ops_agent.api")


@asynccontextmanager
async def lifespan(app: FastAPI):
    setup_logging(settings.log_file)
    service = ChatService(settings)
    await service.start()
    app.state.chat = service
    log.info("chat API ready")
    try:
        yield
    finally:
        await service.stop()


app = FastAPI(title="NIOP Ops Agent", lifespan=lifespan)


def require_chat_key(x_chat_key: str | None = Header(default=None)) -> None:
    expected = settings.chat_api_key.get_secret_value() if settings.chat_api_key else ""
    if not expected:
        raise HTTPException(status_code=503, detail="Chat API key is not configured.")
    if not x_chat_key or not secrets.compare_digest(x_chat_key, expected):
        raise HTTPException(status_code=401, detail="Invalid chat key.")


class ChatRequest(BaseModel):
    message: str = Field(min_length=1, max_length=2000)
    session_id: str | None = Field(default=None, max_length=100)


def sse(kind: str, payload: dict) -> str:
    return f"event: {kind}\ndata: {json.dumps(payload, ensure_ascii=False, default=str)}\n\n"


@app.get("/health")
async def health() -> dict:
    return {"status": "ok"}


@app.post("/chat", dependencies=[Depends(require_chat_key)])
async def chat(req: ChatRequest) -> StreamingResponse:
    service: ChatService = app.state.chat
    try:
        turn = await service.begin(req.session_id, req.message)
    except SessionNotFound:
        raise HTTPException(status_code=404, detail="Conversation not found. Start a new one.")
    except SessionBusy:
        raise HTTPException(status_code=409, detail="This conversation is still answering the previous message.")

    async def stream():
        yield sse("session", {"session_id": turn.session_id})
        async for kind, payload in turn.events():
            yield sse(kind, payload)

    return StreamingResponse(
        stream(),
        media_type="text/event-stream",
        headers={"Cache-Control": "no-cache", "X-Accel-Buffering": "no"},
    )