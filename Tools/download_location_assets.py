import json,socket
from pathlib import Path
root=Path(__file__).resolve().parents[1];out=root/'output/level-locations'
records=[]
for label,uid in [('MilitaryTent','d95296298b2a4caca8a5c3ce4450bb15'),('WarehousePack','320c272513a94c0482dbb3c4e79d61f5'),('BrickApartments','a2a27bec2e364496b34ae399ee93cb67'),('MallInterior','5ede6f812941428dae4b31a3487623fd')]:
    with socket.create_connection(('127.0.0.1',9876),timeout=15) as s:
        s.settimeout(180);s.sendall(json.dumps({'type':'download_sketchfab_model','params':{'uid':uid}}).encode());data=b''
        while True:
            part=s.recv(65536)
            if not part:raise RuntimeError('Blender disconnected')
            data+=part
            try:result=json.loads(data);break
            except json.JSONDecodeError:pass
    records.append(dict(label=label,uid=uid,result=result))
    (out/'download-records.json').write_text(json.dumps(records,indent=2))
    print(json.dumps(records[-1]),flush=True)
