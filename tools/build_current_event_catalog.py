"""Build the player-facing Japanese catalog from the release README and current icons."""
from __future__ import annotations

import json
import math
import re
from dataclasses import dataclass
from functools import lru_cache
from io import BytesIO
from pathlib import Path
from xml.sax.saxutils import escape

from PIL import Image
from reportlab.lib.colors import HexColor
from reportlab.lib.pagesizes import A4
from reportlab.lib.styles import ParagraphStyle
from reportlab.lib.utils import ImageReader
from reportlab.pdfbase import pdfmetrics
from reportlab.pdfbase.ttfonts import TTFont
from reportlab.pdfgen import canvas
from reportlab.platypus import Paragraph

ROOT = Path(__file__).resolve().parents[1]
VERSION = json.loads((ROOT / "package/manifest.json").read_text(encoding="utf-8-sig"))["version_number"]
OUTPUT = ROOT / "output/pdf" / f"StageFlux_Event_List_v{VERSION}.pdf"
W, H = A4
M = 32
BG = HexColor("#081015")
PANEL = HexColor("#132029")
INNER = HexColor("#091115")
INK = HexColor("#F3F2E9")
MUTED = HexColor("#BECBD0")
CYAN = HexColor("#63D9EF")
AMBER = HexColor("#F0B653")
RISK = {"低": "#70A650", "中": "#CF9E35", "高": "#B84143"}

