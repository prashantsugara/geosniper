// Deterministic motion graphics and interactive gameplay canvas engine.
import { CITIES, getCityForDate, findCity } from './cities.mjs?v=2.2';

export const DURATION = 18;
const C = { bg: '#071314', panel: '#102224', line: '#29484a', white: '#f3f3e9', muted: '#9bb4b1', gold: '#efb557', teal: '#74e0cd', red: '#ff4d4d' };
const clamp = (v, a = 0, b = 1) => Math.max(a, Math.min(b, v));
const ease = v => 1 - Math.pow(1 - clamp(v), 3);
const smooth = v => { v = clamp(v); return v * v * (3 - 2 * v); };
const mix = (a, b, t) => a + (b - a) * t;

let photo;
let activeCity = getCityForDate();
const flowFrames = { portrait: [], landscape: [] };

export function setActiveCity(city) {
  activeCity = typeof city === 'string' ? findCity(city) : (city || getCityForDate());
}
export function getActiveCity() {
  return activeCity;
}

export async function ready() {
  photo = new Image();
  photo.src = new URL('./player.png', import.meta.url).href;
  await photo.decode().catch(() => {});

  // Pre-load all 301 Google Flow 3D flight dive & sniper kill shot frames (10.05s @ 30fps)
  const loads = [];
  for (let i = 1; i <= 301; i++) {
    const pImg = new Image();
    pImg.src = new URL(`templates/frames_portrait/f_${String(i).padStart(3, '0')}.jpg`, import.meta.url).href;
    flowFrames.portrait[i] = pImg;
    loads.push(new Promise(res => {
      if (pImg.complete && pImg.naturalWidth > 0) return res();
      pImg.onload = () => res();
      pImg.onerror = () => res();
    }));

    const lImg = new Image();
    lImg.src = new URL(`templates/frames_landscape/f_${String(i).padStart(3, '0')}.jpg`, import.meta.url).href;
    flowFrames.landscape[i] = lImg;
    loads.push(new Promise(res => {
      if (lImg.complete && lImg.naturalWidth > 0) return res();
      lImg.onload = () => res();
      lImg.onerror = () => res();
    }));
  }
  await Promise.all(loads);
}

