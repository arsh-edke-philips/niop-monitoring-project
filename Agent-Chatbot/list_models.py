import asyncio
from pathlib import Path

from copilot import CopilotClient


async def main() -> None:
    home = Path(".copilot-home").resolve()
    home.mkdir(exist_ok=True)
    async with CopilotClient(mode="empty", base_directory=str(home)) as client:
        for m in await client.list_models():
            print(f"{m.id:35} {m.name}")


asyncio.run(main())