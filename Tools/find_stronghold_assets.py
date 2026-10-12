import requests,json
from pathlib import Path
rows={}
for query in ['desert building','afghan building','arab building','sandbag']:
 data=requests.get('https://api.sketchfab.com/v3/search',params={'type':'models','q':query,'downloadable':'true','count':16},timeout=30).json()
 for m in data.get('results',[]):
  if m.get('faceCount',1000000)>150000:continue
  license=m.get('license',{}).get('label','')
  if license not in ['CC Attribution','CC0 Public Domain']:continue
  rows[m['uid']]={'uid':m['uid'],'name':m['name'],'faces':m['faceCount'],'license':license,'creator':m['user']['displayName'],'url':m['viewerUrl'],'description':m.get('description','')[:500],'thumbnail':max(m['thumbnails']['images'],key=lambda i:i['width'])['url']}
root=Path(r'C:/Users/Geeta Sugara/Documents/ChatGPT/sniper2')
(root/'output/reference-building/candidates.json').write_text(json.dumps(list(rows.values()),indent=2))
print(json.dumps(list(rows.values())))