function rr(g, x, y, w, h, r = 20, fill = C.panel, stroke = null) {
  g.beginPath();
  g.roundRect(x, y, w, h, r);
  if (fill) { g.fillStyle = fill; g.fill(); }
  if (stroke) { g.strokeStyle = stroke; g.lineWidth = 2; g.stroke(); }
}
function txt(g, value, x, y, size = 36, color = C.white, weight = 500, align = 'left') {
  g.font = `${weight} ${size}px "Segoe UI", Arial, sans-serif`;
  g.fillStyle = color;
  g.textAlign = align;
  g.textBaseline = 'alphabetic';
  g.fillText(value, x, y);
}
function line(g, x1, y1, x2, y2, color = C.line, width = 2) {
  g.beginPath();
  g.moveTo(x1, y1);
  g.lineTo(x2, y2);
  g.strokeStyle = color;
  g.lineWidth = width;
  g.stroke();
}
function circle(g, x, y, r, color, stroke = null, width = 2) {
  g.beginPath();
  g.arc(x, y, r, 0, Math.PI * 2);
  if (color) { g.fillStyle = color; g.fill(); }
  if (stroke) { g.strokeStyle = stroke; g.lineWidth = width; g.stroke(); }
}
function poly(g, points, fill, stroke = null) {
  g.beginPath();
  points.forEach(([x, y], i) => i ? g.lineTo(x, y) : g.moveTo(x, y));
  g.closePath();
  g.fillStyle = fill;
  g.fill();
  if (stroke) { g.strokeStyle = stroke; g.lineWidth = 1; g.stroke(); }
}
function arrow(g, x, y, color = C.gold) {
  line(g, x - 20, y, x + 20, y, color, 4);
  line(g, x + 7, y - 13, x + 20, y, color, 4);
  line(g, x + 7, y + 13, x + 20, y, color, 4);
}
function target(g, x, y, r = 22, color = C.gold) {
  circle(g, x, y, r, null, color, 3);
  circle(g, x, y, 5, color);
  for (const [dx, dy] of [[1, 0], [-1, 0], [0, 1], [0, -1]]) {
    line(g, x + dx * (r - 6), y + dy * (r - 6), x + dx * (r + 9), y + dy * (r + 9), color, 3);
  }
}
function check(g, x, y, color = C.teal) {
  g.beginPath();
  g.moveTo(x - 16, y);
  g.lineTo(x - 3, y + 12);
  g.lineTo(x + 23, y - 17);
  g.strokeStyle = color;
  g.lineWidth = 6;
  g.lineCap = 'round';
  g.stroke();
}
function badge(g, label, x, y, w, color = C.teal) {
  rr(g, x, y, w, 47, 10, '#102c2d', color + '66');
  txt(g, label, x + 17, y + 32, 23, color, 650);
}
function pillButton(g, label, x, y, w, active = false, customFill = null) {
  rr(g, x, y, w, 108, 20, customFill || (active ? C.teal : C.gold));
  txt(g, label, x + 30, y + 69, 33, C.bg, 700);
  arrow(g, x + w - 55, y + 55, C.bg);
}
function tap(g, x, y, t, start) {
  const d = t - start;
  if (d < -0.55 || d > 1) return;
  const approach = ease((d + 0.55) / 0.55);
  g.save();
  g.globalAlpha *= d > 0.65 ? 1 - (d - 0.65) / 0.35 : 1;
  const px = x + mix(90, 0, approach), py = y + mix(105, 0, approach);
  g.shadowBlur = 14;
  g.shadowColor = '#ffffff77';
  circle(g, px, py, 19, C.white);
  circle(g, px, py, 29, null, '#ffffff99', 2);
  g.shadowBlur = 0;
  if (d >= 0) {
    g.globalAlpha *= 1 - clamp(d);
    circle(g, x, y, 28 + d * 85, null, C.teal, 4);
    circle(g, x, y, 20 + d * 45, null, C.gold, 2);
  }
  g.restore();
}
function background(g, t) {
  g.fillStyle = C.bg;
  g.fillRect(0, 0, 1080, 1920);
  const grad = g.createRadialGradient(810, 750, 40, 510, 700, 1100);
  grad.addColorStop(0, '#153b36');
  grad.addColorStop(1, C.bg);
  g.fillStyle = grad;
  g.fillRect(0, 0, 1080, 1920);
  g.strokeStyle = '#67a99d0c';
  g.lineWidth = 1;
  for (let i = -8; i < 16; i++) {
    line(g, i * 120 + (t * 9) % 120, 0, i * 120 + 300 + (t * 9) % 120, 1920, '#78b4aa09');
    line(g, 0, i * 140 + (t * 5) % 140, 1080, i * 140 + (t * 5) % 140, '#78b4aa09');
  }
}
function brand(g) {
  target(g, 90, 102, 22, C.gold);
  txt(g, 'GEO SNIPER', 137, 115, 30, C.white, 650);
  txt(g, `GPS OVERWATCH // ${activeCity.name.toUpperCase()}`, 77, 171, 20, C.muted, 600);
}
function playerChip(g, x = 754, y = 74) {
  rr(g, x, y, 252, 78, 39, '#0a191cec', C.line);
  circle(g, x + 42, y + 39, 25, C.teal);
  txt(g, '01', x + 42, y + 48, 22, C.bg, 700, 'center');
  txt(g, 'PLAYER 01', x + 81, y + 45, 22, C.white, 650);
}
function title(g, small, a, b, t) {
  const p = ease(t / 0.55);
  g.save();
  g.globalAlpha *= p;
  txt(g, small, 76, 247 + 24 * (1 - p), 25, C.gold, 650);
  txt(g, a, 73, 344 + 24 * (1 - p), 79, C.white, 700);
  if (b) txt(g, b, 73, 434 + 24 * (1 - p), 79, C.white, 700);
  g.restore();
}
function disclosure(g) {
  rr(g, 55, 1780, 970, 62, 13, '#071314e8', '#42615f');
  txt(g, 'INTERACTIVE DEMO · SIMULATED GAMEPLAY · GPS CONCEPT', 540, 1820, 24, C.white, 600, 'center');
}

