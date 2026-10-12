import json,socket,sys
if sys.argv[1]=='--code-file':
 from pathlib import Path
 payload={'type':'execute_code','params':{'code':Path(sys.argv[2]).read_text(encoding='utf-8')}}
else:
 payload=json.loads(sys.argv[1])
with socket.create_connection(('127.0.0.1',9876),timeout=15) as s:
 s.settimeout(180)
 s.sendall(json.dumps(payload).encode())
 data=b''
 while True:
  part=s.recv(65536)
  if not part:break
  data+=part
  try:
   result=json.loads(data)
   print(json.dumps(result))
   break
  except json.JSONDecodeError:pass
