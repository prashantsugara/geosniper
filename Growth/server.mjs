import http from 'node:http';
import fs from 'node:fs';
import path from 'node:path';
import {fileURLToPath,pathToFileURL} from 'node:url';
import {randomBytes} from 'node:crypto';
import {Store} from './lib/store.mjs';
import {Growth,mediaPath} from './lib/core.mjs';
import {tick,report} from './lib/worker.mjs';
import {connectionStatus} from './lib/platforms.mjs';
import {renderPreset,probeVideo,createPoster,presets} from './lib/render.mjs';

export const root=path.dirname(fileURLToPath(import.meta.url));
export function loadEnv() { const envFile=path.join(root,'.env'); if(fs.existsSync(envFile)) process.loadEnvFile(envFile); }
function json(res,status,data) { res.writeHead(status,{'Content-Type':'application/json'});res.end(JSON.stringify(data)); }
async function body(req) {
  let text=''; for await(const chunk of req) { text+=chunk; if(text.length>64000) throw Error('Request too large.'); }
  return JSON.parse(text||'{}');
}
function serveFile(req,res,file,type) {
  const size=fs.statSync(file).size, range=req.headers.range;
  res.setHeader('Content-Type',type); res.setHeader('Accept-Ranges','bytes');
  if(range) {
    const match=/^bytes=(\d+)-(\d*)$/.exec(range), start=Number(match?.[1]), end=match?.[2]?Number(match[2]):size-1;
    if(!match || start>end || end>=size) {res.writeHead(416,{'Content-Range':`bytes */${size}`});res.end();return;}
    res.writeHead(206,{'Content-Range':`bytes ${start}-${end}/${size}`,'Content-Length':end-start+1});fs.createReadStream(file,{start,end}).pipe(res);
  } else { res.writeHead(200,{'Content-Length':size});fs.createReadStream(file).pipe(res); }
}
export function createApp(growth,{port=4318,worker=true}={}) {
  const csrf=randomBytes(32).toString('hex'), origin=`http://127.0.0.1:${port}`;
  let working=false, rendering=false, renderError='', mutation=Promise.resolve();
  async function runWorker() { if(working)return []; working=true; try{return await tick(growth);}finally{working=false;} }
  const server=http.createServer(async(req,res)=>{
    res.setHeader('Cache-Control','no-store');res.setHeader('X-Content-Type-Options','nosniff');
    res.setHeader('Content-Security-Policy',"default-src 'self'; script-src 'self'; style-src 'self'; img-src 'self'; media-src 'self'; connect-src 'self'; frame-ancestors 'none'; base-uri 'none'; form-action 'self'");
    if(req.headers.host!==`127.0.0.1:${port}` || (req.headers.origin && req.headers.origin!==origin)) return json(res,403,{error:'Local same-origin access only.'});
    try {
      const url=new URL(req.url,origin);
      if(req.method==='GET') {
        if(url.pathname==='/api/state') return json(res,200,{...growth.snapshot(),csrf,connections:connectionStatus(),presets,rendering,renderError,workerRunning:working});
        if(url.pathname==='/api/report') {res.setHeader('Content-Type','text/markdown; charset=utf-8');res.setHeader('Content-Disposition','attachment; filename="growth-report.md"');return res.end(report(growth));}
        if(url.pathname.startsWith('/media/')) {const asset=growth.store.asset(url.pathname.slice(7));return serveFile(req,res,mediaPath(growth.root,asset.name),'video/mp4');}
        if(url.pathname.startsWith('/poster/')) {
          const asset=growth.store.asset(url.pathname.slice(8)), file=mediaPath(growth.root,asset.name).replace(/\.mp4$/i,'.jpg');
          if(!fs.existsSync(file) || path.dirname(fs.realpathSync(file))!==fs.realpathSync(path.join(growth.root,'media')))return json(res,404,{error:'No preview image.'});
          return serveFile(req,res,file,'image/jpeg');
        }
        if(url.pathname==='/brand.png') return serveFile(req,res,path.resolve(root,'../Store/Promo/geosniper-feature-1024x500.png'),'image/png');
        const files={'/':['index.html','text/html; charset=utf-8'],'/app.js':['app.js','text/javascript'],'/style.css':['style.css','text/css']};
        if(files[url.pathname]) return serveFile(req,res,path.join(root,'public',files[url.pathname][0]),files[url.pathname][1]);
        return json(res,404,{error:'Not found.'});
      }
      if(req.method!=='POST' || req.headers['x-csrf-token']!==csrf || req.headers['content-type']!=='application/json') return json(res,403,{error:'Invalid local request token.'});
      const data=await body(req);
      const handle=async()=>{
        const id=data.id;
        switch(url.pathname) {
          case '/api/plan':return growth.plan(data.start);
          case '/api/settings':return growth.settings(data);
          case '/api/post/edit':return growth.edit(id,data);
          case '/api/post/approve':return growth.approve(id,data.revision);
          case '/api/post/published':return growth.confirmPublished(id,data.url);
          case '/api/metrics':return growth.metrics(data);
          case '/api/asset/register': {
            const file=mediaPath(growth.root,data.name); await probeVideo(growth.root,file);await createPoster(growth.root,file);return growth.register(data.name);
          }
          case '/api/asset/review':if(data.confirm!==true)throw Error('Confirm the review checklist first.');return growth.reviewAsset(id);
          case '/api/tick':return runWorker();
          case '/api/render': {
            if(rendering) throw Error('A video is already rendering.');
            if(!presets.some(p=>p.id===id))throw Error('Unknown preset.');
            rendering=true;renderError='';
            renderPreset(growth.root,id).then(name=>growth.register(name)).then(()=>growth.store.event('Draft video rendered. Preview and review it.')).catch(e=>{renderError=e.message;growth.store.event('Render failed. Check FFmpeg configuration.');}).finally(()=>rendering=false);
            return {started:true};
          }
          default:throw Error('Unknown action.');
        }
      };
      const pending=mutation.then(handle); mutation=pending.catch(()=>{});return json(res,200,await pending);
    } catch(error) {return json(res,400,{error:error.message});}
  });
  const timer=worker?setInterval(()=>runWorker().catch(()=>growth.store.event('Worker interrupted; review queue.')),60000):null;
  timer?.unref();server.on('close',()=>clearInterval(timer));return server;
}
if(process.argv[1] && import.meta.url===pathToFileURL(path.resolve(process.argv[1])).href) {
  loadEnv();fs.mkdirSync(path.join(root,'media'),{recursive:true});
  const store=new Store(path.join(root,'data/growth.sqlite'));
  const growth=new Growth(store,root), port=4318;
  const server=createApp(growth,{port});
  server.on('error',e=>{console.error(e.code==='EADDRINUSE'?'Growth Studio is already running on port 4318.':e.message);process.exitCode=1;});
  server.listen(port,'127.0.0.1',()=>{store.recover();console.log(`Geo Sniper Growth Studio: http://127.0.0.1:${port}\nFree local workflow. Keep this process running for scheduled delivery.`);});
}