// Procedural schematic blocks for 3D map
const blocks = [];
let seed = 1729;
function rnd() { seed = (Math.imul(seed, 1664525) + 1013904223) >>> 0; return seed / 4294967296; }
for (let x = -630; x <= 630; x += 140) {
  for (let z = -630; z <= 630; z += 140) {
    if (Math.abs(x + z * 0.34 - 220) < 100) continue;
    for (let b = 0; b < 3; b++) blocks.push({ x: x + 12 + (b % 2) * 54, z: z + 14 + Math.floor(b / 2) * 56, w: 36 + rnd() * 14, d: 32 + rnd() * 17, h: 15 + rnd() * 50, lit: rnd() > 0.8 });
  }
}
blocks.sort((a, b) => (a.x + a.z) - (b.x + b.z));

// Tactical 3D Map displaying chosen location and nearby tactical sectors
function map(g, { x = 54, y = 500, w = 972, h = 870, t = 0, zoom = 1, scan = 0, showNearby = true } = {}) {
  g.save();
  rr(g, x, y, w, h, 28, '#0b2022', C.line);
  g.beginPath();
  g.roundRect(x + 2, y + 2, w - 4, h - 4, 27);
  g.clip();

  const theta = 0.12 + Math.sin(t * 0.08) * 0.05, scale = w / 1120 * zoom;
  const project = (a, b, height = 0) => {
    const u = a * Math.cos(theta) - b * Math.sin(theta), v = a * Math.sin(theta) + b * Math.cos(theta);
    return [x + w * 0.49 + (u - v) * 0.65 * scale, y + h * 0.53 + (u + v) * 0.33 * scale - height * scale];
  };

  const base = g.createLinearGradient(x, y, x + w, y + h);
  base.addColorStop(0, '#102b2d');
  base.addColorStop(1, '#071718');
  g.fillStyle = base;
  g.fillRect(x, y, w, h);

  // Isometric Grid Lines
  for (let q = -900; q <= 900; q += 140) {
    let a = project(q, -1000), b = project(q, 1000);
    line(g, ...a, ...b, '#274448', 22 * scale);
    line(g, ...a, ...b, '#527775', 1 * scale);
    a = project(-1000, q);
    b = project(1000, q);
    line(g, ...a, ...b, '#274448', 22 * scale);
    line(g, ...a, ...b, '#527775', 1 * scale);
  }

  // 3D Isometric Buildings
  for (const b of blocks) {
    const a = project(b.x, b.z), c = project(b.x + b.w, b.z), d = project(b.x + b.w, b.z + b.d), e = project(b.x, b.z + b.d);
    const roof = [project(b.x, b.z, b.h), project(b.x + b.w, b.z, b.h), project(b.x + b.w, b.z + b.d, b.h), project(b.x, b.z + b.d, b.h)];
    poly(g, [a, c, roof[1], roof[0]], '#1c3536');
    poly(g, [c, d, roof[2], roof[1]], '#244243');
    poly(g, [d, e, roof[3], roof[2]], '#163234');
    poly(g, roof, b.lit ? '#708076' : '#416261', '#65877b55');
  }

  // Primary Selected GPS Location
  const gps = project(-70, 0);

  // Radar wave pulses
  if (scan > 0) {
    for (let i = 0; i < 3; i++) {
      const p = ((t * 0.4 + i / 3) % 1);
      g.save();
      g.globalAlpha *= scan * (1 - p) * 0.8;
      circle(g, ...gps, 30 + p * w * 0.45, null, C.teal, 2);
      g.restore();
    }
  }

  // Draw Nearby Locations / Tactical Sectors on Map
  if (showNearby && activeCity.nearby && activeCity.nearby.length > 0) {
    activeCity.nearby.forEach((poi, idx) => {
      const dx = poi.dx ?? (-240 + idx * 240);
      const dz = poi.dz ?? (-200 + idx * 220);
      const [px, py] = project(dx, dz, 30);

      // Dashed line connecting primary target to nearby location
      g.save();
      g.setLineDash([8, 6]);
      g.lineDashOffset = -t * 20;
      line(g, gps[0], gps[1], px, py, '#74e0cd55', 2);
      g.restore();

      // Nearby location pin
      g.save();
      g.shadowBlur = 12;
      g.shadowColor = '#74e0cd44';
      poly(g, [[px - 14, py], [px, py + 32], [px + 14, py]], '#305954');
      circle(g, px, py, 20, '#153533', C.teal, 2);
      txt(g, `0${idx + 1}`, px, py + 6, 17, C.white, 750, 'center');

      // Callout box with Nearby Name & Distance
      const boxW = Math.max(160, poi.name.length * 9.5 + 40);
      const boxX = px - boxW / 2;
      const boxY = py + 36;
      rr(g, boxX, boxY, boxW, 46, 10, '#091c1ff0', '#3b6761');
      txt(g, poi.name, px, boxY + 22, 16, C.white, 650, 'center');
      txt(g, `${poi.dist} · [${poi.dir}]`, px, boxY + 38, 14, C.gold, 600, 'center');
      g.restore();
    });
  }

  // Primary GPS Target Marker
  g.save();
  g.shadowBlur = 24;
  g.shadowColor = '#efb557aa';
  circle(g, ...gps, 22, C.gold);
  circle(g, ...gps, 36, null, '#ffe3a8', 3);
  circle(g, ...gps, 7, C.bg);
  target(g, gps[0], gps[1], 44, C.gold);

  // Primary Target Callout Badge
  const pW = 340;
  const pX = gps[0] - pW / 2;
  const pY = gps[1] - 78;
  rr(g, pX, pY, pW, 52, 12, '#0c2224fa', C.gold);
  target(g, pX + 28, pY + 26, 15, C.gold);
  txt(g, `PRIMARY: ${activeCity.name.toUpperCase()}`, pX + 54, pY + 27, 20, C.gold, 700);
  txt(g, `${activeCity.district}`, pX + 54, pY + 44, 15, C.white, 600);
  g.restore();

  g.restore();
  txt(g, 'N ↑', x + w - 63, y + 56, 24, C.muted, 600);
  txt(g, `GPS: ${activeCity.coords}`, x + 25, y + h - 27, 21, C.muted, 600);
  return { gps };
}

