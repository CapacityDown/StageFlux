from __future__ import annotations

from pathlib import Path

from reportlab.lib.colors import HexColor, white
from reportlab.lib.pagesizes import A4
from reportlab.lib.utils import ImageReader
from reportlab.pdfgen import canvas

import build_event_list_pdf as base


OUTPUT = (
    Path(__file__).resolve().parents[1]
    / "output"
    / "pdf"
    / "StageFlux_Event_List_v4.2.3_EN.pdf"
)

RISK_LABELS = {
    "low": "LOW",
    "medium": "MEDIUM",
    "high": "HIGH",
}

# Asset key, display name, category, enabled, chance, risk, description, defaults/notes.
EVENTS = [
    ("Feather", "Feather", "Physics / Support", True, 5, "low",
     "Makes selected stage targets lighter, easier to lift, and more buoyant.",
     "Targets are configurable: valuables, items, doors, players, enemies, and more"),
    ("ZeroGravity", "Zero Gravity", "Physics / Support", True, 5, "medium",
     "Removes gravity from selected stage targets so they float more easily.",
     "Valuable protection: ON / protection remains for 2 seconds after the event"),
    ("Battery", "Battery Charge", "Physics / Support", True, 5, "low",
     "Repeatedly restores charge to battery-powered items during the event.",
     "Restores 5 battery charge every 4 seconds"),
    ("Heal", "Heal", "Physics / Support", True, 5, "low",
     "Repeatedly restores health to every living player during the event.",
     "Restores 10 health every 2 seconds"),
    ("Indestructible", "Indestructible", "Physics / Support", True, 5, "low",
     "Prevents selected valuables and items from breaking during the event.",
     "The affected target categories can be changed in the settings"),
    ("Fragility", "Fragility", "Physics / Support", True, 5, "high",
     "Makes valuables extremely fragile, allowing light impacts to reduce their value.",
     "Fragility: 10x normal / only applies when valuables are targeted"),
    ("GumballHypnosis", "Gumball Hypnosis", "Special Effect", True, 5, "medium",
     "Adds a screen effect and pulls a holder's gaze toward the object being held.",
     "Does not activate when holding doors, players, or enemies"),
    ("HealingAura", "Healing Aura", "Spawn Event", True, 5, "low",
     "Creates glowing areas at random stage locations that heal players who touch them.",
     "1-3 every 10 seconds / each aura provides up to 50 total health"),
    ("StarBarrage", "Star Barrage", "Spawn Event", True, 5, "high",
     "Launches dangerous star projectiles from random stage positions and directions.",
     "3-6 projectiles every 2 seconds / valuable protection: ON"),
    ("SpiderScare", "Spider Scare", "Spawn Event", True, 5, "medium",
     "Triggers the vanilla spider screen effect for randomly selected players.",
     "1-3 players every 10 seconds / follows the arachnophobia setting"),
    ("TrafficShock", "Traffic Shock", "Spawn Event", True, 5, "high",
     "Shocks and tumbles random players using the vanilla red traffic-light effect.",
     "Targets 1-3 players every 10 seconds"),
    ("DangerousValuables", "Dangerous Valuables", "Valuable Event", True, 5, "high",
     "Activates compatible dangerous valuables that were already placed on the stage.",
     "5-10 every 10 seconds / creates none / valuable protection: OFF"),
    ("Roll", "Roll", "Physics Hazard", False, 5, "high",
     "Violently rotates and moves targets. Players can steer toward their view direction.",
     "Disabled by default / collisions and movement may kill enemies"),
    ("Void", "Void", "Physics Hazard", False, 5, "high",
     "Creates multiple dangerous voids and moves them to new random locations repeatedly.",
     "Disabled by default / 3-5 every 10 seconds / may kill players"),
    ("Levitation", "Levitation", "Spawn Event", True, 5, "medium",
     "Creates areas at random stage locations that lift objects and players.",
     "2-5 every 10 seconds / valuable protection: ON"),
    ("Shockwave", "Shockwave", "Spawn Event", True, 5, "medium",
     "Launches knockback grenades in random directions across the stage.",
     "5-10 every 10 seconds / spawns at least 3 m from players"),
    ("StunBlast", "Stun Blast", "Spawn Event", True, 5, "medium",
     "Launches grenades that stun nearby targets in random directions.",
     "5-10 every 10 seconds / spawns at least 3 m from players"),
    ("ExplosionRain", "Explosion Rain", "Spawn Event", True, 5, "high",
     "Launches explosive grenades into the stage from random positions and directions.",
     "5-10 every 10 seconds / spawns at least 3 m from players"),
    ("EnemyWave", "Enemy Wave", "Enemy Event", True, 5, "high",
     "Activates 1-3 additional enemies that are available for the current stage.",
     "Replenishes every 10 seconds / removes added enemies when the event ends"),
    ("Minefield", "Minefield", "Spawn Event", True, 5, "high",
     "Places armed vanilla mines and replenishes mines that trigger or are destroyed.",
     "Maintains 10-15 mines / mines counting down remain after the event"),
    ("Freeze", "Freeze", "Enemy Control", True, 5, "medium",
     "Continuously freezes targeted enemies so they cannot move during the event.",
     "Only activates when enemies are included in the target settings"),
    ("Stun", "Stun", "Enemy Control", True, 5, "medium",
     "Continuously stuns compatible enemies so they cannot act during the event.",
     "Only activates when enemies are included in the target settings"),
    ("EnemyWarp", "Enemy Warp", "Enemy Control", True, 5, "high",
     "Teleports enemies to different valid locations across the stage.",
     "3 enemies every 10 seconds / destinations are at least 3 m from players"),
    ("EnemyHunt", "Enemy Hunt", "Enemy Control", True, 5, "high",
     "After all extractions, plays a lure sound in an occupied room and sends enemies there.",
     "Retargets every 5 seconds / affects vanilla participants"),
    ("EnemySpeedUp", "Enemy Speed Up", "Enemy Control", True, 5, "high",
     "Increases movement and acceleration for all enemies, including later spawns.",
     "150% speed / cannot activate with Enemy Speed Down"),
    ("EnemySpeedDown", "Enemy Speed Down", "Enemy Control", True, 5, "low",
     "Reduces movement and acceleration for all enemies, including later spawns.",
     "50% speed / cannot activate with Enemy Speed Up"),
    ("EnemyRegen", "Enemy Regen", "Enemy Control", True, 5, "high",
     "Gradually restores health to every enemy while the event is active.",
     "Restores 10 health every 5 seconds"),
    ("EnemyPurge", "Enemy Purge", "Enemy Control", True, 5, "low",
     "Gradually damages every enemy while the event is active.",
     "Deals 10 damage every 5 seconds / this effect can kill enemies"),
    ("DamagePulse", "Damage Pulse", "Player Event", True, 5, "high",
     "Repeatedly damages every living player while the event is active.",
     "Deals 5 damage every 5 seconds / this effect alone leaves at least 1 health"),
    ("SecondChance", "Second Chance", "Player Event", True, 5, "low",
     "Revives players at their death position 2 seconds after they die during the event.",
     "Once per player / players already dead when the event starts are excluded"),
    ("Knockback", "Knockback", "Player Event", True, 5, "medium",
     "Periodically launches every living player in a random direction.",
     "Every 5 seconds / knockback strength is configurable"),
    ("Flicker", "Flicker", "Player Event", True, 5, "medium",
     "Repeatedly flashes a red light around every living player.",
     "Every 2 seconds / intensity defaults to 200%"),
    ("Quake", "Quake", "Stage / Object", True, 5, "high",
     "Shakes players and loose floor objects in random directions like an earthquake.",
     "Every 5 seconds / valuable protection: ON"),
    ("DoorChaos", "Door Chaos", "Stage / Object", True, 5, "medium",
     "Repeatedly opens and closes normal doors, large doors, and hinged objects.",
     "30% every 2 seconds / force 12 / internally includes doors for this event"),
    ("ValueSurge", "Value Surge", "Stage / Object", True, 5, "low",
     "Raises the price of every valuable, including those in extraction, carts, and trucks.",
     "150% value / ending removes only the multiplier and preserves damage"),
    ("ValueCrash", "Value Crash", "Stage / Object", True, 5, "high",
     "Lowers the price of every valuable, including those in extraction, carts, and trucks.",
     "50% value / ending removes only the multiplier and preserves damage"),
]

