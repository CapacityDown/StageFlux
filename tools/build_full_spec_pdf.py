from __future__ import annotations

import html
import re
from pathlib import Path

from reportlab.lib.colors import HexColor, white
from reportlab.lib.enums import TA_CENTER, TA_LEFT
from reportlab.lib.pagesizes import A4
from reportlab.lib.styles import ParagraphStyle
from reportlab.lib.units import mm
from reportlab.lib.utils import ImageReader
from reportlab.pdfbase import pdfmetrics
from reportlab.pdfbase.ttfonts import TTFont
from reportlab.platypus import (
    CondPageBreak,
    Flowable,
    Image,
    KeepTogether,
    ListFlowable,
    ListItem,
    PageBreak,
    Paragraph,
    SimpleDocTemplate,
    Spacer,
    Table,
    TableStyle,
)

from build_event_list_pdf import EVENTS, icon_asset_path


ROOT = Path(__file__).resolve().parents[1]
README = ROOT / "package" / "README.md"
OUTPUT = ROOT / "output" / "pdf" / "StageFlux_Full_Specification_v4.2.0.pdf"
FONT_REGULAR = Path(r"C:\Windows\Fonts\BIZ-UDGothicR.ttc")
FONT_BOLD = Path(r"C:\Windows\Fonts\BIZ-UDGothicB.ttc")

PAGE_W, PAGE_H = A4
MARGIN_X = 18 * mm
TOP_MARGIN = 17 * mm
BOTTOM_MARGIN = 14 * mm
CONTENT_W = PAGE_W - MARGIN_X * 2

BG = HexColor("#071015")
PANEL = HexColor("#101A1F")
PANEL_DARK = HexColor("#0A1216")
INK = HexColor("#E7ECE7")
MUTED = HexColor("#B4BDB7")
GRID = HexColor("#304048")
CYAN = HexColor("#46C6E8")
MAGENTA = HexColor("#E34B9A")
ORANGE = HexColor("#E49A29")
GREEN = HexColor("#70A650")
YELLOW = HexColor("#CF9E35")
RED = HexColor("#B84143")

EVENT_INFO = {event[0]: event for event in EVENTS}
EVENT_HEADING_ICONS = {
    "Feather": ["Feather"],
    "Zero Gravity": ["ZeroGravity"],
    "Battery Charge": ["Battery Charge"],
    "Heal": ["Heal"],
    "Indestructible": ["Indestructible"],
    "Fragility": ["Fragility"],
    "Gumball Hypnosis": ["GumballHypnosis"],
    "Healing Aura": ["HealingAura"],
    "Star Barrage": ["StarBarrage"],
    "Spider Scare": ["SpiderScare"],
    "Traffic Shock": ["TrafficShock"],
    "Dangerous Valuables": ["DangerousValuables"],
    "Roll": ["Roll"],
    "Void": ["Void"],
    "Levitation": ["Levitation"],
    "Shockwave / Stun Blast / Explosion Rain": ["Shockwave", "StunBlast", "ExplosionRain"],
    "Enemy Wave": ["EnemyWave"],
    "Minefield": ["Minefield"],
}
RISK_COLOR = {
    "low": GREEN,
    "medium": YELLOW,
    "high": RED,
}


def register_fonts() -> None:
    pdfmetrics.registerFont(TTFont("BIZUD", str(FONT_REGULAR), subfontIndex=0))
    pdfmetrics.registerFont(TTFont("BIZUDB", str(FONT_BOLD), subfontIndex=0))


STYLES = {
    "body": ParagraphStyle(
        "body",
        fontName="BIZUDB",
        fontSize=9.2,
        leading=14,
        textColor=INK,
        wordWrap="CJK",
        spaceAfter=7,
    ),
    "small": ParagraphStyle(
        "small",
        fontName="BIZUDB",
        fontSize=7.6,
        leading=10.5,
        textColor=INK,
        wordWrap="CJK",
    ),
    "table": ParagraphStyle(
        "table",
        fontName="BIZUDB",
        fontSize=7.1,
        leading=10,
        textColor=INK,
        wordWrap="CJK",
    ),
    "table_header": ParagraphStyle(
        "table_header",
        fontName="BIZUDB",
        fontSize=7.2,
        leading=10,
        textColor=white,
        wordWrap="CJK",
        alignment=TA_LEFT,
    ),
    "h3": ParagraphStyle(
        "h3",
        fontName="BIZUDB",
        fontSize=19,
        leading=24,
        textColor=INK,
        spaceBefore=8,
        spaceAfter=10,
        keepWithNext=True,
    ),
    "h4": ParagraphStyle(
        "h4",
        fontName="BIZUDB",
        fontSize=13,
        leading=17,
        textColor=INK,
        spaceAfter=0,
    ),
    "toc_title": ParagraphStyle(
        "toc_title",
        fontName="BIZUDB",
        fontSize=12,
        leading=16,
        textColor=CYAN,
        alignment=TA_LEFT,
    ),
    "toc_body": ParagraphStyle(
        "toc_body",
        fontName="BIZUDB",
        fontSize=8.5,
        leading=13,
        textColor=INK,
        wordWrap="CJK",
    ),
}


