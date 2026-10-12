from PIL import Image,ImageDraw,ImageFont
from pathlib import Path
import random,uuid,re
out=Path('Assets/Resources/Textures/StoryDocument.png');out.parent.mkdir(parents=True,exist_ok=True)
im=Image.new('RGB',(256,384),(235,231,215));d=ImageDraw.Draw(im);font=lambda n:ImageFont.truetype('C:/Windows/Fonts/arial.ttf',n)
d.rectangle((10,10,245,373),outline=(155,151,137),width=1)
d.text((22,23),'FIELD ARCHIVE',font=font(22),fill=(24,40,55))
d.text((22,52),'SIGNED DISPATCH / 042',font=font(12),fill=(52,62,69))
d.line((22,76,231,76),fill=(65,79,89),width=2)
d.text((22,91),'ROUTE: CIVIC SURVEY',font=font(13),fill=(35,45,51))
d.text((22,114),'AUTHORIZATION RECORD',font=font(12),fill=(35,45,51))
r=random.Random(20)
for y in range(146,290,12):
 for x in range(22,214,28):d.line((x,y,min(x+r.randint(10,26),231),y),fill=(80,83,78),width=2)
d.rectangle((31,307,225,339),outline=(145,45,35),width=2);d.text((46,313),'CONFIDENTIAL',font=font(19),fill=(145,45,35))
d.text((22,353),'ORIGINAL / EVIDENCE COPY',font=font(10),fill=(60,67,68))
im.save(out)
p=Path(str(out)+'.meta')
guid=re.search(r'guid: (\w+)',p.read_text()).group(1) if p.exists() else uuid.uuid4().hex
settings=Path('Assets/Resources/Textures/M40ScoutCard.png.meta').read_text()
p.write_text(re.sub(r'guid: \w+','guid: '+guid,settings,count=1))
