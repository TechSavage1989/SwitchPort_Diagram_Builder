const {chromium}=require('playwright');
const path=require('path'),assert=require('assert'),fs=require('fs');
(async()=>{
 const browser=await chromium.launch({headless:true,args:['--no-sandbox'],...(process.env.SWITCHPORT_TEST_BROWSER?{executablePath:process.env.SWITCHPORT_TEST_BROWSER}:{})});
 try{
  const page=await browser.newPage({viewport:{width:884,height:611}});
  await page.addInitScript(()=>{let listener;window.__switchportMessages=[];window.__switchportDeliver=data=>listener({data});window.chrome={webview:{addEventListener:(name,fn)=>listener=fn,postMessage:msg=>window.__switchportMessages.push(msg)}};});
  await page.goto('file://'+path.resolve(__dirname,'../SwitchportDiagramBuilder/wwwroot/index.html'));
  assert(await page.locator('.app-logo').evaluate(el=>el.complete&&el.naturalWidth>0&&el.naturalHeight>0));
  assert(!(await page.locator('#removeLogo').isVisible()));
  await page.click('#chooseLogo');assert.equal((await page.evaluate(()=>window.__switchportMessages.at(-1))).action,'chooseLogo');
  const preview='data:image/png;base64,'+fs.readFileSync(path.resolve(__dirname,'../Branding/Switchport_Diagram_Builder_Icon.png')).toString('base64');
  await page.evaluate(preview=>{
   window.__switchportDeliver({type:'selected',path:'C:/Configs/Client.txt',name:'Client.txt',hostname:'Client-Access'});
   window.__switchportDeliver({type:'complete',message:'Reports created.',chartAvailable:true});
   window.__switchportDeliver({type:'logoSelected',name:'Client logo.png',preview});
  },preview);
  assert(await page.locator('#logoPreview').isVisible());assert(await page.locator('#removeLogo').isVisible());
  assert.equal(await page.locator('#logoName').innerText(),'Client logo.png');assert(!(await page.locator('#openChart').isDisabled()),'logo edits preserve generated project history');
  assert(await page.locator('#logoPreview').evaluate(el=>el.complete&&el.naturalWidth>0));
  if(process.env.Switchport_TEST_SCREENSHOT)await page.screenshot({path:process.env.Switchport_TEST_SCREENSHOT});
  await page.click('#run');assert(await page.locator('#chooseLogo').isDisabled());assert(await page.locator('#removeLogo').isDisabled());
  await page.evaluate(()=>window.__switchportDeliver({type:'complete',message:'Reports created.',chartAvailable:true}));
  await page.click('#removeLogo');assert.equal((await page.evaluate(()=>window.__switchportMessages.at(-1))).action,'removeLogo');
  await page.evaluate(()=>window.__switchportDeliver({type:'logoSelected',name:'',preview:''}));
  assert(!(await page.locator('#logoPreview').isVisible()));assert(!(await page.locator('#removeLogo').isVisible()));assert(!(await page.locator('#openChart').isDisabled()),'logo edits preserve generated project history');
  await page.evaluate(preview=>window.__switchportDeliver({type:'logoSelected',name:'Saved logo.png',preview}),preview);
  await page.setViewportSize({width:600,height:430});
  assert(await page.evaluate(()=>document.documentElement.scrollHeight<=innerHeight+1));
  console.log('PASS app SVG display, logo selection/preview/removal, restored logo state, latest chart history retention, generation locks and compact layout.');
 }finally{await browser.close();}
})().catch(error=>{console.error(error);process.exit(1)});