# Player-facing default settings and cautions, checked against StagePhysicsConfig
# and ExtendedEventConfig. Names/risk/state/chance/descriptions come from README.
DETAILS = {
    "Feather": ("物理・支援", "イベント中、対象を軽量化。", "効果対象は種類ごとに設定できます。"),
    "Zero Gravity": ("物理・支援", "貴重品保護 ON。終了後も2秒間保護。", "保護には貴重品を対象にする設定も必要です。"),
    "Battery Charge": ("アイテム", "4秒ごとに電池残量を5ポイント回復。", "電池を使うアイテムが対象です。"),
    "Heal": ("プレイヤー", "2秒ごとに体力を10回復。", "生きているプレイヤーが対象です。"),
    "Indestructible": ("物理・支援", "イベント中、対象を破損から保護。", "対象の種類は設定で変更できます。"),
    "Fragility": ("貴重品", "壊れやすさ1000%（通常の10倍）。", "貴重品に作用。対象フィルターに関係なく適用します。"),
    "Gumball Hypnosis": ("操作・視界", "物を持っている間、視線を引き寄せる。", "手を離すと解除。扉・プレイヤー・敵は対象外です。"),
    "Healing Aura": ("回復エリア", "10秒ごとに1-3個。1個で合計50回復。", "出現した光のエリアに触れると回復できます。"),
    "Star Barrage": ("投射物", "2秒ごとに3-6発。貴重品保護 ON。", "プレイヤーがダメージを受け、死亡することがあります。"),
    "Spider Scare": ("操作・視界", "10秒ごとに1-3人の位置で発生。", "周囲にも作用。ゲームのクモ恐怖症設定に対応します。"),
    "Traffic Shock": ("プレイヤー", "10秒ごとに1-3人を感電・転倒。", "ダメージや死亡につながることがあります。"),
    "Dangerous Valuables": ("貴重品", "10秒ごとに5-10個を選択。保護 OFF。", "貴重品は増えません。終了時も作動状態を戻しません。"),
    "Roll": ("物理ハザード", "無効。貴重品保護 ON。", "有効にすると、激しい移動や衝突で敵が死亡する場合があります。"),
    "Void": ("物理ハザード", "無効。10秒ごとに3-5個を再配置。", "有効にするとプレイヤーが死亡する場合があります。"),
    "Levitation": ("浮遊エリア", "10秒ごとに2-5個。貴重品保護 ON。", "エリアに入った物やプレイヤーが浮き上がります。"),
    "Shockwave": ("グレネード", "10秒ごとに5-10個。投射の強さ6-12。", "吹き飛ばしで転落や衝突が起きる場合があります。"),
    "Stun Blast": ("グレネード", "10秒ごとに5-10個。投射の強さ6-12。", "爆発の周囲をスタンさせます。"),
    "Explosion Rain": ("グレネード", "10秒ごとに5-10個。投射の強さ6-12。", "爆発でプレイヤーが死亡することがあります。"),
    "Enemy Wave": ("敵の追加", "1-3体。10秒ごとに不足分を補充。", "既定では終了時に追加した敵を消します。"),
    "Minefield": ("地雷", "10-15個。10秒ごとに補充。", "終了時、起爆カウント中の地雷だけは残ります。"),
    "Freeze": ("敵の行動", "イベント中、敵の凍結を維持。", "敵の対象フィルターがOFFでも作用します。"),
    "Stun": ("敵の行動", "イベント中、対応する敵の気絶を維持。", "敵の対象フィルターがOFFでも作用します。"),
    "Enemy Warp": ("敵の移動", "10秒ごとに3体を別の場所へ移動。", "移動先はプレイヤーから3m離れた場所を優先します。"),
    "Enemy Hunt": ("敵の誘導", "全納品完了後、5秒ごとに誘導。", "プレイヤーのいる部屋へ敵を呼び寄せます。"),
    "Enemy Speed Up": ("敵の強化", "移動・加速を通常の150%に。", "途中から現れた敵にも作用します。"),
    "Enemy Speed Down": ("敵の弱体化", "移動・加速を通常の50%に。", "途中から現れた敵にも作用します。"),
    "Enemy Regen": ("敵の強化", "5秒ごとに敵の体力を10回復。", "敵の最大体力を超えて回復しません。"),
    "Enemy Purge": ("敵への攻撃", "5秒ごとに敵へ10ダメージ。", "既定では、この効果で敵を倒せます。"),
    "Damage Pulse": ("プレイヤー", "5秒ごとに5ダメージ。致死回避 ON。", "他の攻撃や転落を含めた生存を保証する設定ではありません。"),
    "Second Chance": ("プレイヤー", "死亡の2秒後に復活。各プレイヤー1回。", "イベント開始前から死亡していた人は対象外です。"),
    "Knockback": ("プレイヤー", "5秒ごと。横方向の力8、上方向の力3。", "転落や衝突でダメージを受けることがあります。"),
    "Flicker": ("視界", "2秒ごとに赤く点滅。明るさ200%。", "光の点滅が苦手な場合は無効にしてください。"),
    "Quake": ("物理ハザード", "5秒ごと。揺さぶる力8。保護 ON。", "プレイヤーの転落や物の衝突に注意してください。"),
    "Door Chaos": ("扉・ヒンジ", "2秒ごとに30%を開閉。力12。", "扉の対象設定は無視。トラック・店・納品扉は除外します。"),
    "Value Surge": ("貴重品の価値", "価格150%。終了時に倍率を解除。", "損傷は残ります。期間中の納品で生じた金袋は除外します。"),
    "Value Crash": ("貴重品の価値", "価格50%。終了時に倍率を解除。", "損傷は残ります。期間中の納品で生じた金袋は除外します。"),
    "Restoration": ("貴重品の修復", "5秒ごとに元の満額の5%を修復。", "壊れて消えた物は戻りません。価格変動の倍率も維持します。"),
    "Battery Drain": ("アイテム", "4秒ごとに電池を5ポイント消費。", "残量の下限0%。終了しても消費分は戻りません。"),
    "Heavy Cargo": ("物理ハザード", "重さ200%。貴重品保護 ON。", "対象設定に従います。プレイヤー・敵・扉は除外します。"),
    "Butterfingers": ("操作妨害", "8秒ごとに手持ちの物を落とす。", "収納中の物は対象外。落とした貴重品は既定で2秒保護します。"),
    "Enemy Blindness": ("敵の弱体化", "敵の視認距離を25%に。", "音への反応や、始まっている追跡は解除しません。"),
    "Enemy Armor": ("敵の強化", "敵が受けるダメージを50%に。", "他の防御効果と併用。無敵状態を解除する効果ではありません。"),
    "Enemy Vulnerability": ("敵の弱体化", "敵が受けるダメージを200%に。", "他の防御効果と併用。無敵状態を解除する効果ではありません。"),
    "Supply Drop": ("補給", "15秒ごとに1-2個。1ステージ合計10個。", "医療品や探索道具。終了後も残り、普通に回収できます。"),
    "Player Swap": ("プレイヤー", "15秒ごとに2人の位置を交換。", "2人以上が必要。手持ち・空中・しゃがみ・危険な移動先は除外。"),
    "Shared Pain": ("チームへの危険", "受けたダメージの25%を他の生存者へ。", "1人1回25まで。致死 OFFでも同時被弾などで死亡する場合あり。"),
}