def normalize(text: str) -> str:
    return (
        text.replace("\u2013", "-")
        .replace("\u2011", "-")
        .replace("\u2212", "-")
        .replace("：", ":")
    )


def inline_markup(text: str) -> str:
    text = normalize(text)
    placeholders: list[str] = []

    def stash_code(match: re.Match[str]) -> str:
        value = html.escape(match.group(1))
        placeholders.append(f"<font color='#46C6E8'>{value}</font>")
        return f"@@CODE{len(placeholders) - 1}@@"

    text = re.sub(r"`([^`]+)`", stash_code, text)
    text = html.escape(text)
    text = re.sub(
        r"\*\*(.+?)\*\*",
        r"<font color='#E49A29'>\1</font>",
        text,
    )
    for index, value in enumerate(placeholders):
        text = text.replace(f"@@CODE{index}@@", value)
    return text


class SectionBand(Flowable):
    def __init__(self, title: str, color=CYAN, height: float = 31):
        super().__init__()
        self.title = title
        self.color = color
        self.height = height
        self.width = CONTENT_W

    def draw(self) -> None:
        self.canv.setFillColor(PANEL)
        self.canv.setStrokeColor(self.color)
        self.canv.setLineWidth(1.3)
        self.canv.roundRect(0, 0, self.width, self.height, 6, fill=1, stroke=1)
        self.canv.setFillColor(self.color)
        self.canv.rect(0, 0, 5, self.height, fill=1, stroke=0)
        self.canv.setFont("BIZUDB", 14)
        self.canv.setFillColor(INK)
        self.canv.drawString(14, 9, self.title)


def event_risk_color(icon_name: str):
    info = EVENT_INFO.get(icon_name)
    return RISK_COLOR.get(info[5], CYAN) if info else CYAN


def event_header(title: str, icons: list[str]) -> Table:
    icon_flowables = []
    for icon_name in icons:
        icon_flowables.append(
            Image(str(icon_asset_path(icon_name)), width=13 * mm, height=13 * mm)
        )
    icon_table = Table([icon_flowables], colWidths=[14 * mm] * len(icon_flowables))
    icon_table.setStyle(TableStyle([("VALIGN", (0, 0), (-1, -1), "MIDDLE")]))

    color = event_risk_color(icons[0])
    title_para = Paragraph(inline_markup(title), STYLES["h4"])
    table = Table(
        [[icon_table, title_para]],
        colWidths=[15 * mm * len(icons), CONTENT_W - 15 * mm * len(icons)],
        hAlign="LEFT",
    )
    table.setStyle(TableStyle([
        ("BACKGROUND", (0, 0), (-1, -1), PANEL),
        ("BOX", (0, 0), (-1, -1), 1.4, color),
        ("LINEBEFORE", (0, 0), (0, 0), 4, color),
        ("LEFTPADDING", (0, 0), (-1, -1), 7),
        ("RIGHTPADDING", (0, 0), (-1, -1), 8),
        ("TOPPADDING", (0, 0), (-1, -1), 5),
        ("BOTTOMPADDING", (0, 0), (-1, -1), 5),
        ("VALIGN", (0, 0), (-1, -1), "MIDDLE"),
    ]))
    return table