EVENTS = [event[:4] + (6,) + event[5:] for event in EVENTS]


def draw_cover(c: canvas.Canvas) -> None:
    base.draw_background(c)
    c.setFont("BIZUDGothicBold", 13)
    c.setFillColor(base.CYAN)
    c.drawString(base.MARGIN, base.PAGE_H - 52, "HOST-ONLY RANDOM STAGE EVENT MOD")

    c.setFont("BIZUDGothicBold", 39)
    c.setFillColor(white)
    c.drawString(base.MARGIN, base.PAGE_H - 117, "STAGE FLUX")
    c.setFont("BIZUDGothicBold", 24)
    c.setFillColor(base.INK)
    c.drawString(base.MARGIN, base.PAGE_H - 158, "EVENT CATALOG")

    c.setFont("BIZUDGothicBold", 12)
    c.setFillColor(base.MUTED)
    c.drawString(base.MARGIN, base.PAGE_H - 189, "v4.2.3  /  ALL 36 EVENTS AND HUD ICONS")

    cover_icons = ["Feather", "ZeroGravity", "EnemyWave", "DangerousValuables"]
    icon_size = 105
    gap = 18
    start_x = (base.PAGE_W - (icon_size * 2 + gap)) / 2
    start_y = 420
    risk_colors = [
        base.RISK["low"][1],
        base.RISK["medium"][1],
        base.RISK["high"][1],
        base.RISK["high"][1],
    ]
    for index, (name, border) in enumerate(zip(cover_icons, risk_colors)):
        col = index % 2
        row = index // 2
        x = start_x + col * (icon_size + gap)
        y = start_y - row * (icon_size + gap)
        c.setFillColor(base.CARD)
        c.setStrokeColor(border)
        c.setLineWidth(2)
        c.roundRect(x - 7, y - 7, icon_size + 14, icon_size + 14, 8, fill=1, stroke=1)
        c.drawImage(
            ImageReader(str(base.icon_asset_path(name))),
            x, y, icon_size, icon_size,
            preserveAspectRatio=True, mask="auto",
        )

    base.paragraph(
        c,
        "Stage Flux shakes up every run with configurable, random stage-wide events. "
        "This catalog lists the HUD icon, risk level, default enabled state, default "
        "selection chance, and player-facing behavior for every event.",
        base.MARGIN, 230, base.PAGE_W - base.MARGIN * 2, 90,
        size=12, leading=18,
    )

    for index, risk in enumerate(("low", "medium", "high")):
        color = base.RISK[risk][1]
        x = base.MARGIN + index * 116
        c.setFillColor(color)
        c.roundRect(x, 118, 100, 26, 5, fill=1, stroke=0)
        c.setFont("BIZUDGothicBold", 8.5)
        c.setFillColor(white)
        c.drawCentredString(x + 50, 127, f"RISK {RISK_LABELS[risk]}")

    c.setFont("BIZUDGothicBold", 8)
    c.setFillColor(base.MUTED)
    c.drawString(
        base.MARGIN,
        90,
        "Risk colors match the Stage Flux graphical HUD.",
    )
    base.draw_footer(c, 1, "EVENT CATALOG / COVER / ENGLISH")
    c.showPage()


