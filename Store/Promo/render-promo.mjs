// Render with Node.js. All footage is from the project's own Android recording.
// No runtime game files are modified. No reference/inspiration video is used.
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { spawnSync } from 'node:child_process';

const dir = path.dirname(fileURLToPath(import.meta.url));
const root = path.resolve(dir, '../..');
const work = path.join(dir, 'work');
const ffmpeg = process.env.PROMO_FFMPEG || path.join(root, '.utmp/promo-tools/imageio_ffmpeg/binaries/ffmpeg-win-x86_64-v7.1.exe');
const footage = path.join(root, 'issues/issues.mp4');
const feature = path.join(dir, 'geosniper-feature-1024x500.png');
const masterArt = path.join(dir, 'geosniper-feature-master.png');
const out = path.join(dir, 'geosniper-gps-trailer-1080p.mp4');
const reuseClips = process.argv.includes('--reuse-clips');
fs.mkdirSync(work, { recursive: true });
for (const file of [ffmpeg, footage, feature, masterArt]) if (!fs.existsSync(file)) throw Error(`Missing input: ${file}`);

const clips = [
  { start: 141, duration: 5, title: 'YOUR CITY. YOUR NEXT MISSION.', sub: 'Sniper missions built from real-world map data.', topic: 'REAL-WORLD MAPS', credit: true },
  { start: 206, duration: 5.3, title: 'REAL STREETS. A NEW PERSPECTIVE.', sub: 'Explore game environments shaped by mapped buildings and roads.', topic: 'EXPLORE YOUR SECTOR' },
  { start: 118, duration: 5.3, title: 'FIND YOUR OPENING.', sub: 'Track moving targets through your scope.', topic: 'PRECISION AIMING' },
  { start: 167.2, duration: 5.3, title: 'TAKE THE SHOT.', sub: 'Time your aim. Commit when the moment is right.', topic: 'TACTICAL SHOOTING' },
  { start: 149, duration: 5.3, title: 'CHOOSE YOUR NEXT LOCATION.', sub: 'Use GPS nearby, or search for a place by name.', topic: 'GPS + PLACE SEARCH' },
];

function run(args, logName) {
  const r = spawnSync(ffmpeg, ['-hide_banner', '-nostdin', '-y', ...args], { cwd: dir, encoding: 'utf8', maxBuffer: 8 * 1024 * 1024 });
  if (logName) fs.writeFileSync(path.join(work, logName), r.stderr || '');
  if (r.status !== 0) throw Error((r.stderr || String(r.error)).slice(-7000));
  return r.stderr;
}
const font = "fontfile='C\\:/Windows/Fonts/arial.ttf'";
const bold = "fontfile='C\\:/Windows/Fonts/arialbd.ttf'";
const text = (value, x, y, size, color, heavy = false) => `drawtext=${heavy ? bold : font}:text='${value}':x=${x}:y=${y}:fontsize=${size}:fontcolor=${color}`;

