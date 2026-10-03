import fs from 'node:fs';
import path from 'node:path';
import {createHash} from 'node:crypto';
import {makePlan, campaignLink} from './content.mjs';

export const digest = text => createHash('sha256').update(text).digest('hex');
export async function fileHash(file) {
  const hash=createHash('sha256'); for await(const chunk of fs.createReadStream(file)) hash.update(chunk); return hash.digest('hex');
}
export function mediaPath(root, name) {
  if(typeof name!=='string' || !/^[a-zA-Z0-9][a-zA-Z0-9_.-]*\.mp4$/i.test(name)) throw Error('Use a simple MP4 filename in Growth/media.');
  const base=fs.realpathSync(path.join(root,'media')), file=fs.realpathSync(path.join(base,name));
  if(path.dirname(file)!==base || !fs.statSync(file).isFile()) throw Error('Media must be a regular file inside Growth/media.');
  return file;
}
export function fingerprint(post, asset, settings) {
  return digest(JSON.stringify([post.title,post.caption,post.assetId,post.scheduledAt,asset.sha256,
    settings.delivery,settings.youtubePrivacy,settings.releaseLive,settings.packageName]));
}
export function validateSettings(input, prior) {
  const next={...prior};
  for(const key of ['releaseLive','publishingEnabled']) if(key in input) {
    if(typeof input[key]!=='boolean') throw Error(`${key} must be true or false.`); next[key]=input[key];
  }
  for(const [key, allowed] of Object.entries({delivery:['export','api'],youtubePrivacy:['private','unlisted','public']})) if(key in input) {
    if(!allowed.includes(input[key])) throw Error(`Invalid ${key}.`); next[key]=input[key];
  }
  if('packageName' in input) { if(!/^[a-z][a-z0-9_]*(\.[a-z][a-z0-9_]*)+$/.test(input.packageName)) throw Error('Invalid Android package name.'); next.packageName=input.packageName; }
  for(const [key,host] of [['youtubeProfile','www.youtube.com'],['instagramProfile','www.instagram.com']]) if(key in input) {
    if(input[key]) { const url=new URL(input[key]); if(url.protocol!=='https:' || url.hostname!==host || url.username || url.password) throw Error(`Use an https://${host} profile URL.`); }
    next[key]=input[key];
  }
  return next;
}
export class Growth {
  constructor(store, root) { this.store=store; this.root=root; }
  plan(start) { let added=0; for(const post of makePlan(start,this.store.settings())) added+=Number(this.store.insert(post)); this.store.event(`Prepared ${added} draft posts.`); return {added}; }
  async register(name) {
    const file=mediaPath(this.root,name), hash=await fileHash(file), id=digest(name).slice(0,16);
    const asset={id,name,sha256:hash,reviewed:false,createdAt:new Date().toISOString(),note:'Review current-build accuracy, rights, location privacy, audio and vertical framing.'};
    const old=this.store.assets().find(a=>a.id===id); if(old?.sha256===hash) return old;
    this.store.saveAsset(asset); return asset;
  }
  async reviewAsset(id) {
    const asset=this.store.asset(id); asset.sha256=await fileHash(mediaPath(this.root,asset.name)); asset.reviewed=true;
    asset.reviewedAt=new Date().toISOString(); this.store.saveAsset(asset); this.store.event(`Media reviewed: ${asset.name}`); return asset;
  }
  edit(id, changes) {
    const post=this.store.post(id);
    if(!['draft','approved','needs_review','exported'].includes(post.status)) throw Error('This post cannot be edited. Reconcile any upload first.');
    if(post.status==='needs_review' && post.startedAt && changes.confirmNotPublished!==true) throw Error('First check the platform. Confirm no upload exists before reusing this post.');
    if(changes.revision!==post.revision) throw Error('This draft changed. Refresh before saving.');
    for(const key of ['title','caption','assetId','scheduledAt']) if(key in changes) post[key]=String(changes[key]);
    if(!post.title.trim() || post.title.length>100 || !post.caption.trim() || post.caption.length>2200) throw Error('Title: 1–100 characters. Caption: 1–2200.');
    if(!Number.isFinite(Date.parse(post.scheduledAt))) throw Error('Choose a valid schedule.');
    post.scheduledAt=new Date(post.scheduledAt).toISOString();
    if(post.assetId) this.store.asset(post.assetId);
    post.status='draft'; post.approvalHash=''; post.error=''; post.startedAt=''; post.remoteId=''; post.revision++;
    post.releaseLive=this.store.settings().releaseLive; this.store.save(post); return post;
  }
  async approve(id, revision) {
    const post=this.store.post(id), settings=this.store.settings();
    if(post.status!=='draft' || post.revision!==revision) throw Error('Save and review the latest draft first.');
    if(Date.parse(post.scheduledAt)<Date.now()) throw Error('Choose a future publishing time.');
    if(post.releaseLive!==settings.releaseLive) throw Error('Release status changed. Update the caption and save before approval.');
    if(!settings.releaseLive && /download now|available now|play\.google\.com|out now|is on google play/i.test(post.caption+' '+post.title)) throw Error('Pre-launch posts must not claim the game is available.');
    const asset=this.store.asset(post.assetId);
    if(!asset.reviewed || await fileHash(mediaPath(this.root,asset.name))!==asset.sha256) throw Error('Preview and review the current video first.');
    const latest=this.store.post(id);
    if(latest.revision!==post.revision || latest.status!=='draft') throw Error('Draft changed during review. Refresh and try again.');
    post.approvalHash=fingerprint(post,asset,settings); post.status='approved'; post.approvedAt=new Date().toISOString(); this.store.save(post); return post;
  }
  settings(changes) {
    if(this.store.posts().some(p=>p.status==='publishing')) throw Error('Wait for active delivery before changing settings.');
    const old=this.store.settings(), next=validateSettings(changes,old); this.store.saveSettings(next);
    for(const post of this.store.posts().filter(p=>p.status==='approved')) {
      if(fingerprint(post,this.store.asset(post.assetId),next)!==post.approvalHash) { post.status='draft'; post.approvalHash=''; post.revision++; this.store.save(post); }
    }
    return next;
  }
  confirmPublished(id, url) {
    const post=this.store.post(id);
    if(!['exported','needs_review','uploaded_private'].includes(post.status)) throw Error('Export or reconcile this post before marking published.');
    const u=new URL(url), hosts=post.channel==='youtube'?['www.youtube.com','youtube.com','youtu.be']:['www.instagram.com','instagram.com'];
    if(u.protocol!=='https:' || !hosts.includes(u.hostname) || u.username || u.password || u.pathname==='/') throw Error('Enter this post’s public platform URL.');
    const valid=post.channel==='youtube' ? ((u.pathname==='/watch' && /^[\w-]{11}$/.test(u.searchParams.get('v')||'')) || /^\/shorts\/[\w-]{11}\/?$/.test(u.pathname) || (u.hostname==='youtu.be' && /^\/[\w-]{11}\/?$/.test(u.pathname))) : /^\/(reel|p)\/[\w-]+\/?$/.test(u.pathname);
    if(!valid) throw Error('Use a video or Reel URL, not a profile URL.');
    post.status='published'; post.publishedUrl=u.toString(); post.publishedAt=new Date().toISOString(); post.error=''; this.store.save(post); return post;
  }
  metrics(input) {
    const post=this.store.post(input.postId); if(post.status!=='published') throw Error('Record results for a published post.');
    if(!/^\d{4}-\d{2}-\d{2}$/.test(input.day) || !Number.isFinite(Date.parse(input.day)) || input.day>new Date().toISOString().slice(0,10)) throw Error('Use an observation date no later than today.');
    if(new Date(input.day).toISOString().slice(0,10)!==input.day) throw Error('Invalid observation date.');
    const row={postId:post.id,day:input.day,source:'manual'};
    for(const key of ['views','likes','comments','shares','storeClicks','installs']) {
      const value=input[key]; row[key]=value==='' || value===undefined || value===null ? null : Number(value);
      if(row[key]!==null && (!Number.isSafeInteger(row[key]) || row[key]<0)) throw Error('Metrics must be non-negative whole numbers, or blank if unknown.');
    }
    this.store.metric(row); return row;
  }
  snapshot() {
    const settings=this.store.settings(), posts=this.store.posts(), latest=new Map();
    for(const metric of this.store.metrics()) latest.set(metric.postId,metric);
    const totals={}; for(const key of ['views','likes','comments','shares','storeClicks','installs']) {
      const values=[...latest.values()].map(m=>m[key]).filter(v=>v!==null); totals[key]=values.length?values.reduce((a,b)=>a+b,0):null;
    }
    const byPillar=['gps','skill','devlog'].map(pillar=>{
      const rows=posts.filter(p=>p.pillar===pillar && latest.has(p.id)).map(p=>latest.get(p.id));
      const views=rows.filter(r=>r.views!==null).map(r=>r.views); return {pillar,samples:views.length,averageViews:views.length?Math.round(views.reduce((a,b)=>a+b,0)/views.length):null};
    });
    return {settings,posts,assets:this.store.assets(),metrics:[...latest.values()],totals,byPillar,events:this.store.events(),
      links:{youtube:campaignLink('youtube',settings),instagram:campaignLink('instagram',settings)},
      nextExperiment:latest.size<6?'Gather at least six measured posts before comparing themes. Start with map-to-gameplay hooks; record results at 24 hours and 7 days.':'Compare posts within the same channel and similar age. Test one new opening against your current GPS hook. Results are directional, not proof of causation.'};
  }
}