def make_table(rows: list[list[str]]) -> Table:
    col_count = len(rows[0])
    if col_count == 2:
        widths = [38 * mm, CONTENT_W - 38 * mm]
    elif col_count == 3:
        widths = [31 * mm, 70 * mm, CONTENT_W - 101 * mm]
    elif col_count == 4:
        widths = [42 * mm, 22 * mm, 32 * mm, CONTENT_W - 96 * mm]
    else:
        widths = [CONTENT_W / col_count] * col_count

    formatted: list[list[Paragraph]] = []
    for row_index, row in enumerate(rows):
        style = STYLES["table_header"] if row_index == 0 else STYLES["table"]
        formatted.append([Paragraph(inline_markup(cell), style) for cell in row])

    table = Table(
        formatted,
        colWidths=widths,
        repeatRows=1,
        splitByRow=1,
        hAlign="LEFT",
    )
    style_commands = [
        ("BACKGROUND", (0, 0), (-1, 0), HexColor("#1B5968")),
        ("TEXTCOLOR", (0, 0), (-1, -1), INK),
        ("GRID", (0, 0), (-1, -1), 0.55, GRID),
        ("VALIGN", (0, 0), (-1, -1), "TOP"),
        ("LEFTPADDING", (0, 0), (-1, -1), 5),
        ("RIGHTPADDING", (0, 0), (-1, -1), 5),
        ("TOPPADDING", (0, 0), (-1, -1), 5),
        ("BOTTOMPADDING", (0, 0), (-1, -1), 5),
    ]
    for row_index in range(1, len(rows)):
        if row_index % 2 == 0:
            style_commands.append(
                ("BACKGROUND", (0, row_index), (-1, row_index), PANEL_DARK)
            )
        else:
            style_commands.append(
                ("BACKGROUND", (0, row_index), (-1, row_index), PANEL)
            )
    table.setStyle(TableStyle(style_commands))
    return table


def flush_paragraph(buffer: list[str], story: list) -> None:
    if not buffer:
        return
    text = " ".join(value.strip() for value in buffer)
    story.append(Paragraph(inline_markup(text), STYLES["body"]))
    buffer.clear()


def flush_bullets(items: list[str], story: list) -> None:
    if not items:
        return
    flowables = [
        ListItem(
            Paragraph(inline_markup(item), STYLES["body"]),
            leftIndent=4,
        )
        for item in items
    ]
    story.append(
        ListFlowable(
            flowables,
            bulletType="bullet",
            start="circle",
            leftIndent=17,
            bulletFontName="BIZUDB",
            bulletFontSize=7,
            bulletColor=CYAN,
            spaceAfter=8,
        )
    )
    items.clear()


def parse_japanese_readme() -> list:
    source = README.read_text(encoding="utf-8")
    source = source.split("## 日本語", 1)[1]
    lines = source.splitlines()
    story: list = []
    paragraph_buffer: list[str] = []
    bullets: list[str] = []
    index = 0

    while index < len(lines):
        line = lines[index].rstrip()
        stripped = line.strip()

        if stripped.startswith("|"):
            flush_paragraph(paragraph_buffer, story)
            flush_bullets(bullets, story)
            table_lines: list[str] = []
            while index < len(lines) and lines[index].strip().startswith("|"):
                table_lines.append(lines[index].strip())
                index += 1
            parsed_rows = [
                [cell.strip() for cell in row.strip("|").split("|")]
                for row in table_lines
            ]
            parsed_rows = [
                row for row in parsed_rows
                if not all(re.fullmatch(r":?-{3,}:?", cell) for cell in row)
            ]
            story.append(make_table(parsed_rows))
            story.append(Spacer(1, 10))
            continue

        if stripped.startswith("### "):
            flush_paragraph(paragraph_buffer, story)
            flush_bullets(bullets, story)
            title = stripped[4:].strip()
            if title == "設定":
                story.append(PageBreak())
            else:
                story.append(CondPageBreak(85))
            story.append(SectionBand(title, CYAN))
            story.append(Spacer(1, 8))
            index += 1
            continue

        if stripped.startswith("#### "):
            flush_paragraph(paragraph_buffer, story)
            flush_bullets(bullets, story)
            title = stripped[5:].strip()
            story.append(CondPageBreak(125))
            icons = EVENT_HEADING_ICONS.get(title)
            if icons:
                story.append(event_header(title, icons))
            else:
                color = MAGENTA if "イベント" in title else ORANGE
                story.append(SectionBand(title, color, height=27))
            story.append(Spacer(1, 7))
            index += 1
            continue

        if stripped.startswith("- "):
            flush_paragraph(paragraph_buffer, story)
            bullets.append(stripped[2:].strip())
            index += 1
            continue

        if not stripped:
            flush_paragraph(paragraph_buffer, story)
            flush_bullets(bullets, story)
            index += 1
            continue

        paragraph_buffer.append(stripped)
        index += 1

    flush_paragraph(paragraph_buffer, story)
    flush_bullets(bullets, story)
    return story


