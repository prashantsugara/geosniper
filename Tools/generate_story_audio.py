"""Bake offline commander narration. Kokoro is a development tool, not an app dependency."""
import argparse, hashlib, json, sys, wave
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
parser = argparse.ArgumentParser()
parser.add_argument('--runtime', type=Path, default=ROOT/'.utmp/kokoro-runtime')
parser.add_argument('--models', type=Path, default=ROOT/'.utmp/voice-models')
parser.add_argument('--preview', action='store_true')
parser.add_argument('--floor-radio', action='store_true')
args = parser.parse_args()
sys.path.insert(0, str(args.runtime))
import numpy as np
from kokoro_onnx import Kokoro

voice = Kokoro(str(args.models/'kokoro-v1.0.onnx'), str(args.models/'voices-v1.0.bin'))
style = voice.voices['am_michael']*.65 + voice.voices['am_fenrir']*.35
operations = json.loads((ROOT/'Assets/Resources/Campaign/Story.json').read_text(encoding='utf-8-sig'))['operations']
opening = [
    'Rook, this is Command. Your scout is marked. Take the rooftop shot, or use the stairs and windows. Watch his patrol. Once he is down, withdraw to the green beacon and hold for extraction.',
    'Rook. Mina is moving through the district. Armed patrols are closing in. Cover her route. Keep her alive until she reaches our extraction vehicle.',
    'Rook. The marked courier has the checkpoint schedule. Watch his route. Lead your shot. Stop him before he reaches the exit.'
]
output = ROOT/('output/commander-voice' if args.preview else 'Assets/Resources/Audio/Story')
output.mkdir(parents=True, exist_ok=True)
manifest = []
jobs = []
for node, operation in enumerate(operations):
    jobs.extend([(node, 'Mission', opening[node] if node<3 else 'Rook. '+operation['briefing']+' '+operation['objective']),
                 (node, 'Outcome', operation['outcome'])])
jobs.append((0, 'Radio', 'Scout confirmed. Patrols are responding. Move to the green beacon. Hold your ground.'))
if args.floor_radio:
    jobs=[(floor,'FloorRadio','Rook, Command here. Your objective is on '+('the ground floor' if floor==0 else 'floor '+str(floor+1))+'. Guards cover the other floors. Use the stairs. Stay sharp.') for floor in range(6)]
if args.preview: jobs = jobs[:1]
for node, part, text in jobs:
    samples, rate = voice.create(text, voice=style, speed=.95, lang='en-us', sentence_pause=.22, clause_pause=.08)
    if not np.isfinite(samples).all(): raise ValueError('Non-finite voice samples')
    peak=float(np.max(np.abs(samples)))
    if peak<.01: raise ValueError('Silent narration')
    samples = samples.astype(np.float32)*(.88/peak)
    fade=min(int(rate*.012),len(samples)//2)
    samples[:fade]*=np.linspace(0,1,fade);samples[-fade:]*=np.linspace(1,0,fade)
    samples=np.concatenate([np.zeros(int(rate*.08)),samples,np.zeros(int(rate*.12))])
    filename=f'Radio_Floor{node}.wav' if part=='FloorRadio' else 'Radio_OpeningExit.wav' if part=='Radio' else f'{part}_{node:02}.wav'
    with wave.open(str(output/filename),'wb') as wav:
        wav.setnchannels(1);wav.setsampwidth(2);wav.setframerate(rate);wav.writeframes((samples*32767).astype('<i2').tobytes())
    manifest.append(dict(id=node, part=part,file=filename,textHash=hashlib.sha256(text.encode()).hexdigest(),voice='Kokoro-1.0 / Michael 65% + Fenrir 35%',speed=.95,text=text,seconds=round(len(samples)/rate,2)))
    print(f'{filename}: {len(samples)/rate:.1f}s',flush=True)
(output/('floor-radio-manifest.json' if args.floor_radio else 'manifest.json')).write_text(json.dumps(manifest,indent=2),encoding='utf-8')