def draw_card(
    c: canvas.Canvas,
    event: tuple,
    x: float,
    y: float,
    w: float,
    h: float,
) -> None:
    asset, name, category, enabled, chance, risk, description, settings = event
    risk_color = base.RISK[risk][1]

    c.setFillColor(base.CARD)
    c.setStrokeColor(risk_color)
    c.setLineWidth(1.8)
    c.roundRect(x, y, w, h, 8, fill=1, stroke=1)

    c.setFillColor(risk_color)
    c.roundRect(x + 9, y + h - 23, 72, 15, 4, fill=1, stroke=0)
    c.setFont("BIZUDGothicBold", 6.8)
    c.setFillColor(white)
    c.drawCentredString(x + 45, y + h - 18.2, f"RISK {RISK_LABELS[risk]}")

    c.setFont("BIZUDGothicBold", 6.8)
    c.setFillColor(base.MUTED)
    c.drawRightString(x + w - 9, y + h - 18.2, category)

    icon_size = 70
    icon_x = x + 12
    icon_y = y + h - 104
    c.setFillColor(base.CARD_INNER)
    c.roundRect(icon_x - 3, icon_y - 3, icon_size + 6, icon_size + 6, 5, fill=1, stroke=0)
    c.drawImage(
        ImageReader(str(base.icon_asset_path(asset))),
        icon_x, icon_y, icon_size, icon_size,
        preserveAspectRatio=True, mask="auto",
    )

    title_x = icon_x + icon_size + 12
    title_w = x + w - 10 - title_x
    base.paragraph(c, name, title_x, y + h - 46, title_w, 42, size=13, leading=15)

    enabled_color = HexColor("#70A650") if enabled else HexColor("#59636A")
    enabled_text = "DEFAULT ON" if enabled else "DEFAULT OFF"
    c.setFillColor(enabled_color)
    c.roundRect(title_x, y + h - 99, 70, 17, 4, fill=1, stroke=0)
    c.setFont("BIZUDGothicBold", 6.4)
    c.setFillColor(white)
    c.drawCentredString(title_x + 35, y + h - 93.3, enabled_text)
    c.setFillColor(HexColor("#1D5969"))
    c.roundRect(title_x + 76, y + h - 99, 58, 17, 4, fill=1, stroke=0)
    c.setFillColor(white)
    c.drawCentredString(title_x + 105, y + h - 93.3, f"CHANCE {chance}%")

    c.setStrokeColor(HexColor("#2A3941"))
    c.setLineWidth(0.5)
    c.line(x + 10, y + h - 114, x + w - 10, y + h - 114)
    base.paragraph(
        c, description, x + 12, y + h - 128, w - 24, 58,
        size=8.4, leading=12,
    )

    c.setFillColor(base.CARD_INNER)
    c.roundRect(x + 10, y + 12, w - 20, 42, 5, fill=1, stroke=0)
    base.paragraph(
        c,
        f"<font color='#46C6E8'>DEFAULT / NOTES</font>  {settings}",
        x + 16, y + 45, w - 32, 31,
        size=7.3, color=base.INK, leading=10,
    )