def contents_page() -> list:
    groups = [
        ("01  基本仕様", "概要 / 注意事項 / 必須MOD / 導入 / マルチプレイ"),
        ("02  イベントシステム", "5モード / ステージ抽選 / 個別抽選 / 同時発動 / 排他"),
        ("03  共通設定", "General / Timing / Safety"),
        ("04  全34イベント", "物理・支援 / 生成ハザード / 敵 / プレイヤー / ステージ"),
        ("05  対象と保護", "Targets / Cosmetic Boxes / 貴重品保護 / 解除遅延"),
        ("06  通知とHUD", "チャット / TTS抑制 / カウントダウン / Graphical / Classic"),
        ("07  互換性", "DroneToOrbItem / 復活・新規生成物 / 拡張ステージ"),
        ("08  アイコン索引", "全イベントのアイコン・危険度・既定ON/OFF・確率"),
    ]
    rows = []
    for index, (title, body) in enumerate(groups):
        color = (CYAN, MAGENTA, ORANGE)[index % 3]
        cell = Table(
            [[
                Paragraph(title, ParagraphStyle(
                    f"toc_title_{index}",
                    parent=STYLES["toc_title"],
                    textColor=color,
                )),
                Paragraph(body, STYLES["toc_body"]),
            ]],
            colWidths=[48 * mm, CONTENT_W - 48 * mm],
        )
        cell.setStyle(TableStyle([
            ("BACKGROUND", (0, 0), (-1, -1), PANEL),
            ("BOX", (0, 0), (-1, -1), 1.0, color),
            ("LINEBEFORE", (0, 0), (0, 0), 4, color),
            ("VALIGN", (0, 0), (-1, -1), "MIDDLE"),
            ("LEFTPADDING", (0, 0), (-1, -1), 10),
            ("RIGHTPADDING", (0, 0), (-1, -1), 10),
            ("TOPPADDING", (0, 0), (-1, -1), 11),
            ("BOTTOMPADDING", (0, 0), (-1, -1), 11),
        ]))
        rows.extend([cell, Spacer(1, 7)])
    return [
        SectionBand("CONTENTS / 収録内容", CYAN, height=36),
        Spacer(1, 15),
        *rows,
        PageBreak(),
    ]


