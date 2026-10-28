from __future__ import annotations

import importlib.util
from pathlib import Path


REPOSITORY_ROOT = Path(__file__).resolve().parents[2]
SPEC = importlib.util.spec_from_file_location(
    "check_meta", REPOSITORY_ROOT / "tools" / "check_meta.py"
)
assert SPEC is not None and SPEC.loader is not None
CHECK_META = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(CHECK_META)


def test_accepts_files_and_populated_directories_with_meta(tmp_path: Path) -> None:
    assets = tmp_path / "Assets"
    folder = assets / "Models"
    folder.mkdir(parents=True)
    (assets / "Models.meta").write_text("folder-guid", encoding="utf-8")
    (folder / "engine.fbx").write_text("mesh", encoding="utf-8")
    (folder / "engine.fbx.meta").write_text("asset-guid", encoding="utf-8")

    missing, orphan = CHECK_META.find_problems(assets)

    assert missing == []
    assert orphan == []


def test_flags_folder_meta_when_residual_directory_is_empty(
    tmp_path: Path,
) -> None:
    assets = tmp_path / "Assets"
    residual_folder = assets / "RemovedModel"
    residual_folder.mkdir(parents=True)
    folder_meta = assets / "RemovedModel.meta"
    folder_meta.write_text("old-guid", encoding="utf-8")

    missing, orphan = CHECK_META.find_problems(assets)

    assert missing == []
    assert orphan == [folder_meta]


def test_flags_file_meta_without_counterpart(tmp_path: Path) -> None:
    assets = tmp_path / "Assets"
    assets.mkdir()
    orphan_meta = assets / "missing.prefab.meta"
    orphan_meta.write_text("old-guid", encoding="utf-8")

    missing, orphan = CHECK_META.find_problems(assets)

    assert missing == []
    assert orphan == [orphan_meta]
