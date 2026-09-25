from __future__ import annotations

from pathlib import Path
from typing import Iterable

from reportlab.lib.colors import Color, HexColor, white
from reportlab.lib.enums import TA_LEFT
from reportlab.lib.pagesizes import A4
from reportlab.lib.styles import ParagraphStyle
from reportlab.lib.utils import ImageReader
from reportlab.pdfbase import pdfmetrics
from reportlab.pdfbase.ttfonts import TTFont
from reportlab.pdfgen import canvas
from reportlab.platypus import Paragraph


ROOT = Path(__file__).resolve().parents[1]
OUTPUT = ROOT / "output" / "pdf" / "StageFlux_Event_List_v4.2.3.pdf"
ICON_DIR = ROOT / "Assets" / "EventIcons" / "Runtime"
PDF_ICON_DIR = ROOT / "Assets" / "EventIcons" / "PDF"
FONT_REGULAR_PATH = Path(r"C:\Windows\Fonts\BIZ-UDGothicR.ttc")
FONT_BOLD_PATH = Path(r"C:\Windows\Fonts\BIZ-UDGothicB.ttc")

PAGE_W, PAGE_H = A4
MARGIN = 28
CYAN = HexColor("#46C6E8")
MAGENTA = HexColor("#E34B9A")
ORANGE = HexColor("#E49A29")
INK = HexColor("#E4E8E2")
MUTED = HexColor("#AAB1AC")
BACKGROUND = HexColor("#081015")
CARD = HexColor("#111A1F")
CARD_INNER = HexColor("#0A1115")

RISK = {
    "low": ("低", HexColor("#70A650")),
    "medium": ("中", HexColor("#CF9E35")),
    "high": ("高", HexColor("#B84143")),
}

