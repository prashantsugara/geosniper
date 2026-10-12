import json, urllib.request, urllib.parse
from concurrent.futures import ThreadPoolExecutor
from pathlib import Path
root=Path(__file__).resolve().parents[1]
def search(query):
    url='https://api.sketchfab.com/v3/search?'+urllib.parse.urlencode({'type':'models','q':query,'downloadable':'true','count':24})
    data=json.load(urllib.request.urlopen(url,timeout=30))
    result=[]
    for m in data.get('results',[]):
        label=m.get('license',{}).get('label','')
        if label not in ('CC Attribution','CC0 Public Domain') or m.get('faceCount',999999)>60000:continue
        result.append(dict(query=query,uid=m['uid'],name=m['name'],faces=m['faceCount'],creator=m['user']['displayName'],license=label,url=m['viewerUrl'],thumbnail=max(m['thumbnails']['images'],key=lambda t:t['width'])['url']))
    return sorted(result,key=lambda r:r['faces'])
with ThreadPoolExecutor(max_workers=4) as pool:
    groups=list(pool.map(search,['military tent','warehouse building','apartment building','shopping mall']))
out=root/'output/level-locations';out.mkdir(parents=True,exist_ok=True)
(out/'asset-candidates.json').write_text(json.dumps(sum(groups,[]),indent=2))
for group in groups:print(json.dumps(group[:8]))
