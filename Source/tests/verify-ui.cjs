const {chromium}=require('playwright');
const path=require('path'),assert=require('assert');
(async()=>{
 const browser=await chromium.launch({headless:true,args:['--no-sandbox'],...(process.env.SWITCHPORT_TEST_BROWSER?{executablePath:process.env.SWITCHPORT_TEST_BROWSER}:{})});
 try{
  const page=await browser.newPage({viewport:{width:884,height:611}});
  await page.addInitScript(()=>{
   let listener;window.__switchportMessages=[];
   window.__switchportDeliver=data=>listener({data});
   window.chrome={webview:{addEventListener:(name,fn)=>listener=fn,postMessage:msg=>{
    window.__switchportMessages.push(msg);
    if(msg.action==='loadText')queueMicrotask(()=>listener({data:{type:'selected',path:'',name:msg.name,hostname:msg.text.match(/hostname\s+"?([^"\r\n]+)/i)?.[1]||''}}));
   }}};
  });
  await page.goto('file://'+path.resolve(__dirname,'../SwitchportDiagramBuilder/wwwroot/index.html'));
  await page.evaluate(()=>window.__switchportDeliver({type:'outputSelected',path:'D:/Client Reports'}));
  assert(await page.locator('#run').isDisabled());
  await page.click('#paste');assert(await page.locator('#pasteDialog').isVisible());
  assert(await page.locator('#usePaste').isDisabled());
  assert.equal(await page.evaluate(()=>document.activeElement.id),'pasteText');
  await page.locator('#pasteText').fill('   \n');assert(await page.locator('#usePaste').isDisabled());
  const config='; JL256A Configuration Editor\r\nhostname "Paste-Lab"\r\ntrunk 49-50 trk1 lacp\r\nvlan 10\r\n untagged 1-48\r\n exit\r\n';
  await page.locator('#pasteText').fill(config);
  const entered=await page.locator('#pasteText').inputValue();
  if(process.env.Switchport_TEST_SCREENSHOT)await page.screenshot({path:process.env.Switchport_TEST_SCREENSHOT.replace('.png','-paste.png')});
  await page.click('#usePaste');await page.waitForFunction(()=>!document.getElementById('pasteDialog').open);
  assert.equal((await page.evaluate(()=>window.__switchportMessages.findLast(m=>m.action==='loadText'))).text,entered);
  assert((await page.locator('#file').innerText()).includes('Paste-Lab'));
  assert(!(await page.locator('#run').isDisabled()));
  await page.click('#run');
  assert.deepStrictEqual(await page.evaluate(()=>window.__switchportMessages.at(-1)),{action:'run'});
  assert(await page.locator('#paste').isDisabled());
  let count=await page.evaluate(()=>window.__switchportMessages.length);
  await page.evaluate(()=>{const dt=new DataTransfer();dt.items.add(new File(['hostname Busy-Lab'],'busy.cfg'));document.body.dispatchEvent(new DragEvent('drop',{bubbles:true,cancelable:true,dataTransfer:dt}));});
  assert.equal(await page.evaluate(()=>window.__switchportMessages.length),count);
  await page.evaluate(()=>window.__switchportDeliver({type:'complete',message:'Reports created.',chartAvailable:true}));
  assert(!(await page.locator('#openChart').isDisabled()));
  console.log('PASS paste dialog, empty-text guard, focus, text fidelity, hostname and generation lock.');
  const drop=async(options)=>{
   await page.evaluate(({text,name,encoding,multiple,oversized})=>{
    const dt=new DataTransfer();let bytes=text;
    if(encoding==='utf16'){
     const values=[255,254];for(const char of text)values.push(char.charCodeAt(0)&255,char.charCodeAt(0)>>8);bytes=new Uint8Array(values);
    }
    dt.items.add(new File([oversized?new Uint8Array(20_000_001):bytes],name));
    if(multiple)dt.items.add(new File(['hostname Second'],'second.txt'));
    document.body.dispatchEvent(new DragEvent('dragenter',{bubbles:true,cancelable:true,dataTransfer:dt}));
    document.body.dispatchEvent(new DragEvent('dragover',{bubbles:true,cancelable:true,dataTransfer:dt}));
    const event=new DragEvent('drop',{bubbles:true,cancelable:true,dataTransfer:dt});document.body.dispatchEvent(event);window.__dropPrevented=event.defaultPrevented;
   },options);
  };
  await drop({text:'hostname "Drop-Lab"\nvlan 10\n',name:'Client config with spaces.cfg'});
  await page.waitForFunction(()=>document.getElementById('file').textContent.includes('Drop-Lab'));
  assert((await page.locator('#file').innerText()).includes('Client config with spaces.cfg'));
  assert(await page.locator('#openChart').isDisabled());assert(await page.evaluate(()=>window.__dropPrevented));
  assert(!(await page.evaluate(()=>document.body.classList.contains('drop-active'))));
  await drop({text:'hostname "UTF16-Lab"\r\n',name:'UTF16.conf',encoding:'utf16'});
  await page.waitForFunction(()=>document.getElementById('file').textContent.includes('UTF16-Lab'));
  assert.equal((await page.evaluate(()=>window.__switchportMessages.at(-1))).text,'hostname "UTF16-Lab"\r\n');
  count=await page.evaluate(()=>window.__switchportMessages.length);
  for(const options of [{text:'config',name:'one.txt',multiple:true},{text:'',name:'empty.txt'},{text:'config',name:'large.txt',oversized:true}]){
   await drop(options);await page.waitForFunction(()=>document.getElementById('status').className==='error');
   assert.equal(await page.evaluate(()=>window.__switchportMessages.length),count);
   assert(!(await page.locator('#run').isDisabled()));
   assert((await page.locator('#file').innerText()).includes('UTF16-Lab'));
  }
  console.log('PASS file drop, UTF-16 decoding, no navigation, stale chart reset, multi-file/empty/20 MB guards and retained selection.');
  await page.evaluate(()=>{const dt=new DataTransfer();dt.setData('text/plain','hostname Text-Drop\n');document.body.dispatchEvent(new DragEvent('drop',{bubbles:true,cancelable:true,dataTransfer:dt}));});
  await page.waitForFunction(()=>document.getElementById('file').textContent.includes('Text-Drop'));
  await page.click('#paste');await page.locator('#pasteText').fill('hostname Unused');await page.keyboard.press('Escape');
  assert((await page.locator('#file').innerText()).includes('Text-Drop'));
  await page.evaluate(()=>window.__switchportDeliver({type:'selected',path:'C:/Configs/File.txt',name:'File.txt',hostname:'File-Lab'}));
  await page.click('#run');await page.evaluate(()=>window.__switchportDeliver({type:'blocked',message:'Generation blocked.'}));
  assert(!(await page.locator('#paste').isDisabled()));
  assert(await page.locator('#openChart').isDisabled());
  if(process.env.Switchport_TEST_SCREENSHOT)await page.screenshot({path:process.env.Switchport_TEST_SCREENSHOT});
  console.log('PASS text drop, modal cancel, file reselection and blocked-run recovery.');
 }finally{await browser.close();}
})().catch(error=>{console.error(error);process.exit(1)});