EVENTS = [
    ("Feather", "フェザー", "物理・支援", True, 5, "low",
     "ステージ内の対象が軽くなり、持ち上げやすく、ふわりと動くようになります。",
     "対象は設定で選択可能: 貴重品・アイテム・扉・プレイヤー・敵など"),
    ("ZeroGravity", "無重力", "物理・支援", True, 5, "medium",
     "ステージ内の対象から重力をなくし、空中に浮かびやすくします。",
     "貴重品の保護: ON / 終了後も2秒間保護"),
    ("Battery Charge", "バッテリー充電", "物理・支援", True, 5, "low",
     "電池を使うアイテムの残量を、イベント中に繰り返し回復します。",
     "4秒ごとにバッテリーを5回復"),
    ("Heal", "ヒール", "物理・支援", True, 5, "low",
     "生きているプレイヤーの体力を、イベント中に繰り返し回復します。",
     "2秒ごとに体力を10回復"),
    ("Indestructible", "破壊不能", "物理・支援", True, 5, "low",
     "対象になった貴重品やアイテムが、イベント中は壊れなくなります。",
     "何を対象にするかは設定で変更可能"),
    ("Fragility", "脆弱化", "物理・支援", True, 5, "high",
     "貴重品が非常に壊れやすくなり、軽い衝突でも価値を失いやすくなります。",
     "壊れやすさ: 通常の10倍 / 貴重品を対象にした場合のみ"),
    ("GumballHypnosis", "ガムボール催眠", "特殊効果", True, 5, "medium",
     "対象物を持つプレイヤーへ画面効果を与え、視線を対象へ引き寄せます。",
     "扉・プレイヤー・敵を掴んでも発動しません"),
    ("HealingAura", "回復オーラ", "生成イベント", True, 5, "low",
     "触れると回復できる光のエリアが、ステージのランダムな場所に現れます。",
     "10秒ごとに1-3個 / 1個から合計50まで回復可能"),
    ("StarBarrage", "スターバラージ", "生成イベント", True, 5, "high",
     "ステージのランダムな場所と方向から、危険な星の弾が飛んできます。",
     "2秒ごとに3-6発 / 貴重品の保護: ON"),
    ("SpiderScare", "スパイダー恐怖", "生成イベント", True, 5, "medium",
     "ランダムなプレイヤーの画面に、蜘蛛が現れる恐怖演出を発生させます。",
     "10秒ごとに1-3人 / クモ恐怖症向けの表示設定に対応"),
    ("TrafficShock", "トラフィックショック", "生成イベント", True, 5, "high",
     "信号機の赤信号と同じ効果で、ランダムなプレイヤーを感電・転倒させます。",
     "10秒ごとに1-3人"),
    ("DangerousValuables", "危険な貴重品", "貴重品イベント", True, 5, "high",
     "ステージに最初から置かれている、危険な仕掛け付き貴重品を作動させます。",
     "10秒ごとに5-10個 / 新しく増やさない / 貴重品の保護: OFF"),
    ("Roll", "ロール", "物理ハザード", False, 5, "high",
     "対象が激しく回転・移動します。プレイヤーは見ている方向へ動けます。",
     "既定では無効 / 衝突などで敵が死亡する可能性あり"),
    ("Void", "ボイド", "物理ハザード", False, 5, "high",
     "危険なボイドが複数現れ、一定時間ごとに別の場所へ出現し直します。",
     "既定では無効 / 10秒ごとに3-5個 / プレイヤーが死亡する可能性あり"),
    ("Levitation", "浮遊", "生成イベント", True, 5, "medium",
     "物やプレイヤーを浮かせるエリアが、ステージのランダムな場所に現れます。",
     "10秒ごとに2-5個 / 貴重品の保護: ON"),
    ("Shockwave", "ショックウェーブ", "生成イベント", True, 5, "medium",
     "吹き飛ばし効果のあるグレネードが、ランダムな方向へ投げ込まれます。",
     "10秒ごとに5-10個 / プレイヤーから3m以上離れた場所に出現"),
    ("StunBlast", "スタンブラスト", "生成イベント", True, 5, "medium",
     "周囲をスタンさせるグレネードが、ランダムな方向へ投げ込まれます。",
     "10秒ごとに5-10個 / プレイヤーから3m以上離れた場所に出現"),
    ("ExplosionRain", "爆発の雨", "生成イベント", True, 5, "high",
     "爆発するグレネードが、ステージ内へランダムな方向から投げ込まれます。",
     "10秒ごとに5-10個 / プレイヤーから3m以上離れた場所に出現"),
    ("EnemyWave", "敵ウェーブ", "敵イベント", True, 5, "high",
     "現在のステージに登場できる敵が、追加で1-3体活動を始めます。",
     "10秒ごとに不足分を補充 / イベント終了時に追加した敵を消去"),
    ("Minefield", "地雷原", "生成イベント", True, 5, "high",
     "起動済みの地雷がステージ内に置かれ、作動・破壊されると補充されます。",
     "10秒ごとに合計10-15個へ補充 / 起爆中の地雷は終了後も残る"),
    ("Freeze", "凍結", "敵制御", True, 5, "medium",
     "イベント中、対象の敵が凍りついて動けなくなります。",
     "敵を効果対象にしている場合のみ発動"),
    ("Stun", "スタン", "敵制御", True, 5, "medium",
     "イベント中、対応している敵が気絶して行動できなくなります。",
     "敵を効果対象にしている場合のみ発動"),
    ("EnemyWarp", "敵ワープ", "敵制御", True, 5, "high",
     "敵が突然ワープし、ステージ内の別の場所から現れます。",
     "10秒ごとに3体 / プレイヤーから3m以上離れた場所へ移動"),
    ("EnemyHunt", "敵ハント", "敵制御", True, 5, "high",
     "全納品完了後、プレイヤーがいる部屋で誘導音を鳴らし敵を向かわせます。",
     "5秒ごとに音で敵を誘導 / MODを入れていない参加者にも同じ効果"),
    ("EnemySpeedUp", "敵速度上昇", "敵制御", True, 5, "high",
     "すべての敵の移動と加速が速くなり、途中から現れた敵にも適用されます。",
     "通常の1.5倍 / 敵速度低下とは同時に発動しない"),
    ("EnemySpeedDown", "敵速度低下", "敵制御", True, 5, "low",
     "すべての敵の移動と加速が遅くなり、途中から現れた敵にも適用されます。",
     "通常の半分 / 敵速度上昇とは同時に発動しない"),
    ("EnemyRegen", "敵再生", "敵制御", True, 5, "high",
     "イベント中、すべての敵が少しずつ体力を回復します。",
     "5秒ごとに体力を10回復"),
    ("EnemyPurge", "敵粛清", "敵制御", True, 5, "low",
     "イベント中、すべての敵が少しずつダメージを受けます。",
     "5秒ごとに10ダメージ / この効果で敵が倒れることがあります"),
    ("DamagePulse", "ダメージパルス", "プレイヤー", True, 5, "high",
     "イベント中、生きているすべてのプレイヤーが繰り返しダメージを受けます。",
     "5秒ごとに5ダメージ / この効果だけでは体力が1未満にならない"),
    ("SecondChance", "セカンドチャンス", "プレイヤー", True, 5, "low",
     "イベント開始後に死亡したプレイヤーを、死亡地点で2秒後に復活させます。",
     "各プレイヤー1回 / 開始前に死亡していたプレイヤーは対象外"),
    ("Knockback", "ノックバック", "プレイヤー", True, 5, "medium",
     "生きているプレイヤーが、ランダムな方向へ定期的に吹き飛ばされます。",
     "5秒ごと / 吹き飛ばす強さは設定で変更可能"),
    ("Flicker", "フリッカー", "プレイヤー", True, 5, "medium",
     "生きているプレイヤーの周囲で、赤いライトが繰り返し点滅します。",
     "2秒ごと / 明るさは通常効果の2倍"),
    ("Quake", "地震", "ステージ・物体", True, 5, "high",
     "プレイヤーや床に置かれた物が、地震のようにランダムな方向へ揺さぶられます。",
     "5秒ごと / 貴重品の保護: ON"),
    ("DoorChaos", "ドアカオス", "ステージ・物体", True, 5, "medium",
     "通常の扉や大型扉、ふた付きの物が、イベント中に何度も開閉します。",
     "2秒ごとに対象の30% / 開閉力12 / 扉を対象外にしていても本イベント中だけ作用"),
    ("ValueSurge", "価値上昇", "ステージ・物体", True, 5, "low",
     "納品所やカート、トラック内を含む、すべての貴重品の価格が上がります。",
     "通常価格の1.5倍 / 終了時は損傷を維持して倍率だけ解除"),
    ("ValueCrash", "価値暴落", "ステージ・物体", True, 5, "high",
     "納品所やカート、トラック内を含む、すべての貴重品の価格が下がります。",
     "通常価格の半分 / 終了時は損傷を維持して倍率だけ解除"),
]

