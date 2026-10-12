"""Create original, repeatable surface swatches; no external asset dependencies."""
from pathlib import Path
from PIL import Image, ImageDraw
import random, re, uuid, math

root = Path('Assets/Resources/Textures/Interiors')
root.mkdir(parents=True, exist_ok=True)
template = Path('Assets/Resources/Textures/M40ScoutCard.png.meta').read_text()
for style in ('Plaster', 'StoneTile', 'Oak'):
    rng = random.Random(83)
    im = Image.new('RGB', (512, 512))
    for y in range(512):
        for x in range(512):
            noise = rng.uniform(-4, 4)
            if style == 'Plaster':
                tone = 222 + noise + 2 * math.sin(x * math.tau / 512) * math.cos(y * math.tau / 512)
                rgb = (tone, tone - 5, tone - 14)
            elif style == 'StoneTile':
                seam = x % 128 < 3 or y % 128 < 3
                tone = 111 if seam else 178 + noise + ((x // 128 * 7 + y // 128 * 11) % 5) * 2
                rgb = (tone, tone - 4, tone - 12)
            else:
                offset = (x // 64 % 2) * 128
                grain = 8 * math.sin(x * .45 + 2 * math.sin(y * math.tau / 512))
                tone = 151 + noise + grain
                rgb = (tone, tone * .76, tone * .49)
            im.putpixel((x, y), tuple(max(0, min(255, int(c))) for c in rgb))
    path = root / (style + '.png')
    im.save(path)
    meta = Path(str(path) + '.meta')
    guid = re.search(r'guid: (\w+)', meta.read_text()).group(1) if meta.exists() else uuid.uuid4().hex
    settings = re.sub(r'guid: \w+', 'guid: ' + guid, template, count=1)
    settings = re.sub(r'wrapU: \d+', 'wrapU: 0', settings)
    settings = re.sub(r'wrapV: \d+', 'wrapV: 0', settings)
    meta.write_text(settings)
