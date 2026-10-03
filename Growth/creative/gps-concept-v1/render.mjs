import fs from 'node:fs';
import path from 'node:path';
import os from 'node:os';
import http from 'node:http';
import { fileURLToPath, pathToFileURL } from 'node:url';
import { spawn } from 'node:child_process';
import { once } from 'node:events';
import { ffmpegPath, ffmpegRun } from '../../lib/render.mjs';
import { findCity, getCityForDate, fetchRealLocation } from './cities.mjs';

const dir = path.dirname(fileURLToPath(import.meta.url));
const root = path.resolve(dir, '../..');

function browserPath() {
  if (process.env.GROWTH_CHROMIUM) return process.env.GROWTH_CHROMIUM;
  const base = path.join(process.env.LOCALAPPDATA || path.join(os.homedir(), 'AppData/Local'), 'ms-playwright');
  if (fs.existsSync(base)) {
    for (const item of fs.readdirSync(base).filter(v => /^chromium_headless_shell-\d+$/.test(v)).sort((a, b) => Number(b.split('-').at(-1)) - Number(a.split('-').at(-1)))) {
      const executable = path.join(base, item, 'chrome-headless-shell-win64/chrome-headless-shell.exe');
      if (fs.existsSync(executable)) return executable;
    }
  }
  return undefined;
}

async function playwright() {
  if (process.env.GROWTH_PLAYWRIGHT_MODULE) return import(pathToFileURL(process.env.GROWTH_PLAYWRIGHT_MODULE).href);
  const bundled = path.join(os.homedir(), '.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright/index.mjs');
  if (fs.existsSync(bundled)) return import(pathToFileURL(bundled).href);
  try { return await import('playwright'); } catch { throw Error('Rendering needs local Playwright and Chromium. See the concept README.'); }
}

function effects(file) {
  const sampleRate = 48000, seconds = 18, n = sampleRate * seconds, left = new Float32Array(n), right = new Float32Array(n);
  let seed = 1729;
  const noise = () => { seed = (Math.imul(seed, 1664525) + 1013904223) >>> 0; return seed / 2147483648 - 1; };
  function add(start, length, volume, signal) {
    const first = Math.floor(start * sampleRate), count = Math.min(Math.floor(length * sampleRate), n - first);
    for (let i = 0; i < count; i++) {
      const value = volume * signal(i / sampleRate, i / count);
      left[first + i] += value;
      right[first + i] += value * 0.94;
    }
  }

  // UI interaction taps
  for (const at of [1.5, 2.8]) {
    add(at, 0.09, 0.22, (t, p) => Math.sin(2 * Math.PI * (780 * t - 1400 * t * t)) * Math.exp(-t * 55));
    add(at + 0.08, 0.19, 0.13, (t, p) => Math.sin(2 * Math.PI * 1200 * t) * Math.exp(-t * 19) * Math.min(t * 160, 1));
  }

  // Google Flow 3D Dive Wind & Atmospheric Rush (3.5s - 9.0s)
  add(3.5, 5.5, 0.18, (t, p) => noise() * Math.sin(Math.PI * p) * (0.4 + Math.sin(t * 120) * 0.2));

  // Transitions ambient sweeps
  for (const at of [3.2, 8.8, 11.3, 13.5]) {
    add(at, 0.65, 0.075, (t, p) => noise() * Math.sin(Math.PI * p) ** 2 * (0.35 + Math.sin(t * 170) * 0.15));
  }

  // Radar waypoint pings
  for (const at of [1.5, 2.0, 2.5]) {
    add(at, 0.18, 0.07, t => Math.sin(2 * Math.PI * 1700 * t) * Math.exp(-t * 27));
  }

  // Heartbeat during ADS Aiming (9.0s & 10.0s)
  for (const hb of [9.0, 10.0]) {
    add(hb, 0.15, 0.35, t => Math.sin(2 * Math.PI * 55 * t) * Math.exp(-t * 22));
  }

  // Sniper Rifle Gunshot Blast (Barrett .50 Cal at 11.5s - user video kill shot)
  add(11.5, 0.35, 0.62, t => noise() * Math.exp(-t * 25));
  add(11.5, 0.55, 0.72, (t, p) => Math.sin(2 * Math.PI * (85 * t - 35 * t * t)) * Math.exp(-t * 7));

  // Red Hitmarker Metallic Tick (at 11.7s)
  add(11.7, 0.08, 0.35, t => Math.sin(2 * Math.PI * 2200 * t) * Math.exp(-t * 45));

  // Bolt-action mechanical reload (at 12.7s)
  add(12.7, 0.16, 0.22, t => Math.sin(2 * Math.PI * (1200 - t * 3500) * t) * Math.exp(-t * 22));

  // Cinematic Sub-bass drop (outro at 13.55s)
  add(13.55, 1.8, 0.18, (t, p) => Math.sin(2 * Math.PI * (52 * t + 1.8 * (1 - Math.exp(-t * 15)))) * Math.exp(-t * 5));

  const wav = Buffer.alloc(44 + n * 4);
  wav.write('RIFF');
  wav.writeUInt32LE(wav.length - 8, 4);
  wav.write('WAVEfmt ', 8);
  wav.writeUInt32LE(16, 16);
  wav.writeUInt16LE(1, 20);
  wav.writeUInt16LE(2, 22);
  wav.writeUInt32LE(sampleRate, 24);
  wav.writeUInt32LE(sampleRate * 4, 28);
  wav.writeUInt16LE(4, 32);
  wav.writeUInt16LE(16, 34);
  wav.write('data', 36);
  wav.writeUInt32LE(n * 4, 40);
  for (let i = 0; i < n; i++) {
    wav.writeInt16LE(Math.round(Math.max(-0.95, Math.min(0.95, left[i])) * 32767), 44 + i * 4);
    wav.writeInt16LE(Math.round(Math.max(-0.95, Math.min(0.95, right[i])) * 32767), 46 + i * 4);
  }
  fs.writeFileSync(file, wav);
}

