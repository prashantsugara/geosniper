import path from 'node:path';
import fs from 'node:fs';
import {Store} from './lib/store.mjs';
import {Growth} from './lib/core.mjs';
import {report} from './lib/worker.mjs';
import {renderPreset,presets} from './lib/render.mjs';
import {root,loadEnv} from './server.mjs';

loadEnv();
const store=new Store(path.join(root,'data/growth.sqlite')), growth=new Growth(store,root);
try {
  switch(process.argv[2]) {
    case 'plan':console.log(growth.plan(process.argv[3]||new Date(Date.now()+86400000).toISOString().slice(0,10)));break;
    case 'status':console.log(JSON.stringify({settings:store.settings(),posts:store.posts().map(({id,status})=>({id,status}))},null,2));break;
    case 'report': {
      fs.mkdirSync(path.join(root,'exports'),{recursive:true});const file=path.join(root,'exports',`report-${new Date().toISOString().slice(0,10)}.md`);fs.writeFileSync(file,report(growth));console.log(file);break;
    }
    case 'render': {
      for(const preset of presets.filter(p=>!process.argv[3] || p.id===process.argv[3])) {const name=await renderPreset(root,preset.id);await growth.register(name);console.log(`Draft rendered: ${name}`);}break;
    }
    default:console.log('node Growth/cli.mjs plan [YYYY-MM-DD] | render [gps|scope|streets] | status | report');
  }
} finally {store.close();}
