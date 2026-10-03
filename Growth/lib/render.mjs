import fs from 'node:fs';
import path from 'node:path';
import {spawn} from 'node:child_process';

export const presets = [
  {id:'gps',name:'Your city. Your mission.',lines:['YOUR CITY.','YOUR MISSION.'],start:141,duration:12,framing:'wide',label:'REAL-WORLD MAPS',detail:'Mapped streets. Fictional missions.'},
  {id:'scope',name:'Wait for the opening.',lines:['WAIT FOR','THE OPENING.'],start:167.2,duration:12,framing:'scope',label:'PRECISION MOMENT',detail:'Settle your aim. Pick your moment.'},
  {id:'streets',name:'Recognize these streets?',lines:['RECOGNIZE','THESE STREETS?'],start:206,duration:12,framing:'wide',label:'A DIFFERENT PERSPECTIVE',detail:'Game environments built from map data.'}
];
export function ffmpegPath(root) {
  return process.env.PROMO_FFMPEG || path.resolve(root,'../.utmp/promo-tools/imageio_ffmpeg/binaries/ffmpeg-win-x86_64-v7.1.exe');
}
export function ffmpegRun(root,args) {
  return new Promise((resolve,reject)=>{
    const proc=spawn(ffmpegPath(root),['-hide_banner','-nostdin',...args],{windowsHide:true}); let log='';
    proc.stderr.on('data',v=>{log=(log+v.toString()).slice(-12000);});
    const timer=setTimeout(()=>proc.kill(),600000);
    proc.on('error',()=>{clearTimeout(timer);reject(Error('FFmpeg could not start. Set PROMO_FFMPEG in Growth/.env to a local FFmpeg executable.'));});
    proc.on('close',code=>{clearTimeout(timer);code===0?resolve(log):reject(Error(`Video processing failed (exit ${code}). ${log.slice(-1800)}`));});
  });
}
export async function probeVideo(root,file) {
  const log=await ffmpegRun(root,['-i',file,'-t','0','-f','null','-']);
  const size=log.match(/Video: h264[^\n]*?\b(\d{3,4})x(\d{3,4})\b/), duration=log.match(/Duration: (\d+):(\d+):(\d+(?:\.\d+)?)/);
  if(!size || !duration || !/Audio: aac/.test(log)) throw Error('Use H.264 MP4 with AAC audio.');
  const seconds=Number(duration[1])*3600+Number(duration[2])*60+Number(duration[3]);
  if(Number(size[1])!==1080 || Number(size[2])!==1920 || seconds<3 || seconds>60.2) throw Error('This studio expects a 1080 x 1920 video, between 3 and 60 seconds.');
  return {width:1080,height:1920,duration:seconds};
}
export async function createPoster(root,file) {
  const target=file.replace(/\.mp4$/i,'.jpg');
  if(fs.existsSync(target)) return;
  await ffmpegRun(root,['-n','-ss','1','-i',file,'-frames:v','1','-vf','scale=360:640','-q:v','3',target]);
}
export async function renderPreset(root,id) {
  const preset=presets.find(p=>p.id===id); if(!preset) throw Error('Unknown video preset.');
  const repo=path.resolve(root,'..'), media=path.join(root,'media'); fs.mkdirSync(media,{recursive:true});
  const filename=`${id}-${Date.now()}.mp4`, output=path.join(media,filename);
  // Restrict drawtext values to authored constants. User input never enters an FFmpeg filter expression.
  const font="fontfile='C\\:/Windows/Fonts/arial.ttf'", bold="fontfile='C\\:/Windows/Fonts/arialbd.ttf'";
  const text=(value,y,size,color='white',heavy=false)=>`drawtext=${heavy?bold:font}:text='${value}':x=72:y=${y}:fontsize=${size}:fontcolor=${color}`;
  const fg=preset.framing==='scope'?'crop=ih:ih:(iw-ih)/2:0,scale=1008:1008':'scale=1008:-2';
  const y=preset.framing==='scope'?510:660;
  const filters=[
    `[0:v]fps=30,split=2[bg][fg];[bg]scale=1080:1920:force_original_aspect_ratio=increase,crop=1080:1920,boxblur=35:2,eq=brightness=-0.20:saturation=0.35[back];[fg]${fg}[front];[back][front]overlay=36:${y}`,
    'drawbox=x=0:y=0:w=iw:h=460:color=0x081214@0.94:t=fill',
    'drawbox=x=0:y=1540:w=iw:h=380:color=0x081214@0.95:t=fill',
    'drawbox=x=72:y=105:w=44:h=6:color=0xEDAF4D:t=fill',text('GEO SNIPER',146,34,'0xEDAF4D',true),
    text(preset.lines[0],225,76,'white',true),text(preset.lines[1],316,76,'white',true),
    text(preset.label,466,27,'0x75DDD1',true),
    text(preset.detail,1580,35),text('COMING TO ANDROID',1645,29,'0xEDAF4D',true),
    text('Follow for the launch update.',1695,33),
    text('Development footage - review pending',1760,23,'0x9CB0B3'),
    text('Map data - OpenStreetMap contributors',1802,21,'0x9CB0B3'),
    'format=yuv420p[v]'
  ].join(',');
  // Existing project-owned Android capture and original synthesized score. No reference videos.
  await ffmpegRun(root,['-n','-ss',String(preset.start),'-t',String(preset.duration),'-i',path.join(repo,'issues/issues.mp4'),
    '-i',path.join(repo,'Store/Promo/geosniper-original-score.wav'),'-filter_complex',filters,
    '-map','[v]','-map','1:a:0','-t',String(preset.duration),'-af','volume=0.35,afade=t=out:st=10.5:d=1.5',
    '-c:v','libx264','-preset','fast','-crf','20','-maxrate','8M','-bufsize','16M','-g','60','-flags','+cgop',
    '-c:a','aac','-b:a','128k','-ar','48000','-movflags','+faststart',output]);
  await probeVideo(root,output); await createPoster(root,output); return filename;
}
