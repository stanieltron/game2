"""Check actual rail pixels stay on their tiles in --factory-qa captures."""
import sys
from pathlib import Path
from PIL import Image


def rail_at(image, x, y):
    scale = min(image.width / 1600, image.height / 900)
    x, y = round(x * scale), round(y * scale)
    for px in range(x - 1, x + 2):
        for py in range(y - 1, y + 2):
            r, g, b = image.getpixel((px, py))[:3]
            if r > 100 and g > 90 and b < 130 and r > b * 1.4 and g > b * 1.2:
                return True
    return False


def verify(folder):
    production = Image.open(folder / "03-production.png")
    # The vertical belt at (-11,-1), in a panned 48px/tile map.
    for side in (-1, 1):
        assert rail_at(production, 823 + side * 48 * .27, 530), (
            f"{folder}: placed vertical belt rail escaped its tile"
        )
    for direction, (dx, dy) in enumerate(((1, 0), (0, 1), (-1, 0), (0, -1))):
        preview = Image.open(folder / f"11-belt-preview-{direction}.png")
        # Preview at (-8,-2), 38px/tile; check both rails, away from arrows.
        for side in (-1, 1):
            x = 922 - dx * 38 * .2 - dy * side * 38 * .27
            y = 477 - dy * 38 * .2 + dx * side * 38 * .27
            assert rail_at(preview, x, y), f"{folder}: preview rotation {direction} rail displaced"
    print(f"PASS {folder}: placed vertical belt and all four preview orientations")


if __name__ == "__main__":
    for argument in sys.argv[1:]:
        verify(Path(argument))
