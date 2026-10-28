import importlib.util
import json
from pathlib import Path


SCRIPT = Path(__file__).parents[2] / "tools" / "validate_session.py"
SPEC = importlib.util.spec_from_file_location("validate_session", SCRIPT)
MODULE = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(MODULE)


def make_session(tmp_path: Path, events: list[dict]) -> Path:
    for name in ("session.json", "protocol_snapshot.json"):
        (tmp_path / name).write_text("{}", encoding="utf-8")
    (tmp_path / "events.csv").write_text("event\n", encoding="utf-8")
    (tmp_path / "events.jsonl").write_text(
        "".join(json.dumps(event) + "\n" for event in events),
        encoding="utf-8",
    )
    return tmp_path


def test_interaction_in_pretest_is_contamination(tmp_path):
    result = MODULE.validate_session(
        make_session(
            tmp_path,
            [{"eventName": "rpm_changed", "phase": "PreTest",
              "elapsedMilliseconds": 10}],
        )
    )
    assert result["contaminated"] is True
    assert result["valid"] is False


def test_interaction_in_main_tasks_is_allowed(tmp_path):
    result = MODULE.validate_session(
        make_session(
            tmp_path,
            [{"eventName": "component_selected", "phase": "MainTasks",
              "elapsedMilliseconds": 10}],
        )
    )
    assert result["contaminated"] is False
    assert result["valid"] is True