// Scene 1: 0.0s – 3.5s Tactical GPS Satellite Lock with Nearby Locations Map
function tacticalLock(g, s, t) {
  brand(g);
  playerChip(g);
  title(g, '01 / GPS SATELLITE LOCK', 'LOCATING', `${activeCity.name.toUpperCase()}.`, s);

  // Tactical Map with chosen location & nearby POIs
  map(g, { y: 640, h: 825, t, zoom: 1.05, scan: clamp(s / 1.0), showNearby: true });

  // Location Query Input Display
  rr(g, 77, 486, 926, 123, 22, '#f2f2e6');
  target(g, 124, 548, 20, '#345c57');
  const query = `${activeCity.name}, ${activeCity.country}`;
  txt(g, s < 0.4 ? 'Locating target...' : query.slice(0, Math.floor((s - 0.4) * 24)), 174, 562, 38, C.bg, 650);

  // Status Badge
  badge(g, s < 2.0 ? 'GPS ACQUIRING...' : 'GPS CONFIRMED', 79, 680, 290, s < 2.0 ? C.teal : C.gold);

  // Bottom Briefing Card
  rr(g, 77, 1340, 926, 250, 25, '#102324f5', C.line);
  txt(g, s < 2.5 ? 'ACQUIRING SATELLITE SECTOR FEED' : 'SECTOR LOCKED // SATELLITE READY', 112, 1392, 23, s < 2.5 ? C.teal : C.gold, 650);
  txt(g, query, 110, 1452, 46, C.white, 700);
  txt(g, `${activeCity.district} · ${activeCity.coords}`, 111, 1500, 25, C.muted);
  txt(g, `Target: ${activeCity.target} (Range: ${activeCity.rangeM}M · Wind: ${activeCity.wind})`, 111, 1538, 22, C.muted);

  // Action Button
  pillButton(g, s < 2.5 ? 'INITIATING RECON...' : `DROP INTO ${activeCity.name.toUpperCase()}`, 77, 1614, 926, s >= 2.5);
  tap(g, 755, 1668, t, 2.8);
}

