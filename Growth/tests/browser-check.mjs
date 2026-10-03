// Optional development check. Pass an installed Playwright module's absolute index.mjs path.
import {pathToFileURL} from 'node:url';
import fs from 'node:fs';
import path from 'node:path';
import assert from 'node:assert/strict';
import {root} from '../server.mjs';
const {chromium}=await import(pathToFileURL(process.argv[2]).href);
const browser=await chromium.launch({headless:true,...(process.argv[3]?{executablePath:process.argv[3]}:{})});
const page=await browser.newPage({viewport:{width:1440,height:1050},deviceScaleFactor:1});
const errors=[];page.on('pageerror',error=>errors.push(error.message));
const out=path.join(root,'data/qa');fs.mkdirSync(out,{recursive:true});
try {
  await page.goto('http://127.0.0.1:4318');await page.getByRole('heading',{name:'Make your city the hook.'}).waitFor();
  await page.screenshot({path:path.join(out,'overview-desktop.png'),fullPage:true});
  for(const section of ['calendar','studio','results','settings']) {
    await page.locator(`[data-page="${section}"]`).click();await page.locator('h1').waitFor();
    if(section==='calendar') {
      assert.equal(await page.locator('.post-card').count(),24);
      await page.locator('[data-action="edit"]').first().click();await page.locator('#editor').waitFor();
      await page.getByRole('button',{name:'Approve saved draft'}).click();
      await page.locator('#toast.error').waitFor();assert.match(await page.locator('#toast').innerText(),/registered video/);
      await page.getByRole('button',{name:'Close editor'}).click();
      await page.locator('#toast').waitFor({state:'hidden'});
    }
    if(section==='studio') {
      assert.equal(await page.locator('.asset-card').count(),3);
      await page.waitForFunction(()=>[...document.querySelectorAll('video')].every(v=>v.readyState>=1));
      assert.deepEqual(await page.locator('video').evaluateAll(videos=>videos.map(v=>[v.videoWidth,v.videoHeight])),[[1080,1920],[1080,1920],[1080,1920]]);
      await page.locator('video').first().evaluate(video=>{video.muted=true;return video.play();});
      await page.waitForFunction(()=>document.querySelector('video')?.currentTime>6);
      await page.locator('video').first().evaluate(video=>video.pause());
    }
    await page.screenshot({path:path.join(out,`${section}-desktop.png`),fullPage:true});
    assert.equal(await page.evaluate(()=>document.documentElement.scrollWidth>innerWidth),false,section+' overflows');
  }
  await page.setViewportSize({width:390,height:844});await page.locator('[data-page="overview"]').click();
  await page.screenshot({path:path.join(out,'overview-mobile.png'),fullPage:true});
  assert.equal(await page.evaluate(()=>document.documentElement.scrollWidth>innerWidth),false,'Mobile overflows');
  assert.deepEqual(errors,[]);console.log('Browser checks passed: all five views, draft approval guard, three video dimensions, desktop/mobile overflow, no JS errors.');
}finally{await browser.close();}
