import { ready, render, DURATION, setActiveCity, getActiveCity } from './scene.mjs?v=2.2';
import { CITIES, resolveLocation, fetchRealLocation } from './cities.mjs?v=2.2';

const canvas = document.querySelector('canvas');
const slider = document.querySelector('#time');
const clock = document.querySelector('#clock');
const locationInput = document.querySelector('#locationInput');
const btnSetLocation = document.querySelector('#btnSetLocation');
const citySelect = document.querySelector('#citySelect');
const nearbyList = document.querySelector('#nearbyList');
const demoToggle = document.querySelector('#demoToggle');
const combatHud = document.querySelector('#combatHud');
const canvasHint = document.querySelector('#canvasHint');
const scoreDisplay = document.querySelector('#scoreDisplay');
const btnBreath = document.querySelector('#btnBreath');
const btnFire = document.querySelector('#btnFire');

let time = 0, playing = false, format = 'portrait', previous = performance.now();
let audioCtx = null;
let interactiveMode = false;
let score = 0;

const interactiveState = {
  aimX: 0,
  aimY: 0,
  holdBreath: false,
  fired: false,
  fireTime: 0,
  eliminated: false
};

function getAudio() {
  if (!audioCtx && (window.AudioContext || window.webkitAudioContext)) {
    const Ctx = window.AudioContext || window.webkitAudioContext;
    audioCtx = new Ctx();
  }
  if (audioCtx && audioCtx.state === 'suspended') {
    audioCtx.resume().catch(() => {});
  }
  return audioCtx;
}

// Procedural Web Audio Sound Engine
function playTap() {
  const ctx = getAudio();
  if (!ctx) return;
  const now = ctx.currentTime;
  const osc = ctx.createOscillator(), gain = ctx.createGain();
  osc.type = 'sine';
  osc.frequency.setValueAtTime(780, now);
  osc.frequency.exponentialRampToValueAtTime(140, now + 0.08);
  gain.gain.setValueAtTime(0.2, now);
  gain.gain.exponentialRampToValueAtTime(0.001, now + 0.08);
  osc.connect(gain); gain.connect(ctx.destination);
  osc.start(now); osc.stop(now + 0.09);
}

function playPing(freq = 1700) {
  const ctx = getAudio();
  if (!ctx) return;
  const now = ctx.currentTime, osc = ctx.createOscillator(), gain = ctx.createGain();
  osc.type = 'sine';
  osc.frequency.setValueAtTime(freq, now);
  gain.gain.setValueAtTime(0.09, now);
  gain.gain.exponentialRampToValueAtTime(0.001, now + 0.18);
  osc.connect(gain); gain.connect(ctx.destination);
  osc.start(now); osc.stop(now + 0.19);
}

function playGunshot() {
  const ctx = getAudio();
  if (!ctx) return;
  const now = ctx.currentTime;

  // 1. Sharp high-frequency transient noise crack
  const bufferSize = ctx.sampleRate * 0.4;
  const buffer = ctx.createBuffer(1, bufferSize, ctx.sampleRate);
  const data = buffer.getChannelData(0);
  for (let i = 0; i < bufferSize; i++) data[i] = (Math.random() * 2 - 1) * Math.exp(-i / (ctx.sampleRate * 0.035));
  const noise = ctx.createBufferSource();
  noise.buffer = buffer;
  const noiseGain = ctx.createGain();
  noiseGain.gain.setValueAtTime(0.65, now);
  noiseGain.gain.exponentialRampToValueAtTime(0.001, now + 0.35);
  noise.connect(noiseGain); noiseGain.connect(ctx.destination);
  noise.start(now);

  // 2. Heavy sub-bass body kick (Barrett .50 Caliber punch)
  const kick = ctx.createOscillator(), kickGain = ctx.createGain();
  kick.type = 'sine';
  kick.frequency.setValueAtTime(95, now);
  kick.frequency.exponentialRampToValueAtTime(35, now + 0.45);
  kickGain.gain.setValueAtTime(0.75, now);
  kickGain.gain.exponentialRampToValueAtTime(0.001, now + 0.5);
  kick.connect(kickGain); kickGain.connect(ctx.destination);
  kick.start(now); kick.stop(now + 0.55);

  // 3. Bolt-action mechanical cycle sound after 1.1s
  setTimeout(() => {
    if (!ctx) return;
    const t0 = ctx.currentTime;
    const bolt = ctx.createOscillator(), boltGain = ctx.createGain();
    bolt.type = 'triangle';
    bolt.frequency.setValueAtTime(1400, t0);
    bolt.frequency.setValueAtTime(600, t0 + 0.06);
    boltGain.gain.setValueAtTime(0.18, t0);
    boltGain.gain.exponentialRampToValueAtTime(0.001, t0 + 0.14);
    bolt.connect(boltGain); boltGain.connect(ctx.destination);
    bolt.start(t0); bolt.stop(t0 + 0.15);
  }, 1100);
}