export async function renderConcept({ format = 'portrait', name, city = null, progress = () => {} } = {}) {
  if (!['portrait', 'landscape'].includes(format)) throw Error('Unknown video format.');
  const cityObj = city ? (typeof city === 'object' ? city : await fetchRealLocation(city)) : getCityForDate();
  const filename = name || `geosniper-${cityObj.id}-${format}-${Date.now()}.mp4`;
  if (!/^[\w-]+\.mp4$/.test(filename)) throw Error('Invalid output filename.');

  const output = path.join(root, 'media', filename);
  if (fs.existsSync(output)) fs.unlinkSync(output);
  const poster = output.replace('.mp4', '.jpg');
  if (fs.existsSync(poster)) fs.unlinkSync(poster);
  const qa = path.join(root, 'data/concept-qa', path.basename(filename, '.mp4'));
  fs.mkdirSync(qa, { recursive: true });
  const audio = path.join(qa, 'interface-sounds.wav');
  effects(audio);

  const { chromium } = await playwright();
  const browser = await chromium.launch({ headless: true, executablePath: browserPath() });

  // Dynamic HTTP Server serving all assets including templates/frames
  const server = http.createServer((req, res) => {
    const rawKey = decodeURIComponent(new URL(req.url, 'http://local').pathname.slice(1)) || 'preview.html';
    const filePath = path.join(dir, rawKey);
    if (!filePath.toLowerCase().startsWith(dir.toLowerCase()) || !fs.existsSync(filePath) || fs.statSync(filePath).isDirectory()) {
      res.writeHead(404);
      return res.end();
    }
    const ext = path.extname(filePath).toLowerCase();
    const mimeTypes = {
      '.html': 'text/html; charset=utf-8',
      '.css': 'text/css',
      '.js': 'text/javascript',
      '.mjs': 'text/javascript',
      '.json': 'application/json',
      '.png': 'image/png',
      '.jpg': 'image/jpeg',
      '.jpeg': 'image/jpeg',
      '.mp4': 'video/mp4'
    };
    res.setHeader('Content-Type', mimeTypes[ext] || 'application/octet-stream');
    fs.createReadStream(filePath).pipe(res);
  });
  await new Promise(resolve => server.listen(0, '127.0.0.1', resolve));

  let encoder;
  try {
    const page = await browser.newPage({
      viewport: { width: format === 'portrait' ? 1080 : 1920, height: format === 'portrait' ? 1920 : 1080 },
      deviceScaleFactor: 1
    });
    const errors = [];
    page.on('pageerror', error => errors.push(error.message));
    await page.goto(`http://127.0.0.1:${server.address().port}/preview.html`);
    await page.waitForFunction(() => window.sceneReady);

    // Apply active city to headless canvas renderer
    await page.evaluate(c => {
      if (window.setActiveCity) window.setActiveCity(c);
      const sel = document.querySelector('#citySelect');
      if (sel) sel.value = c.id;
    }, cityObj);

    const args = [
      '-hide_banner', '-nostdin', '-y', '-f', 'image2pipe', '-vcodec', 'mjpeg', '-framerate', '30', '-i', 'pipe:0',
      '-i', path.resolve(root, '../Store/Promo/geosniper-original-score.wav'), '-i', audio,
      '-filter_complex', '[1:a]volume=0.55[bed];[bed][2:a]amix=inputs=2:duration=shortest:normalize=0,alimiter=limit=0.9,afade=t=in:st=0:d=0.3,afade=t=out:st=16.5:d=1.5[a]',
      '-map', '0:v:0', '-map', '[a]', '-t', '18', '-c:v', 'libx264', '-preset', 'fast', '-crf', '18', '-maxrate', '10M', '-bufsize', '20M',
      '-pix_fmt', 'yuv420p', '-g', '60', '-flags', '+cgop',
      '-colorspace', 'bt709', '-color_primaries', 'bt709', '-color_trc', 'bt709', '-c:a', 'aac', '-b:a', '128k', '-ar', '48000', '-movflags', '+faststart', output
    ];
    encoder = spawn(ffmpegPath(root), args, { windowsHide: true });
    let errorLog = '';
    encoder.stderr.on('data', d => errorLog = (errorLog + d).slice(-10000));
    encoder.stdin.on('error', () => {});

    const completion = new Promise((resolve, reject) => {
      encoder.once('error', reject);
      encoder.once('close', code => code === 0 ? resolve() : reject(Error(`Encoder failed: ${errorLog.slice(-2000)}`)));
    });
    completion.catch(() => {});

    const keyframes = new Set([30, 85, 160, 270, 345, 410, 480]);
    for (let frame = 0; frame < 540; frame++) {
      if (encoder.exitCode !== null) throw Error(`Encoder stopped: ${errorLog.slice(-1500)}`);
      const encoded = await page.evaluate(({ time, format }) => {
        window.renderAt(time, format);
        return document.querySelector('canvas').toDataURL('image/jpeg', 0.95).split(',')[1];
      }, { time: frame / 30, format });

      const data = Buffer.from(encoded, 'base64');
      if (keyframes.has(frame)) fs.writeFileSync(path.join(qa, `frame-${String(frame).padStart(3, '0')}.jpg`), data);
      if (!encoder.stdin.write(data)) await once(encoder.stdin, 'drain');
      if (frame % 90 === 0) progress(`${format} (${cityObj.name}): ${Math.round(frame / 540 * 100)}%`);
    }
    encoder.stdin.end();
    await completion;
    if (errors.length) throw Error(errors.join('\n'));

    await ffmpegRun(root, ['-v', 'error', '-i', output, '-f', 'null', '-']);
    await ffmpegRun(root, ['-y', '-ss', '11.7', '-i', output, '-frames:v', '1', '-vf', format === 'portrait' ? 'scale=360:640' : 'scale=960:540', '-q:v', '2', output.replace('.mp4', '.jpg')]);

    const manifest = {
      name: filename,
      city: cityObj.name,
      country: cityObj.country,
      coords: cityObj.coords,
      mission: cityObj.mission,
      format,
      width: format === 'portrait' ? 1080 : 1920,
      height: format === 'portrait' ? 1920 : 1080,
      duration: 18,
      fps: 30,
      googleFlow3DDive: true,
      gameplay: true,
      fictionalCombat: true,
      music: 'Store/Promo/geosniper-original-score.wav',
      effects: 'Synthesized gunshot blast, hitmarker & 3D dive audio',
      disclosure: 'INTERACTIVE DEMO · SIMULATED GAMEPLAY · GPS CONCEPT',
      createdAt: new Date().toISOString(),
      fullDecode: 'passed'
    };
    fs.writeFileSync(path.join(qa, 'manifest.json'), JSON.stringify(manifest, null, 2));
    progress(`${format} (${cityObj.name}): complete (${filename})`);
    return { filename, qa, manifest, city: cityObj };
  } finally {
    if (encoder?.exitCode === null) encoder.kill();
    await browser.close();
    await new Promise(resolve => server.close(resolve));
  }
}

if (process.argv[1] && import.meta.url === pathToFileURL(path.resolve(process.argv[1])).href) {
  const format = process.argv[2] || 'portrait';
  const name = process.argv[3];
  const city = process.argv[4];
  await renderConcept({ format, name, city, progress: console.log });
}