@dataclass(frozen=True)
class Event:
    name: str
    risk: str
    enabled: str
    chance: str
    description: str

    @property
    def key(self) -> str:
        return "Battery" if self.name == "Battery Charge" else self.name.replace(" ", "")


def load_events() -> list[Event]:
    readme = (ROOT / "package/README.md").read_text(encoding="utf-8-sig")
    ja = readme.split("## 日本語", 1)[1].split("### イベント一覧", 1)[1]
    table = ja.split("\n### ", 1)[0]
    events = []
    for line in table.splitlines():
        if not line.startswith("| "):
            continue
        fields = [s.strip() for s in line.strip("|").split("|")]
        if len(fields) == 5 and fields[1] in RISK:
            events.append(Event(*fields))
    assert len(events) == 46, f"Expected 46 events, found {len(events)}"
    assert {e.name for e in events} == set(DETAILS), "Catalog notes must match release events exactly"
    assert all(e.chance == "6%" for e in events)
    assert {e.name for e in events if e.enabled == "無効"} == {"Roll", "Void"}
    enums = (ROOT / "EventModels.cs").read_text(encoding="utf-8-sig")
    keys = re.findall(r"^\s*(\w+)\s*=\s*1L?\s*<<", enums, re.M)
    assert set(keys) == {e.key for e in events}, "Missing or obsolete enum events"
    for event in events:
        assert icon_path(event.key).exists(), event.name
    return events


def icon_path(key: str) -> Path:
    # These large exports use the current frame-free artwork. Prefer the existing
    # compact PDF exports; the new events use their high-resolution masters.
    for part in (f"PDF/{key}.jpg", f"masters/{key}.png", f"Runtime/{key}.png"):
        path = ROOT / "Assets/EventIcons" / part
        if path.exists():
            return path
    raise FileNotFoundError(key)


def text(c, content, x, top, width, max_height, size=11, leading=None, color=INK):
    p = Paragraph(content, ParagraphStyle(
        "catalog", fontName="BIZBold", fontSize=size, leading=leading or size * 1.45,
        textColor=color, wordWrap="CJK", splitLongWords=False,
    ))
    _, height = p.wrap(width, max_height)
    if height > max_height + 0.05:
        raise ValueError(f"Text overflow ({height:.1f} > {max_height:.1f}): {content}")
    p.drawOn(c, x, top - height)
    return height


@lru_cache(maxsize=80)
def image_reader(path, background):
    if path.suffix.lower() in (".jpg", ".jpeg"):
        return ImageReader(str(path))
    # Optimize embedded copies only. The original artwork is never modified.
    with Image.open(path) as source:
        rgba = source.convert("RGBA")
        rgba.thumbnail((768, 768), Image.Resampling.LANCZOS)
        rgb = Image.new("RGB", rgba.size, background)
        rgb.paste(rgba, mask=rgba.getchannel("A"))
        buffer = BytesIO()
        rgb.save(buffer, "JPEG", quality=95, subsampling=0, optimize=True)
        buffer.seek(0)
        return ImageReader(buffer)


def image(c, path, x, y, size, background="#081015"):
    c.drawImage(image_reader(path, background), x, y, size, size, preserveAspectRatio=True)


def line(c, value, x, y, size=11, color=INK):
    c.setFillColor(color)
    c.setFont("BIZBold", size)
    c.drawString(x, y, value)


def start_page(c, title, subtitle, page, total, bookmark=None):
    c.setFillColor(BG)
    c.rect(0, 0, W, H, fill=1, stroke=0)
    for x, width, color in ((0, W * .5, CYAN), (W * .5, W * .3, AMBER), (W * .8, W * .2, HexColor("#DD629F"))):
        c.setFillColor(color)
        c.rect(x, H - 5, width, 5, fill=1, stroke=0)
    line(c, title, M, H - 39, 19)
    line(c, subtitle, M, H - 58, 9, MUTED)
    c.setStrokeColor(HexColor("#31434D"))
    c.setLineWidth(.7)
    c.line(M, 28, W - M, 28)
    line(c, f"STAGE FLUX  /  v{VERSION}  /  EVENT CATALOG", M, 14, 8, MUTED)
    c.drawRightString(W - M, 14, f"{page:02d} / {total:02d}")
    if bookmark:
        c.bookmarkPage(bookmark)
        c.addOutlineEntry(title or "Stage Flux - イベント一覧", bookmark, 0, False)


