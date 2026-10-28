"""Valida integridade mínima e contaminação de uma sessão MTRM."""

from __future__ import annotations

import argparse
import json
from pathlib import Path


INTERACTION_EVENTS = {
    "rpm_changed",
    "motor_power_changed",
    "motor_expansion_changed",
    "component_selected",
    "component_name_queried",
}
TEST_PHASES = {"pretest", "posttest"}


def validate_session(session_dir: Path) -> dict:
    required = ("session.json", "protocol_snapshot.json", "events.jsonl", "events.csv")
    missing = [name for name in required if not (session_dir / name).is_file()]
    if missing:
        return {"valid": False, "missing": missing, "contaminated": False}

    events = []
    malformed = []
    for line_number, line in enumerate(
        (session_dir / "events.jsonl").read_text(encoding="utf-8").splitlines(),
        start=1,
    ):
        try:
            events.append(json.loads(line))
        except json.JSONDecodeError:
            malformed.append(line_number)

    contamination = [
        {
            "line": index,
            "event": event.get("eventName", ""),
            "phase": event.get("phase", ""),
            "elapsedMilliseconds": event.get("elapsedMilliseconds", -1),
        }
        for index, event in enumerate(events, start=1)
        if event.get("eventName") in INTERACTION_EVENTS
        and str(event.get("phase", "")).lower() in TEST_PHASES
    ]
    return {
        "valid": not malformed and not contamination,
        "eventCount": len(events),
        "malformedLines": malformed,
        "contaminated": bool(contamination),
        "contaminationEvents": contamination,
    }


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("session_dir", type=Path)
    args = parser.parse_args()
    result = validate_session(args.session_dir)
    print(json.dumps(result, ensure_ascii=False, indent=2))
    return 0 if result["valid"] else 2


if __name__ == "__main__":
    raise SystemExit(main())
