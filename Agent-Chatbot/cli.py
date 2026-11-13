import asyncio
import logging
import sys

from app.agent import OpsAgent
from app.config import get_settings
from app.dotnet_client import DataServiceUnavailable


def setup_logging(path: str) -> None:
    logging.basicConfig(
        filename=path,
        level=logging.INFO,
        encoding="utf-8",
        format="%(asctime)s %(levelname)s %(name)s: %(message)s",
    )
    logging.getLogger("httpx").setLevel(logging.WARNING)


async def main() -> int:
    settings = get_settings()
    setup_logging(settings.log_file)

    agent = OpsAgent(settings)
    try:
        await agent.start()
    except DataServiceUnavailable as ex:
        print(f"Cannot start: {ex}. Is NIOP_Monitoring running, and does AGENT_API_KEY match Agent:ApiKey?")
        await agent.stop()
        return 1

    print("NIOP Ops assistant. Ask a question, or type 'exit'.")
    try:
        while True:
            try:
                question = (await asyncio.to_thread(input, "\nyou> ")).strip()
            except EOFError:
                break
            if not question:
                continue
            if question.lower() in {"exit", "quit"}:
                break

            print("bot> ", end="", flush=True)
            streamed = False

            def on_text(chunk: str) -> None:
                nonlocal streamed
                streamed = True
                print(chunk, end="", flush=True)

            try:
                answer = await agent.ask(question, on_text=on_text)
            except Exception as ex:
                logging.exception("turn failed")
                print(f"\n[error: {ex}]")
                continue
            if not streamed:
                print(answer, end="")
            print()
    finally:
        await agent.stop()
    return 0


if __name__ == "__main__":
    sys.exit(asyncio.run(main()))