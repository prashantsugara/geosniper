import {DatabaseSync} from 'node:sqlite';
import fs from 'node:fs';
import path from 'node:path';
import {defaults} from './content.mjs';

export class Store {
  constructor(file) {
    if (file !== ':memory:') fs.mkdirSync(path.dirname(file), {recursive:true});
    this.db = new DatabaseSync(file);
    this.db.exec(`PRAGMA journal_mode=WAL; PRAGMA busy_timeout=5000;
      CREATE TABLE IF NOT EXISTS settings (id INTEGER PRIMARY KEY CHECK(id=1), body TEXT NOT NULL);
      CREATE TABLE IF NOT EXISTS posts (id TEXT PRIMARY KEY, status TEXT NOT NULL, body TEXT NOT NULL);
      CREATE TABLE IF NOT EXISTS assets (id TEXT PRIMARY KEY, body TEXT NOT NULL);
      CREATE TABLE IF NOT EXISTS metrics (postId TEXT NOT NULL, day TEXT NOT NULL, body TEXT NOT NULL, PRIMARY KEY(postId,day));
      CREATE TABLE IF NOT EXISTS events (id INTEGER PRIMARY KEY, at TEXT NOT NULL, message TEXT NOT NULL);`);
    this.db.prepare('INSERT OR IGNORE INTO settings VALUES(1,?)').run(JSON.stringify(defaults));
  }
  settings() { return {...defaults, ...JSON.parse(this.db.prepare('SELECT body FROM settings WHERE id=1').get().body)}; }
  saveSettings(value) { this.db.prepare('UPDATE settings SET body=? WHERE id=1').run(JSON.stringify(value)); }
  posts() { return this.db.prepare('SELECT body FROM posts ORDER BY id').all().map(r=>JSON.parse(r.body)).sort((a,b)=>a.scheduledAt.localeCompare(b.scheduledAt)||a.id.localeCompare(b.id)); }
  post(id) { const row = this.db.prepare('SELECT body FROM posts WHERE id=?').get(id); if(!row) throw Error('Post not found.'); return JSON.parse(row.body); }
  insert(post) { return this.db.prepare('INSERT OR IGNORE INTO posts VALUES(?,?,?)').run(post.id,post.status,JSON.stringify(post)).changes; }
  save(post) { this.db.prepare('UPDATE posts SET status=?,body=? WHERE id=?').run(post.status,JSON.stringify(post),post.id); }
  claim(id) {
    this.db.exec('BEGIN IMMEDIATE');
    try {
      const post=this.post(id); if(post.status!=='approved') { this.db.exec('ROLLBACK'); return null; }
      const recent=this.posts().some(p=>p.id!==id && p.channel===post.channel && (p.status==='publishing' ||
        (['published','exported','uploaded_private'].includes(p.status) && Date.now()-Date.parse(p.completedAt||p.publishedAt)<24*3600000)));
      if(recent) { this.db.exec('ROLLBACK'); return null; }
      post.status='publishing'; post.startedAt=new Date().toISOString(); this.save(post); this.db.exec('COMMIT'); return post;
    } catch(e) { this.db.exec('ROLLBACK'); throw e; }
  }
  assets() { return this.db.prepare('SELECT body FROM assets ORDER BY id').all().map(r=>JSON.parse(r.body)); }
  asset(id) { const row=this.db.prepare('SELECT body FROM assets WHERE id=?').get(id); if(!row) throw Error('Choose a registered video.'); return JSON.parse(row.body); }
  saveAsset(asset) { this.db.prepare('INSERT OR REPLACE INTO assets VALUES(?,?)').run(asset.id,JSON.stringify(asset)); }
  metric(value) { this.db.prepare('INSERT OR REPLACE INTO metrics VALUES(?,?,?)').run(value.postId,value.day,JSON.stringify(value)); }
  metrics() { return this.db.prepare('SELECT body FROM metrics ORDER BY day').all().map(r=>JSON.parse(r.body)); }
  event(message) { this.db.prepare('INSERT INTO events(at,message) VALUES(?,?)').run(new Date().toISOString(),message); }
  events() { return this.db.prepare('SELECT at,message FROM events ORDER BY id DESC LIMIT 30').all(); }
  recover() {
    for(const post of this.posts().filter(p=>p.status==='publishing')) {
      post.status='needs_review'; post.error='Interrupted delivery. Check the platform and exports before rescheduling; it may already have uploaded.'; this.save(post);
    }
  }
  close() { this.db.close(); }
}