def current_runtime_spec() -> list:
    risk_rows = [
        ["危険度", "枠色", "対象イベント"],
        ["Low", "#70A650", "Feather / Battery Charge / Heal / Indestructible / Enemy Purge / Second Chance / Value Surge / Healing Aura"],
        ["Medium", "#CF9E35", "Zero Gravity / Levitation / Freeze / Stun / Shockwave / Stun Blast / Knockback / Flicker / Door Chaos / Gumball Hypnosis / Spider Scare"],
        ["High", "#B84143", "Fragility / Roll / Void / Explosion Rain / Enemy Wave / Minefield / Enemy Warp / Enemy Hunt / Enemy Regen / Damage Pulse / Quake / Value Crash / Star Barrage / Traffic Shock / Dangerous Valuables"],
    ]
    sync_rows = [
        ["同期項目", "仕様"],
        ["現在・次回イベント", "発動中の組み合わせと、待機中に確定済みの次回組み合わせをホストから同期します。"],
        ["時間", "フェーズ全体時間とサーバー終了時刻を同期し、全クライアントで同じ共有ストップウォッチを表示します。"],
        ["モード", "AllModeで選ばれた実際のモードを含め、ステージで使用中のモードを同期します。"],
        ["スロット上限", "ステージ開始時のホスト側MaxSimultaneousEffectsを1-5へ固定し、参加者HUDの鍵表示にも使用します。"],
        ["バニラ参加者", "ゲーム効果とチャット通知を受けます。MOD UIは存在しないためHUD同期は表示しません。"],
    ]
    maintenance_rows = [
        ["項目", "現行仕様"],
        ["動的対象更新", "発動中は約0.25秒間隔で対象を確認し、新規生成された物理対象や復活したプレイヤーへ必要な効果を適用します。"],
        ["重複適用", "同じ対象へ効果を毎フレームかけ直さず、対象IDと各アダプターの状態を追跡して必要時だけ更新します。"],
        ["Roll", "プレイヤーは視線方向へ操舵し、プレイヤー以外はランダム方向へ移動します。終了時に適用状態を解除します。"],
        ["Void", "イベント中は10秒ごとに既存Voidを入れ替え、毎回新しいランダム地点へ複数生成します。"],
        ["貴重品保護解除", "Zero Gravity、Roll、Voidはイベント別の1-5秒設定（既定2秒）、その他の保護対象イベントはSafetyの1-5秒設定（既定2秒）を使用します。"],
        ["生成物の終了処理", "イベントが生成した効果元・投射物・地雷・危険な貴重品などは、各イベントの設定と種類に応じて停止・削除・デスポーンします。"],
        ["権限", "効果選択とゲーム状態変更はホスト権限で行います。シングルプレイは同じホスト処理経路を使用します。"],
    ]
    flow_rows = [
        ["フェーズ", "標準動作"],
        ["ステージ開始", "20%のステージ抽選に当選し、選択可能イベントがある場合だけ有効化します。"],
        ["待機", "10-300秒の範囲で整数抽選。RandomEachEventでは待機開始時に次回イベントを確定し、HUDへ危険度を表示します。"],
        ["開始通知", "イベント名を表示・発言して3秒後、設定有効時は5から0までカウントし、0で発動します。"],
        ["発動中", "同時イベントは共通の効果時間を使用します。イベント中の対象追加・復活を定期的に追跡します。"],
        ["終了通知", "Persistent以外は設定有効時に3、2、1を通知し、終了処理後にEndを通知します。"],
        ["Persistent", "300秒の長時間効果として扱い、終了10秒前に再適用してステージ終了まで維持します。"],
    ]
    return [
        PageBreak(),
        SectionBand("現行ランタイム・HUD詳細仕様", CYAN, height=36),
        Spacer(1, 10),
        Paragraph(
            "以下はREADMEの設定表に加え、v4.2.0の実装で使用している表示・同期・フェーズ制御の詳細です。",
            STYLES["body"],
        ),
        SectionBand("イベント状態遷移", ORANGE, height=27),
        Spacer(1, 7),
        make_table(flow_rows),
        Spacer(1, 12),
        SectionBand("グラフィカルHUD", MAGENTA, height=27),
        Spacer(1, 7),
        ListFlowable(
            [
                ListItem(Paragraph(text, STYLES["body"]), leftIndent=4)
                for text in [
                    "HUDはプレイ可能なステージのMain状態だけに表示します。ショップ、ロビー、アリーナ、チュートリアルでは表示しません。",
                    "物理スロット数はステージ中常に5枠です。ホスト側上限を超える枠には鍵を表示し、設定変更は次のステージから反映します。",
                    "待機中は次回イベント集合の最大危険度、発動中は各イベント個別の危険度を枠色で示します。",
                    "残り時間の数値表示は行わず、同時イベント共通のアナログストップウォッチを1個表示します。",
                    "アイコン切替時は対象スロットが同時に回転し、縦型は上から下、横型は左から右へ停止します。",
                    "アイコン切替は約75msごと、最初の停止は約600ms、以降は約180ms差、停止後の収束は約220msです。",
                    "HUDアニメーションはローカル表示だけで、ゲーム上の効果やチャット通知を遅延させません。",
                    "R.E.P.O.のUIフォントを取得して適用します。取得前または取得不可の場合は同梱UI側のフォントで表示します。",
                ]
            ],
            bulletType="bullet",
            leftIndent=17,
            bulletColor=MAGENTA,
            bulletFontName="BIZUDB",
            bulletFontSize=7,
        ),
        Spacer(1, 10),
        make_table(risk_rows),
        Spacer(1, 12),
        PageBreak(),
        SectionBand("マルチプレイ同期", CYAN, height=27),
        Spacer(1, 7),
        make_table(sync_rows),
        Spacer(1, 12),
        SectionBand("適用更新・終了処理", ORANGE, height=27),
        Spacer(1, 7),
        make_table(maintenance_rows),
    ]


