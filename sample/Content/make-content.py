# Copyright (c) 2026 Hot Chai Games LLC. Licensed under the MIT License.
#
# make-content.py: the sample's art and sounds, procedural (shapes, tones and noise computed from
# numbers, with fixed seeds) except for the character, so the sample can be shared under the
# repository's MIT license. Writes Sprites/*.png (the character's idle and jump frames, the coin's
# spin, two gems, a heart, a star and the nine-patch panel), LaunchScreen/logo.png and the app's
# icons (in the desktop, ios, android and webgl folders beside Content), and Sounds/*.wav (blip,
# chime, coin, jump and pop). The character, Source/character.png, is the framework sample's; the
# font, Fonts/Inter-Medium.ttf, is Inter's, under the SIL Open Font License 1.1 (Fonts/OFL.txt).
#
#   python sample/Content/make-content.py
#
# Needs numpy and Pillow.
import math, os, wave
import numpy as np
from PIL import Image, ImageDraw, ImageFilter

HERE = os.path.dirname(os.path.abspath(__file__))
SS = 4  # supersampling, for antialiased edges


def path(*parts):
    p = os.path.join(HERE, *parts)
    os.makedirs(os.path.dirname(p), exist_ok=True)
    return p


def canvas(w, h):
    return Image.new('RGBA', (w * SS, h * SS), (0, 0, 0, 0))


