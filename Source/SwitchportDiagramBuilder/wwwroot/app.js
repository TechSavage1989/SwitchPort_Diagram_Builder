const el=id=>document.getElementById(id);
const MAX_INPUT_BYTES=20_000_000;
let updating=false,hasInput=false,busy=false,loading=false,dragDepth=0,hasLogo=false,copying=false,deviceLoading=false,deviceSaving=false,deviceRequest=0,deviceDraft=null,modelChoices=[];
let workspaceItems=[],workspaceDirty=false,selectedId=null,batchLoading=false,draggedItem=null;
let workspaceOutputLabel='',workspaceSuggestedLabel='',workspaceEffectiveLabel='',combinedAvailable=false,combinedPageCount=0,previewIndex=0,previewRequest=0,previewLoading=false;
let project={available:false,chartAvailable:false,folderAvailable:false,combined:false,pageCount:0,pdfAvailable:false,label:'',output:'',stale:false,summary:''},projectKnown=false;
const post=message=>window.chrome.webview.postMessage(message);
const locked=()=>updating||busy||loading||deviceLoading;
const editorOpen=()=>['deviceDialog','pasteDialog','workspaceLabelDialog','combinedPreviewDialog'].some(id=>el(id).open);
function controls(){
 for(const id of ['choose','paste','chooseOutput','chooseLogo','openWorkspace'])el(id).disabled=locked();
 el('run').disabled=locked()||!hasInput;
 el('saveWorkspace').disabled=locked()||(!workspaceItems.length&&!workspaceDirty);
 el('runAll').disabled=locked()||!workspaceItems.length;el('combinePdf').disabled=locked()||!workspaceItems.length;
 el('openChart').disabled=locked()||!project.chartAvailable;el('open').disabled=updating||!project.folderAvailable;
 el('editWorkspaceLabel').disabled=locked();el('applyWorkspaceLabel').disabled=locked();el('workspaceLabelInput').disabled=locked();
 el('previousCombined').disabled=previewLoading||previewIndex<=0;el('nextCombined').disabled=previewLoading||previewIndex>=combinedPageCount-1;el('openCombined').disabled=locked()||!project.pdfAvailable;
 el('workspaceList').querySelectorAll('button').forEach(button=>button.disabled=locked()||button.dataset.boundary==='true');
 el('workspaceList').querySelectorAll('li').forEach(row=>row.draggable=!locked());
 el('devices').disabled=locked()||!hasInput;el('saveDevices').disabled=deviceLoading||!deviceDraft;el('cancelDevices').disabled=deviceSaving;
 el('deviceRows').querySelectorAll('input,textarea').forEach(input=>input.disabled=deviceLoading);
 el('removeLogo').disabled=locked()||!hasLogo;
 el('usePaste').disabled=locked()||!el('pasteText').value.trim();
}
function showResult(type,message){
 el('status').className=type;el('status').textContent=message;el('outputSection').hidden=!message;
 el('copyOutput').disabled=copying||!message;el('copyFeedback').textContent='';document.body.classList.toggle('has-output',!!message);
}
function status(type,message){
 if(project.available){el('operationStatus').className=type;el('operationStatus').textContent=message;el('operationStatus').hidden=!message;}
 else showResult(type,message);
}
function renderProject(message){
 const changed=project.output!==message.output;
 projectKnown=true;project={available:false,chartAvailable:false,folderAvailable:false,combined:false,pageCount:0,pdfAvailable:false,label:'',output:'',stale:false,summary:'',...message};
 combinedAvailable=project.available&&project.combined&&project.chartAvailable;combinedPageCount=project.pageCount||0;
 if((changed||!combinedAvailable)&&el('combinedPreviewDialog').open)el('combinedPreviewDialog').close();
 el('projectName').textContent=project.available?(project.label||'Generated project'):'No project generated yet';el('projectName').title=project.output||'';
 el('projectStale').hidden=!project.available||!project.stale;
 if(project.available){showResult('complete',project.summary||[project.label,project.output?'Output: '+project.output:''].filter(Boolean).join('\n'));if(changed){el('operationStatus').hidden=true;el('operationStatus').textContent='';}}
 controls();
}
// A complete event can also restore a previous host's single-project handoff.
function legacyProject(message){
 if(projectKnown)return;
 const text=[message.message,message.details,message.output?'Output: '+message.output:''].filter(Boolean).join('\n\n');
 renderProject({available:true,chartAvailable:!!message.chartAvailable,folderAvailable:!!message.output,label:message.label||'Generated project',output:message.output||'',summary:text,combined:false,pdfAvailable:false,stale:false});projectKnown=false;
}
function inputError(message){
 loading=false;controls();status('error',message);
 if(el('pasteDialog').open)el('pasteError').textContent=message;
}
function loadText(text,name){
 if(locked())return;
 if(!text.trim()){inputError('The configuration is empty.');return;}
 if(new TextEncoder().encode(text).length>MAX_INPUT_BYTES){inputError('Configuration exceeds the 20 MB limit.');return;}
 loading=true;controls();el('pasteError').textContent='';
 status('status','Loading configuration...');post({action:'loadText',text,name});
}
el('choose').onclick=()=>{if(!locked())post({action:'choose'});};
el('paste').onclick=()=>{if(!locked()){el('pasteError').textContent='';el('pasteDialog').showModal();post({action:'updateEditing',enabled:true});el('pasteText').focus();controls();}};
el('cancelPaste').onclick=()=>{if(!loading)el('pasteDialog').close();};
el('pasteDialog').addEventListener('cancel',event=>{if(loading)event.preventDefault();});
el('pasteDialog').addEventListener('close',()=>post({action:'updateEditing',enabled:false}));
el('pasteText').oninput=()=>{el('pasteError').textContent='';controls();};
el('usePaste').onclick=()=>loadText(el('pasteText').value,'Pasted configuration');
el('chooseOutput').onclick=()=>{if(!locked())post({action:'chooseOutput'});};
el('chooseLogo').onclick=()=>{if(!locked())post({action:'chooseLogo'});};
el('removeLogo').onclick=()=>{if(!locked()&&hasLogo)post({action:'removeLogo'});};
el('open').onclick=()=>{if(!updating&&project.folderAvailable)post({action:'open'});};
el('copyOutput').onclick=()=>{
 const text=el('status').textContent;if(copying||!text.trim())return;
 copying=true;el('copyOutput').disabled=true;el('copyFeedback').textContent='';post({action:'copyOutput',text});
};
el('openChart').onclick=()=>{if(!locked()&&!el('openChart').disabled)post({action:'openChart'});};
el('openWorkspace').onclick=()=>{if(!locked())post({action:'openWorkspace'});};
el('saveWorkspace').onclick=()=>{if(!locked()&&(workspaceItems.length||workspaceDirty))post({action:'saveWorkspace'});};