EVENTS = [event[:4] + (6,) + event[5:] for event in EVENTS]


def icon_asset_name(event_name: str) -> str:
    return "Battery" if event_name == "Battery Charge" else event_name


def icon_asset_path(event_name: str) -> Path:
    asset_name = icon_asset_name(event_name)
    for extension in (".jpg", ".png"):
        high_resolution_path = PDF_ICON_DIR / f"{asset_name}{extension}"
        if high_resolution_path.exists():
            return high_resolution_path
    return ICON_DIR / f"{asset_name}.png"


def register_fonts() -> None:
    pdfmetrics.registerFont(TTFont("BIZUDGothic", str(FONT_REGULAR_PATH), subfontIndex=0))
    pdfmetrics.registerFont(TTFont("BIZUDGothicBold", str(FONT_BOLD_PATH), subfontIndex=0))


def paragraph(
    c: canvas.Canvas,
    text: str,
    x: float,
    y_top: float,
    width: float,
    height: float,
    *,
    size: float = 8.5,
    color: Color = INK,
    leading: float | None = None,
) -> None:
    style = ParagraphStyle(
        "body",
        fontName="BIZUDGothicBold",
        fontSize=size,
        leading=leading or size * 1.45,
        textColor=color,
        alignment=TA_LEFT,
        wordWrap="CJK",
        spaceAfter=0,
        spaceBefore=0,
    )
    p = Paragraph(text, style)
    _, used_h = p.wrap(width, height)
    p.drawOn(c, x, y_top - used_h)