def draw_event_pages(c: canvas.Canvas) -> None:
    card_gap_x = 10
    card_gap_y = 10
    card_w = (base.PAGE_W - base.MARGIN * 2 - card_gap_x) / 2
    top = base.PAGE_H - 69
    bottom = 33
    card_h = (top - bottom - card_gap_y * 2) / 3

    page_number = 2
    for page_events in base.chunks(EVENTS, 6):
        base.draw_background(c)
        c.setFont("BIZUDGothicBold", 18)
        c.setFillColor(base.INK)
        c.drawString(base.MARGIN, base.PAGE_H - 37, "EVENT CATALOG")
        c.setFont("BIZUDGothicBold", 8)
        c.setFillColor(base.MUTED)
        c.drawRightString(
            base.PAGE_W - base.MARGIN,
            base.PAGE_H - 34,
            f"{(page_number - 2) * 6 + 1:02d} - "
            f"{min((page_number - 1) * 6, len(EVENTS)):02d} / {len(EVENTS):02d}",
        )

        for index, event in enumerate(page_events):
            col = index % 2
            row = index // 2
            x = base.MARGIN + col * (card_w + card_gap_x)
            y = top - (row + 1) * card_h - row * card_gap_y
            draw_card(c, event, x, y, card_w, card_h)

        base.draw_footer(c, page_number, "EVENT CATALOG / ENGLISH")
        c.showPage()
        page_number += 1


