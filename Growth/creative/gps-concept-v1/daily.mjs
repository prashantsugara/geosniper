#!/usr/bin/env node
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { renderConcept } from './render.mjs';
import { getCityForDate, findCity, CITIES, fetchRealLocation } from './cities.mjs';

const dir = path.dirname(fileURLToPath(import.meta.url));
const root = path.resolve(dir, '../..');
const mediaDir = path.join(root, 'media');

// Parse CLI flags
const args = process.argv.slice(2);
let cityArg = null;
let formatArg = 'both';

for (let i = 0; i < args.length; i++) {
  if (args[i] === '--city' && args[i + 1]) cityArg = args[++i];
  else if (args[i] === '--format' && args[i + 1]) formatArg = args[++i];
  else if (!args[i].startsWith('--') && !cityArg) cityArg = args[i];
}

const city = cityArg ? await fetchRealLocation(cityArg) : getCityForDate();
const dateStr = new Date().toISOString().slice(0, 10);
console.log(`\n======================================================`);
console.log(`🎯 GEOSNIPER DAILY OPS: ${city.name.toUpperCase()}, ${city.country.toUpperCase()}`);
console.log(`📍 GPS: ${city.coords} // ${city.district}`);
console.log(`🚁 Mission: ${city.mission}`);
console.log(`🎯 Target: ${city.target} (${city.rangeM}M, Wind: ${city.wind})`);
console.log(`📅 Date: ${dateStr}`);
console.log(`======================================================\n`);

const formats = formatArg === 'both' ? ['portrait', 'landscape'] : [formatArg];
const results = [];

for (const fmt of formats) {
  const filename = `geosniper-daily-${city.id}-${fmt}-${dateStr}.mp4`;
  console.log(`🎬 Rendering ${fmt.toUpperCase()} video (${filename})...`);
  const res = await renderConcept({
    format: fmt,
    name: filename,
    city: city,
    progress: msg => console.log(`   [${fmt}] ${msg}`)
  });
  results.push(res);
}

// Generate Social Media Publishing Kit
const socialCaption = `🎯 TARGET ACQUIRED: ${city.name.toUpperCase()}, ${city.country.toUpperCase()}
📍 GPS: ${city.coords}
🚁 OPERATION: ${city.mission}
🎯 OBJECTIVE: ${city.target}
📏 DISTANCE: ${city.rangeM} METERS | WIND: ${city.wind}

Can you make this shot? 
Drop your home city in the comments for tomorrow's satellite mission! 👇

#GeoSniper #SniperGame #GoogleFlow #GamingShorts #AndroidGaming #MobileGame #TargetEliminated
`;

const captionPath = path.join(mediaDir, `geosniper-daily-${city.id}-caption.txt`);
fs.writeFileSync(captionPath, socialCaption, 'utf-8');

console.log(`\n✅ DAILY BATCH COMPLETE!`);
results.forEach(r => console.log(`   📹 ${r.format}: ${path.join(mediaDir, r.filename)}`));
console.log(`   📝 Social Caption: ${captionPath}\n`);