def cover(c, events, total):
    start_page(c, "", "", 1, total, "cover")
    line(c, "HOST-ONLY RANDOM STAGE EVENTS", M, H - 59, 11, CYAN)
    line(c, "STAGE FLUX", M, H - 116, 35)
    line(c, "イベント一覧", M, H - 158, 25)
    line(c, f"v{VERSION}  |  全{len(events)}イベント", M, H - 190, 13, MUTED)
    image(c, ROOT / "package/icon.png", W - M - 142, H - 205, 142)
    text(c, "いつもの探索に、予測できない変化を。<br/>"
         "ホストが導入すれば、MOD未導入の参加者にも効果が適用されます。",
         M, H - 240, W - M * 2, 74, size=15, leading=23)
    for index, key in enumerate(("Feather", "EnemySpeedUp", "Restoration", "SupplyDrop")):
        image(c, icon_path(key), M + index * 133, 365, 115)
    text(c, "アイコン / 効果の説明 / 危険度 / 主な既定設定 / 注意点",
         M, 335, W - M * 2, 26, size=12, color=CYAN)
    c.setFillColor(PANEL)
    c.roundRect(M, 147, W - M * 2, 145, 8, fill=1, stroke=0)
    sections = [("全46イベント", "02 - 13", "events"),
                ("抽選・時間・対象の共通設定", "14", "settings"),
                ("危険な組み合わせと安全設定", "15 - 16", "safety")]
    for i, (label, pages, dest) in enumerate(sections):
        y = 258 - i * 39
        line(c, label, M + 17, y, 13)
        c.drawRightString(W - M - 17, y, pages)
        c.linkRect("", dest, (M + 10, y - 7, W - M - 10, y + 19), relative=0, thickness=0)
    text(c, "日本語の説明・英語のイベント名。数値は新規設定時の既定値です。<br/>"
         "ホストが設定を変更している場合、実際の効果や確率は異なります。",
         M, 110, W - M * 2, 45, size=11, color=MUTED)
    c.showPage()


def card(c, e, index, x, y, w, h):
    category, defaults, note = DETAILS[e.name]
    risk = HexColor(RISK[e.risk])
    c.setFillColor(PANEL)
    c.roundRect(x, y, w, h, 8, fill=1, stroke=0)
    c.setFillColor(risk)
    c.roundRect(x, y + h - 5, w, 5, 2, fill=1, stroke=0)
    c.bookmarkPage(e.key)
    c.addOutlineEntry(f"{index:02d}  {e.name}", e.key, 1, False)
    text(c, escape(e.name), x + 13, y + h - 18, w - 26, 38, size=14.5, leading=17)
    image(c, icon_path(e.key), x + 10, y + h - 156, 102, "#132029")
    tx = x + 125
    line(c, category, tx, y + h - 73, 9, MUTED)
    c.setFillColor(risk)
    c.roundRect(tx, y + h - 102, w - 140, 20, 4, fill=1, stroke=0)
    line(c, f"危険度  {e.risk}", tx + 8, y + h - 96, 10)
    line(c, f"既定  {'ON' if e.enabled == '有効' else 'OFF'}", tx, y + h - 124, 11,
         CYAN if e.enabled == "有効" else AMBER)
    line(c, f"抽選確率  {e.chance}", tx, y + h - 146, 10)
    used = text(c, escape(e.description), x + 13, y + h - 170, w - 26, 68, size=11.5, leading=17)
    top = y + h - 170 - used - 10
    bottom = y + 12
    c.setFillColor(INNER)
    c.roundRect(x + 10, bottom, w - 20, top - bottom, 5, fill=1, stroke=0)
    text(c, f"<font color='#63D9EF'>既定</font>  {escape(defaults)}<br/>"
         f"<font color='#F0B653'>注意</font>  {escape(note)}",
         x + 18, top - 8, w - 36, top - bottom - 16, size=10.7, leading=15.5)


