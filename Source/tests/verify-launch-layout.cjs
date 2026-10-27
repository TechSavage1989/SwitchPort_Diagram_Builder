const {chromium}=require('playwright');
const path=require('path'),assert=require('assert');
(async()=>{
 const browser=await chromium.launch({headless:true,args:['--no-sandbox'],...(process.env.SWITCHPORT_TEST_BROWSER?{executablePath:process.env.SWITCHPORT_TEST_BROWSER}:{})});
 try{
  for(const viewport of [{width:884,height:611},{width:1264,height:811},{width:1164,height:721},{width:1000,height:700},{width:786,height:460},{width:660,height:490},{width:600,height:430}]){
   const page=await browser.newPage({viewport});
   await page.addInitScript(()=>{let listener;window.__switchportMessages=[];window.__switchportDeliver=data=>listener({data});window.chrome={webview:{addEventListener:(name,fn)=>listener=fn,postMessage:msg=>{window.__switchportMessages.push(msg);if(msg.action==='ready')queueMicrotask(()=>listener({data:{type:'outputSelected',path:'C:/Users/Engineer/Documents/Network Reports'}}));}}};});
   await page.goto('file://'+path.resolve(__dirname,'../SwitchportDiagramBuilder/wwwroot/index.html'));
   const result=await page.evaluate(()=>({height:innerHeight,width:innerWidth,scrollHeight:document.documentElement.scrollHeight,scrollWidth:document.documentElement.scrollWidth,controls:[...document.querySelectorAll('button,input')].filter(el=>el.getClientRects().length).map(el=>{const b=el.getBoundingClientRect();return {id:el.id,top:b.top,bottom:b.bottom,left:b.left,right:b.right}}),branding:document.body.innerText}));
   assert(result.scrollHeight<=result.height+1,JSON.stringify(result));assert(result.scrollWidth<=result.width+1,JSON.stringify(result));
   for(const b of result.controls)assert(b.top>=0&&b.bottom<=result.height&&b.left>=0&&b.right<=result.width,b.id+' outside launch viewport');
   assert(!/webview2/i.test(result.branding));
   console.log('PASS launch layout without page scrollbar:',viewport.width+'x'+viewport.height);
   if(viewport.width===884){
    assert(await page.locator('#openChart').isDisabled());
    await page.evaluate(()=>{window.__switchportDeliver({type:'models',models:[{sku:'JG932A',name:'HPE 5130 EI 24G'},{sku:'JL675A',name:'Aruba 6100 48G'}]});window.__switchportDeliver({type:'selected',path:'C:/Configs/Lab config.txt',name:'Lab config.txt',hostname:'Client-Access'});});
    assert((await page.locator('#file').innerText()).includes('Client-Access'));
    await page.click('#run');
    const payload=await page.evaluate(()=>window.__switchportMessages.at(-1));
    assert.deepStrictEqual(payload,{action:'run'});
    assert(await page.locator('#devices').isDisabled());
    assert(await page.locator('#openChart').isDisabled());
    await page.evaluate(()=>window.__switchportDeliver({type:'blocked',message:'Review inventory'}));
    assert(!(await page.locator('#devices').isDisabled()));
    assert(await page.locator('#openChart').isDisabled());
    await page.evaluate(()=>window.__switchportDeliver({type:'complete',message:'Reports created.',chartAvailable:false}));
    assert(await page.locator('#openChart').isDisabled());
    await page.evaluate(()=>window.__switchportDeliver({type:'complete',message:'Reports created.',chartAvailable:true}));
    assert(!(await page.locator('#openChart').isDisabled()));
    await page.click('#openChart');
    assert.deepStrictEqual(await page.evaluate(()=>window.__switchportMessages.at(-1)),{action:'openChart'});
    await page.evaluate(()=>window.__switchportDeliver({type:'complete',message:'Reports created.',chartAvailable:true}));
    await page.evaluate(()=>window.__switchportDeliver({type:'selected',path:'C:/Configs/Next.txt',name:'Next.txt'}));
    assert(!(await page.locator('#openChart').isDisabled()));
    assert((await page.locator('#file').innerText())==='Next.txt');
    await page.evaluate(()=>{window.__switchportDeliver({type:'complete',message:'Reports created.',chartAvailable:true});window.__switchportDeliver({type:'outputSelected',path:'C:/Reports'});});
    assert(!(await page.locator('#openChart').isDisabled()));
    await page.evaluate(()=>{window.__switchportDeliver({type:'complete',message:'Reports created.',chartAvailable:true});window.__switchportDeliver({type:'chartUnavailable',message:'Build reports again.'});});
    assert(await page.locator('#openChart').isDisabled());
    await page.evaluate(()=>{window.__switchportDeliver({type:'selected',path:'C:/Configs/Lab config.txt',name:'Lab config.txt',hostname:'Client-Access'});window.__switchportDeliver({type:'complete',message:'Reports created.',chartAvailable:true});});
    console.log('PASS Device details action, hostname display, Open chart action and stale/missing/blocked chart states');
   }
   if(process.env.Switchport_TEST_SCREENSHOT&&viewport.width===884)await page.screenshot({path:process.env.Switchport_TEST_SCREENSHOT});
   await page.close();
  }
 }finally{await browser.close();}
})().catch(error=>{console.error(error);process.exit(1)});
