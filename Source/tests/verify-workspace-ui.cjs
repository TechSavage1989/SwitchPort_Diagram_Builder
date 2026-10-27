const {chromium}=require('playwright');
const path=require('path'),assert=require('assert'),fs=require('fs');
(async()=>{
 const browser=await chromium.launch({headless:true,...(process.env.SWITCHPORT_TEST_BROWSER?{executablePath:process.env.SWITCHPORT_TEST_BROWSER}:{})});
 try{
  const page=await browser.newPage({viewport:{width:1180,height:760}}),errors=[];
  page.on('pageerror',e=>errors.push(e.message));
  await page.addInitScript(()=>{let listener;window.__messages=[];window.__deliver=data=>listener({data});window.chrome={webview:{addEventListener:(name,fn)=>listener=fn,postMessage:message=>window.__messages.push(message)}};});
  await page.goto('file://'+path.resolve(__dirname,'../SwitchportDiagramBuilder/wwwroot/index.html'));
  const deliver=m=>page.evaluate(m=>window.__deliver(m),m),last=()=>page.evaluate(()=>window.__messages.at(-1));
  const item=(id,name,hostname,model)=>({id,name,hostname,model,dirty:false,error:''});
  const one=item('one','same.cfg','Access-A','JL256A'),two=item('two','same.cfg','Access-B','CRS326'),three=item('three','third.cfg','Core <b>literal</b>','J9851A');
  let items=[one,two,three];
  const workspace=async(selected='two',dirty=true)=>deliver({type:'workspace',items,selectedId:selected,dirty});
  const selected=async(id='two',chartAvailable=false,statusText='')=>{const i=items.find(i=>i.id===id);await deliver({type:'selected',id,name:i?.name||'',hostname:i?.hostname||'',chartAvailable,statusType:statusText?'complete':'',statusText});};
  const button=(id,action)=>page.locator(`button[data-item-id="${id}"][data-workspace-action="${action}"]`);
  assert(await page.locator('#runAll').isDisabled());assert(await page.locator('#saveWorkspace').isDisabled());
  await page.click('#openWorkspace');assert.deepEqual(await last(),{action:'openWorkspace'});
  await workspace();await selected();
  assert.equal(await page.locator('.workspace-row').count(),3);assert.equal(await page.locator('#workspaceList b').count(),0);
  assert.equal(await button('two','select').getAttribute('aria-current'),'true');
  assert(await button('one','up').isDisabled());assert(await button('three','down').isDisabled());
  assert((await page.locator('#workspaceList').innerText()).includes('same.cfg · CRS326'));assert(await page.locator('#workspaceDirty').isVisible());
  await button('two','up').click();assert.deepEqual(await last(),{action:'moveItem',id:'two',index:0});
  items=[two,one,three];await workspace();
  assert.equal(await button('two','select').getAttribute('aria-current'),'true');assert(await button('two','up').isDisabled());
  await button('two','down').focus();await page.keyboard.press('Enter');assert.deepEqual(await last(),{action:'moveItem',id:'two',index:1});
  items=[one,two,three];await workspace();
  await page.evaluate(()=>{const source=document.querySelector('li[data-item-id="three"]'),target=document.querySelector('li[data-item-id="one"]'),dataTransfer=new DataTransfer();source.dispatchEvent(new DragEvent('dragstart',{bubbles:true,cancelable:true,dataTransfer}));target.dispatchEvent(new DragEvent('dragover',{bubbles:true,cancelable:true,dataTransfer}));target.dispatchEvent(new DragEvent('drop',{bubbles:true,cancelable:true,dataTransfer}));source.dispatchEvent(new DragEvent('dragend',{bubbles:true,dataTransfer}));});
  assert.deepEqual(await last(),{action:'moveItem',id:'three',index:0});
  items=[three,one,two];await workspace();assert.equal(await button('two','select').getAttribute('aria-current'),'true');
  await button('one','select').click();assert.deepEqual(await last(),{action:'selectItem',id:'one'});assert(await page.locator('#run').isDisabled());
  await workspace('one');await selected('one',true,'A saved output\nOutput: A-folder');
  assert(!(await page.locator('#openChart').isDisabled()));assert.equal(await page.locator('#status').innerText(),'A saved output\nOutput: A-folder');
  await page.click('#copyOutput');assert.deepEqual(await last(),{action:'copyOutput',text:'A saved output\nOutput: A-folder'});await deliver({type:'outputCopied',message:'Copied.'});
  await button('two','select').click();await workspace('two');await selected('two',false,'B previous validation');
  assert(await page.locator('#openChart').isDisabled());assert.equal(await page.locator('#status').innerText(),'B previous validation');
  await button('one','select').click();await workspace('one');await selected('one',true,'A saved output\nOutput: A-folder');
  assert(!(await page.locator('#openChart').isDisabled()));
  await button('two','remove').click();assert.deepEqual(await last(),{action:'removeItem',id:'two'});
  // The native host owns confirmation. No optimistic removal occurs before its response.
  assert.equal(await page.locator('.workspace-row').count(),3);items=[three,one];await workspace('one');assert.equal(await button('one','select').getAttribute('aria-current'),'true');
  await page.click('#saveWorkspace');assert.deepEqual(await last(),{action:'saveWorkspace'});await workspace('one',false);assert(await page.locator('#workspaceDirty').isHidden());
  await deliver({type:'workspaceSaved',message:'Workspace saved securely.'});assert.equal(await page.locator('#status').innerText(),'A saved output\nOutput: A-folder');assert.equal(await page.locator('#importResult').innerText(),'Workspace saved securely.');
  console.log('PASS independent selection/output restoration, safe literal labels, duplicate filenames, reorder by keyboard and drag, host-confirmed removal and explicit save/open.');

  await page.evaluate(()=>{const dt=new DataTransfer();for(const [name,text] of [['first.cfg','hostname First'],['empty.cfg',''],['first.cfg','hostname Duplicate']])dt.items.add(new File([text],name));document.body.dispatchEvent(new DragEvent('drop',{bubbles:true,cancelable:true,dataTransfer:dt}));});
  await page.waitForFunction(()=>window.__messages.at(-1)?.action==='loadFiles');let message=await last();
  assert.deepEqual(message.files.map(f=>f.name),['first.cfg','empty.cfg','first.cfg']);assert.equal(message.files[0].text,'hostname First');assert(message.files[1].error.includes('empty'));assert.equal(message.files[2].text,'hostname Duplicate');
  assert(await page.locator('#paste').isDisabled());
  items.push(item('four','first.cfg','First','JL256A'));await workspace('four');await selected('four');
  assert(await page.locator('#paste').isDisabled(),'intermediate selection cannot unlock an in-flight import');
  items.push(item('five','first.cfg','Duplicate','JL256A'));await workspace('four');
  await deliver({type:'importComplete',message:'Added 2 configurations.',errors:['empty.cfg: The configuration is empty.']});
  assert(!(await page.locator('#paste').isDisabled()));assert.equal(await page.locator('.workspace-row').count(),4);assert((await page.locator('#importResult').innerText()).includes('empty.cfg'));
  await page.click('#paste');await page.locator('#pasteText').fill('hostname Pasted');await page.click('#usePaste');assert.deepEqual(await last(),{action:'loadText',text:'hostname Pasted',name:'Pasted configuration'});
  items.push(item('six','Pasted configuration','Pasted','JL256A'));await workspace('six');await selected('six');
  assert(await page.locator('#pasteDialog').isHidden());assert.equal(await page.locator('.workspace-row').count(),5);
  assert(await button('six','select').evaluate(el=>{const row=el.getBoundingClientRect(),list=document.getElementById('workspaceList').getBoundingClientRect();return row.top>=list.top&&row.bottom<=list.bottom;}),'new selected item is visible inside the scrollable list');
  console.log('PASS additive mixed-validity multi-drop, per-file errors, duplicate retention, import lock, and paste as a new item.');

  await selected('six',true,'Selected report result');await page.locator('#combinePdf').check();await page.click('#runAll');assert.deepEqual(await last(),{action:'runAll',combined:true});
  assert(await button('one','select').isDisabled());assert(await page.locator('#openWorkspace').isDisabled());assert(await page.locator('#saveWorkspace').isDisabled());
  await deliver({type:'batchComplete',message:'Built 5 configurations.',details:'1. Core\n2. Access-A\n3. First\n4. Duplicate\n5. Pasted',output:'Batch-folder'});
  assert.equal(await page.locator('#status').innerText(),'Selected report result');assert(!(await page.locator('#openBatch').isDisabled()));
  await page.click('#openBatch');assert.deepEqual(await last(),{action:'openBatch'});
  await page.click('#copyOutput');assert.deepEqual(await last(),{action:'copyOutput',text:'Selected report result'});await deliver({type:'outputCopied',message:'Copied.'});
  await page.locator('#combinePdf').uncheck();await page.click('#runAll');assert.deepEqual(await last(),{action:'runAll',combined:false});await deliver({type:'batchComplete',message:'Built all.',output:'Next-batch'});
  console.log('PASS batch and selected output separation, combined-PDF opt-in, ordered summary and generation locks.');

  fs.mkdirSync('build',{recursive:true});
  await deliver({type:'importComplete',message:'',errors:[]});
  for(const viewport of [{width:1180,height:760},{width:900,height:650},{width:884,height:611}]){
   await page.setViewportSize(viewport);
   await page.waitForFunction(()=>{const selected=document.querySelector('.workspace-row.selected'),list=document.getElementById('workspaceList');if(!selected)return false;const row=selected.getBoundingClientRect(),bounds=list.getBoundingClientRect();return row.top>=bounds.top&&row.bottom<=bounds.bottom;});
   const bounds=await page.evaluate(()=>({width:innerWidth,height:innerHeight,scrollWidth:document.documentElement.scrollWidth,scrollHeight:document.documentElement.scrollHeight,listScroll:document.getElementById('workspaceList').scrollHeight,listClient:document.getElementById('workspaceList').clientHeight}));
   assert(bounds.scrollWidth<=bounds.width+1,JSON.stringify(bounds));
   for(const id of ['choose','paste','run','runAll','saveWorkspace','openWorkspace'])assert(await page.locator('#'+id).evaluate(el=>{const b=el.getBoundingClientRect();return b.top>=0&&b.bottom<=innerHeight&&b.left>=0&&b.right<=innerWidth;}),id+' fits '+viewport.width+'x'+viewport.height);
   assert(bounds.listScroll>bounds.listClient,'long configuration list scrolls inside its panel');
   await page.screenshot({path:'build/workspace-'+viewport.width+'x'+viewport.height+'.png',fullPage:true});
  }
  await deliver({type:'workspace',items,selectedId:'six',dirty:false,batchAvailable:false});assert(await page.locator('#batchResult').isHidden());assert(await page.locator('#openBatch').isDisabled());
  items=[];await workspace(null,false);await deliver({type:'selected',id:null,name:''});assert(await page.locator('#run').isDisabled());assert(await page.locator('#runAll').isDisabled());assert(await page.locator('#workspaceEmpty').isVisible());
  assert.deepEqual(errors,[]);console.log('PASS normal/small-window controls, bounded ordered-list scrolling, empty workspace and no browser errors.');
 }finally{await browser.close();}
})().catch(error=>{console.error(error);process.exit(1)});
