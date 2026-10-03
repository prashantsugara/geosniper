import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import {Store} from '../lib/store.mjs';
import {Growth,mediaPath,fingerprint} from '../lib/core.mjs';
import {defaults,makePlan,campaignLink} from '../lib/content.mjs';
import {tick} from '../lib/worker.mjs';
import {createPublishers,safeUploadUrl} from '../lib/platforms.mjs';
import {createApp} from '../server.mjs';

function fixture(t){const dir=fs.mkdtempSync(path.join(os.tmpdir(),'geosniper-growth-test-'));fs.mkdirSync(path.join(dir,'media'));const store=new Store(':memory:'),growth=new Growth(store,dir);t.after(()=>{store.close();fs.rmSync(dir,{recursive:true,force:true});});return{store,growth,dir};}
async function ready(t,{api=false}={}){const f=fixture(t);if(api)f.growth.settings({delivery:'api',publishingEnabled:true});f.growth.plan('2099-01-01');const post=f.store.posts().find(p=>p.channel==='instagram');fs.writeFileSync(path.join(f.dir,'media/test.mp4'),'fixture-not-real-video');const asset=await f.growth.register('test.mp4');await f.growth.reviewAsset(asset.id);f.growth.edit(post.id,{revision:post.revision,assetId:asset.id});await f.growth.approve(post.id,f.store.post(post.id).revision);const p=f.store.post(post.id);p.scheduledAt=new Date(Date.now()-1000).toISOString();p.approvalHash=fingerprint(p,f.store.asset(asset.id),f.store.settings());f.store.save(p);return{...f,post:p,asset};}
test('plan produces 12 concepts / 24 drafts, prelaunch copy and no invented availability',()=>{
  const posts=makePlan('2026-09-21',defaults);assert.equal(posts.length,24);assert.equal(new Set(posts.map(p=>p.concept)).size,12);
  assert.ok(posts.every(p=>p.status==='draft' && /review is pending/.test(p.caption) && !/download now/i.test(p.caption)));
  assert.equal(campaignLink('youtube',defaults),'');assert.throws(()=>makePlan('2026-02-31',defaults));
  const link=new URL(campaignLink('instagram',{...defaults,releaseLive:true}));assert.equal(new URLSearchParams(link.searchParams.get('referrer')).get('utm_source'),'instagram');
});
test('plan idempotent and edits preserved',t=>{const {growth,store}=fixture(t);assert.equal(growth.plan('2099-01-01').added,24);let p=store.posts()[0];growth.edit(p.id,{revision:p.revision,title:'My honest hook'});assert.equal(growth.plan('2099-01-01').added,0);assert.equal(store.post(p.id).title,'My honest hook');});
test('approval requires reviewed assets, future time and current revision',async t=>{const {growth,store,dir}=fixture(t);growth.plan('2099-01-01');const p=store.posts()[0];await assert.rejects(growth.approve(p.id,1),/registered/);fs.writeFileSync(path.join(dir,'media/x.mp4'),'x');const asset=await growth.register('x.mp4');growth.edit(p.id,{revision:1,assetId:asset.id});await assert.rejects(growth.approve(p.id,2),/Preview/);await growth.reviewAsset(asset.id);await assert.rejects(growth.approve(p.id,1),/latest/);await growth.approve(p.id,2);assert.equal(store.post(p.id).status,'approved');});
test('changed video invalidates delivery; no external call',async t=>{const {growth,store,post,dir}=await ready(t,{api:true});fs.appendFileSync(path.join(dir,'media/test.mp4'),'changed');let calls=0;await tick(growth,{publishers:{instagram:async()=>{calls++;}}});assert.equal(calls,0);assert.equal(store.post(post.id).status,'needs_review');});
test('simultaneous workers claim a post once and uncertain errors never retry',async t=>{const {growth,store,post}=await ready(t,{api:true});let calls=0;const publishers={instagram:async()=>{calls++;await new Promise(r=>setTimeout(r,15));throw Error('Network interrupted.');}};await Promise.all([tick(growth,{publishers}),tick(growth,{publishers})]);await tick(growth,{publishers});assert.equal(calls,1);assert.equal(store.post(post.id).status,'needs_review');assert.throws(()=>growth.edit(post.id,{revision:2}),/check the platform/);});
test('missed slots are not delivered in a catch-up burst',async t=>{const {growth,store,post}=await ready(t);post.scheduledAt=new Date(Date.now()-13*3600000).toISOString();post.approvalHash=fingerprint(post,store.asset(post.assetId),store.settings());store.save(post);await tick(growth);assert.equal(store.post(post.id).status,'needs_review');assert.match(store.post(post.id).error,/Missed/);});
test('export copies video and text but does not mark published',async t=>{const {growth,store,post,dir}=await ready(t);await tick(growth);const p=store.post(post.id);assert.equal(p.status,'exported');assert.ok(fs.existsSync(path.join(dir,p.exportFolder,'video.mp4')));assert.throws(()=>growth.confirmPublished(p.id,'https://www.instagram.com/profile'),/video or Reel/);growth.confirmPublished(p.id,'https://www.instagram.com/reel/ExampleId/');assert.equal(store.post(p.id).status,'published');});
test('settings changes revoke approvals; prelaunch cannot claim availability',async t=>{const {growth,store,post}=await ready(t);growth.settings({releaseLive:true});assert.equal(store.post(post.id).status,'draft');growth.settings({releaseLive:false});const p=growth.edit(post.id,{revision:store.post(post.id).revision,caption:'Download now!',scheduledAt:'2099-01-01T10:00:00Z'});await assert.rejects(growth.approve(p.id,p.revision),/Pre-launch/);});
test('publishing disabled makes no API call',async t=>{const {growth,store,post}=await ready(t,{api:true});growth.settings({publishingEnabled:false});await tick(growth,{publishers:{instagram:async()=>{throw Error('Must not be called');}}});assert.equal(store.post(post.id).status,'approved');});
test('restart leaves uncertain upload for human reconciliation',async t=>{const {growth,store,post}=await ready(t);assert.ok(store.claim(post.id));store.recover();assert.equal(store.post(post.id).status,'needs_review');assert.match(store.post(post.id).error,/Interrupted/);});
test('metrics use latest cumulative snapshot, blank is unknown not zero',async t=>{const {growth,store,post}=await ready(t);post.status='published';store.save(post);growth.metrics({postId:post.id,day:'2025-01-01',views:100,likes:0});growth.metrics({postId:post.id,day:'2025-01-02',views:250,likes:5});growth.metrics({postId:post.id,day:'2025-01-02',views:260,likes:5});assert.equal(growth.snapshot().totals.views,260);assert.equal(growth.snapshot().totals.installs,null);assert.throws(()=>growth.metrics({postId:post.id,day:'2025-01-03',views:-1}),/non-negative/);});
test('path and upload URL restrictions prevent traversal and token leakage',t=>{const {dir}=fixture(t);assert.throws(()=>mediaPath(dir,'../.env'));assert.throws(()=>mediaPath(dir,'C:\\secret.mp4'));assert.throws(()=>safeUploadUrl('https://www.googleapis.com.evil.test/file','www.googleapis.com'));assert.throws(()=>safeUploadUrl('https://user:secret@www.googleapis.com/file','www.googleapis.com'));assert.equal(safeUploadUrl('https://rupload.facebook.com/path','rupload.facebook.com'),'https://rupload.facebook.com/path');});
test('per-channel transaction lock enforces rolling daily delivery cap',async t=>{const {store,post}=await ready(t);const other=store.posts().find(p=>p.channel===post.channel && p.id!==post.id);other.status='approved';store.save(other);assert.ok(store.claim(post.id));assert.equal(store.claim(other.id),null);const current=store.post(post.id);current.status='exported';current.completedAt=new Date().toISOString();store.save(current);assert.equal(store.claim(other.id),null);});
test('YouTube API uses requested privacy but reports server-forced private accurately',async t=>{
  const {dir}=fixture(t),file=path.join(dir,'media/y.mp4');fs.writeFileSync(file,'video');const calls=[];
  const fetcher=async(url,options)=>{calls.push({url,options});if(options.body instanceof fs.ReadStream){for await(const chunk of options.body){void chunk;}}
    if(calls.length===1)return new Response(JSON.stringify({access_token:'fake'}));if(calls.length===2)return new Response('',{headers:{location:'https://www.googleapis.com/upload/test'}});return new Response(JSON.stringify({id:'abcdefghijk',status:{privacyStatus:'private'}}));};
  const publisher=createPublishers({env:{YOUTUBE_CLIENT_ID:'client',YOUTUBE_CLIENT_SECRET:'secret',YOUTUBE_REFRESH_TOKEN:'refresh'},fetcher});
  const result=await publisher.youtube({title:'Test',caption:'Test'},file,{youtubePrivacy:'public'},()=>{});assert.equal(result.status,'uploaded_private');assert.equal(JSON.parse(calls[1].options.body).status.privacyStatus,'public');assert.equal(calls[2].options.redirect,'error');
});
test('Instagram API uploads local bytes, waits for processing, then publishes once',async t=>{
  const {dir}=fixture(t),file=path.join(dir,'media/i.mp4');fs.writeFileSync(file,'video');const urls=[],states=[];const responses=[{id:'111',uri:'https://rupload.facebook.com/ig-api-upload/test'}, {success:true},{status_code:'FINISHED'},{id:'222'},{permalink:'https://www.instagram.com/reel/test/'}];
  const fetcher=async(url,options)=>{urls.push(url);if(options.body instanceof fs.ReadStream){for await(const chunk of options.body){void chunk;}}return new Response(JSON.stringify(responses.shift()));};
  const api=createPublishers({env:{INSTAGRAM_USER_ID:'123',META_ACCESS_TOKEN:'fake',META_API_VERSION:'v99.0'},fetcher,delay:async()=>{}});
  const result=await api.instagram({caption:'Test'},file,{},s=>states.push(s));assert.equal(result.status,'published');assert.equal(urls.filter(u=>u.endsWith('/media_publish')).length,1);assert.equal(states.at(-1).remoteId,'222');
});
test('local HTTP blocks cross-origin and missing-CSRF writes, serves state',async t=>{
  const {growth}=fixture(t),port=14318,server=createApp(growth,{port,worker:false});await new Promise(resolve=>server.listen(port,'127.0.0.1',resolve));t.after(()=>new Promise(resolve=>server.close(resolve)));
  const base=`http://127.0.0.1:${port}`;let response=await fetch(base+'/api/state');const state=await response.json();assert.equal(response.status,200);assert.ok(state.csrf);
  response=await fetch(base+'/api/plan',{method:'POST',headers:{'Content-Type':'application/json'},body:'{}'});assert.equal(response.status,403);
  response=await fetch(base+'/api/state',{headers:{Origin:'https://evil.test'}});assert.equal(response.status,403);
  response=await fetch(base+'/api/plan',{method:'POST',headers:{'Content-Type':'application/json','X-CSRF-Token':state.csrf},body:JSON.stringify({start:'2099-01-01'})});assert.equal((await response.json()).added,24);
});