def draw_background(c: canvas.Canvas) -> None:
    c.setFillColor(BACKGROUND)
    c.rect(0, 0, PAGE_W, PAGE_H, fill=1, stroke=0)
    c.setFillColor(CYAN)
    c.rect(0, PAGE_H - 5, PAGE_W * 0.42, 5, fill=1, stroke=0)
    c.setFillColor(MAGENTA)
    c.rect(PAGE_W * 0.42, PAGE_H - 5, PAGE_W * 0.33, 5, fill=1, stroke=0)
    c.setFillColor(ORANGE)
    c.rect(PAGE_W * 0.75, PAGE_H - 5, PAGE_W * 0.25, 5, fill=1, stroke=0)


def draw_footer(c: canvas.Canvas, page_number: int, label: str = "EVENT CATALOG") -> None:
    c.setStrokeColor(HexColor("#27353D"))
    c.setLineWidth(0.6)
    c.line(MARGIN, 23, PAGE_W - MARGIN, 23)
    c.setFont("BIZUDGothicBold", 7.5)
    c.setFillColor(MUTED)
    c.drawString(MARGIN, 11, f"STAGE FLUX v4.2.3  /  {label}")
    c.drawRightString(PAGE_W - MARGIN, 11, f"{page_number:02d}")


def draw_cover(c: canvas.Canvas) -> None:
    draw_background(c)
    c.setFont("BIZUDGothicBold", 13)
    c.setFillColor(CYAN)
    c.drawString(MARGIN, PAGE_H - 52, "HOST-ONLY RANDOM STAGE EVENT MOD")

    c.setFont("BIZUDGothicBold", 39)
    c.setFillColor(white)
    c.drawString(MARGIN, PAGE_H - 117, "STAGE FLUX")
    c.setFont("BIZUDGothicBold", 24)
    c.setFillColor(INK)
    c.drawString(MARGIN, PAGE_H - 158, "イベント一覧")

    c.setFont("BIZUDGothicBold", 12)
    c.setFillColor(MUTED)
    c.drawString(MARGIN, PAGE_H - 189, "v4.2.3  /  全36イベント・実装アイコン収録")

    cover_icons = ["Feather", "ZeroGravity", "EnemyWave", "DangerousValuables"]
    icon_size = 105
    gap = 18
    start_x = (PAGE_W - (icon_size * 2 + gap)) / 2
    start_y = 420
    risk_colors = [RISK["low"][1], RISK["medium"][1], RISK["high"][1], RISK["high"][1]]
    for index, (name, border) in enumerate(zip(cover_icons, risk_colors)):
        col = index % 2
        row = index // 2
        x = start_x + col * (icon_size + gap)
        y = start_y - row * (icon_size + gap)
        c.setFillColor(CARD)
        c.setStrokeColor(border)
        c.setLineWidth(2)
        c.roundRect(x - 7, y - 7, icon_size + 14, icon_size + 14, 8, fill=1, stroke=1)
        c.drawImage(
            ImageReader(str(icon_asset_path(name))),
            x, y, icon_size, icon_size,
            preserveAspectRatio=True,
            mask="auto",
        )

    paragraph(
        c,
        "設定した確率でステージ全体を揺さぶるイベントMOD。"
        "本書では、HUDで使用されるアイコンと危険度、既定の有効状態・抽選確率、"
        "各イベントの概要を一覧化しています。",
        MARGIN,
        230,
        PAGE_W - MARGIN * 2,
        90,
        size=12,
        leading=18,
    )

    for index, risk in enumerate(("low", "medium", "high")):
        label, color = RISK[risk]
        x = MARGIN + index * 116
        c.setFillColor(color)
        c.roundRect(x, 118, 100, 26, 5, fill=1, stroke=0)
        c.setFont("BIZUDGothicBold", 8.5)
        c.setFillColor(white)
        c.drawCentredString(x + 50, 127, f"危険度 {label}")

    c.setFont("BIZUDGothicBold", 8)
    c.setFillColor(MUTED)
    c.drawString(MARGIN, 90, "危険度はStage FluxのグラフィカルHUDと同じ色分けです。")
    draw_footer(c, 1, "EVENT CATALOG / COVER")
    c.showPage()