def draw_notes(c: canvas.Canvas) -> None:
    base.draw_background(c)
    c.setFont("BIZUDGothicBold", 22)
    c.setFillColor(base.INK)
    c.drawString(base.MARGIN, base.PAGE_H - 56, "SELECTION AND SAFETY")
    c.setFont("BIZUDGothicBold", 9)
    c.setFillColor(base.MUTED)
    c.drawString(base.MARGIN, base.PAGE_H - 78, "Rules shared by every event")

    notes = [
        ("Stage activation roll", "At the start of each stage, events are enabled with a default 20% chance. If the roll fails, that stage has no events or dedicated HUD."),
        ("Independent event rolls", "Each enabled event rolls its own default 6% chance. Roll and Void start disabled; every other event starts enabled."),
        ("Recurring intervals", "Events that repeat projectiles, mines, pulses, or other actions can use intervals from 1 to 300 seconds."),
        ("Simultaneous events", "Up to 3 events activate together by default. This limit is configurable from 1 to 5; excess successful rolls are reduced randomly."),
        ("Dangerous combinations", "Safety filtering is enabled by default. It avoids excessive overlap between forced movement, lethal hazards, enemy pressure, visual disruption, and valuable loss."),
        ("Always exclusive pairs", "Freeze/Stun, Enemy Speed Up/Down, Enemy Regen/Purge, Indestructible/Fragility, and Value Surge/Crash never activate together."),
        ("Valuable protection", "Protection can be configured per hazardous event and can remain for 1-5 seconds afterward. It is disabled when valuables are not targeted."),
    ]
    y = base.PAGE_H - 120
    for index, (title, body) in enumerate(notes):
        color = (base.CYAN, base.MAGENTA, base.ORANGE)[index % 3]
        c.setFillColor(base.CARD)
        c.setStrokeColor(color)
        c.setLineWidth(1.3)
        c.roundRect(base.MARGIN, y - 84, base.PAGE_W - base.MARGIN * 2, 74, 7, fill=1, stroke=1)
        c.setFont("BIZUDGothicBold", 11)
        c.setFillColor(color)
        c.drawString(base.MARGIN + 14, y - 34, title)
        base.paragraph(
            c, body, base.MARGIN + 14, y - 45,
            base.PAGE_W - base.MARGIN * 2 - 28, 35,
            size=8.6, leading=12,
        )
        y -= 86

    c.setFillColor(base.CARD)
    c.roundRect(base.MARGIN, 47, base.PAGE_W - base.MARGIN * 2, 60, 7, fill=1, stroke=0)
    base.paragraph(
        c,
        "Note: Defaults in this document apply to new v4.2.3 configurations. "
        "Existing users keep their previously saved values.",
        base.MARGIN + 14, 91, base.PAGE_W - base.MARGIN * 2 - 28, 38,
        size=9.2, leading=14,
    )
    base.draw_footer(c, 8, "EVENT CATALOG / NOTES / ENGLISH")
    c.showPage()


