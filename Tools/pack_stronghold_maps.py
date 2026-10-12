"""Convert licensed ORM channels to Unity Standard metallic/smoothness layout."""
from pathlib import Path
from PIL import Image, ImageOps
root = Path(__file__).resolve().parents[1] / 'Assets/Resources/Models/Stronghold'
for atlas in ('Plaster', 'Stone', 'Timber', 'Sandbag_surface_0'):
    source = Image.open(root / (atlas + '_orm.png')).convert('RGB')
    ao, roughness, metallic = source.split()
    zero = Image.new('L', source.size, 0)
    # Plaster, stone, timber and woven cloth are dielectric. The cloth's blue
    # channel is fully white; retaining it makes the bag act like dark metal.
    Image.merge('RGBA', (zero, zero, zero, ImageOps.invert(roughness))).save(root / (atlas + '_metallic.png'))
    # Standard's occlusion sampler reads green rather than the ORM red channel.
    Image.merge('RGB', (ao, ao, ao)).save(root / (atlas + '_occlusion.png'))
