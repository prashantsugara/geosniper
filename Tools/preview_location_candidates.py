import json, socket, base64
from pathlib import Path
root=Path(__file__).resolve().parents[1];out=root/'output/level-locations';out.mkdir(parents=True,exist_ok=True)
uids=['d95296298b2a4caca8a5c3ce4450bb15','320c272513a94c0482dbb3c4e79d61f5','a2a27bec2e364496b34ae399ee93cb67','5ede6f812941428dae4b31a3487623fd']
for uid in uids:
    with socket.create_connection(('127.0.0.1',9876),timeout=15) as s:
        s.settimeout(90);s.sendall(json.dumps({'type':'get_sketchfab_model_preview','params':{'uid':uid}}).encode());data=b''
        while True:
            data+=s.recv(65536)
            try:result=json.loads(data);break
            except json.JSONDecodeError:pass
    result=result.get('result',result)
    if 'image_data' in result:
        path=out/(uid+'.'+result.get('format','jpeg'));path.write_bytes(base64.b64decode(result.pop('image_data')));print(str(path))
    else:print(json.dumps(result))