def event_pages(c, events, total):
    gap = 14
    cw = (W - M * 2 - gap) / 2
    top, bottom = H - 77, 43
    ch = (top - bottom - gap) / 2
    for offset in range(0, len(events), 4):
        page = offset // 4 + 2
        start_page(c, "イベントカタログ", f"{offset + 1:02d} - {min(offset + 4, len(events)):02d} / {len(events)}  |  効果・既定設定・注意点", page, total,
                   "events" if offset == 0 else None)
        for i, e in enumerate(events[offset:offset + 4]):
            x = M + (i % 2) * (cw + gap)
            y = top - (i // 2 + 1) * ch - (i // 2) * gap
            card(c, e, offset + i + 1, x, y, cw, ch)
        if offset + 4 >= len(events):
            text(c, "危険度はHUDと同じ区分です。<br/>低危険度でも、他の効果や地形との組み合わせで事故が起こる場合があります。",
                 M + 16, 319, W - M * 2 - 32, 85, size=13, color=MUTED)
        c.showPage()


def box(c, title, body, top, height, color=CYAN, size=11):
    c.setFillColor(PANEL)
    c.roundRect(M, top - height, W - M * 2, height, 6, fill=1, stroke=0)
    line(c, title, M + 14, top - 23, 12, color)
    text(c, body, M + 14, top - 34, W - M * 2 - 28, height - 44, size=size, leading=16)


def settings(c, total):
    start_page(c, "抽選と共通設定", "すべてREPOConfigで変更できます。数値は新規導入時の既定値。", 14, total, "settings")
    rows = [
        ("ステージで有効になる確率: 50%", "ステージ開始時に1回抽選します。外れたステージでは効果・専用HUD・開始通知は出ません。"),
        ("各イベント: 独立して6% / 同時に最大3個", "設定可能な同時数は1-5個。確率の合計が100%を超えても問題ありません。当選後、同時数と組み合わせ条件により候補を絞ります。"),
        ("待機45-90秒 / 効果15-30秒", "範囲内の整数秒を使用。いずれも10-300秒で設定できます。繰り返し発生する効果の間隔は、別途1-300秒で変更できます。"),
        ("標準モード: RandomEachEvent", "毎回、組み合わせ・効果時間・待機時間を抽選。ステージ固定、納品ごと変更、常時持続、モード自体の抽選も選べます。常時持続以外は待機から始まります。"),
        ("対象設定は、広い種類に作用する効果へ適用", "Featherなどは対象を選択可能。貴重品・物・武器・プレイヤー・敵はON、Cosmetic Boxesと扉はOFF。敵専用・プレイヤー専用など対象が決まった効果はこの設定を無視します。"),
        ("各イベントの有効 / 無効を選択可能", "既定OFFはRollとVoidだけです。Player SwapとShared Painは生存者2人以上が必要。各イベントの6%は、実際のステージ全体での発生率を保証する数値ではありません。"),
    ]
    top = H - 78
    for title, body in rows:
        box(c, title, body, top, 105)
        top -= 113
    c.showPage()


def safety(c, total):
    start_page(c, "危険な組み合わせと安全設定", "既定では危険な組み合わせを許可しません。完全な安全を保証する設定ではありません。", 15, total, "safety")
    box(c, "危険な組み合わせ: OFF（許可しない）", "強制移動同士、致命的な効果同士、敵への対処を難しくする効果同士などは重なりません。操作や視界の妨害、貴重品損失の増幅にも制限があります。", H - 78, 91)
    line(c, "常に同時発動しない8組", M, H - 194, 15, CYAN)
    pairs = [("Freeze", "Stun"), ("Enemy Speed Up", "Enemy Speed Down"),
             ("Enemy Regen", "Enemy Purge"), ("Indestructible", "Fragility"),
             ("Value Surge", "Value Crash"), ("Feather", "Heavy Cargo"),
             ("Battery Charge", "Battery Drain"), ("Enemy Armor", "Enemy Vulnerability")]
    for i, (a, b) in enumerate(pairs):
        y = H - 224 - i * 30
        c.setFillColor(PANEL if i % 2 == 0 else INNER)
        c.roundRect(M, y - 9, W - 2 * M, 27, 3, fill=1, stroke=0)
        line(c, a, M + 13, y, 11)
        line(c, "/", W / 2, y, 11, AMBER)
        line(c, b, W / 2 + 23, y, 11)
    box(c, "貴重品を守る設定", "対象の貴重品設定と、イベントごとの保護設定が両方ONなら保護します。保護設定はDangerous Valuablesのみ既定OFF、他はON。通常は終了後も2秒（設定1-5秒）保護。Butterfingersは落とした物だけ、落下時から2秒です。", 369, 120)
    box(c, "知っておきたい注意点", "危険な組み合わせをONにしても上の8組と同時数の上限は残ります。<br/>貴重品に危険な効果は、既定で同時に2個まで。FragilityやValue Crashは、物を危険にさらす効果と重ならないよう選ばれます。", 237, 110, AMBER)
    text(c, "出現距離の3mは優先する目安です。地形や置ける場所により、狭いステージでは近くに現れる場合があります。",
         M, 105, W - M * 2, 52, size=11, color=MUTED)
    c.showPage()


def groups(c, total):
    start_page(c, "組み合わせ制限の読み方", "1つのイベントが複数のグループに入ることがあります。以下は同時抽選の制限です。", 16, total, "groups")
    # Complete group membership, automatically checked against the source below.
    risk_groups = [
        ("強制移動", "ForcedMovement", "同じグループ、致命的、広範囲、操作妨害と併用しません。"),
        ("致命的な効果", "LethalHazard", "同じグループ、強制移動、敵の脅威、操作妨害と併用しません。"),
        ("広範囲の危険", "AreaHazard", "強制移動、操作妨害と併用しません。"),
        ("敵の脅威", "EnemyPressure", "同じグループ、致命的、操作妨害と併用しません。"),
        ("操作妨害", "ControlImpairment", "同じグループ、強制移動、致命的、広範囲、敵の脅威と併用しません。"),
        ("視界妨害", "VisualImpairment", "同じグループ同士では併用しません。"),
        ("貴重品への危険", "ValuableRisk", "同時に2個まで。価値損失の増幅と併用しません。"),
        ("価値損失の増幅", "ValueLossAmplifier", "貴重品を危険にさらすイベントと併用しません。"),
    ]
    source = (ROOT / "StagePhysicsConfig.cs").read_text(encoding="utf-8-sig")
    rows = re.findall(r"StageEffect\.(\w+) => ((?:EffectRisk\.\w+(?: \| )?)+)", source)
    lookup = {e.key: e.name for e in load_events()}
    top = H - 78
    for title, key, explanation in risk_groups:
        names = [lookup[enum] for enum, flags in rows if "EffectRisk." + key in flags]
        if key in ("ForcedMovement", "LethalHazard", "AreaHazard", "ValuableRisk"):
            names.append("Minefield*")
        body = escape(" / ".join(names))
        pstyle = ParagraphStyle("measure", fontName="BIZBold", fontSize=9.5, leading=13.2, wordWrap="CJK")
        p = Paragraph(body, pstyle)
        _, used = p.wrap(W - M * 2 - 24, 200)
        height = used + 49
        c.setFillColor(PANEL)
        c.roundRect(M, top - height, W - M * 2, height, 5, fill=1, stroke=0)
        line(c, title, M + 12, top - 18, 11, CYAN)
        text(c, escape(explanation), M + 12, top - 24, W - M * 2 - 24, 17, size=9, leading=13)
        text(c, body, M + 12, top - 42, W - M * 2 - 24, used, size=9.5, leading=13.2, color=MUTED)
        top -= height + 7
    text(c, "* Minefieldは広範囲に分類。爆発地雷がONなら致命的・貴重品危険、"
         "衝撃波地雷がONなら強制移動・貴重品危険にも分類します（既定では両方ON）。",
         M, top - 3, W - M * 2, top - 42, size=9.6, leading=14, color=MUTED)
    c.showPage()


def main():
    pdfmetrics.registerFont(TTFont("BIZBold", r"C:\Windows\Fonts\BIZ-UDGothicB.ttc", subfontIndex=0))
    events = load_events()
    total = 1 + math.ceil(len(events) / 4) + 3
    assert total == 16
    OUTPUT.parent.mkdir(parents=True, exist_ok=True)
    c = canvas.Canvas(str(OUTPUT), pagesize=A4, pageCompression=1)
    c.setTitle(f"Stage Flux v{VERSION} - イベント一覧")
    c.setAuthor("CapackMods")
    c.setSubject("46イベントのアイコン・効果・既定値・危険な組み合わせ")
    cover(c, events, total)
    event_pages(c, events, total)
    settings(c, total)
    safety(c, total)
    groups(c, total)
    c.save()
    print(json.dumps({"pdf": str(OUTPUT), "events": len(events), "pages": total, "version": VERSION}, ensure_ascii=False))


if __name__ == "__main__":
    main()