// Scene 2: 3.5s – 13.55s The FULL Google Flow 10.05s Video (301 frames) with Live Kill Shot at 11.5s
function fullCombatFlight(g, s, t, interactiveState = null) {
  brand(g);
  playerChip(g);

  // Frame calculation for the full 10.05-second Google Flow clip (301 frames total)
  const clipTime = clamp(s, 0, 10.05);
  const frameIdx = Math.max(1, Math.min(301, Math.floor((clipTime / 10.05) * 300) + 1));

  // Beat phase title
  const isKillShot = (t >= 11.5);
  const isAiming = (t >= 9.0 && t < 11.5);
  if (isKillShot) {
    title(g, '03 / CONFIRMED KILL', 'TARGET NEUTRALIZED.', `${activeCity.name.toUpperCase()}.`, s - 8.0);
  } else if (isAiming) {
    title(g, '02 / SNIPER OVERWATCH', 'TARGET IN SIGHT.', 'TAKE THE SHOT.', s - 5.5);
  } else {
    title(g, '02 / SATELLITE 3D DIVE', 'DESCENDING TO', `${activeCity.name.toUpperCase()}.`, s);
  }

  const sx = 54, sy = 490, sw = 972, sh = 1090;
  g.save();
  rr(g, sx, sy, sw, sh, 28, '#051214', C.line);
  g.beginPath();
  g.roundRect(sx + 2, sy + 2, sw - 4, sh - 4, 27);
  g.clip();

  // Draw Google Flow Video Frame
  const frameImg = flowFrames.portrait[frameIdx];
  if (frameImg && frameImg.complete && frameImg.naturalWidth > 0) {
    g.drawImage(frameImg, sx, sy, sw, sh);
  } else {
    map(g, { x: sx, y: sy, w: sw, h: sh, t, zoom: 1.2, scan: 1, showNearby: false });
  }

  // Tactical Optic Vignette
  const vig = g.createRadialGradient(sx + sw / 2, sy + sh / 2, sw * 0.35, sx + sw / 2, sy + sh / 2, sw * 0.7);
  vig.addColorStop(0, 'rgba(0,0,0,0.02)');
  vig.addColorStop(1, 'rgba(3,16,18,0.78)');
  g.fillStyle = vig;
  g.fillRect(sx, sy, sw, sh);

  // Dynamic Telemetry Readout
  badge(g, `${activeCity.name.toUpperCase()} · ${activeCity.district}`, sx + 25, sy + 30, 420, C.gold);
  const alt = Math.round(mix(2450, 148, ease(Math.min(1, s / 5.5))));
  txt(g, `ALT: ${alt.toLocaleString()} M`, sx + sw - 30, sy + 58, 26, C.white, 700, 'right');
  txt(g, isAiming || isKillShot ? 'OPTIC: 8X THERMAL ADS' : 'SPEED: 460 KM/H', sx + sw - 30, sy + 92, 22, C.teal, 600, 'right');
  txt(g, `GPS: ${activeCity.coords}`, sx + sw - 30, sy + 124, 20, C.muted, 600, 'right');

  // Muzzle Flash Effect (at kill shot t = 11.5s)
  if (t >= 11.5 && t < 11.8) {
    const flashProgress = (t - 11.5) / 0.3;
    const flashAlpha = 1 - flashProgress;
    g.save();
    const flashGrad = g.createRadialGradient(sx + sw / 2, sy + sh / 2, 20, sx + sw / 2, sy + sh / 2, sw * 0.6);
    flashGrad.addColorStop(0, `rgba(255, 245, 210, ${0.92 * flashAlpha})`);
    flashGrad.addColorStop(0.35, `rgba(255, 170, 40, ${0.65 * flashAlpha})`);
    flashGrad.addColorStop(1, 'rgba(0,0,0,0)');
    g.fillStyle = flashGrad;
    g.fillRect(sx, sy, sw, sh);
    g.restore();
  }

  // Red Hitmarker ✕ on optic reticle (at t >= 11.5s to 12.3s)
  if (t >= 11.5 && t < 12.3) {
    const hmProgress = (t - 11.5) / 0.8;
    const hmAlpha = clamp(1 - hmProgress);
    const cx = sx + sw * 0.5, cy = sy + sh * 0.5;
    g.save();
    g.strokeStyle = `rgba(255, 55, 55, ${hmAlpha})`;
    g.lineWidth = 5;
    line(g, cx - 24, cy - 24, cx - 8, cy - 8, g.strokeStyle, 5);
    line(g, cx + 24, cy - 24, cx + 8, cy - 8, g.strokeStyle, 5);
    line(g, cx - 24, cy + 24, cx - 8, cy + 8, g.strokeStyle, 5);
    line(g, cx + 24, cy + 24, cx + 8, cy + 8, g.strokeStyle, 5);
    g.restore();
  }

  // Combat Banner: TARGET ELIMINATED (at t >= 11.5s to 13.55s)
  if (t >= 11.5) {
    const bannerAlpha = clamp((t - 11.5) / 0.15);
    const cx = sx + sw * 0.5, cy = sy + sh * 0.38;
    g.save();
    g.globalAlpha *= bannerAlpha;
    rr(g, cx - 280, cy - 65, 560, 115, 18, '#0b2321f5', C.gold);
    txt(g, '🎯 TARGET ELIMINATED', cx, cy - 20, 32, C.gold, 750, 'center');
    txt(g, `${activeCity.rangeM}M HEADSHOT · +1,250 PTS`, cx, cy + 22, 26, C.white, 700, 'center');
    g.restore();
  }

  g.restore();

  // Bottom Weapon Status Card
  rr(g, 77, 1610, 926, 125, 22, '#0e2224f5', C.line);
  txt(g, isKillShot ? `CONFIRMED KILL: ${activeCity.name.toUpperCase()}` : `MISSION: ${activeCity.mission}`, 108, 1658, 28, isKillShot ? C.gold : C.white, 650);
  txt(g, isKillShot ? `Objective Neutralized · Barrett .50 Caliber · 100% Accuracy` : `Target: ${activeCity.target} · Approaching perch`, 108, 1700, 22, C.muted);
}

