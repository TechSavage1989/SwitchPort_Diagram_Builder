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
  const publish=patch=>deliver({type:'project',available:true,chartAvailable:true,folderAvailable:true,combined:false,pageCount:1,pdfAvailable:false,stale:false,label:'Access-A',output:'A-folder',summary:'A saved output\nOutput: A-folder',...patch});
  const previewProject=async()=>{await page.click('#openChart');assert.deepEqual(await last(),{action:'openChart'});assert(await page.locator('#combinedPreviewDialog').isHidden(),'Open chart requests a native PNG, never a PDF or modal');await page.click('#previewSheets');return last();};
  const button=(id,action)=>page.locator(`button[data-item-id="${id}"][data-workspace-action="${action}"]`);
  assert(await page.locator('#runAll').isDisabled());assert(await page.locator('#saveWorkspace').isDisabled());assert(await page.locator('#combinePdf').isChecked(),'shared A4 sheets are enabled by default');
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
  await workspace('one');await selected('one',true,'A saved output\nOutput: A-folder');await publish({});
  assert(!(await page.locator('#openChart').isDisabled()));assert.equal(await page.locator('#status').innerText(),'A saved output\nOutput: A-folder');
  await page.click('#copyOutput');assert.deepEqual(await last(),{action:'copyOutput',text:'A saved output\nOutput: A-folder'});await deliver({type:'outputCopied',message:'Copied.'});
  await button('two','select').click();await workspace('two');await selected('two',false,'B previous validation');
  assert(!(await page.locator('#openChart').isDisabled()));assert.equal(await page.locator('#status').innerText(),'A saved output\nOutput: A-folder');
  await button('one','select').click();await workspace('one');await selected('one',true,'A saved output\nOutput: A-folder');
  assert(!(await page.locator('#openChart').isDisabled()));
  await button('two','remove').click();assert.deepEqual(await last(),{action:'removeItem',id:'two'});
  // Native removal is immediate/reversible; no optimistic list edits before its response.
  assert.equal(await page.locator('.workspace-row').count(),3);items=[three,one];await workspace('one');assert.equal(await button('one','select').getAttribute('aria-current'),'true');
  await deliver({type:'workspace',items,selectedId:'one',dirty:true,canUndo:true,undoLabel:'Undo removal'});
  assert(!(await page.locator('#clearWorkspace').isDisabled()));await page.click('#undoWorkspace');assert.deepEqual(await last(),{action:'undoWorkspace'});
  await page.click('#clearWorkspace');assert.deepEqual(await last(),{action:'clearWorkspace'});
  await deliver({type:'workspace',items:[],selectedId:null,dirty:true,canUndo:true,undoLabel:'Undo clear all'});await deliver({type:'selected',id:null,name:''});
  await publish({stale:true,emptyWorkspace:true});assert((await page.locator('#projectStale').innerText()).includes('Previous project'));assert(!(await page.locator('#openChart').isDisabled()));assert(await page.locator('#clearWorkspace').isDisabled());
  await page.click('#undoWorkspace');assert.deepEqual(await last(),{action:'undoWorkspace'});await workspace('one');await selected('one');
  await page.click('#saveWorkspace');assert.deepEqual(await last(),{action:'saveWorkspace'});await workspace('one',false);assert(await page.locator('#workspaceDirty').isHidden());
  await deliver({type:'workspaceSaved',message:'Workspace saved securely.'});assert.equal(await page.locator('#status').innerText(),'A saved output\nOutput: A-folder');assert.equal(await page.locator('#importResult').innerText(),'Workspace saved securely.');
  console.log('PASS independent selection/output restoration, safe literal labels, duplicate filenames, reorder by keyboard and drag, host-confirmed removal and explicit save/open.');

  await deliver({type:'workspace',items,selectedId:'one',dirty:false,outputLabel:'',suggestedLabel:'Campus access',effectiveLabel:'Campus access',workspaceName:'Campus access.spworkspace.json'});
  assert.equal(await page.locator('#workspaceOutputName').innerText(),'Project name: Campus access');
  await page.click('#editWorkspaceLabel');assert.equal(await page.locator('#workspaceLabelInput').inputValue(),'');assert(!(await page.locator('#showWorkspaceTitle').isChecked()));assert.equal(await page.locator('#workspaceLabelInput').getAttribute('placeholder'),'Campus access');
  await page.locator('#workspaceLabelInput').fill('Unused label');await page.click('#cancelWorkspaceLabel');
  assert.equal(await page.evaluate(()=>window.__messages.filter(m=>m.action==='setWorkspaceLabel').length),0);
  await page.click('#editWorkspaceLabel');await page.locator('#workspaceLabelInput').fill('  North campus <b>literal</b>  ');await page.click('#applyWorkspaceLabel');
  assert.deepEqual(await page.evaluate(()=>window.__messages.findLast(m=>m.action==='setWorkspaceLabel')),{action:'setWorkspaceLabel',text:'North campus <b>literal</b>'});
  await deliver({type:'workspace',items,selectedId:'one',dirty:true,outputLabel:'North campus <b>literal</b>',suggestedLabel:'Campus access',effectiveLabel:'North campus <b>literal</b>',workspaceName:'Campus access.spworkspace.json'});
  assert.equal(await page.locator('#workspaceOutputName b').count(),0);assert.equal(await page.locator('#status').innerText(),'A saved output\nOutput: A-folder');
  await page.click('#editWorkspaceLabel');assert.equal(await page.locator('#workspaceLabelInput').inputValue(),'North campus <b>literal</b>');await page.locator('#workspaceLabelInput').fill('');await page.keyboard.press('Enter');
  assert.deepEqual(await page.evaluate(()=>window.__messages.findLast(m=>m.action==='setWorkspaceLabel')),{action:'setWorkspaceLabel',text:''});
  await deliver({type:'workspace',items,selectedId:'one',dirty:true,outputLabel:'',suggestedLabel:'Access-A + Core',effectiveLabel:'Access-A + Core',workspaceName:''});
  assert.equal(await page.locator('#workspaceOutputName').innerText(),'Project name: Access-A + Core');
  await page.click('#editWorkspaceLabel');await page.locator('#showWorkspaceTitle').check();await page.click('#cancelWorkspaceLabel');
  assert.equal(await page.evaluate(()=>window.__messages.filter(m=>m.action==='setWorkspaceTitle').length),0,'Cancel does not change title preference');
  await page.click('#editWorkspaceLabel');await page.locator('#showWorkspaceTitle').check();await page.click('#applyWorkspaceLabel');
  assert.deepEqual(await page.evaluate(()=>window.__messages.findLast(m=>m.action==='setWorkspaceTitle')),{action:'setWorkspaceTitle',enabled:true});
  await deliver({type:'workspace',items,selectedId:'one',dirty:true,outputLabel:'Output label only',suggestedLabel:'West Closet',effectiveLabel:'Output label only',showWorkspaceTitle:true});
  await page.click('#editWorkspaceLabel');assert(await page.locator('#showWorkspaceTitle').isChecked());assert((await page.locator('#workspaceTitlePreview').innerText()).includes('West Closet'));assert(!(await page.locator('#workspaceTitlePreview').innerText()).includes('Output label only'));await page.click('#cancelWorkspaceLabel');
  console.log('PASS optional workspace/output label, explicit Apply/Cancel, automatic saved-workspace/switch naming display and literal text without altering selected output.');


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
  const batchSummary='Built 5 configurations.\n3 shared A4 sheets.\nVLAN 10 uses different names across configurations.\nOutput: Batch-folder';
  await publish({label:'Campus project',output:'Batch-folder',summary:batchSummary,combined:true,pageCount:3,pdfAvailable:true});
  await deliver({type:'batchComplete',message:'Built 5 configurations.',details:'1. Core\n2. Access-A\n3. First\n4. Duplicate\n5. Pasted',output:'Batch-folder',combinedAvailable:true,pageCount:3,warnings:['VLAN 10 uses different names across configurations.']});
  assert.equal(await page.locator('#status').innerText(),batchSummary);assert(!(await page.locator('#open').isDisabled()));
  await page.click('#open');assert.deepEqual(await last(),{action:'open'});
  await page.click('#openPdf');assert.deepEqual(await last(),{action:'openCombined'});

  assert((await page.locator('#status').innerText()).includes('3 shared A4 sheets.'));assert((await page.locator('#status').innerText()).includes('VLAN 10 uses different names'));
  assert.equal(await page.locator('#previewCombined').count(),0);assert.equal(await page.locator('#openBatch').count(),0);let preview=await previewProject();
  assert.equal(preview.action,'previewCombined');assert.equal(preview.index,0);assert(await page.locator('#previousCombined').isDisabled());assert(await page.locator('#nextCombined').isDisabled());
  const previewSrc='data:image/png;base64,'+fs.readFileSync(path.resolve(__dirname,'../Branding/Switchport_Diagram_Builder_Icon.png')).toString('base64');
  await deliver({type:'combinedPreview',requestId:preview.requestId,index:0,count:3,name:'Campus shared sheets',src:previewSrc});
  assert.equal(await page.locator('#combinedPreviewImage').getAttribute('src'),previewSrc);assert.equal(await page.locator('#combinedPreviewPage').innerText(),'Page 1 of 3');assert(!(await page.locator('#nextCombined').isDisabled()));
  await page.click('#nextCombined');preview=await last();assert.equal(preview.index,1);
  await deliver({type:'combinedPreview',requestId:preview.requestId,index:1,count:3,name:'Campus shared sheets',src:previewSrc});
  assert.equal(await page.locator('#combinedPreviewPage').innerText(),'Page 2 of 3');assert(!(await page.locator('#nextCombined').isDisabled()));assert(!(await page.locator('#previousCombined').isDisabled()));
  await page.click('#nextCombined');preview=await last();assert.equal(preview.index,2);await deliver({type:'combinedPreview',requestId:preview.requestId,index:2,count:3,name:'Campus shared sheets',src:previewSrc});assert.equal(await page.locator('#combinedPreviewPage').innerText(),'Page 3 of 3');assert(await page.locator('#nextCombined').isDisabled());
  await page.click('#openPreviewPng');assert.deepEqual(await last(),{action:'openChart',index:2});
  await page.click('#openCombined');assert.deepEqual(await last(),{action:'openCombined'});
  for(const viewport of [{width:900,height:650},{width:600,height:430}]){
   await page.setViewportSize(viewport);
   const box=await page.locator('#combinedPreviewDialog').boundingBox();assert(box.x>=0&&box.y>=0&&box.x+box.width<=viewport.width+.5&&box.y+box.height<=viewport.height+.5);
   for(const id of ['previousCombined','nextCombined','openPreviewPng','openCombined','closeCombinedPreview'])assert(await page.locator('#'+id).evaluate(el=>{const b=el.getBoundingClientRect();return b.top>=0&&b.bottom<=innerHeight&&b.left>=0&&b.right<=innerWidth;}),id+' fits compact preview');
  }
  await page.click('#previousCombined');preview=await last();assert.equal(preview.index,1);await deliver({type:'combinedPreviewError',requestId:preview.requestId,message:'Page 2 PNG is missing.'});assert.equal(await page.locator('#combinedPreviewPage').innerText(),'Page 2 of 3');assert(!(await page.locator('#previousCombined').isDisabled()));assert(!(await page.locator('#nextCombined').isDisabled()),'missing middle page retains navigation to page 3');await page.click('#nextCombined');preview=await last();assert.equal(preview.index,2);await page.click('#closeCombinedPreview');
  await deliver({type:'combinedPreview',requestId:preview.requestId,index:0,count:3,name:'Late result',src:previewSrc});assert(await page.locator('#combinedPreviewDialog').isHidden());assert.equal(await page.locator('#combinedPreviewImage').getAttribute('src'),null);
  await page.setViewportSize({width:1180,height:760});preview=await previewProject();
  await deliver({type:'combinedPreviewError',requestId:preview.requestId,message:'Saved preview is unavailable. Build shared sheets again.'});assert((await page.locator('#combinedPreviewStatus').innerText()).includes('unavailable'));
  await page.click('#closeCombinedPreview');preview=await previewProject();
  await deliver({type:'combinedPreview',requestId:preview.requestId,index:0,count:3,name:'External image must be rejected',src:'https://example.invalid/remote.png'});assert(await page.locator('#combinedPreviewImage').isHidden());
  await page.keyboard.press('Escape');assert(await page.locator('#combinedPreviewDialog').isHidden());
  console.log('PASS shared-sheet default, page-count/palette warnings, exact PNG preview payload, page navigation, PDF opening, compact modal, error recovery and stale/external preview rejection.');

  await page.click('#copyOutput');assert.deepEqual(await last(),{action:'copyOutput',text:batchSummary});await deliver({type:'outputCopied',message:'Copied.'});
  await page.locator('#combinePdf').uncheck();await page.click('#runAll');assert.deepEqual(await last(),{action:'runAll',combined:false});await publish({label:'Individual chart project',output:'Next-batch',summary:'Built all individually.\nOutput: Next-batch',combined:true,pageCount:5,pdfAvailable:false});await deliver({type:'batchComplete',message:'Built all.',output:'Next-batch'});
  preview=await previewProject();assert(await page.locator('#openCombined').isDisabled(),'an individual-charts project does not advertise a nonexistent combined PDF');await page.click('#closeCombinedPreview');
  console.log('PASS unified latest-project output, shared-sheet opt-out, ordered summary and generation locks.');


  // The most recent success wins, regardless of current selection or source changes.
  await page.click('#run');assert.deepEqual(await last(),{action:'run'});
  await publish({label:'Newest single chart',output:'Single-latest',summary:'Single project output',combined:false,pageCount:1});await deliver({type:'complete',message:'Reports created.',chartAvailable:true});
  await selected('three',false,'Old selected validation');await workspace('three');assert.equal(await page.locator('#projectName').innerText(),'Newest single chart');
  await publish({label:'Newest single chart',output:'Single-latest',summary:'Single project output',combined:false,pageCount:1,stale:true});
  assert(await page.locator('#projectStale').isVisible());assert(!(await page.locator('#openChart').isDisabled()));
  await page.click('#run');await deliver({type:'blocked',message:'This attempt failed.',details:'Validation error',output:'Failed-attempt'});
  assert.equal(await page.locator('#status').innerText(),'Single project output');assert((await page.locator('#operationStatus').innerText()).includes('This attempt failed.'));assert(!(await page.locator('#openChart').isDisabled()));
  await page.click('#copyOutput');assert.deepEqual(await last(),{action:'copyOutput',text:'Single project output'});await deliver({type:'outputCopied',message:'Copied.'});
  await page.click('#openChart');assert.deepEqual(await last(),{action:'openChart'});assert(await page.locator('#combinedPreviewDialog').isHidden());
  await page.click('#open');assert.deepEqual(await last(),{action:'open'});
  await publish({label:'Newest single chart',output:'Single-latest',summary:'Single project output',chartAvailable:false,pdfAvailable:true,folderAvailable:true,stale:true});assert(await page.locator('#openChart').isDisabled());assert(!(await page.locator('#openPdf').isDisabled()));assert(!(await page.locator('#open').isDisabled()));
  await publish({label:'Newest single chart',output:'Single-latest',summary:'Single project output',stale:true});
  await selected('six');await workspace('six');
  console.log('PASS single → shared project → individual-chart project → single latest-project actions; failed builds/selection/source edits preserve success and Copy; stale/missing-file states are explicit.');

  fs.mkdirSync('build',{recursive:true});
  await deliver({type:'importComplete',message:'',errors:[]});
  for(const viewport of [{width:1180,height:760},{width:900,height:650},{width:884,height:611}]){
   await page.setViewportSize(viewport);
   await page.waitForFunction(()=>{const selected=document.querySelector('.workspace-row.selected'),list=document.getElementById('workspaceList');if(!selected)return false;const row=selected.getBoundingClientRect(),bounds=list.getBoundingClientRect();return row.top>=bounds.top&&row.bottom<=bounds.bottom;});
   const bounds=await page.evaluate(()=>({width:innerWidth,height:innerHeight,scrollWidth:document.documentElement.scrollWidth,scrollHeight:document.documentElement.scrollHeight,listScroll:document.getElementById('workspaceList').scrollHeight,listClient:document.getElementById('workspaceList').clientHeight}));
   assert(bounds.scrollWidth<=bounds.width+1,JSON.stringify(bounds));
   for(const id of ['choose','paste','run','runAll','saveWorkspace','openWorkspace','clearWorkspace','undoWorkspace','editWorkspaceLabel','openChart','openPdf','open','copyOutput'])assert(await page.locator('#'+id).evaluate(el=>{const b=el.getBoundingClientRect();return b.top>=0&&b.bottom<=innerHeight&&b.left>=0&&b.right<=innerWidth;}),id+' fits '+viewport.width+'x'+viewport.height);
   assert(bounds.listScroll>bounds.listClient,'long configuration list scrolls inside its panel');
   await page.screenshot({path:'build/workspace-'+viewport.width+'x'+viewport.height+'.png',fullPage:true});
  }
  await deliver({type:'workspace',items,selectedId:'six',dirty:false,batchAvailable:false});assert(!(await page.locator('#openChart').isDisabled()));assert(!(await page.locator('#open').isDisabled()));
  items=[];await workspace(null,false);await deliver({type:'selected',id:null,name:''});assert(await page.locator('#run').isDisabled());assert(await page.locator('#runAll').isDisabled());assert(await page.locator('#workspaceEmpty').isVisible());
  await workspace(null,true);assert(!(await page.locator('#saveWorkspace').isDisabled()),'a cleared or named empty workspace can be saved');await page.click('#saveWorkspace');assert.deepEqual(await last(),{action:'saveWorkspace'});
  assert.deepEqual(errors,[]);console.log('PASS normal/small-window controls, bounded ordered-list scrolling, empty workspace and no browser errors.');
 }finally{await browser.close();}
})().catch(error=>{console.error(error);process.exit(1)});
