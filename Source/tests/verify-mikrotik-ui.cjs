const {chromium}=require('playwright');
const assert=require('assert'),path=require('path'),fs=require('fs');
(async()=>{
 const browser=await chromium.launch({headless:true,args:['--no-sandbox'],...(process.env.HUBTECH_TEST_BROWSER?{executablePath:process.env.HUBTECH_TEST_BROWSER}:{})});
 try{
  const page=await browser.newPage({viewport:{width:1100,height:720}});
  await page.addInitScript(()=>{let listener;window.__messages=[];window.__deliver=data=>listener({data});window.chrome={webview:{addEventListener:(name,fn)=>listener=fn,postMessage:m=>{window.__messages.push(m);if(m.action==='loadBackup')queueMicrotask(()=>listener({data:{type:'selected',name:m.name,hostname:'ScaleFiberSwitch2'}}));}}};});
  await page.goto('file://'+path.resolve(__dirname,'../HUBTechDiagramBuilder/wwwroot/index.html'));
  const bytes=Array.from(fs.readFileSync(process.argv[2]));
  await page.evaluate(bytes=>{const dt=new DataTransfer();dt.items.add(new File([new Uint8Array(bytes)],'Winthrop-switch2.backup'));document.body.dispatchEvent(new DragEvent('drop',{bubbles:true,cancelable:true,dataTransfer:dt}));},bytes);
  await page.waitForFunction(()=>window.__messages.some(m=>m.action==='loadBackup'));
  const message=await page.evaluate(()=>window.__messages.find(m=>m.action==='loadBackup'));
  assert(Buffer.from(message.text,'base64').equals(Buffer.from(bytes)),'Binary bytes must survive drag/drop unchanged');
  assert((await page.locator('#file').innerText()).includes('ScaleFiberSwitch2'));
  assert(!(await page.locator('#run').isDisabled()));
  await page.evaluate(()=>{const dt=new DataTransfer();dt.items.add(new File([new Uint8Array([239,168,145,115,0,0,0,0])],'encrypted.backup'));document.body.dispatchEvent(new DragEvent('drop',{bubbles:true,cancelable:true,dataTransfer:dt}));});
  await page.waitForFunction(()=>document.getElementById('status').textContent.includes('encrypted'));
  assert(!(await page.locator('#choose').isDisabled()));
  console.log('PASS binary drag/drop preserves exact backup bytes, loads hostname, enables generation and handles encrypted input without locking the UI.');
 }finally{await browser.close();}
})().catch(error=>{console.error(error);process.exit(1);});