// Scene 3: 13.55s – 18.0s Outro & Daily CTA
function outro(g, s, t) {
  map(g, { x: -180, y: 235, w: 1460, h: 1450, t, zoom: 1.2, showNearby: true, scan: 0.5 });
  const shade = g.createLinearGradient(0, 0, 0, 1920);
  shade.addColorStop(0, '#071314ee');
  shade.addColorStop(0.55, '#071314dd');
  shade.addColorStop(1, '#071314fa');
  g.fillStyle = shade;
  g.fillRect(0, 0, 1080, 1920);

  brand(g);
  const p = ease(s / 0.7);
  g.save();
  g.translate(0, 40 * (1 - p));
  g.globalAlpha *= p;

  txt(g, 'MISSION', 71, 580, 85, C.gold, 750);
  txt(g, 'ACCOMPLISHED.', 71, 675, 85, C.white, 750);
  txt(g, 'YOUR CITY.', 71, 800, 110, C.white, 750);
  txt(g, 'YOUR NEXT', 71, 915, 100, C.white, 750);
  txt(g, 'MISSION.', 71, 1030, 110, C.gold, 750);

  line(g, 78, 1090, 1004, 1090, '#4b6e62');
  txt(g, 'GEO SNIPER', 77, 1170, 54, C.white, 650);
  txt(g, 'DAILY WORLD GPS MISSIONS · COMING TO ANDROID', 80, 1222, 26, C.teal, 650);

  pillButton(g, 'FOLLOW FOR DAILY OPS', 77, 1320, 926);
  txt(g, 'Drop your city in the comments! 👇', 79, 1500, 38, C.white, 650);
  txt(g, `Today's Operation: ${activeCity.name}, ${activeCity.country}`, 80, 1575, 27, C.muted);
  txt(g, `GPS Target: ${activeCity.coords} · ${activeCity.district}`, 80, 1618, 24, C.muted);
  g.restore();
}