function playHitmarker() {
  const ctx = getAudio();
  if (!ctx) return;
  const now = ctx.currentTime;
  const osc = ctx.createOscillator(), gain = ctx.createGain();
  osc.type = 'sine';
  osc.frequency.setValueAtTime(2200, now);
  gain.gain.setValueAtTime(0.38, now);
  gain.gain.exponentialRampToValueAtTime(0.001, now + 0.07);
  osc.connect(gain); gain.connect(ctx.destination);
  osc.start(now); osc.stop(now + 0.08);
}

function playHeartbeat() {
  const ctx = getAudio();
  if (!ctx) return;
  const now = ctx.currentTime;
  [0, 0.14].forEach(delay => {
    const osc = ctx.createOscillator(), gain = ctx.createGain();
    osc.type = 'sine';
    osc.frequency.setValueAtTime(60, now + delay);
    osc.frequency.exponentialRampToValueAtTime(30, now + delay + 0.12);
    gain.gain.setValueAtTime(0.25, now + delay);
    gain.gain.exponentialRampToValueAtTime(0.001, now + delay + 0.12);
    osc.connect(gain); gain.connect(ctx.destination);
    osc.start(now + delay); osc.stop(now + delay + 0.14);
  });
}

// Audio cues for the 18.0-second sequence (with kill shot at 11.5s)
const cues = [
  { t: 1.5, fn: () => playPing(1500) },
  { t: 2.8, fn: playTap },
  { t: 3.5, fn: () => playPing(1900) },
  { t: 9.0, fn: playHeartbeat },
  { t: 10.0, fn: playHeartbeat },
  { t: 11.5, fn: playGunshot },
  { t: 11.7, fn: playHitmarker },
  { t: 13.55, fn: () => playPing(1200) }
];

function checkSoundCues(prevT, currT) {
  for (const cue of cues) {
    if (prevT < cue.t && currT >= cue.t) {
      cue.fn();
    }
  }
}

// Populate Quick Global Preset Dropdown
function initDropdown() {
  citySelect.innerHTML = '';
  for (const c of CITIES) {
    const opt = document.createElement('option');
    opt.value = c.id;
    opt.textContent = `${c.name}, ${c.country} · ${c.district}`;
    citySelect.appendChild(opt);
  }
}

// Update UI display for detected nearby sectors
function updateNearbyDisplay(city) {
  if (!nearbyList) return;
  nearbyList.innerHTML = '';
  if (city.nearby && city.nearby.length) {
    city.nearby.forEach((poi, idx) => {
      const item = document.createElement('div');
      item.className = 'nearby-item';
      item.innerHTML = `<span>0${idx + 1} · ${poi.name}</span><span class="dist">${poi.dist} [${poi.dir}]</span>`;
      nearbyList.appendChild(item);
    });
  }
}

// Apply manual location from input (supports "Tokyo", "/location Rome", ";/location Paris", "Haldwani", "52.52, 13.40")
async function applyManualLocation() {
  const raw = locationInput.value.trim();
  if (!raw) return;
  getAudio();
  playTap();

  btnSetLocation.textContent = '⏳ Locating...';
  btnSetLocation.disabled = true;

  try {
    const city = await fetchRealLocation(raw);
    setActiveCity(city);
    updateNearbyDisplay(city);

    // Sync or add custom option to dropdown
    let matchOpt = Array.from(citySelect.options).find(o => o.value === city.id);
    if (!matchOpt) {
      matchOpt = document.createElement('option');
      matchOpt.value = city.id;
      matchOpt.textContent = `📍 ${city.name}, ${city.country} (Custom Location)`;
      citySelect.insertBefore(matchOpt, citySelect.firstChild);
    }
    citySelect.value = city.id;
    locationInput.value = city.name;
    draw();
  } catch (err) {
    console.error('Failed to resolve location:', err);
  } finally {
    btnSetLocation.textContent = '📍 Locate';
    btnSetLocation.disabled = false;
  }
}

btnSetLocation.onclick = applyManualLocation;
locationInput.onkeydown = e => {
  if (e.key === 'Enter') {
    e.preventDefault();
    applyManualLocation();
  }
};

citySelect.onchange = async e => {
  getAudio();
  playTap();
  const city = await fetchRealLocation(e.target.value);
  locationInput.value = city.name;
  setActiveCity(city);
  updateNearbyDisplay(city);
  draw();
};

await ready();
initDropdown();
const initialCity = getActiveCity();
locationInput.value = initialCity.name;
citySelect.value = initialCity.id;
updateNearbyDisplay(initialCity);

window.renderAt = (t, f = 'portrait') => {
  time = t; format = f;
  render(canvas, t, f, interactiveMode ? interactiveState : null);
  return true;
};
window.setActiveCity = async c => {
  const city = typeof c === 'string' ? await fetchRealLocation(c) : c;
  setActiveCity(city);
  locationInput.value = city.name;
  updateNearbyDisplay(city);
  let matchOpt = Array.from(citySelect.options).find(o => o.value === city.id);
  if (!matchOpt) {
    matchOpt = document.createElement('option');
    matchOpt.value = city.id;
    matchOpt.textContent = `📍 ${city.name}, ${city.country} (Custom Location)`;
    citySelect.insertBefore(matchOpt, citySelect.firstChild);
  }
  citySelect.value = city.id;
  draw();
};
window.getActiveCity = getActiveCity;
window.sceneReady = true;

