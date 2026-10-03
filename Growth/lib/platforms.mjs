import fs from 'node:fs';
import {setTimeout as sleep} from 'node:timers/promises';

// Official APIs only. No scraping, browser passwords, follower bots or paid services.
export function connectionStatus(env=process.env) {
  return {
    youtube:!!(env.YOUTUBE_CLIENT_ID && env.YOUTUBE_CLIENT_SECRET && env.YOUTUBE_REFRESH_TOKEN),
    instagram:!!(env.INSTAGRAM_USER_ID && env.META_ACCESS_TOKEN && /^v\d+\.\d+$/.test(env.META_API_VERSION||''))
  };
}
export function safeUploadUrl(value, host) {
  const url=new URL(value);
  if(url.protocol!=='https:' || url.hostname!==host || url.port || url.username || url.password) throw Error('Platform returned an unexpected upload host.');
  return url.toString();
}
export function createPublishers({env=process.env,fetcher=fetch,delay=sleep}={}) {
  async function request(url, options={}) {
    let response;
    try { response=await fetcher(url,{...options,redirect:'error',signal:AbortSignal.timeout(180000)}); }
    catch { throw Error('Platform connection interrupted. Check your account before retrying.'); }
    if(!response.ok) throw Error(`Platform rejected the request (HTTP ${response.status}). Check credentials, permissions, quota and platform dashboard.`);
    return response;
  }
  async function json(url,options) { return (await request(url,options)).json(); }
  return {
    async youtube(post,file,settings,checkpoint) {
      if(!connectionStatus(env).youtube) throw Error('YouTube OAuth credentials are not configured.');
      const token=await json('https://oauth2.googleapis.com/token',{method:'POST',body:new URLSearchParams({
        client_id:env.YOUTUBE_CLIENT_ID,client_secret:env.YOUTUBE_CLIENT_SECRET,refresh_token:env.YOUTUBE_REFRESH_TOKEN,grant_type:'refresh_token'
      })});
      if(!token.access_token) throw Error('YouTube did not provide an access token.');
      const headers={Authorization:`Bearer ${token.access_token}`}, size=fs.statSync(file).size;
      const init=await request('https://www.googleapis.com/upload/youtube/v3/videos?uploadType=resumable&part=snippet,status',{
        method:'POST',headers:{...headers,'Content-Type':'application/json','X-Upload-Content-Length':String(size),'X-Upload-Content-Type':'video/mp4'},
        body:JSON.stringify({snippet:{title:post.title,description:post.caption,categoryId:'20'},status:{privacyStatus:settings.youtubePrivacy,selfDeclaredMadeForKids:false,embeddable:true}})
      });
      const session=safeUploadUrl(init.headers.get('location'),'www.googleapis.com');
      checkpoint({stage:'youtube_upload_started'}); // Session URLs may contain credentials; never expose them to the UI.
      const result=await json(session,{method:'PUT',headers:{...headers,'Content-Type':'video/mp4','Content-Length':String(size)},body:fs.createReadStream(file),duplex:'half'});
      if(!result.id || !/^[\w-]+$/.test(result.id)) throw Error('Upload may have completed; check YouTube Studio.');
      checkpoint({remoteId:result.id,stage:'youtube_uploaded'});
      // API projects pending audit can force private. Never infer public from the requested setting.
      const isPublic=result.status?.privacyStatus==='public';
      return {remoteId:result.id,url:`https://www.youtube.com/watch?v=${result.id}`,visibility:result.status?.privacyStatus||'unknown',status:isPublic?'published':'uploaded_private'};
    },
    async instagram(post,file,settings,checkpoint) {
      if(!connectionStatus(env).instagram || !/^\d+$/.test(env.INSTAGRAM_USER_ID)) throw Error('Instagram Business account, token and Graph API version are not configured.');
      const base=`https://graph.facebook.com/${env.META_API_VERSION}/`, headers={Authorization:`Bearer ${env.META_ACCESS_TOKEN}`};
      const form=data=>({method:'POST',headers,body:new URLSearchParams(data)});
      const container=await json(base+env.INSTAGRAM_USER_ID+'/media',form({media_type:'REELS',upload_type:'resumable',caption:post.caption,share_to_feed:'true'}));
      if(!/^\d+$/.test(container.id||'')) throw Error('Instagram did not return a valid container ID.');
      checkpoint({containerId:container.id,stage:'instagram_container_created'});
      const upload=safeUploadUrl(container.uri,'rupload.facebook.com'), size=fs.statSync(file).size;
      await request(upload,{method:'POST',headers:{Authorization:`OAuth ${env.META_ACCESS_TOKEN}`,offset:'0',file_size:String(size),'Content-Type':'application/octet-stream','Content-Length':String(size)},body:fs.createReadStream(file),duplex:'half'});
      let ready=false;
      for(let i=0;i<30;i++) {
        const state=await json(base+container.id+'?fields=status_code',{headers});
        if(state.status_code==='FINISHED') { ready=true; break; }
        if(['ERROR','EXPIRED'].includes(state.status_code)) throw Error('Instagram could not process this media. Check its format and platform dashboard.');
        await delay(3000);
      }
      if(!ready) throw Error('Instagram processing is not finished. Check the platform before retrying.');
      checkpoint({stage:'instagram_publish_requested'});
      const result=await json(base+env.INSTAGRAM_USER_ID+'/media_publish',form({creation_id:container.id}));
      if(!/^\d+$/.test(result.id||'')) throw Error('Instagram publish response was uncertain. Check the account.');
      checkpoint({remoteId:result.id,stage:'instagram_published'});
      let url='';
      try { url=(await json(base+result.id+'?fields=permalink',{headers})).permalink||''; } catch { /* ID is enough to prove successful publish; don't repeat it. */ }
      return {remoteId:result.id,url,status:'published'};
    }
  };
}
