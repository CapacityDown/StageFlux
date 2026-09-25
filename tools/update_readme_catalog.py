from __future__ import annotations

from pathlib import Path

import build_event_list_pdf as japanese
import build_event_list_pdf_en as english


ROOT = Path(__file__).resolve().parents[1]
README = ROOT / "package" / "README.md"


def escape_cell(value: str) -> str:
    return value.replace("|", r"\|").replace("\n", " ").strip()


def catalog_table(
    events: list[tuple],
    *,
    language: str,
    display_names: list[str] | None = None,
) -> str:
    if language == "en":
        intro = (
            "All 36 events are listed below using native Markdown so the catalog "
            "displays directly on Thunderstore."
        )
        headers = ("Event", "Risk", "Default", "Chance", "Description")
        risk_labels = {"low": "Low", "medium": "Medium", "high": "High"}
        enabled_labels = {True: "On", False: "Off"}
    else:
        intro = (
            "全36イベントをThunderstore上で確実に表示できるMarkdown形式で掲載しています。"
            "危険度、デフォルト状態、デフォルト確率、簡潔な動作を確認できます。"
        )
        headers = ("イベント", "危険度", "デフォルト", "確率", "内容")
        risk_labels = {"low": "低", "medium": "中", "high": "高"}
        enabled_labels = {True: "有効", False: "無効"}

    rows = [
        f"| {' | '.join(headers)} |",
        "|---|---:|---:|---:|---|",
    ]
    for index, (_, name, _, enabled, chance, risk, description, _) in enumerate(events):
        display_name = display_names[index] if display_names is not None else name
        rows.append(
            "| "
            + " | ".join(
                (
                    escape_cell(display_name),
                    risk_labels[risk],
                    enabled_labels[enabled],
                    f"{chance}%",
                    escape_cell(description),
                )
            )
            + " |"
        )
    return intro + "\n\n" + "\n".join(rows)


def replace_section(
    source: str,
    *,
    heading: str,
    next_heading: str,
    replacement: str,
) -> str:
    start = source.index(heading)
    end = source.index(next_heading, start)
    return source[:start] + replacement.rstrip() + "\n\n" + source[end:]


def main() -> None:
    text = README.read_text(encoding="utf-8-sig")
    text = replace_section(
        text,
        heading="### Event Catalog",
        next_heading="### Gameplay warnings",
        replacement="### Event Catalog\n\n" + catalog_table(english.EVENTS, language="en"),
    )
    text = replace_section(
        text,
        heading="### イベント一覧",
        next_heading="### ゲームプレイ上の注意",
        replacement="### イベント一覧\n\n"
        + catalog_table(
            japanese.EVENTS,
            language="ja",
            display_names=[event[1] for event in english.EVENTS],
        ),
    )
    text = text.replace("| `ChancePercent` | `5` |", "| `ChancePercent` | `6` |")
    text = text.replace("| `ChancePercent` | `8` |", "| `ChancePercent` | `6` |")
    text = text.replace("ChancePercent=5", "ChancePercent=6")
    text = text.replace("ChancePercent=8", "ChancePercent=6")
    README.write_text(text, encoding="utf-8", newline="\n")


if __name__ == "__main__":
    main()
