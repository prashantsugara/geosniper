from pathlib import Path
from PIL import Image, ImageOps
root=Path(__file__).resolve().parent.parent
def convert(source, output, key, prefix, tile=''):
 output.mkdir(parents=True,exist_ok=True)
 def load(suffix):
  return Image.open(source/(prefix+suffix+tile+'.png')).resize((1024,1024),Image.Resampling.LANCZOS)
 load('_BaseColor').convert('RGB').save(output/(key+'_BaseColor.png'))
 load('_Normal').convert('RGB').save(output/(key+'_Normal.png'))
 metal=load('_Metallic').convert('L');smooth=ImageOps.invert(load('_Roughness').convert('L'))
 Image.merge('RGBA',(metal,metal,metal,smooth)).save(output/(key+'_MetallicSmoothness.png'))
for i in range(7):
 key='Material'+str(i).zfill(3)
 prefix=('grip_' if i==0 else 'Desert Eagle_')+key
 convert(root/'.utmp/cgtrader/desert-eagle/Textures',root/'Assets/Resources/Models/Weapons/Authored/Eagle',key,prefix)
convert(root/'.utmp/cgtrader/m4a1/textures',root/'Assets/Resources/Models/Weapons/Authored/M4A1','T_M4A1','Final_M4_Gun')
for prefix in ['Material','Material.001','Material.002','Material.003']:
 convert(root/'.utmp/cgtrader/mp5/BSDF/2K',root/'Assets/Resources/Models/Weapons/Authored/MP5',prefix.replace('.',''),prefix,'.1001')
source=root/'.utmp/cgtrader/m40/packed';out=root/'Assets/Resources/Models/Weapons/Authored/M40';out.mkdir(parents=True,exist_ok=True)
for key in ['Scope','Rust']:
 def load(name):return Image.open(source/(key+'_'+name+'.jpg')).resize((1024,1024),Image.Resampling.LANCZOS)
 load('Color' if key=='Scope' else 'Color(Albedo)').convert('RGB').save(out/(key+'_BaseColor.png'))
 load('Normal').convert('RGB').save(out/(key+'_Normal.png'))
 metal=Image.new('L',(1024,1024),180 if key=='Scope' else 0)
 smooth=load('Glossy').convert('L') if key=='Scope' else ImageOps.invert(load('Roughness').convert('L'))
 Image.merge('RGBA',(metal,metal,metal,smooth)).save(out/(key+'_MetallicSmoothness.png'))