const scenes = [
  { start: 0, fn: tacticalLock },
  { start: 3.5, fn: fullCombatFlight },
  { start: 13.55, fn: outro }
];

function portrait(g, t, interactiveState = null) {
  background(g, t);
  for (let i = 0; i < scenes.length; i++) {
    const scene = scenes[i], end = scenes[i + 1]?.start ?? DURATION + 1;
    if (t < scene.start - 0.35 || t > end + 0.35) continue;
    const alpha = Math.min(i === 0 ? 1 : smooth((t - scene.start + 0.35) / 0.7), i === scenes.length - 1 ? 1 : 1 - smooth((t - end + 0.35) / 0.7));
    if (alpha <= 0) continue;
    g.save();
    g.globalAlpha = alpha;
    scene.fn(g, Math.max(0, t - scene.start), t, interactiveState);
    g.restore();
  }
  disclosure(g);
  const progress = clamp(t / DURATION);
  rr(g, 76, 1877, 928, 4, 2, '#37524c');
  rr(g, 76, 1877, Math.max(4, 928 * progress), 4, 2, C.gold);
}

let portraitBuffer;
export function render(canvas, time, format = 'portrait', interactiveState = null) {
  const t = clamp(time, 0, DURATION - 0.001), g = canvas.getContext('2d', { alpha: false });
  if (format === 'portrait') {
    if (canvas.width !== 1080) canvas.width = 1080;
    if (canvas.height !== 1920) canvas.height = 1920;
    portrait(g, t, interactiveState);
    return;
  }
  if (canvas.width !== 1920) canvas.width = 1920;
  if (canvas.height !== 1080) canvas.height = 1080;
  if (!portraitBuffer) {
    portraitBuffer = document.createElement('canvas');
    portraitBuffer.width = 1080;
    portraitBuffer.height = 1920;
  }
  portrait(portraitBuffer.getContext('2d', { alpha: false }), t, interactiveState);

  // Landscape Canvas Background
  g.fillStyle = C.bg;
  g.fillRect(0, 0, 1920, 1080);
  const shade = g.createLinearGradient(0, 0, 1920, 0);
  shade.addColorStop(0, '#04111355');
  shade.addColorStop(0.6, '#041113ee');
  g.fillStyle = shade;
  g.fillRect(0, 0, 1920, 1080);

  target(g, 112, 112, 25, C.gold);
  txt(g, 'GEO SNIPER', 158, 125, 34, C.white, 650);
  txt(g, 'YOUR CITY.', 90, 320, 100, C.white, 750);
  txt(g, 'YOUR NEXT', 90, 430, 95, C.white, 750);
  txt(g, 'MISSION.', 90, 540, 100, C.gold, 750);

  const label = t < 3.5 ? `01 / GPS SATELLITE LOCK · ${activeCity.name.toUpperCase()}` :
    t < 9.0 ? `02 / SATELLITE 3D DIVE` :
    t < 11.5 ? `03 / ADS SNIPER ACQUISITION` :
    t < 13.55 ? `04 / KILL SHOT · TARGET ELIMINATED` :
    `05 / DAILY GPS OPS · DROP YOUR CITY`;

  txt(g, label, 94, 660, 26, C.teal, 650);
  txt(g, `Target: ${activeCity.name}, ${activeCity.country} [${activeCity.coords}]`, 94, 720, 24, C.muted);
  txt(g, `Sector: ${activeCity.district} · Range: ${activeCity.rangeM}M`, 94, 760, 23, C.muted);
  if (activeCity.nearby && activeCity.nearby.length) {
    txt(g, `Nearby Sectors: ${activeCity.nearby.map(n => n.name).join(' · ')}`, 94, 800, 21, C.muted);
  }
  txt(g, 'Drop your city in the comments for tomorrow\'s mission! 👇', 94, 940, 24, C.gold);

  // Right-hand Phone Frame with live motion
  rr(g, 1146, 33, 568, 1014, 35, '#253c38', '#688b78');
  g.save();
  g.beginPath();
  g.roundRect(1155, 42, 550, 996, 28);
  g.clip();
  g.drawImage(portraitBuffer, 1155, 42, 550, 996);
  g.restore();
}