el('editWorkspaceLabel').onclick=()=>{
 if(locked()||editorOpen())return;
 el('workspaceLabelInput').value=workspaceOutputLabel;el('workspaceLabelInput').placeholder=workspaceSuggestedLabel||'Automatic';
 el('workspaceLabelAutomatic').textContent='Automatic: '+(workspaceSuggestedLabel||'Switch names, or the saved workspace filename');
 el('workspaceLabelDialog').showModal();post({action:'updateEditing',enabled:true});el('workspaceLabelInput').focus();
};
el('cancelWorkspaceLabel').onclick=()=>el('workspaceLabelDialog').close();
el('workspaceLabelDialog').addEventListener('close',()=>post({action:'updateEditing',enabled:false}));
el('applyWorkspaceLabel').onclick=()=>{
 if(locked())return;
 const text=el('workspaceLabelInput').value.trim();
 if(text!==workspaceOutputLabel)post({action:'setWorkspaceLabel',text});
 el('workspaceLabelDialog').close();
};
el('workspaceLabelInput').addEventListener('keydown',event=>{if(event.key==='Enter'){event.preventDefault();el('applyWorkspaceLabel').click();}});
function requestCombinedPreview(index){
 if(!combinedAvailable||locked()||previewLoading)return;
 previewIndex=index;previewLoading=true;previewRequest++;el('combinedPreviewPage').textContent='Page '+(index+1)+' of '+combinedPageCount;el('combinedPreviewImage').hidden=true;el('combinedPreviewImage').removeAttribute('src');
 el('combinedPreviewStatus').textContent='Loading combined sheet...';controls();post({action:'previewCombined',index,requestId:previewRequest});
}
function showProjectPreview(){
 if(!combinedAvailable||locked()||editorOpen())return;
 previewIndex=0;el('combinedPreviewName').textContent=project.label||workspaceEffectiveLabel;el('combinedPreviewPage').textContent='';
 el('combinedPreviewDialog').showModal();post({action:'updateEditing',enabled:true});requestCombinedPreview(0);
};
el('previousCombined').onclick=()=>{if(previewIndex>0)requestCombinedPreview(previewIndex-1);};
el('nextCombined').onclick=()=>{if(previewIndex<combinedPageCount-1)requestCombinedPreview(previewIndex+1);};
el('openCombined').onclick=()=>{if(project.pdfAvailable&&!locked())post({action:'openCombined'});};
el('closeCombinedPreview').onclick=()=>el('combinedPreviewDialog').close();
el('combinedPreviewImage').addEventListener('error',()=>{if(el('combinedPreviewDialog').open){el('combinedPreviewImage').hidden=true;el('combinedPreviewStatus').textContent='The saved PNG preview could not be displayed. Open the PDF or build the sheets again.';}});
el('combinedPreviewDialog').addEventListener('close',()=>{previewRequest++;previewLoading=false;el('combinedPreviewImage').removeAttribute('src');el('combinedPreviewImage').hidden=true;post({action:'updateEditing',enabled:false});controls();});

