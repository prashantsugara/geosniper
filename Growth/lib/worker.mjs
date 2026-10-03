import fs from 'node:fs';
import path from 'node:path';
import {fileHash,mediaPath,fingerprint} from './core.mjs';
import {createPublishers} from './platforms.mjs';

export async function exportPack(root,post,asset,file) {
  const dir=path.join(root,'exports',`${post.id}-r${post.revision}`); fs.mkdirSync(dir,{recursive:true});
  await fs.promises.copyFile(file,path.join(dir,'video.mp4'));
  fs.writeFileSync(path.join(dir,'caption.txt'),`${post.title}\n\n${post.caption}\n`);
  fs.writeFileSync(path.join(dir,'post.json'),JSON.stringify({id:post.id,channel:post.channel,title:post.title,caption:post.caption,scheduledAt:post.scheduledAt,mediaSha256:asset.sha256},null,2));
  fs.writeFileSync(path.join(dir,'UPLOAD.md'),`# ${post.channel==='youtube'?'YouTube Short':'Instagram Reel'}\n\nUpload video.mp4 and copy caption.txt. Preview on a phone before publishing. This pack is not a published post. Record its URL in Growth Studio after publishing.\n\nShorts description URLs are not clickable. Use a channel profile link. Reels should point to the profile bio.\n\nScheduled: ${post.scheduledAt}\n`);
  return {status:'exported',exportFolder:path.relative(root,dir)};
}

export async function tick(growth,{publishers=createPublishers(),now=Date.now()}={}) {
  const {store,root}=growth, results=[];
  for(const candidate of store.posts().filter(p=>p.status==='approved' && Date.parse(p.scheduledAt)<=now)) {
    const settings=store.settings();
    if(settings.delivery==='api' && !settings.publishingEnabled) continue;
    const post=store.claim(candidate.id); if(!post) continue;
    try {
      if(now-Date.parse(post.scheduledAt)>12*3600000) throw Error('Missed by over 12 hours. Choose a new slot; no catch-up burst will be sent.');
      const asset=store.asset(post.assetId), file=mediaPath(root,asset.name);
      if(!asset.reviewed || await fileHash(file)!==asset.sha256 || post.approvalHash!==fingerprint(post,asset,settings)) throw Error('Video, caption or release settings changed. Review and approve again.');
      // Mark each external phase durably before the next step. Ambiguous failures never auto-retry.
      const checkpoint=fields=>{ Object.assign(post,fields); store.save(post); };
      const result=settings.delivery==='export' ? await exportPack(root,post,asset,file)
        : await publishers[post.channel](post,file,settings,checkpoint);
      Object.assign(post,result,{publishedUrl:result.url||'',completedAt:new Date(now).toISOString(),error:''});
      if(result.status==='published') post.publishedAt=new Date(now).toISOString();
      store.save(post); store.event(`${post.id}: ${post.status}`); results.push({id:post.id,status:post.status});
    } catch(error) {
      post.status='needs_review'; post.error=error.message; store.save(post); store.event(`${post.id}: needs review`); results.push({id:post.id,status:post.status});
    }
  }
  return results;
}

export function report(growth) {
  const state=growth.snapshot();
  return `# Geo Sniper organic growth report\n\nGenerated: ${new Date().toISOString()}\nRelease: ${state.settings.releaseLive?'Live (operator confirmed)':'Pre-launch / pending approval'}\n\n`+
    `Drafts: ${state.posts.filter(p=>p.status==='draft').length}\nApproved: ${state.posts.filter(p=>p.status==='approved').length}\nPublished: ${state.posts.filter(p=>p.status==='published').length}\nNeeds review: ${state.posts.filter(p=>p.status==='needs_review').length}\n\n`+
    Object.entries(state.totals).map(([k,v])=>`${k}: ${v===null?'not measured':v}`).join('\n')+
    `\n\nLatest cumulative observation per post; snapshots are NOT added together. Totals include measured posts only. Views are not unique people. Clicks/installs require an actual attribution source; no estimates are invented.\n\n## Next experiment\n\n${state.nextExperiment}\n\nKeep city hooks central. Compare the same channel at the same post age. Check retention in each platform's native analytics. Organic reach and store rankings are not guaranteed.\n`;
}