def icon_card(event: tuple) -> Table:
    name, ja_name, _, enabled, chance, risk, _, _ = event
    color = RISK_COLOR[risk]
    image = Image(str(icon_asset_path(name)), width=15 * mm, height=15 * mm)
    label = Paragraph(
        f"<font color='#FFFFFF'>{name}</font><br/>"
        f"<font color='#B4BDB7'>{ja_name}</font><br/>"
        f"<font color='{color.hexval()}'>"
        f"{risk.upper()} / {'ON' if enabled else 'OFF'} / {chance}%</font>",
        ParagraphStyle(
            f"icon_{name}",
            fontName="BIZUDB",
            fontSize=6.6,
            leading=9,
            textColor=INK,
            alignment=TA_CENTER,
            wordWrap="CJK",
        ),
    )
    card = Table([[image], [label]], colWidths=[CONTENT_W / 4 - 5])
    card.setStyle(TableStyle([
        ("BACKGROUND", (0, 0), (-1, -1), PANEL),
        ("BOX", (0, 0), (-1, -1), 1.1, color),
        ("ALIGN", (0, 0), (-1, -1), "CENTER"),
        ("VALIGN", (0, 0), (-1, -1), "MIDDLE"),
        ("TOPPADDING", (0, 0), (-1, 0), 5),
        ("BOTTOMPADDING", (0, 0), (-1, 0), 2),
        ("TOPPADDING", (0, 1), (-1, 1), 2),
        ("BOTTOMPADDING", (0, 1), (-1, 1), 5),
    ]))
    return card


def icon_appendix() -> list:
    story: list = [
        PageBreak(),
        SectionBand("イベントアイコン・危険度索引", CYAN, height=36),
        Spacer(1, 8),
        Paragraph(
            "表記は「危険度 / デフォルト有効状態 / ChancePercent」です。"
            "危険度の枠色はグラフィカルHUDと共通です。",
            STYLES["body"],
        ),
    ]
    for page_start in range(0, len(EVENTS), 20):
        if page_start > 0:
            story.append(PageBreak())
            story.append(SectionBand("イベントアイコン・危険度索引（続き）", CYAN, height=36))
            story.append(Spacer(1, 10))
        page_events = EVENTS[page_start:page_start + 20]
        rows = []
        for row_start in range(0, len(page_events), 4):
            row = [icon_card(event) for event in page_events[row_start:row_start + 4]]
            while len(row) < 4:
                row.append("")
            rows.append(row)
        grid = Table(
            rows,
            colWidths=[CONTENT_W / 4] * 4,
            rowHeights=[34 * mm] * len(rows),
        )
        grid.setStyle(TableStyle([
            ("VALIGN", (0, 0), (-1, -1), "MIDDLE"),
            ("ALIGN", (0, 0), (-1, -1), "CENTER"),
            ("LEFTPADDING", (0, 0), (-1, -1), 3),
            ("RIGHTPADDING", (0, 0), (-1, -1), 3),
            ("TOPPADDING", (0, 0), (-1, -1), 3),
            ("BOTTOMPADDING", (0, 0), (-1, -1), 3),
        ]))
        story.append(grid)
    return story


def draw_page_frame(c, doc) -> None:
    c.saveState()
    c.setFillColor(BG)
    c.rect(0, 0, PAGE_W, PAGE_H, fill=1, stroke=0)
    c.setFillColor(CYAN)
    c.rect(0, PAGE_H - 5, PAGE_W * 0.42, 5, fill=1, stroke=0)
    c.setFillColor(MAGENTA)
    c.rect(PAGE_W * 0.42, PAGE_H - 5, PAGE_W * 0.33, 5, fill=1, stroke=0)
    c.setFillColor(ORANGE)
    c.rect(PAGE_W * 0.75, PAGE_H - 5, PAGE_W * 0.25, 5, fill=1, stroke=0)

    if doc.page == 1:
        draw_cover(c)
        c.restoreState()
        return

    c.setStrokeColor(GRID)
    c.setLineWidth(0.5)
    c.line(MARGIN_X, 25, PAGE_W - MARGIN_X, 25)
    c.setFont("BIZUDB", 7)
    c.setFillColor(MUTED)
    c.drawString(MARGIN_X, 12, "STAGE FLUX v4.2.0  /  FULL SPECIFICATION")
    c.drawRightString(PAGE_W - MARGIN_X, 12, f"{doc.page:02d}")
    c.restoreState()