el('runAll').onclick=()=>{if(locked()||!workspaceItems.length)return;busy=true;controls();post({action:'runAll',combined:el('combinePdf').checked});};
el('run').onclick=()=>{if(locked()||!hasInput)return;busy=true;controls();post({action:'run'});};
function closeDevices(){post({action:'updateEditing',enabled:false});deviceRequest++;deviceLoading=false;deviceSaving=false;deviceDraft=null;controls();}
function renderDevices(rows){
 deviceDraft=rows.map(row=>({...row}));el('deviceRows').replaceChildren();
 const fields=[['Name','Name',80],['SerialNumber','Serial number',80],['AssetTag','Asset tag',80],['Notes','Other details',400]];
 deviceDraft.forEach((row,index)=>{
  const card=document.createElement('div');card.className='device-card'+(row.Kind==='Module'?' device-module':'');card.dataset.deviceId=row.Id;
  const title=document.createElement('h3');title.textContent=row.Label;
  const model=document.createElement('div');model.className='device-model';model.textContent=row.Kind==='Module'?(row.Model||'Model not specified'):'Detected model: '+(row.DetectedModel||row.Model||'Not identified');
  const values=document.createElement('div');values.className='device-fields';
  for(const [key,label,limit] of fields){
   const wrapper=document.createElement('label');wrapper.textContent=label;
   const input=document.createElement(key==='Notes'?'textarea':'input');input.maxLength=limit;input.value=row[key]||'';input.dataset.field=key;input.id='device-'+index+'-'+key;input.autocomplete='off';input.spellcheck=false;
   input.oninput=()=>{deviceDraft[index][key]=input.value;el('deviceError').textContent='';};wrapper.append(input);values.append(wrapper);
  }
  card.append(title);
  if(row.ParentId){const parent=document.createElement('div');parent.className='device-parent';parent.textContent='Installed in '+(rows.find(d=>d.Id===row.ParentId)?.Label||row.ParentId);card.append(parent);}
  card.append(model);
  if(row.Kind!=='Module'){
   const wrapper=document.createElement('label');wrapper.className='device-model-input';wrapper.textContent='Model (optional)';
   const input=document.createElement('input');input.id='device-'+index+'-ModelSelection';input.value=row.ModelSelection||'';input.maxLength=100;input.autocomplete='off';input.spellcheck=false;
   input.placeholder=row.DetectedModel||row.Model?'Auto detect: '+(row.DetectedModel||row.Model):'Choose a supported SKU or type a model name';
   const list=document.createElement('datalist');list.id='device-model-options-'+index;input.setAttribute('list',list.id);
   for(const choice of modelChoices.filter(choice=>!choice.kind||choice.kind===row.Kind)){
    const option=document.createElement('option');option.value=choice.sku;option.label=choice.name;list.append(option);
   }
   input.oninput=()=>{deviceDraft[index].ModelSelection=input.value;el('deviceError').textContent='';};wrapper.append(input,list);card.append(wrapper);
  }
  card.append(values);el('deviceRows').append(card);
 });controls();
}
el('devices').onclick=()=>{
 if(locked()||!hasInput)return;deviceLoading=true;deviceDraft=null;deviceRequest++;controls();
 el('deviceError').textContent='';el('deviceRows').textContent='Detecting devices...';el('deviceDialog').showModal();post({action:'updateEditing',enabled:true});
 post({action:'inspectDevices',requestId:deviceRequest});
};
el('cancelDevices').onclick=()=>el('deviceDialog').close();
el('deviceDialog').addEventListener('close',closeDevices);
el('deviceDialog').addEventListener('cancel',event=>{if(deviceSaving)event.preventDefault();});
el('saveDevices').onclick=()=>{
 if(deviceLoading||!deviceDraft)return;deviceLoading=true;deviceSaving=true;controls();
 const devices=deviceDraft.map(({Id,Name,SerialNumber,AssetTag,Notes,ModelSelection})=>({Id,Name:Name||'',SerialNumber:SerialNumber||'',AssetTag:AssetTag||'',Notes:Notes||'',ModelSelection:ModelSelection||''}));
 post({action:'saveDevices',devices,requestId:deviceRequest});
};

