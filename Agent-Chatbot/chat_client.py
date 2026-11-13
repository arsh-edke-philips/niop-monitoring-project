import asyncio
import json
import sys

import httpx

from app.config import get_settings

URL = "http://127.0.0.1:8001/chat"


async def main() -> None:
    key = get_settings().chat_api_key.get_secret_value()
    session_id = sys.argv[1] if len(sys.argv) > 1 else None

    async with httpx.AsyncClient(timeout=None) as http:
        while True:
            question = input("\nYou: ").strip()
            if question in ("", "exit"):
                break

            body = {"message": question, "session_id": session_id}
            async with http.stream("POST", URL, json=body, headers={"X-Chat-Key": key}) as resp:
                if resp.status_code != 200:
                    print(f"HTTP {resp.status_code}: {(await resp.aread()).decode()}")
                    continue

                kind = ""
                async for line in resp.aiter_lines():
                    if line.startswith("event: "):
                        kind = line[len("event: "):]
                    elif line.startswith("data: "):
                        data = json.loads(line[len("data: "):])
                        if kind == "session":
                            session_id = data["session_id"]
                        elif kind == "tool":
                            print(f"  [tool] {data['name']} {data['arguments']}")
                        elif kind == "table":
                            more = " (more exist)" if data["truncated"] else ""
                            print(f"  [table] {data['entity']}: {len(data['rows'])} rows{more}")
                        elif kind == "delta":
                            print(data["text"], end="", flush=True)
                        elif kind == "error":
                            print(f"  [error] {data['message']}")
            print(f"\n  (session {session_id})")


if __name__ == "__main__":
    asyncio.run(main())