"""Verifica pares asset/.meta em um projeto Unity."""

from __future__ import annotations

import argparse
from pathlib import Path


def _directory_has_asset(directory: Path) -> bool:
    """Return whether a Unity folder contains a non-meta, non-hidden asset."""
    return any(
        path.is_file()
        and path.suffix != ".meta"
        and not any(part.startswith(".") for part in path.relative_to(directory).parts)
        for path in directory.rglob("*")
    )


def find_problems(assets: Path) -> tuple[list[Path], list[Path]]:
    """Retorna assets sem meta e metas sem asset correspondente."""
    missing_meta: list[Path] = []
    orphan_meta: list[Path] = []

    for path in assets.rglob("*"):
        if path.name.startswith("."):
            continue
        if path.suffix == ".meta":
            counterpart = Path(str(path)[: -len(".meta")])
            if not counterpart.exists() or (
                counterpart.is_dir() and not _directory_has_asset(counterpart)
            ):
                orphan_meta.append(path)
        elif not Path(f"{path}.meta").exists():
            missing_meta.append(path)

    return missing_meta, orphan_meta


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument(
        "assets",
        nargs="?",
        type=Path,
        default=Path("unity/Assets"),
        help="Diretório Assets do projeto Unity.",
    )
    args = parser.parse_args()
    missing, orphan = find_problems(args.assets)

    for path in missing:
        print(f"SEM_META: {path}")
    for path in orphan:
        print(f"META_ORFAO: {path}")

    print(
        f"CHECK_META assets={args.assets} "
        f"sem_meta={len(missing)} meta_orfao={len(orphan)}"
    )
    return 1 if missing or orphan else 0


if __name__ == "__main__":
    raise SystemExit(main())
