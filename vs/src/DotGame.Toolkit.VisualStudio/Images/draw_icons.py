# Copyright (c) 2026 Hot Chai Games LLC. Licensed under the MIT License.
#
# Draws the extension's folder icons (DotGame.imagemanifest lists them). The folders are Visual Studio's folder
# outline and yellow, in a light and a dark theme's shade, with a filled badge each: tiles for a texture atlas, level
# bars for a sound bank. The content folder has a glyph of its own, the DotGame dot, in either theme. Like Visual
# Studio's folders, they look the same open or closed.
#
#   python draw_icons.py <output folder> [preview.png]
#
# Needs Pillow. The preview shows each icon at 16 px, 4x, on a light and a dark background.

import os, sys
from PIL import Image, ImageDraw

OUT = sys.argv[1]
os.makedirs(OUT, exist_ok=True)
SS = 16  # supersampling factor per 16-unit grid cell
SIZES = (16, 24, 32, 48)

# Visual Studio's folder yellow, as each theme draws it
OUTLINE = {'Light': (200, 158, 70, 255), 'Dark': (195, 170, 119, 255)}
# Its outline's width in pixels at each size: whole pixels, so it's as crisp as Visual Studio's
STROKE = {16: 1, 24: 2, 32: 2, 48: 3}
BLUE = (31, 111, 209, 255)
BLUE_DARK = (18, 74, 150, 255)
PURPLE = (124, 77, 214, 255)
GREEN = (46, 150, 90, 255)
WHITE = (255, 255, 255, 255)

def P(pts, s):
    return [(x * s, y * s) for x, y in pts]

def line(d, s, pts, color, closed=False):
    # A stroke of whole pixels, centered on whole or half pixels so it doesn't blur when scaled down
    k = s / SS
    w = STROKE[round(k * 16)]
    def snap(v):
        return (round(v * k - w / 2) + w / 2) * SS
    pts = [(snap(x), snap(y)) for x, y in pts]
    if closed:
        pts = pts + pts[:1]
    d.line(pts, fill=color, width=w * SS, joint='curve')
    # Round the ends, as the joints are
    r = w * SS / 2
    for x, y in (pts[0], pts[-1]):
        d.ellipse([x - r, y - r, x + r, y + r], fill=color)

def folder(d, s, color):
    # Visual Studio's folder: one outline, a tab and the body, the same open or closed
    line(d, s, [(1.5, 4), (2.5, 3), (5.5, 3), (7, 4.5), (13.5, 4.5), (14.5, 5.5),
                (14.5, 12), (13.5, 13), (2.5, 13), (1.5, 12)], color, closed=True)

def cutout(img, s, box):
    # Clear a margin around the badge so it reads against the folder
    x0, y0, x1, y1 = box
    m = 1.0
    mask = Image.new('L', img.size, 0)
    ImageDraw.Draw(mask).rounded_rectangle(P([(x0 - m, y0 - m), (x1 + m, y1 + m)], s), radius=2 * s, fill=255)
    clear = Image.new('RGBA', img.size, (0, 0, 0, 0))
    img.paste(clear, (0, 0), mask)

def content(d, s):
    # Not a folder: a glyph of its own, as wwwroot has its globe. The DotGame dot.
    d.ellipse(P([(1, 1), (15, 15)], s), fill=BLUE_DARK)
    d.ellipse(P([(1.9, 1.9), (14.1, 14.1)], s), fill=BLUE)
    d.ellipse(P([(5.4, 5.4), (10.6, 10.6)], s), fill=WHITE)

def badge_atlas(d, s):
    for (x, y) in [(8.5, 8.5), (12.3, 8.5), (8.5, 12.3), (12.3, 12.3)]:
        d.rounded_rectangle(P([(x, y), (x + 3.2, y + 3.2)], s), radius=0.5 * s, fill=PURPLE)

def badge_bank(d, s):
    for x, top in [(8.6, 11.5), (10.6, 8.6), (12.6, 10.0), (14.6, 9.2)]:
        d.rounded_rectangle(P([(x - 0.75, top), (x + 0.75, 15.6)], s), radius=0.5 * s, fill=GREEN)

def render(draw, size):
    s = size / 16 * SS
    big = Image.new('RGBA', (size * SS, size * SS), (0, 0, 0, 0))
    draw(big, s)
    # An average of each pixel's samples: aligned edges stay sharp
    return big.resize((size, size), Image.BOX)

def save(img, name):
    img.save(os.path.join(OUT, name))

for size in SIZES:
    save(render(lambda img, s: content(ImageDraw.Draw(img), s), size), f"ContentFolder.{size}.png")

for name, badge in {'Atlas': badge_atlas, 'Bank': badge_bank}.items():
    for theme, color in OUTLINE.items():
        for size in SIZES:
            def draw(img, s):
                folder(ImageDraw.Draw(img), s, color)
                cutout(img, s, (8, 8, 16, 16))
                badge(ImageDraw.Draw(img), s)
            save(render(draw, size), f"{name}Folder.{theme}.{size}.png")

if len(sys.argv) < 3:
    sys.exit(0)

icons = ['ContentFolder', 'AtlasFolder.{theme}', 'BankFolder.{theme}']
sheet = Image.new('RGBA', (len(icons) * 72 + 8, 2 * 76), (0, 0, 0, 0))
for row, (theme, bg) in enumerate([('Light', (245, 245, 245, 255)), ('Dark', (31, 31, 31, 255))]):
    ImageDraw.Draw(sheet).rectangle([0, row * 76, sheet.width, row * 76 + 76], fill=bg)
    for i, n in enumerate(icons):
        icon = Image.open(os.path.join(OUT, f"{n.format(theme=theme)}.16.png")).resize((64, 64), Image.NEAREST)
        sheet.alpha_composite(icon, (8 + i * 72, row * 76 + 6))
sheet.save(sys.argv[2])