def draw_card(c: canvas.Canvas, event: tuple, x: float, y: float, w: float, h: float) -> None:
    name, ja_name, category, enabled, chance, risk, description, settings = event
    risk_label, risk_color = RISK[risk]

    c.setFillColor(CARD)
    c.setStrokeColor(risk_color)
    c.setLineWidth(1.8)
    c.roundRect(x, y, w, h, 8, fill=1, stroke=1)

    c.setFillColor(risk_color)
    c.roundRect(x + 9, y + h - 23, 62, 15, 4, fill=1, stroke=0)
    c.setFont("BIZUDGothicBold", 7.2)
    c.setFillColor(white)
    c.drawCentredString(x + 40, y + h - 18.2, f"危険度 {risk_label}")

    c.setFont("BIZUDGothicBold", 7.2)
    c.setFillColor(MUTED)
    c.drawRightString(x + w - 9, y + h - 18.2, category)

    icon_size = 70
    icon_x = x + 12
    icon_y = y + h - 104
    c.setFillColor(CARD_INNER)
    c.roundRect(icon_x - 3, icon_y - 3, icon_size + 6, icon_size + 6, 5, fill=1, stroke=0)
    c.drawImage(
        ImageReader(str(icon_asset_path(name))),
        icon_x,
        icon_y,
        icon_size,
        icon_size,
        preserveAspectRatio=True,
        mask="auto",
    )

    title_x = icon_x + icon_size + 12
    title_w = x + w - 10 - title_x
    paragraph(c, name, title_x, y + h - 42, title_w, 30, size=13.5, leading=16)
    paragraph(c, ja_name, title_x, y + h - 69, title_w, 22, size=9.2, color=MUTED)

    enabled_color = HexColor("#70A650") if enabled else HexColor("#59636A")
    enabled_text = "既定 ON" if enabled else "既定 OFF"
    c.setFillColor(enabled_color)
    c.roundRect(title_x, y + h - 99, 67, 17, 4, fill=1, stroke=0)
    c.setFont("BIZUDGothicBold", 7)
    c.setFillColor(white)
    c.drawCentredString(title_x + 33.5, y + h - 93.3, enabled_text)
    c.setFillColor(HexColor("#1D5969"))
    c.roundRect(title_x + 73, y + h - 99, 55, 17, 4, fill=1, stroke=0)
    c.setFillColor(white)
    c.drawCentredString(title_x + 100.5, y + h - 93.3, f"発生率 {chance}%")

    c.setStrokeColor(HexColor("#2A3941"))
    c.setLineWidth(0.5)
    c.line(x + 10, y + h - 114, x + w - 10, y + h - 114)
    paragraph(
        c,
        description,
        x + 12,
        y + h - 128,
        w - 24,
        58,
        size=9,
        leading=13,
    )

    c.setFillColor(CARD_INNER)
    c.roundRect(x + 10, y + 12, w - 20, 42, 5, fill=1, stroke=0)
    paragraph(
        c,
        f"<font color='#46C6E8'>既定・注意</font>  {settings}",
        x + 16,
        y + 45,
        w - 32,
        31,
        size=8,
        color=INK,
        leading=11,
    )


def chunks(items: list, size: int) -> Iterable[list]:
    for start in range(0, len(items), size):
        yield items[start:start + size]


