from pathlib import Path

from PIL import Image


ROOT = Path(__file__).resolve().parents[1] / "Assets" / "UI" / "Runtime"
ASSETS = {
    "Lock": 0.84,
    "Stopwatch": 0.88,
    "Ready": 0.62,
    "Panel": 0.98,
}


for name, occupancy in ASSETS.items():
    image = Image.open(ROOT / f"{name}_full.png").convert("RGBA")
    bounds = image.getchannel("A").getbbox()
    if bounds is None:
        raise RuntimeError(f"No visible pixels found in {name}_full.png")

    cropped = image.crop(bounds)
    target_size = 512
    maximum_side = int(target_size * occupancy)
    scale = min(maximum_side / cropped.width, maximum_side / cropped.height)
    resized_size = (
        max(1, round(cropped.width * scale)),
        max(1, round(cropped.height * scale)),
    )
    cropped = cropped.resize(resized_size, Image.Resampling.LANCZOS)

    canvas = Image.new("RGBA", (target_size, target_size), (0, 0, 0, 0))
    canvas.alpha_composite(
        cropped,
        (
            (target_size - resized_size[0]) // 2,
            (target_size - resized_size[1]) // 2,
        ),
    )
    canvas.save(ROOT / f"{name}.png", optimize=True)
    print(f"{name}: source={bounds}, runtime={resized_size}")