// Original 96 BPM instrumental: synthesized pads, bass, arpeggio and percussion.
// No external songs, recordings, loops or samples are included.
const rate = 48000, seconds = 30, total = rate * seconds;
const left = new Float32Array(total), right = new Float32Array(total);
const hz = midi => 440 * 2 ** ((midi - 69) / 12);
let seed = 194207;
function noise() { seed = (Math.imul(seed, 1664525) + 1013904223) >>> 0; return seed / 2147483648 - 1; }
function sound(start, duration, amp, pan, wave) {
  const first = Math.round(start * rate), n = Math.min(Math.round(duration * rate), total - first);
  const l = Math.sqrt((1 - pan) / 2) * amp, r = Math.sqrt((1 + pan) / 2) * amp;
  for (let i = 0; i < n; i++) { const v = wave(i / rate, i / n); left[first + i] += v * l; right[first + i] += v * r; }
}
const tau = Math.PI * 2, beat = 60 / 96, bar = beat * 4;
const chords = [[50, 57, 62, 65], [46, 53, 58, 62], [48, 55, 60, 64], [50, 57, 62, 69]];
for (let b = 0; b < 12; b++) {
  const notes = chords[b % 4], time = b * bar;
  notes.forEach((note, j) => {
    const f = hz(note);
    sound(time, bar + 0.5, 0.035, (j - 1.5) / 2.5, (t, p) => {
      const env = Math.min(t / 0.4, 1) * Math.min((1 - p) * 7, 1);
      return env * (Math.sin(tau * f * t) + 0.32 * Math.sin(tau * f * 1.002 * t) + 0.08 * Math.sin(tau * f * 2 * t));
    });
  });
  for (let step = 0; step < 8; step++) {
    const time2 = time + step * beat / 2;
    const note = notes[[2, 1, 3, 1, 2, 1, 3, 0][step]] + 12, f = hz(note);
    sound(time2, 0.58, b < 2 ? 0.02 : 0.045, step % 2 ? 0.35 : -0.35,
      t => Math.min(t / 0.007, 1) * Math.exp(-t * 8) * (Math.sin(tau * f * t) + 0.16 * Math.sin(tau * 2 * f * t)));
    if (b >= 2 && b < 11) sound(time2, 0.07, step % 2 ? 0.018 : 0.012, 0.3, t => noise() * Math.exp(-t * 62));
  }
  for (let step = 0; step < 4; step++) {
    const t0 = time + step * beat, bassHz = hz(notes[0] - 12);
    sound(t0, beat * 0.84, b < 2 ? 0.06 : 0.11, 0,
      t => Math.min(t / 0.013, 1) * Math.exp(-t * 4) * (Math.sin(tau * bassHz * t) + 0.13 * Math.sin(tau * 2 * bassHz * t)));
    if (b >= 2 && b < 11 && step % 2 === 0) sound(t0, 0.38, 0.18, 0,
      t => Math.sin(tau * (44 * t + 2.9 * (1 - Math.exp(-t * 28)))) * Math.exp(-t * 14));
    if (b >= 4 && b < 11 && step % 2 === 1) sound(t0, 0.17, 0.045, -0.12,
      t => (noise() * 0.7 + Math.sin(tau * 185 * t) * 0.3) * Math.exp(-t * 26));
  }
}
let peak = 0;
for (let i = 0; i < total; i++) {
  const t = i / rate, env = Math.min(t / 0.4, 1, (seconds - t) / 1.1);
  left[i] = Math.tanh(left[i] * 1.7) * env; right[i] = Math.tanh(right[i] * 1.7) * env;
  peak = Math.max(peak, Math.abs(left[i]), Math.abs(right[i]));
}
const wav = Buffer.alloc(44 + total * 4);
wav.write('RIFF'); wav.writeUInt32LE(wav.length - 8, 4); wav.write('WAVEfmt ', 8);
wav.writeUInt32LE(16, 16); wav.writeUInt16LE(1, 20); wav.writeUInt16LE(2, 22);
wav.writeUInt32LE(rate, 24); wav.writeUInt32LE(rate * 4, 28); wav.writeUInt16LE(4, 32); wav.writeUInt16LE(16, 34);
wav.write('data', 36); wav.writeUInt32LE(total * 4, 40);
for (let i = 0; i < total; i++) {
  wav.writeInt16LE(Math.round(left[i] / peak * 26000), 44 + i * 4);
  wav.writeInt16LE(Math.round(right[i] / peak * 26000), 46 + i * 4);
}
fs.writeFileSync(path.join(dir, 'geosniper-original-score.wav'), wav);
console.log('Original instrumental created.');

const encoded = [];
for (let i = 0; i < clips.length; i++) {
  const c = clips[i], file = path.join(work, `clip-${i}.mp4`);
  if (reuseClips && fs.existsSync(file)) { encoded.push(file); continue; }
  // Fit the entire wide phone recording inside a designed 16:9 frame without stretching or cropping.
  const filters = [
    'fps=30,scale=1920:864:flags=lanczos,setsar=1',
    'pad=1920:1080:0:128:color=0x102531',
    'drawbox=x=56:y=29:w=4:h=56:color=0xF3B24E:t=fill',
    text('GEO SNIPER', 80, 38, 34, '0xF3B24E', true),
    'drawbox=x=352:y=28:w=1:h=56:color=0x41606C:t=fill',
    text(c.title, 392, 24, 44, '0xF6F5EF', true),
    text(c.sub, 394, 79, 23, '0xB8CDD4'),
    'drawbox=x=0:y=122:w=1920:h=2:color=0x40BECA:t=fill',
    text(c.topic, 60, 1016, 23, '0xF3B24E', true),
    text(c.credit ? 'Map data (c) OpenStreetMap contributors' : 'GEO SNIPER  /  GPS SHOOTING GAME', '(w-text_w)/2', 1019, 19, '0xABC1CA'),
    text(`0${i + 1} / 05`, 1775, 1016, 22, '0xE7EDF0'),
    'format=yuv420p',
  ];
  console.log(`Rendering gameplay scene ${i + 1}/${clips.length} (${c.start}s).`);
  run(['-ss', String(c.start), '-i', footage, '-t', String(c.duration), '-an', '-vf', filters.join(','), '-c:v', 'libx264', '-crf', '18', '-preset', 'fast', '-r', '30', '-video_track_timescale', '15360', file], `clip-${i}.log`);
  encoded.push(file);
}