def draw_event_pages(c: canvas.Canvas) -> None:
    card_gap_x = 10
    card_gap_y = 10
    card_w = (PAGE_W - MARGIN * 2 - card_gap_x) / 2
    top = PAGE_H - 69
    bottom = 33
    card_h = (top - bottom - card_gap_y * 2) / 3

    page_number = 2
    for page_events in chunks(EVENTS, 6):
        draw_background(c)
        c.setFont("BIZUDGothicBold", 18)
        c.setFillColor(INK)
        c.drawString(MARGIN, PAGE_H - 37, "イベントカタログ")
        c.setFont("BIZUDGothicBold", 8)
        c.setFillColor(MUTED)
        c.drawRightString(
            PAGE_W - MARGIN,
            PAGE_H - 34,
            f"{(page_number - 2) * 6 + 1:02d} - "
            f"{min((page_number - 1) * 6, len(EVENTS)):02d} / {len(EVENTS):02d}",
        )

        for index, event in enumerate(page_events):
            col = index % 2
            row = index // 2
            x = MARGIN + col * (card_w + card_gap_x)
            y = top - (row + 1) * card_h - row * card_gap_y
            draw_card(c, event, x, y, card_w, card_h)

        draw_footer(c, page_number)
        c.showPage()
        page_number += 1


def draw_notes(c: canvas.Canvas) -> None:
    draw_background(c)
    c.setFont("BIZUDGothicBold", 22)
    c.setFillColor(INK)
    c.drawString(MARGIN, PAGE_H - 56, "抽選と安全設定")
    c.setFont("BIZUDGothicBold", 9)
    c.setFillColor(MUTED)
    c.drawString(MARGIN, PAGE_H - 78, "一覧を読む際の共通ルール")

    notes = [
        ("ステージごとの抽選", "新しいステージが始まると、既定では20%の確率でイベントが有効になります。外れた場合、そのステージではイベントも専用表示も出ません。"),
        ("各イベントの抽選", "各イベントはそれぞれ6%の確率で選ばれます。ロールとボイドだけは最初から無効で、それ以外は有効です。"),
        ("繰り返し発生する間隔", "弾や地雷などがイベント中に何度も現れる場合、その間隔を1-300秒の範囲で変更できます。"),
        ("同時に起きる数", "既定では一度に最大3イベントまで発生します。設定で1-5へ変更でき、当選数が上限を超えた場合はランダムに絞られます。"),
        ("危険な組み合わせ", "危険なイベントが重なりすぎない設定は既定で有効です。強制移動、致命的な攻撃、敵の強化、視界妨害、貴重品損失が重なる候補を除外します。詳細は次ページを参照してください。"),
        ("必ず同時発動しない組み合わせ", "凍結とスタン、敵速度上昇と敵速度低下、敵再生と敵粛清、破壊不能と脆弱化、価値上昇と価値暴落は同時に発生しません。"),
        ("貴重品の保護", "危険なイベントごとに貴重品を壊れなくするか選べます。イベント終了後も1-5秒保護できます。貴重品を効果対象から外した場合は保護されません。"),
    ]
    y = PAGE_H - 120
    for index, (title, body) in enumerate(notes):
        color = (CYAN, MAGENTA, ORANGE)[index % 3]
        c.setFillColor(CARD)
        c.setStrokeColor(color)
        c.setLineWidth(1.3)
        c.roundRect(MARGIN, y - 84, PAGE_W - MARGIN * 2, 74, 7, fill=1, stroke=1)
        c.setFont("BIZUDGothicBold", 11)
        c.setFillColor(color)
        c.drawString(MARGIN + 14, y - 34, title)
        paragraph(
            c, body, MARGIN + 14, y - 45,
            PAGE_W - MARGIN * 2 - 28, 35,
            size=9.2, leading=13,
        )
        y -= 86

    c.setFillColor(CARD)
    c.roundRect(MARGIN, 47, PAGE_W - MARGIN * 2, 60, 7, fill=1, stroke=0)
    paragraph(
        c,
        "注: 本書の既定値はv4.2.3の新規設定を基準にしています。"
        "以前から使用している場合は、すでに保存されている設定が引き続き使われます。",
        MARGIN + 14,
        91,
        PAGE_W - MARGIN * 2 - 28,
        38,
        size=9.2,
        leading=14,
    )
    draw_footer(c, 8, "EVENT CATALOG / NOTES")
    c.showPage()


