// Run explicitly to connect YOUR YouTube channel. This does not upload anything.
import http from 'node:http';
import fs from 'node:fs';
import path from 'node:path';
import {randomBytes,createHash} from 'node:crypto';
import {root,loadEnv} from './server.mjs';
loadEnv();
if(!process.env.YOUTUBE_CLIENT_ID || !process.env.YOUTUBE_CLIENT_SECRET) throw Error('First put your Desktop OAuth client ID and secret in Growth/.env. See README.md.');
const state=randomBytes(32).toString('hex'), verifier=randomBytes(48).toString('base64url');
let redirect='',used=false;
const server=http.createServer(async(req,res)=>{
  res.setHeader('Content-Type','text/plain; charset=utf-8');res.setHeader('Cache-Control','no-store');res.setHeader('Referrer-Policy','no-referrer');
  const url=new URL(req.url,redirect);
  if(url.pathname!=='/' || req.headers.host!==new URL(redirect).host || url.searchParams.get('state')!==state || used) {res.writeHead(400);res.end('Invalid authorization response.');return;}
  used=true;
  try {
    if(!url.searchParams.get('code')) throw Error('Authorization was not completed.');
    const response=await fetch('https://oauth2.googleapis.com/token',{method:'POST',redirect:'error',signal:AbortSignal.timeout(30000),body:new URLSearchParams({
      client_id:process.env.YOUTUBE_CLIENT_ID,client_secret:process.env.YOUTUBE_CLIENT_SECRET,
      code:url.searchParams.get('code'),code_verifier:verifier,redirect_uri:redirect,grant_type:'authorization_code'
    })});
    if(!response.ok) throw Error('Google rejected the token exchange. Check your OAuth application settings.');
    const tokens=await response.json();
    if(!tokens.refresh_token || !/^[\w.\/-]+$/.test(tokens.refresh_token)) throw Error('Google did not return a usable refresh token. Revoke the old grant and connect again.');
    const file=path.join(root,'.env');let content=fs.readFileSync(file,'utf8');
    const setting=`YOUTUBE_REFRESH_TOKEN=${tokens.refresh_token}`;
    content=/^YOUTUBE_REFRESH_TOKEN=.*$/m.test(content)?content.replace(/^YOUTUBE_REFRESH_TOKEN=.*$/m,setting):`${content}\n${setting}\n`;
    fs.writeFileSync(file,content,{mode:0o600});
    res.end('YouTube connected. No video was uploaded. Close this tab and restart Growth Studio. Keep uploads private for your first test.');
    console.log('Refresh token saved locally in ignored Growth/.env. No token printed. Restart Growth Studio.');
  } catch(error) {res.writeHead(400);res.end(error.message);console.error('YouTube connection failed. Check the local browser message.');}
  finally{clearTimeout(timer);server.close();}
});
const timer=setTimeout(()=>{console.error('Authorization timed out. Run the connector again when ready.');server.close();},5*60000);
server.listen(0,'127.0.0.1',()=>{
  redirect=`http://127.0.0.1:${server.address().port}/`;
  const url=new URL('https://accounts.google.com/o/oauth2/v2/auth');
  url.search=new URLSearchParams({client_id:process.env.YOUTUBE_CLIENT_ID,redirect_uri:redirect,response_type:'code',scope:'https://www.googleapis.com/auth/youtube.upload',access_type:'offline',prompt:'consent',state,code_challenge:createHash('sha256').update(verifier).digest('base64url'),code_challenge_method:'S256'}).toString();
  console.log('Open this Google consent link in your browser and choose your own channel:\n'+url.toString());
});
server.on('error',()=>{clearTimeout(timer);console.error('Could not start the local OAuth callback. No credentials were changed.');process.exitCode=1;});