function renderWorkspace(message){
 const selectionChanged=selectedId!==(message.selectedId||null);
 workspaceItems=message.items||[];workspaceDirty=!!message.dirty;selectedId=message.selectedId||null;
 if(typeof message.outputLabel==='string')workspaceOutputLabel=message.outputLabel;
 if(typeof message.suggestedLabel==='string')workspaceSuggestedLabel=message.suggestedLabel;
 if(typeof message.effectiveLabel==='string')workspaceEffectiveLabel=message.effectiveLabel;
 el('workspaceOutputName').textContent='Project name: '+(workspaceEffectiveLabel||'Automatic');
 el('workspaceOutputName').title=(message.workspaceName?'Workspace: '+message.workspaceName+'\n':'')+'Output folder name: '+(workspaceEffectiveLabel||'Automatic');
 const active=document.activeElement,focusId=active?.dataset.itemId,focusAction=active?.dataset.workspaceAction,scrollTop=el('workspaceList').scrollTop;
 el('workspaceList').replaceChildren();el('workspaceCount').textContent=workspaceItems.length;
 el('workspaceDirty').hidden=!message.dirty;el('workspaceEmpty').hidden=workspaceItems.length>0;
 workspaceItems.forEach((item,index)=>{
  const row=document.createElement('li');row.className='workspace-row'+(item.id===selectedId?' selected':'')+(item.error?' has-error':'');row.dataset.itemId=item.id;
  const name=item.hostname||item.name||'Configuration';
  function button(label,action,className){const control=document.createElement('button');control.type='button';control.className=className;control.setAttribute('aria-label',label);control.title=label;control.dataset.itemId=item.id;control.dataset.workspaceAction=action;return control;}
  const choose=button('Select '+name,'select','workspace-select');choose.setAttribute('aria-current',item.id===selectedId?'true':'false');
  const title=document.createElement('strong');title.textContent=(index+1)+'. '+name+(item.dirty?' *':'');
  const detail=document.createElement('small');detail.textContent=[item.name,item.model,item.error].filter(Boolean).join(' · ');choose.title=[name,detail.textContent].filter(Boolean).join('\n');choose.append(title,detail);
  choose.onclick=()=>{if(locked()||item.id===selectedId)return;loading=true;controls();post({action:'selectItem',id:item.id});};
  const order=document.createElement('div');order.className='workspace-order';
  for(const [action,delta,label,glyph] of [['up',-1,'Move up','↑'],['down',1,'Move down','↓']]){
   const move=button(label+' '+name,action,'secondary');move.textContent=glyph;move.dataset.boundary=String(index+delta<0||index+delta>=workspaceItems.length);
   move.onclick=()=>{if(!locked())post({action:'moveItem',id:item.id,index:index+delta});};order.append(move);
  }
  const remove=button('Remove '+name,'remove','secondary workspace-remove');remove.textContent='×';remove.onclick=()=>{if(!locked())post({action:'removeItem',id:item.id});};
  row.append(choose,order,remove);row.draggable=!locked();
  row.addEventListener('dragstart',event=>{if(locked()){event.preventDefault();return;}draggedItem=item.id;event.dataTransfer.effectAllowed='move';event.dataTransfer.setData('application/x-switchport-item',item.id);});
  row.addEventListener('dragover',event=>{if(!draggedItem||locked())return;event.preventDefault();event.stopPropagation();event.dataTransfer.dropEffect='move';row.classList.add('drag-target');});
  row.addEventListener('dragleave',()=>row.classList.remove('drag-target'));
  row.addEventListener('drop',event=>{if(!draggedItem)return;event.preventDefault();event.stopPropagation();row.classList.remove('drag-target');if(!locked()&&draggedItem!==item.id)post({action:'moveItem',id:draggedItem,index});draggedItem=null;});
  row.addEventListener('dragend',()=>{draggedItem=null;el('workspaceList').querySelectorAll('.drag-target').forEach(item=>item.classList.remove('drag-target'));});
  el('workspaceList').append(row);
 });
 controls();el('workspaceList').scrollTop=scrollTop;
 if(selectionChanged)el('workspaceList').querySelector('.selected')?.scrollIntoView({block:'nearest'});
 if(focusId&&focusAction){const target=Array.from(el('workspaceList').querySelectorAll('button')).find(button=>button.dataset.itemId===focusId&&button.dataset.workspaceAction===focusAction);if(target&&!target.disabled){target.focus({preventScroll:true});target.scrollIntoView({block:'nearest'});}}
}
async function readDroppedFile(file){
 if(file.size>MAX_INPUT_BYTES)throw new Error('Configuration exceeds the 20 MB limit.');
 const bytes=new Uint8Array(await file.arrayBuffer());
 if(bytes.length>=4&&bytes[0]===0x88&&bytes[1]===0xac&&bytes[2]===0xa1&&bytes[3]===0xb1){
  let binary='';for(let offset=0;offset<bytes.length;offset+=8192)binary+=String.fromCharCode(...bytes.subarray(offset,offset+8192));
  return {name:file.name,text:btoa(binary),encoding:'base64'};
 }
 if(bytes.length>=3&&bytes[0]===0xef&&bytes[1]===0xa8&&bytes[2]===0x91)throw new Error('This MikroTik backup is encrypted. Load an unencrypted backup or a RouterOS .rsc export.');
 const encoding=bytes[0]===255&&bytes[1]===254?'utf-16le':bytes[0]===254&&bytes[1]===255?'utf-16be':'utf-8';
 const text=new TextDecoder(encoding).decode(bytes);
 if(!text.trim())throw new Error('The configuration is empty.');
 if(new TextEncoder().encode(text).length>MAX_INPUT_BYTES)throw new Error('Configuration exceeds the 20 MB limit.');
 return {name:file.name,text,encoding:'text'};
}

