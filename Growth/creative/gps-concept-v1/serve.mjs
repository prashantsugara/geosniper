import http from 'node:http';
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { fetchRealLocation } from './cities.mjs';

const dir = path.dirname(fileURLToPath(import.meta.url));
const port = 8899;

const server = http.createServer(async (req, res) => {
  res.setHeader('Access-Control-Allow-Origin', '*');
  res.setHeader('Access-Control-Allow-Methods', 'GET, OPTIONS');
  res.setHeader('Cache-Control', 'no-store, no-cache, must-revalidate, max-age=0');
  res.setHeader('Pragma', 'no-cache');
  res.setHeader('Expires', '0');

  if (req.method === 'OPTIONS') {
    res.writeHead(204);
    return res.end();
  }

  // Real Geocoding & Nearby Location API Endpoint (bypasses browser CORS & header limits)
  if (req.url.startsWith('/api/location')) {
    const url = new URL(req.url, `http://127.0.0.1:${port}`);
    const q = url.searchParams.get('q');
    try {
      const loc = await fetchRealLocation(q);
      res.writeHead(200, { 'Content-Type': 'application/json; charset=utf-8' });
      return res.end(JSON.stringify(loc));
    } catch (err) {
      res.writeHead(500, { 'Content-Type': 'application/json' });
      return res.end(JSON.stringify({ error: err.message }));
    }
  }

  const rawKey = decodeURIComponent(new URL(req.url, `http://127.0.0.1:${port}`).pathname.slice(1)) || 'preview.html';
  const filePath = path.join(dir, rawKey);
  if (!filePath.toLowerCase().startsWith(dir.toLowerCase()) || !fs.existsSync(filePath) || fs.statSync(filePath).isDirectory()) {
    res.writeHead(404);
    return res.end('Not found');
  }
  const ext = path.extname(filePath).toLowerCase();
  const types = {
    '.html': 'text/html; charset=utf-8',
    '.css': 'text/css',
    '.js': 'text/javascript',
    '.mjs': 'text/javascript',
    '.json': 'application/json',
    '.png': 'image/png',
    '.jpg': 'image/jpeg',
    '.jpeg': 'image/jpeg',
    '.mp4': 'video/mp4',
    '.wav': 'audio/wav'
  };
  res.setHeader('Content-Type', types[ext] || 'application/octet-stream');
  fs.createReadStream(filePath).pipe(res);
});

server.listen(port, '127.0.0.1', () => {
  console.log(`GeoSniper Interactive Preview running at http://127.0.0.1:${port}/preview.html`);
});
