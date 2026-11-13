import asyncio
import json
import sys
import time
from collections import defaultdict
from datetime import datetime
from pathlib import Path
from typing import Any

from app.agent import OpsAgent
from app.config import get_settings
from cli import setup_logging

CASES_FILE = Path("evals/cases.json")
RESULTS_DIR = Path("evals/results")


def norm(value: Any) -> str:
    return str(value).strip().lower()


def grade(case: dict, calls: list[dict], answer: str) -> tuple[bool, bool]:
    if case.get("no_tools"):
        tool_ok = not calls
    else:
        tool_ok = all(any(call_matches(e, a) for a in calls) for e in case.get("calls", []))

    text = answer.lower()
    answer_ok = all(s.lower() in text for s in case.get("answer_contains", []))
    if case.get("clarify"):
        answer_ok = answer_ok and "?" in answer and not any(c["tool"] == "query_entity" for c in calls)
    return tool_ok, answer_ok


async def run_case(agent: OpsAgent, case: dict) -> dict:
    await agent.reset()
    for earlier in case.get("setup", []):
        await agent.ask(earlier)

    calls: list[dict] = []
    started = time.perf_counter()
    try:
        answer = await agent.ask(case["question"], on_tool=lambda name, args: calls.append({"tool": name, "args": args}))
    except Exception as ex:
        answer = f"[exception: {ex}]"
    seconds = round(time.perf_counter() - started, 1)

    tool_ok, answer_ok = grade(case, calls, answer)
    return {"id": case["id"], "category": case.get("category", ""), "question": case["question"],
            "tool_ok": tool_ok, "answer_ok": answer_ok, "calls": calls, "answer": answer, "seconds": seconds}


def print_summary(results: list[dict]) -> None:
    n = len(results)
    tools = sum(r["tool_ok"] for r in results)
    answers = sum(r["answer_ok"] for r in results)
    print(f"\nTool choice: {tools}/{n} ({tools / n:.0%})   Answers: {answers}/{n} ({answers / n:.0%})")

    by_cat: dict[str, list[dict]] = defaultdict(list)
    for r in results:
        by_cat[r["category"]].append(r)
    for cat, rs in sorted(by_cat.items()):
        ok = sum(r["tool_ok"] and r["answer_ok"] for r in rs)
        print(f"  {cat:<12} {ok}/{len(rs)}")


def matches_one(expected: Any, actual: Any) -> bool:
    options = expected if isinstance(expected, list) else [expected]
    return norm(actual) in {norm(o) for o in options}


def call_matches(expected: dict, actual: dict) -> bool:
    if not matches_one(expected["tool"], actual["tool"]):
        return False
    args = actual["args"]
    for key in ("entity", "group_by", "value"):
        if key in expected and not matches_one(expected[key], args.get(key)):
            return False
    actual_filters = {(norm(f.get("column")), norm(f.get("value"))) for f in args.get("filters") or []}
    return all((norm(c), norm(v)) in actual_filters for c, v in expected.get("filters", {}).items())


async def main(only: set[str]) -> int:
    settings = get_settings()
    setup_logging(settings.log_file)
    cases = json.loads(CASES_FILE.read_text(encoding="utf-8"))
    if only:
        cases = [c for c in cases if c["id"] in only]

    agent = OpsAgent(settings)
    await agent.start()
    results = []
    try:
        for case in cases:
            r = await run_case(agent, case)
            results.append(r)
            status = "PASS" if r["tool_ok"] and r["answer_ok"] else "FAIL"
            print(f"{status} {r['id']:<8} tool={'ok' if r['tool_ok'] else 'X ':<2} "
                  f"answer={'ok' if r['answer_ok'] else 'X ':<2} calls={len(r['calls'])} {r['seconds']}s")
    finally:
        await agent.stop()

    print_summary(results)
    RESULTS_DIR.mkdir(parents=True, exist_ok=True)
    out = RESULTS_DIR / f"{datetime.now():%Y%m%d-%H%M%S}-{settings.copilot_model}.json"
    out.write_text(json.dumps(results, indent=2, ensure_ascii=False), encoding="utf-8")
    print(f"Saved {out}")
    return 0 if all(r["tool_ok"] and r["answer_ok"] for r in results) else 1


if __name__ == "__main__":
    sys.exit(asyncio.run(main(set(sys.argv[1:]))))