function draw() {
  render(canvas, time, format, interactiveMode ? interactiveState : null);
  slider.value = time;
  clock.textContent = `${time.toFixed(1)} / ${DURATION.toFixed(1)} sec`;
}

function stop() {
  playing = false;
  document.querySelector('#play').textContent = 'Play film';
}

function triggerShot() {
  getAudio();
  playGunshot();
  interactiveState.fired = true;
  interactiveState.fireTime = time;

  // Hit detection: within 95px of center crosshair
  const hitDist = Math.hypot(interactiveState.aimX, interactiveState.aimY);
  setTimeout(() => {
    if (hitDist < 120) {
      playHitmarker();
      interactiveState.eliminated = true;
      score += 1250;
      scoreDisplay.textContent = `SCORE: +${score}`;
      setTimeout(() => {
        // Respawn hostile target
        interactiveState.eliminated = false;
        interactiveState.fired = false;
      }, 3000);
    }
  }, 160);
}

// Interactive Demo Mode Toggle
demoToggle.onclick = () => {
  getAudio();
  interactiveMode = !interactiveMode;
  demoToggle.classList.toggle('active', interactiveMode);
  combatHud.classList.toggle('visible', interactiveMode);
  canvasHint.classList.toggle('visible', interactiveMode);
  canvas.classList.toggle('interactive', interactiveMode);

  if (interactiveMode) {
    stop();
    time = 9.0; // Jump to live sniper scope acquisition
    demoToggle.textContent = '🎬 Exit Demo';
  } else {
    demoToggle.textContent = '🎮 Play Demo';
  }
  draw();
};

// Pointer Aiming on Canvas
let isDragging = false;
canvas.onpointerdown = e => {
  getAudio();
  if (interactiveMode || (time >= 9.0 && time < 13.55)) {
    isDragging = true;
    updateAim(e);
  }
};
window.onpointermove = e => {
  if (isDragging) updateAim(e);
};
window.onpointerup = () => {
  isDragging = false;
};

function updateAim(e) {
  const rect = canvas.getBoundingClientRect();
  const relX = (e.clientX - rect.left) / rect.width - 0.5;
  const relY = (e.clientY - rect.top) / rect.height - 0.5;
  interactiveState.aimX = relX * 280;
  interactiveState.aimY = relY * 260;
  draw();
}

// Firing & Breath Controls
btnFire.onclick = () => {
  triggerShot();
  draw();
};
btnBreath.onmousedown = () => {
  getAudio();
  playHeartbeat();
  interactiveState.holdBreath = true;
  draw();
};
window.onmouseup = () => {
  if (interactiveState.holdBreath) {
    interactiveState.holdBreath = false;
    draw();
  }
};

window.onkeydown = e => {
  if (e.code === 'Space') {
    e.preventDefault();
    interactiveState.holdBreath = true;
    playHeartbeat();
    draw();
  }
};
window.onkeyup = e => {
  if (e.code === 'Space') {
    interactiveState.holdBreath = false;
    draw();
  }
};

document.querySelector('#play').onclick = () => {
  getAudio();
  if (interactiveMode) {
    demoToggle.click(); // Return to film mode
  }
  if (playing) {
    stop();
    return;
  }
  if (time >= DURATION - 0.02) time = 0;
  playing = true;
  previous = performance.now();
  document.querySelector('#play').textContent = 'Pause';
};

document.querySelector('#replay').onclick = () => {
  getAudio();
  time = 0;
  playing = true;
  previous = performance.now();
  document.querySelector('#play').textContent = 'Pause';
  draw();
};

document.querySelector('#aspect').onclick = () => {
  playTap();
  format = format === 'portrait' ? 'landscape' : 'portrait';
  document.querySelector('#aspect').textContent = format === 'portrait' ? 'Landscape' : 'Portrait';
  draw();
};

slider.oninput = () => {
  stop();
  time = Number(slider.value);
  draw();
};

for (const button of document.querySelectorAll('[data-time]')) {
  button.onclick = () => {
    getAudio();
    stop();
    time = Number(button.dataset.time);
    playTap();
    if (time >= 9.0 && time < 13.55) {
      combatHud.classList.add('visible');
    } else if (!interactiveMode) {
      combatHud.classList.remove('visible');
    }
    draw();
  };
}

function frame(now) {
  if (playing) {
    const nextTime = Math.min(DURATION - 0.001, time + (now - previous) / 1000);
    checkSoundCues(time, nextTime);
    time = nextTime;
    draw();
    if (time >= DURATION - 0.002) stop();
  }
  previous = now;
  requestAnimationFrame(frame);
}

draw();
requestAnimationFrame(frame);
