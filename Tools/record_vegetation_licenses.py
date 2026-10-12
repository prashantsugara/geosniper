import hashlib,json
from pathlib import Path
root=Path(__file__).resolve().parent.parent
downloads=Path.home()/'Downloads'
items=[
 ('Realistic Trees-Pack of 2','styropyro','https://www.cgtrader.com/free-3d-models/plant/other/realistic-trees-pack-of-2',['glb.zip']),
 ('Low Poly Plant Bush Asset Pack Greenery plants 3D game-ready','Coynese','https://www.cgtrader.com/free-3d-models/plant/bush/low-poly-plant-bush-asset-pack-greenery-plants-3d-game-ready',['Plants.zip']),
 ('Free Basic Grass and Shrubs','connorwilding','https://www.cgtrader.com/free-3d-models/plant/grass/basic-grass',['BasicGrass_01.fbx','BasicGrass_02.fbx','BasicGrass_Textures.zip'])]
records=[]
for title,creator,source,files in items:
 records.append(dict(title=title,creator=creator,source=source,license='Royalty Free License (no AI)',license_url='https://help.cgtrader.com/hc/en-us/articles/360015124437-Royalty-Free-License',downloaded='2026-10-06',downloads=[dict(file=f,sha256=hashlib.sha256((downloads/f).read_bytes()).hexdigest()) for f in files]))
(root/'Store/CGTraderVegetationLicenses.json').write_text(json.dumps(dict(assets=records,changes='Grounded and normalized meshes; trees reduced to approximately 16,000 triangles with whole leaf islands preserved, leaf cards enlarged for canopy coverage, and simplified branches, bushes to 2,000; atlases resized to 1024px, grass opacity assembled from supplied mask. Spatial batches, quality-scaled tree counts and distance culling for gameplay. Source files retained only as local authoring inputs.'),indent=2))