const end = path.join(work, 'endcard.mp4');
console.log('Rendering illustrated end card.');
if (!reuseClips || !fs.existsSync(end)) run(['-loop', '1', '-framerate', '30', '-i', masterArt, '-filter_complex',
  '[0:v]split=2[bg][fg];[bg]scale=1920:1080:force_original_aspect_ratio=increase,crop=1920:1080,gblur=sigma=28[base];[fg]scale=1920:938:flags=lanczos[art];[base][art]overlay=0:71,setsar=1,format=yuv420p[v]',
  '-map', '[v]', '-t', '5.3', '-an', '-c:v', 'libx264', '-crf', '18', '-preset', 'fast', '-r', '30', '-video_track_timescale', '15360', end], 'endcard.log');
encoded.push(end);

const inputs = encoded.flatMap(file => ['-i', file]);
const transitions = ['fade', 'fade', 'fade', 'fade', 'fade'];
const parts = encoded.map((_, i) => `[${i}:v]setpts=PTS-STARTPTS,fps=30,settb=AVTB[v${i}]`);
let label = '[v0]';
for (let i = 1; i < encoded.length; i++) {
  const next = `[mix${i}]`;
  parts.push(`${label}[v${i}]xfade=transition=${transitions[i - 1]}:duration=0.3:offset=${(i * 5 - 0.3).toFixed(1)}${next}`);
  label = next;
}
parts.push(`${label}fade=t=in:st=0:d=0.15,fade=t=out:st=29.4:d=0.6,format=yuv420p[video]`);
parts.push('[6:a]loudnorm=I=-16:TP=-1.5:LRA=9,aresample=48000[audio]');
console.log('Assembling 30-second landscape trailer.');
run([...inputs, '-i', path.join(dir, 'geosniper-original-score.wav'), '-filter_complex_threads', '2', '-filter_complex', parts.join(';'),
  '-map', '[video]', '-map', '[audio]', '-t', '30', '-c:v', 'libx264', '-profile:v', 'high', '-crf', '18', '-preset', 'medium',
  '-pix_fmt', 'yuv420p', '-r', '30', '-color_primaries', 'bt709', '-color_trc', 'bt709', '-colorspace', 'bt709',
  '-c:a', 'aac', '-b:a', '192k', '-ar', '48000', '-ac', '2', '-movflags', '+faststart',
  '-metadata', 'title=Geo Sniper | Your City. Your Next Mission.', '-metadata', 'comment=Captured Android gameplay with an original synthesized instrumental and illustrated end card.', out], 'final-render.log');

console.log('Checking complete decode and creating review frames.');
run(['-v', 'error', '-i', out, '-f', 'null', '-'], 'decode-check.log');
run(['-i', out, '-vf', 'fps=1/5,scale=640:360,tile=3x2', '-frames:v', '1', '-update', '1', path.join(dir, 'trailer-storyboard.jpg')], 'storyboard.log');
for (const [n, sec] of [2, 7, 12, 17, 22, 27].entries()) {
  run(['-ss', String(sec), '-i', out, '-frames:v', '1', '-update', '1', path.join(work, `review-${n + 1}.png`)]);
}
fs.writeFileSync(path.join(dir, 'edit-manifest.json'), JSON.stringify({
  durationSeconds: 30, width: 1920, height: 1080, fps: 30,
  video: 'H.264 High, yuv420p, BT.709', audio: 'AAC stereo, 48 kHz, 192 kb/s',
  sourceFootage: 'issues/issues.mp4', sourceRecorded: '2026-09-16', sourceResolution: '1280x576; scaled, no cropped gameplay',
  liveDeviceCapture: false, crossfades: 0.3, clips,
  originalScore: { title: 'Local Coordinates', bpm: 96, samples: 'None; generated oscillators and seeded noise' },
  artwork: 'Built-in image generation with GameLogo.jpg as the branding reference',
  completeDecodePassed: true,
}, null, 2));
console.log(`Completed: ${out}`);