def draw_cover(c) -> None:
    c.setFillColor(CYAN)
    c.setFont("BIZUDB", 12)
    c.drawString(MARGIN_X, PAGE_H - 64, "HOST-ONLY RANDOM STAGE EVENT MOD")
    c.setFillColor(white)
    c.setFont("BIZUDB", 34)
    c.drawString(MARGIN_X, PAGE_H - 120, "STAGE FLUX")
    c.setFillColor(INK)
    c.setFont("BIZUDB", 23)
    c.drawString(MARGIN_X, PAGE_H - 160, "現行仕様書")
    c.setFillColor(MUTED)
    c.setFont("BIZUDB", 10)
    c.drawString(MARGIN_X, PAGE_H - 187, "v4.2.0  /  2026-07-23  /  日本語版")

    mod_icon_size = 170
    mod_icon_x = (PAGE_W - mod_icon_size) / 2
    mod_icon_y = 430
    c.setFillColor(PANEL)
    c.setStrokeColor(CYAN)
    c.setLineWidth(2.2)
    c.roundRect(
        mod_icon_x - 8,
        mod_icon_y - 8,
        mod_icon_size + 16,
        mod_icon_size + 16,
        12,
        fill=1,
        stroke=1,
    )
    c.drawImage(
        ImageReader(str(ROOT / "package" / "icon.png")),
        mod_icon_x,
        mod_icon_y,
        mod_icon_size,
        mod_icon_size,
        preserveAspectRatio=True,
        mask="auto",
    )
    c.setFont("BIZUDB", 9)
    c.setFillColor(CYAN)
    c.drawCentredString(PAGE_W / 2, 406, "MOD本体アイコン")

    names = ["Feather", "ZeroGravity", "EnemyWave", "DangerousValuables"]
    size = 56
    gap = 12
    start_x = (PAGE_W - (size * 4 + gap * 3)) / 2
    start_y = 319
    c.setFont("BIZUDB", 8)
    c.setFillColor(MUTED)
    c.drawCentredString(PAGE_W / 2, 389, "EVENT ICON EXAMPLES")
    for index, name in enumerate(names):
        x = start_x + index * (size + gap)
        y = start_y
        color = event_risk_color(name)
        c.setFillColor(PANEL)
        c.setStrokeColor(color)
        c.setLineWidth(1.5)
        c.roundRect(x - 4, y - 4, size + 8, size + 8, 6, fill=1, stroke=1)
        c.drawImage(
            ImageReader(str(icon_asset_path(name))),
            x, y, size, size,
            preserveAspectRatio=True,
            mask="auto",
        )

    c.setFillColor(PANEL)
    c.roundRect(MARGIN_X, 103, CONTENT_W, 148, 8, fill=1, stroke=0)
    c.setFont("BIZUDB", 9)
    c.setFillColor(INK)
    lines = [
        "Thunderstore名: StageFlux",
        "プラグイン: StagePhysicsEvents.dll",
        "バージョン: 4.2.0",
        "ホスト専用ゲーム処理 / REPOConfig対応",
        "",
        "全34イベント / 全設定項目 / 抽選・モード・対象・安全仕様",
        "通知 / HUD同期・アニメーション / 互換性 / アイコン索引",
    ]
    for index, line in enumerate(lines):
        c.drawString(MARGIN_X + 15, 229 - index * 17, line)


def build() -> None:
    register_fonts()
    OUTPUT.parent.mkdir(parents=True, exist_ok=True)
    doc = SimpleDocTemplate(
        str(OUTPUT),
        pagesize=A4,
        leftMargin=MARGIN_X,
        rightMargin=MARGIN_X,
        topMargin=TOP_MARGIN,
        bottomMargin=BOTTOM_MARGIN,
        title="Stage Flux v4.2.0 - Full Specification",
        author="Stage Flux",
        subject="Current full specification, configuration, events, HUD, and compatibility",
        pageCompression=1,
    )
    story: list = [
        Spacer(1, PAGE_H - TOP_MARGIN - BOTTOM_MARGIN - 28),
        PageBreak(),
        *contents_page(),
        *parse_japanese_readme(),
        *current_runtime_spec(),
        *icon_appendix(),
    ]
    doc.build(story, onFirstPage=draw_page_frame, onLaterPages=draw_page_frame)
    print(OUTPUT)


if __name__ == "__main__":
    build()