def save(im, name):
    w, h = im.size
    im.resize((w // SS, h // SS), Image.LANCZOS).save(path('Sprites', name + '.png'), optimize=True)


def s(*v):
    return [x * SS for x in v]


def overlay(im, draw):
    # Translucent shapes blended over the image: ImageDraw on its own replaces pixels, alpha included
    layer = Image.new('RGBA', im.size, (0, 0, 0, 0))
    draw(ImageDraw.Draw(layer))
    im.alpha_composite(layer)


def shadow(im, cx, cy, rx, ry, alpha):
    # A soft ellipse under a sprite, on a layer of its own so it's blurred alone
    layer = Image.new('RGBA', im.size, (0, 0, 0, 0))
    ImageDraw.Draw(layer).ellipse(s(cx - rx, cy - ry, cx + rx, cy + ry), fill=(20, 20, 30, alpha))
    im.alpha_composite(layer.filter(ImageFilter.GaussianBlur(SS * 1.5)))


# The character: the framework sample's (Source/character.png), squashed and stretched about its
# feet as it breathes, and lifted as it jumps. It waves with one hand and wears its bandana
# to one side, so it shows which way it faces. Each frame is 192 x 248 with the feet at row 240.
FRAME_W, FRAME_H, FEET = 192, 248, 240
_character = []


def character_image():
    if not _character:
        src = Image.open(path('Source', 'character.png')).convert('RGBA')
        # Cropped to what shows: the image has stray, nearly clear pixels in its top and bottom
        # rows, which would leave the feet above the ground
        src = src.crop(src.getchannel('A').point(lambda v: 255 if v > 128 else 0).getbbox())
        h = 150 * SS
        _character.append(src.resize((round(src.width * h / src.height), h), Image.LANCZOS))
    return _character[0]


def character(name, squash, lift, shadow_scale=1.0):
    im = canvas(FRAME_W, FRAME_H)
    shadow(im, FRAME_W / 2, FEET + 1, 50 * shadow_scale, 6 * shadow_scale, int(110 * shadow_scale))
    src = character_image()
    body = src.resize((round(src.width * squash), round(src.height / squash)), Image.LANCZOS)
    im.alpha_composite(body, ((FRAME_W * SS - body.width) // 2, round((FEET - lift) * SS) - body.height))
    save(im, name)


def characters():
    # Idle: a slow breath, a little taller and narrower and back, about its feet
    for i in range(12):
        a = i / 12 * 2 * math.pi
        character(f'character_idle_{i}', 1 - 0.012 * (1 - math.cos(a)), 0)
    # Jump: crouch, launch, rise, the top, fall, land, settle
    for i, (squash, lift) in enumerate([(1.14, 0), (0.86, 12), (0.9, 40), (0.96, 60), (0.94, 48), (0.9, 20), (1.16, 0), (1.04, 0)]):
        character(f'character_jump_{i}', squash, lift, shadow_scale=max(0.45, 1 - lift / 100))


GOLD, GOLD_DARK, GOLD_LIGHT = (252, 200, 48, 255), (200, 140, 20, 255), (255, 232, 140, 255)


COIN_FRAMES, COIN_R, COIN_T = 16, 26, 5  # frames in half a turn, radius, thickness


def shade(color, f):
    # A color lightened (f > 0) toward white or darkened (f < 0) toward black
    r, g, b, a = color
    t = (255, 255, 255) if f > 0 else (0, 0, 0)
    f = abs(f)
    return (round(r + (t[0] - r) * f), round(g + (t[1] - g) * f), round(b + (t[2] - b) * f), a)


def coin(i):
    # The coin turned about its vertical axis by i / COIN_FRAMES of half a turn. Its faces are alike
    # and its star is symmetric, so half a turn brings it back to the first frame: the frames loop.
    theta = i / COIN_FRAMES * math.pi
    c, sn = math.cos(theta), math.sin(theta)
    rx = COIN_R * abs(c)
    cy = 32
    im = canvas(64, 64)
    d = ImageDraw.Draw(im)
    # The edge: the face's outline swept through the coin's thickness, ridged, lit at the middle
    half = COIN_T / 2 * sn
    steps = 24
    for k in range(steps + 1):
        x = 32 + (k / steps * 2 - 1) * half
        ridge = 0.12 if k % 4 < 2 else -0.08
        light = 0.25 * (1 - abs(k / steps * 2 - 1))
        d.ellipse(s(x - rx - 0.6, cy - COIN_R, x + rx + 0.6, cy + COIN_R), fill=shade(GOLD_DARK, ridge + light - 0.1))
    # The face toward the viewer, lit most when it faces the light, a little to the right
    if rx > 0.8:
        fx = 32 + (half if c >= 0 else -half)
        light = 0.35 * math.cos(theta - 0.35) ** 2 - 0.15
        face, rim = shade(GOLD, light), shade(GOLD_DARK, light)
        d.ellipse(s(fx - rx, cy - COIN_R, fx + rx, cy + COIN_R), fill=rim)
        inner = rx * (COIN_R - 3) / COIN_R
        d.ellipse(s(fx - inner, cy - COIN_R + 3, fx + inner, cy + COIN_R - 3), fill=face)
        ring = rx * 0.74
        if ring > 1.5:
            d.ellipse(s(fx - ring, cy - COIN_R * 0.74, fx + ring, cy + COIN_R * 0.74), outline=rim, width=max(1, round(1.2 * SS * abs(c) ** 0.5)))
        # A raised star, foreshortened with the face, with its lit and shadowed sides
        star = []
        for k in range(10):
            r = 11 if k % 2 == 0 else 4.6
            a = -math.pi / 2 + k * math.pi / 5
            star.append((fx + r * math.cos(a) * abs(c), cy + 1 + r * math.sin(a)))
        d.polygon(s(*[v for p in star for v in (p[0] + 0.6 * abs(c), p[1] + 0.8)]), fill=shade(rim, -0.15))
        d.polygon(s(*[v for p in star for v in p]), fill=shade(GOLD_LIGHT, light))
        # A gloss sliding across the face as it turns, by the angle of the face showing (the back
        # face's is half a turn off), so the last frame leads smoothly into the first
        phi = theta if c >= 0 else theta - math.pi
        gx = fx + rx * (0.35 - 0.6 * math.sin(phi))
        overlay(im, lambda o: o.ellipse(s(gx - rx * 0.18, cy - COIN_R * 0.7, gx + rx * 0.18, cy - COIN_R * 0.25), fill=(255, 255, 255, round(120 * abs(c)))))
    save(im, f'coin_{i}')


def coins():
    # Half a turn in even steps, with the edge, the light and the gloss following the angle: smooth
    # when looped
    for i in range(COIN_FRAMES):
        coin(i)


def gem(name, light, mid, dark):
    # A cut gem: a crown above a girdle and a pavilion to its point, each facet its own shade
    im = canvas(64, 64)
    d = ImageDraw.Draw(im)
    top, girdle, point, left, right = 6, 24, 58, 6, 58
    table_l, table_r = 22, 42
    d.polygon(s(table_l, top, table_r, top, right, girdle, left, girdle), fill=mid)
    d.polygon(s(table_l, top, 32, girdle, left, girdle), fill=light)
    d.polygon(s(table_r, top, right, girdle, 32, girdle), fill=dark)
    d.polygon(s(left, girdle, 32, girdle, 32, point), fill=light)
    d.polygon(s(32, girdle, right, girdle, 32, point), fill=dark)
    d.polygon(s(20, girdle, 32, girdle, 32, point), fill=mid)
    overlay(im, lambda o: o.line(s(table_l + 2, top + 2, 27, top + 9), fill=(255, 255, 255, 170), width=int(2 * SS)))
    save(im, name)


def heart():
    im = canvas(64, 64)
    d = ImageDraw.Draw(im)
    pts = []
    for k in range(200):
        t = k / 200 * 2 * math.pi
        x = 16 * math.sin(t) ** 3
        y = 13 * math.cos(t) - 5 * math.cos(2 * t) - 2 * math.cos(3 * t) - math.cos(4 * t)
        pts += [32 + x * 1.65, 33 - y * 1.65]
    d.polygon(s(*pts), fill=(224, 60, 86, 255))
    overlay(im, lambda o: o.ellipse(s(15, 14, 23, 22), fill=(255, 255, 255, 150)))
    save(im, 'heart')


def star():
    im = canvas(64, 64)
    d = ImageDraw.Draw(im)
    pts = []
    for k in range(10):
        r = 28 if k % 2 == 0 else 12
        a = -math.pi / 2 + k * math.pi / 5
        pts += [32 + r * math.cos(a), 35 + r * math.sin(a)]
    d.polygon(s(*pts), fill=(252, 196, 40, 255), outline=(214, 150, 20, 255), width=int(1.5 * SS))
    save(im, 'star')


def panel():
    # The nine-patch: 24-pixel caps (the sample's ThicknessInt(24)) holding the rounded corners; the
    # middle is plain, so it stretches cleanly. The panel is the top-left 96 pixels, with an 8-pixel
    # shadow below and to the right of it, which the sample draws outside a panel's bounds (an outset).
    im = canvas(104, 104)
    shadow = canvas(104, 104)
    ImageDraw.Draw(shadow).rounded_rectangle(s(4, 4, 101, 101), radius=14 * SS, fill=(30, 20, 10, 90))
    im.alpha_composite(shadow.filter(ImageFilter.GaussianBlur(1.5 * SS)))
    d = ImageDraw.Draw(im)
    d.rounded_rectangle(s(1, 1, 95, 95), radius=14 * SS, fill=(120, 90, 60, 255))
    d.rounded_rectangle(s(5, 5, 91, 91), radius=11 * SS, fill=(250, 246, 236, 255))
    save(im, 'panel')


# The sample's emblem, for the launch screen's logo and the app's icon: the character on a warm
# gradient, standing on a soft shadow
EMBLEM_TOP, EMBLEM_BOTTOM = (255, 200, 96), (239, 104, 72)


def emblem_background(size):
    W = size * SS
    t = np.linspace(0, 1, W)[:, None, None]
    rgb = np.array(EMBLEM_TOP) * (1 - t) + np.array(EMBLEM_BOTTOM) * t
    rgb = np.broadcast_to(rgb, (W, W, 3))
    return Image.fromarray(np.dstack([rgb, np.full((W, W), 255)]).astype(np.uint8), 'RGBA')


def emblem_figure(size, height=0.62, feet=0.84):
    # The character and its shadow on clear: height and feet are fractions of the size
    W = size * SS
    im = Image.new('RGBA', (W, W), (0, 0, 0, 0))
    y = round(W * feet)
    shade = Image.new('RGBA', im.size, (0, 0, 0, 0))
    ImageDraw.Draw(shade).ellipse([W * (0.5 - height * 0.32), y - W * 0.025, W * (0.5 + height * 0.32), y + W * 0.03], fill=(120, 40, 20, 90))
    im.alpha_composite(shade.filter(ImageFilter.GaussianBlur(W * 0.012)))
    src = character_image()
    h = round(W * height)
    body = src.resize((round(src.width * h / src.height), h), Image.LANCZOS)
    im.alpha_composite(body, ((W - body.width) // 2, y - body.height))
    return im


def emblem(size, rounded):
    # Square and opaque, for an app icon, or a rounded square on clear, for the launch screen
    im = emblem_background(size)
    im.alpha_composite(emblem_figure(size))
    if rounded:
        mask = Image.new('L', im.size, 0)
        ImageDraw.Draw(mask).rounded_rectangle([0, 0, im.width - 1, im.height - 1], radius=im.width * 0.22, fill=255)
        im.putalpha(mask)
    return im.resize((size, size), Image.LANCZOS)


def logo_and_icons():
    # The launch screen's logo, and the app's icon in each platform's form, written into the
    # platforms' folders beside Content
    sample = os.path.dirname(HERE)
    emblem(512, rounded=True).save(path('LaunchScreen', 'logo.png'), optimize=True)
    icon = emblem(1024, rounded=False)
    # iOS: one 1024-pixel icon in the app's asset catalog, which Xcode sizes for each use
    ios = os.path.join(sample, 'ios', 'Assets.xcassets', 'AppIcon.appiconset')
    os.makedirs(ios, exist_ok=True)
    icon.convert('RGB').save(os.path.join(ios, 'icon.png'), optimize=True)
    # Windows: the executable's icon, at the sizes Explorer and the taskbar use
    icon.save(os.path.join(sample, 'desktop', 'icon.ico'), sizes=[(16, 16), (24, 24), (32, 32), (48, 48), (64, 64), (128, 128), (256, 256)])
    # The web: the page's icon, and the installed app's
    for n in (180, 192, 512):
        icon.resize((n, n), Image.LANCZOS).save(os.path.join(sample, 'webgl', 'wwwroot', f'icon-{n}.png'), optimize=True)
    # Android: an adaptive icon, a background and a foreground that the launcher masks to its own
    # shape, showing at least the middle two thirds (so the character is smaller), and the plain
    # icon for launchers before Android 8
    res = os.path.join(sample, 'android', 'Resources', 'mipmap-xxxhdpi')
    os.makedirs(res, exist_ok=True)
    emblem_background(432).resize((432, 432), Image.LANCZOS).convert('RGB').save(os.path.join(res, 'icon_background.png'), optimize=True)
    emblem_figure(432, height=0.42, feet=0.73).resize((432, 432), Image.LANCZOS).save(os.path.join(res, 'icon_foreground.png'), optimize=True)
    emblem(192, rounded=True).save(os.path.join(res, 'icon.png'), optimize=True)


def images():
    logo_and_icons()
    characters()
    coins()
    gem('gem_blue', (140, 200, 255, 255), (80, 156, 240, 255), (40, 104, 200, 255))
    gem('gem_green', (140, 240, 170, 255), (70, 200, 120, 255), (30, 150, 80, 255))
    heart()
    star()
    panel()


def write(name, x, rate=44100):
    x = np.clip(x, -1, 1)
    with wave.open(path('Sounds', name + '.wav'), 'wb') as w:
        w.setnchannels(1); w.setframerate(rate); w.setsampwidth(2)
        w.writeframes(np.round(x * 32767).astype('<i2').tobytes())


def envelope(n, attack, decay_rate, rate=44100):
    t = np.arange(n) / rate
    e = np.exp(-t * decay_rate)
    a = min(n, int(attack * rate))
    e[:a] *= np.linspace(0, 1, a)
    e[-64:] *= np.linspace(1, 0, 64)  # no click at the end
    return e


def sweep(f, rate=44100):
    return np.sin(2 * np.pi * np.cumsum(f) / rate)


def normalized(x, peak=0.8):
    return x * peak / np.max(np.abs(x))


def sounds():
    R = 44100
    # Blip: a short square-ish tone, for buttons
    n = int(0.08 * R); t = np.arange(n) / R
    write('blip', normalized(np.tanh(3 * np.sin(2 * np.pi * 880 * t)) * envelope(n, 0.002, 30)))
    # Chime: two bell partials a fifth apart, ringing out
    n = int(0.7 * R); t = np.arange(n) / R
    x = sum(a * np.sin(2 * np.pi * f * t) * np.exp(-t * d) for f, a, d in [(988, 1, 5), (1482, 0.6, 6), (2730, 0.25, 9), (3950, 0.1, 12)])
    write('chime', normalized(x * envelope(n, 0.003, 0)))
    # Coin: two quick rising notes
    n = int(0.32 * R); t = np.arange(n) / R
    f = np.where(t < 0.07, 988, 1319)
    write('coin', normalized(np.tanh(2 * sweep(f)) * envelope(n, 0.002, 9)))
    # Jump: a rising sweep
    n = int(0.24 * R); t = np.arange(n) / R
    write('jump', normalized(np.tanh(2 * sweep(220 + 700 * (t / t[-1]) ** 0.7)) * envelope(n, 0.004, 10)))
    # Pop: a falling pitch drop with a puff of filtered noise
    n = int(0.05 * R); t = np.arange(n) / R
    rng = np.random.default_rng(5)
    noise = np.convolve(rng.standard_normal(n), np.ones(12) / 12, mode='same')
    write('pop', normalized((sweep(900 * np.exp(-t * 40) + 120) + 0.4 * noise) * envelope(n, 0.001, 70)))


if __name__ == '__main__':
    images()
    sounds()
