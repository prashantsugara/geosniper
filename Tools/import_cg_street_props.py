"""Import only assets and metadata from the downloaded Unity package, preserving GUID references."""
import tarfile
from pathlib import Path
root=Path(__file__).resolve().parents[1]
destination=root/'Assets/Resources/Models/CGStreetProps'
with tarfile.open(Path.home()/'Downloads/Free_street_props.unitypackage') as archive:
 for member in archive.getmembers():
  if not member.name.endswith('/pathname'):continue
  original=archive.extractfile(member).read().decode().strip()
  relative=Path(original).relative_to('Assets/Free_Street_Props')
  if relative.name=='demo.unity':continue
  target=destination/relative
  if not target.resolve().is_relative_to(destination.resolve()):raise ValueError('Invalid package path')
  prefix=member.name.rsplit('/',1)[0]
  try:asset=archive.extractfile(prefix+'/asset')
  except KeyError:asset=None
  if asset:
   target.parent.mkdir(parents=True,exist_ok=True)
   target.write_bytes(asset.read())
  else:target.mkdir(parents=True,exist_ok=True)
  try:metadata=archive.extractfile(prefix+'/asset.meta')
  except KeyError:metadata=None
  if metadata:Path(str(target)+'.meta').write_bytes(metadata.read())
print('Imported CGTrader street props and original GUID-linked materials, textures and prefabs.')