def draw_dangerous_combinations(c: canvas.Canvas) -> None:
    base.draw_background(c)
    c.setFont("BIZUDGothicBold", 22)
    c.setFillColor(base.INK)
    c.drawString(base.MARGIN, base.PAGE_H - 56, "DANGEROUS COMBINATIONS")
    c.setFont("BIZUDGothicBold", 9)
    c.setFillColor(base.MUTED)
    c.drawString(base.MARGIN, base.PAGE_H - 78, "Prevents too many hazardous events from overlapping")

    c.setFillColor(base.CARD)
    c.setStrokeColor(base.ORANGE)
    c.setLineWidth(1.5)
    c.roundRect(base.MARGIN, base.PAGE_H - 160, base.PAGE_W - base.MARGIN * 2, 60, 7, fill=1, stroke=1)
    c.setFont("BIZUDGothicBold", 11)
    c.setFillColor(base.ORANGE)
    c.drawString(base.MARGIN + 14, base.PAGE_H - 124, "DEFAULT: NOT ALLOWED")
    base.paragraph(
        c,
        "By default, candidates are filtered when the risk conditions below overlap. "
        "Allowing dangerous combinations still keeps the simultaneous-event limit "
        "and the always-exclusive opposite-effect pairs.",
        base.MARGIN + 151, base.PAGE_H - 116,
        base.PAGE_W - base.MARGIN * 2 - 165, 42,
        size=8.3, leading=12,
    )

    gap = 12
    column_w = (base.PAGE_W - base.MARGIN * 2 - gap) / 2
    left_x = base.MARGIN
    right_x = base.MARGIN + column_w + gap
    main_y = 265
    main_h = 390

    c.setFillColor(base.CARD)
    c.setStrokeColor(base.MAGENTA)
    c.roundRect(left_x, main_y, column_w, main_h, 7, fill=1, stroke=1)
    c.setFont("BIZUDGothicBold", 11)
    c.setFillColor(base.MAGENTA)
    c.drawString(left_x + 14, main_y + main_h - 28, "BLOCKED OVERLAPS")

    blocked_pairs = [
        ("Forced movement", "Forced movement"),
        ("Lethal", "Lethal"),
        ("Forced movement", "Lethal or area-wide"),
        ("Enemy pressure", "Pressure, lethal, or control"),
        ("Control disruption", "Control, movement, lethal, area"),
        ("Visual disruption", "Visual disruption"),
        ("Fragility", "Valuable risk or Value Crash"),
        ("Value Crash", "Valuable risk"),
    ]
    row_y = main_y + main_h - 61
    for left, right in blocked_pairs:
        c.setFillColor(base.CARD_INNER)
        c.roundRect(left_x + 12, row_y - 24, column_w - 24, 29, 5, fill=1, stroke=0)
        c.setFont("BIZUDGothicBold", 6.1)
        c.setFillColor(base.INK)
        c.drawString(left_x + 18, row_y - 13, left)
        c.setFillColor(base.MAGENTA)
        c.drawCentredString(left_x + column_w / 2, row_y - 13, "x")
        c.setFillColor(base.INK)
        c.drawRightString(left_x + column_w - 18, row_y - 13, right)
        row_y -= 34

    c.setFillColor(base.CARD_INNER)
    c.roundRect(left_x + 12, main_y + 12, column_w - 24, 48, 5, fill=1, stroke=0)
    base.paragraph(
        c,
        "<font color='#E49A29'>VALUABLE-DAMAGE LIMIT</font><br/>"
        "At most 2 valuable-risk events can overlap. A third candidate is excluded.",
        left_x + 22, main_y + 53, column_w - 44, 38,
        size=7.1, leading=9.5,
    )

    c.setFillColor(base.CARD)
    c.setStrokeColor(base.CYAN)
    c.roundRect(right_x, main_y, column_w, main_h, 7, fill=1, stroke=1)
    c.setFont("BIZUDGothicBold", 11)
    c.setFillColor(base.CYAN)
    c.drawString(right_x + 14, main_y + main_h - 28, "EVENTS IN EACH RISK GROUP")

    risk_groups = [
        ("FORCED MOVEMENT", "Zero Gravity / Roll / Void / Levitation / Shockwave / Knockback / Quake / Traffic Shock / Dangerous Valuables"),
        ("LETHAL", "Void / Explosion Rain / Damage Pulse / Star Barrage / Traffic Shock / Dangerous Valuables"),
        ("AREA-WIDE", "Void / Shockwave / Stun Blast / Explosion Rain / Star Barrage / Dangerous Valuables / Minefield / Door Chaos"),
        ("ENEMY PRESSURE", "Enemy Wave / Enemy Warp / Enemy Hunt / Enemy Speed Up / Enemy Regen"),
        ("CONTROL / VISUAL DISRUPTION", "Gumball Hypnosis / Spider Scare / Flicker"),
        ("VALUABLE RISK", "Zero Gravity / Roll / Void / Levitation / Shockwave / Explosion Rain / Quake / Door Chaos / Fragility / Star Barrage / Dangerous Valuables"),
        ("VALUE-LOSS AMPLIFIERS", "Fragility / Value Crash"),
    ]
    group_y = main_y + main_h - 52
    for title, body in risk_groups:
        c.setFont("BIZUDGothicBold", 7.1)
        c.setFillColor(base.CYAN)
        c.drawString(right_x + 14, group_y - 10, title)
        base.paragraph(
            c, body, right_x + 14, group_y - 15,
            column_w - 28, 31,
            size=6.0, color=base.INK, leading=8,
        )
        group_y -= 47

    c.setFillColor(base.CARD)
    c.setStrokeColor(HexColor("#70A650"))
    c.roundRect(base.MARGIN, 58, base.PAGE_W - base.MARGIN * 2, 180, 7, fill=1, stroke=1)
    c.setFont("BIZUDGothicBold", 11)
    c.setFillColor(HexColor("#70A650"))
    c.drawString(base.MARGIN + 14, 211, "ALWAYS-EXCLUSIVE PAIRS")
    base.paragraph(
        c,
        "Freeze x Stun  /  Enemy Speed Up x Enemy Speed Down  /  "
        "Enemy Regen x Enemy Purge  /  Indestructible x Fragility  /  "
        "Value Surge x Value Crash",
        base.MARGIN + 14, 195, base.PAGE_W - base.MARGIN * 2 - 28, 42,
        size=8.3, leading=13,
    )
    c.setStrokeColor(HexColor("#2A3941"))
    c.line(base.MARGIN + 14, 147, base.PAGE_W - base.MARGIN - 14, 147)
    base.paragraph(
        c,
        "<font color='#E49A29'>MINEFIELD RISK DEPENDS ON ENABLED MINES</font><br/>"
        "Minefield is normally area-wide. Explosive mines also make it lethal and a "
        "valuable risk. Shockwave mines also add forced movement and valuable risk.<br/>"
        "<font color='#AAB1AC'>Note: Candidate filtering order can change which event is excluded.</font>",
        base.MARGIN + 14, 137, base.PAGE_W - base.MARGIN * 2 - 28, 72,
        size=7.8, leading=11.5,
    )

    base.draw_footer(c, 9, "EVENT CATALOG / DANGEROUS COMBINATIONS / ENGLISH")
    c.showPage()


def main() -> None:
    OUTPUT.parent.mkdir(parents=True, exist_ok=True)
    base.register_fonts()
    c = canvas.Canvas(str(OUTPUT), pagesize=A4, pageCompression=1)
    c.setTitle("Stage Flux v4.2.3 - Event List - English")
    c.setAuthor("Stage Flux")
    c.setSubject("English Stage Flux event catalog with HUD icons")
    draw_cover(c)
    draw_event_pages(c)
    draw_notes(c)
    draw_dangerous_combinations(c)
    c.save()
    print(OUTPUT)


if __name__ == "__main__":
    main()