function filesIn(event){return Array.from(event.dataTransfer?.types||[]).includes('Files');}
function clearDrag(){dragDepth=0;document.body.classList.remove('drop-active');}
document.addEventListener('dragenter',event=>{if(filesIn(event)){event.preventDefault();dragDepth++;if(!locked())document.body.classList.add('drop-active');}});
document.addEventListener('dragleave',event=>{if(filesIn(event)&&--dragDepth<=0)clearDrag();});
document.addEventListener('dragover',event=>{if(filesIn(event)){event.preventDefault();event.dataTransfer.dropEffect=locked()?'none':'copy';}});
document.addEventListener('drop',async event=>{
 const transfer=event.dataTransfer;
 const files=Array.from(transfer?.files||[]);
 const editing=event.target.closest?.('input,textarea');
 if(!files.length&&editing)return;
 event.preventDefault();clearDrag();
 if(locked())return;
 if(Array.from(transfer?.types||[]).includes('application/x-switchport-item'))return;
 if(editorOpen()||el('updateDialog').open)return;
 if(!files.length){const text=transfer?.getData('text/plain');if(text)loadText(text,'Dropped configuration');return;}
 loading=true;batchLoading=files.length>1;controls();status('status',batchLoading?'Reading configurations...':'Reading configuration...');
 if(batchLoading){
  const results=[];let accepted=0,payloadBytes=0;
  for(const file of files){
   try{
    if(workspaceItems.length+accepted>=100)throw new Error('A workspace can contain up to 100 configurations.');
    if(payloadBytes+file.size>100_000_000)throw new Error('The import batch exceeds the 100 MB limit. Add fewer configurations at once.');
    const decoded=await readDroppedFile(file),size=new TextEncoder().encode(decoded.text).length;
    if(payloadBytes+size>100_000_000)throw new Error('The import batch exceeds the 100 MB limit. Add fewer configurations at once.');
    results.push(decoded);payloadBytes+=size;accepted++;
   }catch(error){results.push({name:file.name,error:error.message||'The dropped file could not be read.'});}
  }
  post({action:'loadFiles',files:results});return;
 }
 try{
  const file=await readDroppedFile(files[0]);
  if(file.encoding==='base64'){post({action:'loadBackup',text:file.text,name:file.name});return;}
  loading=false;loadText(file.text,file.name);
 }catch(error){inputError(error.message||'The dropped file could not be read. Choose or drop a text configuration file.');}
});
window.addEventListener('blur',clearDrag);
window.addEventListener('resize',()=>requestAnimationFrame(()=>el('workspaceList').querySelector('.selected')?.scrollIntoView({block:'nearest'})));
window.chrome.webview.addEventListener('message',event=>{
 const m=event.data;
 if(m.type==='updateState'){renderUpdate(m);return;}
 if(m.type==='workspace'){renderWorkspace(m);return;}
 if(m.type==='project'){renderProject(m);return;}
 if(m.type==='openProjectPreview'){showProjectPreview();return;}
 if(m.type==='workspaceSaved'){el('importResult').textContent=m.message;el('importResult').className='import-result';el('importResult').hidden=!m.message;return;}
 if(m.type==='importComplete'){
  batchLoading=false;loading=false;controls();
  el('importResult').textContent=[m.message,...(m.errors||[])].filter(Boolean).join('\n');
  el('importResult').className='import-result'+(m.errors?.length?' error':'');el('importResult').hidden=!el('importResult').textContent;
  return;
 }
 if(m.type==='batchComplete'){
  busy=false;loading=false;
  const details=[m.message,m.pageCount?m.pageCount+' A4 page'+(m.pageCount===1?'':'s')+'.':'',...(m.warnings||[]),m.details,m.output?'Output: '+m.output:''].filter(Boolean).join('\n');
  if(!project.available||project.output!==m.output)status('status',details);
  controls();return;
 }
 if(m.type==='combinedPreview'||m.type==='combinedPreviewError'){
  if(m.requestId!==previewRequest||!el('combinedPreviewDialog').open)return;
  previewLoading=false;
  if(m.type==='combinedPreviewError'||typeof m.src!=='string'||!m.src.startsWith('data:image/png;base64,')||!Number.isInteger(m.index)||!Number.isInteger(m.count)||m.index<0||m.index>=m.count){
   el('combinedPreviewStatus').textContent=m.message||'The combined preview could not be loaded. Open the PDF or build the sheets again.';el('combinedPreviewImage').hidden=true;
  }else{
   previewIndex=m.index;combinedPageCount=m.count;el('combinedPreviewName').textContent=m.name||workspaceEffectiveLabel;el('combinedPreviewPage').textContent='Page '+(m.index+1)+' of '+m.count;
   el('combinedPreviewImage').src=m.src;el('combinedPreviewImage').alt='Project switchport diagram, page '+(m.index+1)+' of '+m.count;el('combinedPreviewImage').hidden=false;el('combinedPreviewStatus').textContent='';
  }
  controls();return;
 }
 if(['devices','devicesSaved','deviceError'].includes(m.type)){
  if(m.requestId!==deviceRequest)return;deviceLoading=false;deviceSaving=false;
  if(m.type==='devices'){el('deviceError').textContent='';renderDevices(m.devices);el('deviceRows').querySelector('input')?.focus();}
  if(m.type==='deviceError'){el('deviceError').textContent=m.message;if(!deviceDraft)el('deviceRows').textContent='No devices to edit.';controls();}
  if(m.type==='devicesSaved'){el('deviceDialog').close();status('status',m.count?'Device details saved. Build reports to apply them.':'Device details cleared. Build reports to apply.');controls();}
  return;
 }
 if(m.type==='outputCopied'||m.type==='outputCopyFailed'){
  copying=false;el('copyOutput').disabled=!el('status').textContent;el('copyFeedback').textContent=m.message;return;
 }
 if(m.type==='logoSelected'){
  hasLogo=!!m.preview;el('logoName').textContent=m.name||'No report logo';el('logoName').title=m.name||'';
  el('logoPreview').hidden=!hasLogo;el('removeLogo').hidden=!hasLogo;
  if(hasLogo)el('logoPreview').src=m.preview;else el('logoPreview').removeAttribute('src');
  status('','');controls();return;
 }
 if(m.type==='models'){modelChoices=m.models;return;}
 if(m.type==='outputSelected'){el('outputFolder').textContent=m.path;controls();return;}
 if(m.type==='selected'){
  hasInput=m.id!==null;loading=batchLoading;selectedId=m.id||selectedId;
  if(el('deviceDialog').open)el('deviceDialog').close();deviceDraft=null;deviceLoading=false;deviceSaving=false;deviceRequest++;
  el('file').textContent=hasInput?(m.hostname?m.hostname+' - '+m.name:m.name):'No configuration loaded';el('file').title=m.path||m.name||'';
  el('pasteError').textContent='';
  if(el('pasteDialog').open)el('pasteDialog').close();
  status('','');controls();return;
 }
 if(m.type==='complete')legacyProject(m);
 if(m.type==='chartUnavailable'&&!projectKnown){project.chartAvailable=false;controls();}
 if(m.type!=='complete'||!project.available)status(m.type,[m.message,m.details,m.output?'Output: '+m.output:''].filter(Boolean).join('\n\n'));
 if(['complete','blocked','error'].includes(m.type)){
  busy=false;loading=false;batchLoading=false;controls();
  if(m.type==='error'&&el('pasteDialog').open)el('pasteError').textContent=m.message;
 }
});
let updateState={};
function renderUpdate(m){
 updateState=m;updating=!!m.installing;controls();
 el('appVersion').textContent='Version '+m.currentVersion;
 el('updateVersion').textContent='Installed: '+m.currentVersion+(m.availableVersion?' | Available: '+m.availableVersion:'');
 el('autoUpdates').checked=m.automaticChecks!==false;
 el('autoUpdates').disabled=updating;
 el('updateStatus').textContent=m.status||'';
 el('updateNotes').textContent=m.notes||'';el('updateNotesSection').hidden=!m.notes;
 el('checkUpdate').disabled=!!(m.checking||m.downloading||m.installing);
 el('downloadUpdate').hidden=!m.availableVersion||!!m.downloaded;
 el('downloadUpdate').disabled=!m.canDownload;
 el('installUpdate').hidden=!m.downloaded&&!m.installing;
 el('installUpdate').disabled=!m.canInstall||locked()||editorOpen();
 el('reviewUpdate').hidden=!m.releaseUrl;el('reviewUpdate').disabled=updating;
 el('laterUpdate').hidden=!m.availableVersion;el('laterUpdate').disabled=updating;
 el('closeUpdates').disabled=updating;
 el('updateProgress').hidden=!m.downloading;el('updateProgress').value=m.progress||0;
 el('updateBadge').hidden=!m.availableVersion;
 if(m.reminder&&m.availableVersion){el('updateNotice').textContent='Version '+m.availableVersion+' is available.';el('updateReminder').hidden=false;}
 if(!m.availableVersion)el('updateReminder').hidden=true;
}
function showUpdates(){
 if(editorOpen()||el('updateDialog').open)return;
 el('updateDialog').showModal();post({action:'updateState'});
}
el('updates').onclick=showUpdates;
el('reviewUpdateNotice').onclick=showUpdates;
el('laterUpdateNotice').onclick=()=>{el('updateReminder').hidden=true;post({action:'updateLater'});};
el('closeUpdates').onclick=()=>{if(!updating)el('updateDialog').close();};
el('updateDialog').addEventListener('cancel',event=>{if(updating)event.preventDefault();});
el('checkUpdate').onclick=()=>post({action:'updateCheck'});
el('downloadUpdate').onclick=()=>{if(updateState.canDownload)post({action:'updateDownload'});};
el('installUpdate').onclick=()=>{if(updateState.canInstall&&!locked()&&!editorOpen())post({action:'updateInstall'});};
el('reviewUpdate').onclick=()=>post({action:'updateReview'});
el('autoUpdates').onchange=()=>post({action:'updatePreference',enabled:el('autoUpdates').checked});
el('laterUpdate').onclick=()=>{el('updateReminder').hidden=true;el('updateDialog').close();post({action:'updateLater'});};
post({action:'ready'});
