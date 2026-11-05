const {chromium}=require('playwright');
const path=require('path'),assert=require('assert');
(async()=>{
 const browser=await chromium.launch({headless:true,args:['--no-sandbox'],
  ...(process.env.SWITCHPORT_TEST_BROWSER?{executablePath:process.env.SWITCHPORT_TEST_BROWSER}:{})});
 try{
  const page=await browser.newPage({viewport:{width:1060,height:740}}),errors=[];
  page.on('pageerror',error=>errors.push(error.message));
  await page.addInitScript(()=>{
   let listener;window.__messages=[];
   window.__deliver=m=>listener({data:m});
   window.chrome={webview:{addEventListener:(event,handler)=>listener=handler,
    postMessage:message=>window.__messages.push(message)}};
  });
  await page.goto('file://'+path.resolve(__dirname,'../SwitchportDiagramBuilder/wwwroot/index.html'));
  const deliver=m=>page.evaluate(m=>window.__deliver(m),m);
  const last=()=>page.evaluate(()=>window.__messages.at(-1));
  const synthetic=[0x50,0x4b,0x03,0x04,0x01,0x02,0x03,0x04];
  const first={id:'one',name:'lab-one.cfg',hostname:'LAB-SW-01',model:'JL256A',supplementCount:0},
        second={id:'two',name:'lab-two.cfg',hostname:'LAB-SW-02',model:'JL256A',supplementCount:0};
  await deliver({type:'workspace',items:[first,second],selectedId:'one',dirty:false});
  await deliver({type:'selected',id:'one',name:'lab-one.cfg',hostname:'LAB-SW-01',supplementary:[]});
  const drop=async(files,target='body')=>{
   const before=await page.evaluate(()=>window.__messages.length);
   await page.evaluate(({files,target})=>{
   const dt=new DataTransfer();
   for(const file of files){
    const data=file.bytes?new Uint8Array(file.bytes):file.text;
    dt.items.add(new File([data],file.name));
   }
   const element=target==='body'?document.body:document.querySelector(target);
   element.dispatchEvent(new DragEvent('dragenter',{bubbles:true,cancelable:true,dataTransfer:dt}));
   const overlay=document.getElementById('dropOverlay');
   window.__overlayText=overlay.innerText;
   const evt=new DragEvent('drop',{bubbles:true,cancelable:true,dataTransfer:dt});
   element.dispatchEvent(evt);window.__dropCancelled=evt.defaultPrevented;
   },{files,target});
   return before;
  };
  const waitForAction=(before,action)=>page.waitForFunction(({before,action})=>window.__messages.length>before&&window.__messages.at(-1)?.action===action,{before,action});
  const clear=async()=>deliver({type:'importComplete',message:'Imported network data.',errors:[]});

  let before=await drop([{name:'auvik.xlsx',bytes:synthetic}]);
  await waitForAction(before,'loadFiles');
  let msg=await last();
  assert.equal(msg.files.length,1);
  assert.equal(msg.files[0].encoding,'xlsx-base64');
  assert.deepEqual([...Buffer.from(msg.files[0].text,'base64')],synthetic);
  assert.equal(msg.id,'one');assert.equal(msg.supplementaryOnly,false);
  assert((await page.evaluate(()=>window.__overlayText)).includes('Auvik reports'));
  assert(await page.evaluate(()=>window.__dropCancelled));
  assert.equal(await page.locator('.workspace-row').count(),2,'Workbook must not add another configuration in the browser');
  await deliver({type:'selected',id:'one',name:'lab-one.cfg',hostname:'LAB-SW-01',
   supplementary:[{id:'a'.repeat(32),name:'auvik.xlsx',kind:'Auvik workbook',count:12,categories:['Neighbor']}]});
  await clear();
  assert.equal(await page.locator('#supplementary').innerText(),'Supplementary (1)');

  before=await drop([{name:'port-details.log',text:'LAB-SW-02#show interfaces\nGi1/0/2 is up, line protocol is up\n'}]);
  await waitForAction(before,'loadFiles');
  msg=await last();assert.equal(msg.files[0].encoding,'text');assert.equal(msg.id,'one');
  assert(msg.files[0].text.includes('show interfaces'));await clear();

  before=await drop([{name:'lab-three.cfg',text:'hostname LAB-SW-03\ninterface 1/1/1\n no shutdown\n'}]);
  await waitForAction(before,'loadText');
  msg=await last();assert.equal(msg.name,'lab-three.cfg');assert(msg.text.includes('hostname LAB-SW-03'));
  await deliver({type:'selected',id:'one',name:'lab-one.cfg',hostname:'LAB-SW-01',supplementary:[]});

  before=await drop([
   {name:'site-report.xlsx',bytes:synthetic},
   {name:'lab-four.cfg',text:'hostname LAB-SW-04\n'},
   {name:'show-relays.txt',text:'LAB-SW-01#show ip dhcp relay\nip helper-address 192.0.2.53\n'}
  ],'.workspace-row[data-item-id="two"]');
  await waitForAction(before,'loadFiles');
  msg=await last();
  assert.deepEqual(msg.files.map(file=>file.name),['site-report.xlsx','lab-four.cfg','show-relays.txt']);
  assert.equal(msg.id,'two','External drop onto a workspace row targets that device when an explicit selection is needed');
  assert.equal(msg.files[0].encoding,'xlsx-base64');assert.equal(msg.files[1].encoding,'text');
  await deliver({type:'selected',id:'one',name:'lab-one.cfg',hostname:'LAB-SW-01',supplementary:[]});await clear();

  // A workbook dropped into the existing modal must still import, not be rejected
  // as an attempt to open the configuration editor.
  await page.click('#supplementary');assert(await page.locator('#supplementaryDialog').isVisible());
  before=await drop([{name:'closet.xlsx',bytes:synthetic}],'#supplementaryDialog');
  await waitForAction(before,'loadFiles');
  msg=await last();assert.equal(msg.supplementaryOnly,true);assert.equal(msg.id,'one');
  await clear();assert((await page.locator('#supplementaryMessage').innerText()).includes('Imported network data.'));

  before=await drop([{name:'not-a-supplement.cfg',text:'hostname LAB-SW-05\n'}],'#supplementaryDialog');
  await waitForAction(before,'loadFiles');
  msg=await last();assert.equal(msg.supplementaryOnly,true);
  await deliver({type:'importComplete',message:'No supplementary files imported.',errors:['Primary configurations are not accepted here.']});
  assert((await page.locator('#supplementaryMessage').innerText()).includes('not accepted'));
  assert(await page.locator('#supplementaryDialog').isVisible());
  await page.click('#suppClose');

  const messagesBefore=await drop([{name:'bad.xlsx',text:'not a ZIP archive'}]);
  await page.waitForFunction(()=>document.getElementById('status').className==='error');
  assert.equal(await page.evaluate(()=>window.__messages.length),messagesBefore,'Malformed .xlsx rejected before native import');
  assert.equal(errors.length,0,'Browser errors: '+errors.join('; '));
  console.log('PASS Auvik/CLI drag auto-detection, binary fidelity, row targeting, mixed drops, primary config compatibility, dialog drops and malformed input guards.');
 }finally{await browser.close();}
})().catch(err=>{console.error(err);process.exitCode=1;});