def draw_dangerous_combinations(c: canvas.Canvas) -> None:
    draw_background(c)
    c.setFont("BIZUDGothicBold", 22)
    c.setFillColor(INK)
    c.drawString(MARGIN, PAGE_H - 56, "危険な組み合わせ")
    c.setFont("BIZUDGothicBold", 9)
    c.setFillColor(MUTED)
    c.drawString(MARGIN, PAGE_H - 78, "危険なイベントが重なりすぎないようにする仕組み")

    c.setFillColor(CARD)
    c.setStrokeColor(ORANGE)
    c.setLineWidth(1.5)
    c.roundRect(MARGIN, PAGE_H - 160, PAGE_W - MARGIN * 2, 60, 7, fill=1, stroke=1)
    c.setFont("BIZUDGothicBold", 11)
    c.setFillColor(ORANGE)
    c.drawString(MARGIN + 14, PAGE_H - 124, "既定: 許可しない")
    paragraph(
        c,
        "既定では、下記の危険条件が重なるイベントを同時発動の候補から外します。"
        "設定で危険な組み合わせを許可しても、同時発動数の上限と、反対の効果同士を避けるルールは残ります。",
        MARGIN + 132,
        PAGE_H - 116,
        PAGE_W - MARGIN * 2 - 146,
        42,
        size=9,
        leading=13,
    )

    gap = 12
    column_w = (PAGE_W - MARGIN * 2 - gap) / 2
    left_x = MARGIN
    right_x = MARGIN + column_w + gap
    main_y = 265
    main_h = 390

    c.setFillColor(CARD)
    c.setStrokeColor(MAGENTA)
    c.roundRect(left_x, main_y, column_w, main_h, 7, fill=1, stroke=1)
    c.setFont("BIZUDGothicBold", 12)
    c.setFillColor(MAGENTA)
    c.drawString(left_x + 14, main_y + main_h - 28, "同時発動から外れる組み合わせ")

    blocked_pairs = [
        ("強制移動", "強制移動"),
        ("致命的", "致命的"),
        ("強制移動", "致命的 または 広範囲"),
        ("敵の脅威", "敵の脅威・致命的・操作妨害"),
        ("操作妨害", "操作妨害・強制移動・致命的・広範囲"),
        ("視界妨害", "視界妨害"),
        ("脆弱化", "貴重品危険 または 価値暴落"),
        ("価値暴落", "貴重品危険"),
    ]
    row_y = main_y + main_h - 61
    for left, right in blocked_pairs:
        c.setFillColor(CARD_INNER)
        c.roundRect(left_x + 12, row_y - 24, column_w - 24, 29, 5, fill=1, stroke=0)
        c.setFont("BIZUDGothicBold", 6.7)
        c.setFillColor(INK)
        c.drawString(left_x + 19, row_y - 13, left)
        c.setFillColor(MAGENTA)
        c.drawCentredString(left_x + column_w / 2, row_y - 13, "×")
        c.setFillColor(INK)
        c.drawRightString(left_x + column_w - 19, row_y - 13, right)
        row_y -= 34

    c.setFillColor(CARD_INNER)
    c.roundRect(left_x + 12, main_y + 12, column_w - 24, 48, 5, fill=1, stroke=0)
    paragraph(
        c,
        "<font color='#E49A29'>貴重品が壊れる危険の上限</font><br/>"
        "貴重品に危険なイベントは同時に2個まで。3個目の候補を外します。",
        left_x + 22,
        main_y + 53,
        column_w - 44,
        38,
        size=7.4,
        leading=10,
    )

    c.setFillColor(CARD)
    c.setStrokeColor(CYAN)
    c.roundRect(right_x, main_y, column_w, main_h, 7, fill=1, stroke=1)
    c.setFont("BIZUDGothicBold", 12)
    c.setFillColor(CYAN)
    c.drawString(right_x + 14, main_y + main_h - 28, "各グループに含まれるイベント")

    risk_groups = [
        ("強制移動 - 吹き飛ばし・浮遊", "無重力 / ロール / ボイド / 浮遊 / ショックウェーブ / ノックバック / 地震 / トラフィックショック / 危険な貴重品"),
        ("致命的 - 死亡につながる攻撃", "ボイド / 爆発の雨 / ダメージパルス / スターバラージ / トラフィックショック / 危険な貴重品"),
        ("広範囲 - 場所全体へ及ぶ危険", "ボイド / ショックウェーブ / スタンブラスト / 爆発の雨 / スターバラージ / 危険な貴重品 / 地雷原 / ドアカオス"),
        ("敵の脅威 - 敵の追加・転送・誘導・強化", "敵ウェーブ / 敵ワープ / 敵ハント / 敵速度上昇 / 敵再生"),
        ("操作・視界妨害", "ガムボール催眠 / スパイダー恐怖 / フリッカー"),
        ("貴重品危険 - 破損につながる効果", "無重力 / ロール / ボイド / 浮遊 / ショックウェーブ / 爆発の雨 / 地震 / ドアカオス / 脆弱化 / スターバラージ / 危険な貴重品"),
        ("価値損失の増幅", "脆弱化 / 価値暴落"),
    ]
    group_y = main_y + main_h - 52
    for title, body in risk_groups:
        c.setFont("BIZUDGothicBold", 7.5)
        c.setFillColor(CYAN)
        c.drawString(right_x + 14, group_y - 10, title)
        paragraph(
            c,
            body,
            right_x + 14,
            group_y - 15,
            column_w - 28,
            31,
            size=6.4,
            color=INK,
            leading=8.5,
        )
        group_y -= 47

    c.setFillColor(CARD)
    c.setStrokeColor(HexColor("#70A650"))
    c.roundRect(MARGIN, 58, PAGE_W - MARGIN * 2, 180, 7, fill=1, stroke=1)
    c.setFont("BIZUDGothicBold", 11)
    c.setFillColor(HexColor("#70A650"))
    c.drawString(MARGIN + 14, 211, "常に同時発動しない組み合わせ")
    paragraph(
        c,
        "凍結 × スタン　/　敵速度上昇 × 敵速度低下　/　"
        "敵再生 × 敵粛清　/　破壊不能 × 脆弱化　/　"
        "価値上昇 × 価値暴落",
        MARGIN + 14,
        195,
        PAGE_W - MARGIN * 2 - 28,
        42,
        size=8.6,
        leading=13,
    )
    c.setStrokeColor(HexColor("#2A3941"))
    c.line(MARGIN + 14, 147, PAGE_W - MARGIN - 14, 147)
    paragraph(
        c,
        "<font color='#E49A29'>地雷原は使用する地雷で危険度が変化</font><br/>"
        "通常は広範囲の危険として扱います。爆発地雷を使う場合は致命的・貴重品危険、"
        "ショックウェーブ地雷を使う場合は強制移動・貴重品危険としても扱います。<br/>"
        "<font color='#AAB1AC'>注: 当選したイベントを確認する順番によって、候補から外れるイベントが変わる場合があります。</font>",
        MARGIN + 14,
        137,
        PAGE_W - MARGIN * 2 - 28,
        72,
        size=8.2,
        leading=12,
    )

    draw_footer(c, 9, "EVENT CATALOG / DANGEROUS COMBINATIONS")
    c.showPage()


def main() -> None:
    OUTPUT.parent.mkdir(parents=True, exist_ok=True)
    register_fonts()
    c = canvas.Canvas(str(OUTPUT), pagesize=A4, pageCompression=1)
    c.setTitle("Stage Flux v4.2.3 - Event List")
    c.setAuthor("Stage Flux")
    c.setSubject("Stage Flux event catalog with HUD icons")
    draw_cover(c)
    draw_event_pages(c)
    draw_notes(c)
    draw_dangerous_combinations(c)
    c.save()
    print(OUTPUT)


if __name__ == "__main__":
    main()
