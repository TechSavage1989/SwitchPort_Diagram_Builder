const el=id=>document.getElementById(id);
const MAX_INPUT_BYTES=20_000_000;
let updating=false,hasInput=false,busy=false,loading=false,dragDepth=0,hasLogo=false,copying=false,deviceLoading=false,deviceSaving=false,deviceRequest=0,deviceDraft=null,modelChoices=[];
const post=message=>window.chrome.webview.postMessage(message);
const locked=()=>updating||busy||loading||deviceLoading;
function controls(){
 for(const id of ['choose','paste','chooseOutput','chooseLogo'])el(id).disabled=locked();
 el('run').disabled=locked()||!hasInput;
 el('devices').disabled=locked()||!hasInput;el('saveDevices').disabled=deviceLoading||!deviceDraft;el('cancelDevices').disabled=deviceSaving;
 el('deviceRows').querySelectorAll('input,textarea').forEach(input=>input.disabled=deviceLoading);
 el('removeLogo').disabled=locked()||!hasLogo;
 el('usePaste').disabled=locked()||!el('pasteText').value.trim();
}
function status(type,message){
 el('status').className=type;el('status').textContent=message;
 el('outputSection').hidden=!message;el('copyOutput').disabled=copying||!message;el('copyFeedback').textContent='';
 document.body.classList.toggle('has-output',!!message);
}
function inputError(message){
 loading=false;controls();status('error',message);
 if(el('pasteDialog').open)el('pasteError').textContent=message;
}
function loadText(text,name){
 if(locked())return;
 if(!text.trim()){inputError('The configuration is empty.');return;}
 if(new TextEncoder().encode(text).length>MAX_INPUT_BYTES){inputError('Configuration exceeds the 20 MB limit.');return;}
 loading=true;controls();el('openChart').disabled=true;el('pasteError').textContent='';
 status('status','Loading configuration...');post({action:'loadText',text,name});
}
el('choose').onclick=()=>{if(!locked())post({action:'choose'});};
el('paste').onclick=()=>{if(!locked()){el('pasteError').textContent='';el('pasteDialog').showModal();post({action:'updateEditing',enabled:true});el('pasteText').focus();controls();}};
el('cancelPaste').onclick=()=>{if(!loading)el('pasteDialog').close();};
el('pasteDialog').addEventListener('close',()=>post({action:'updateEditing',enabled:false}));
el('pasteText').oninput=()=>{el('pasteError').textContent='';controls();};
el('usePaste').onclick=()=>loadText(el('pasteText').value,'Pasted configuration');
el('chooseOutput').onclick=()=>{if(!locked())post({action:'chooseOutput'});};
el('chooseLogo').onclick=()=>{if(!locked())post({action:'chooseLogo'});};
el('removeLogo').onclick=()=>{if(!locked()&&hasLogo)post({action:'removeLogo'});};
el('open').onclick=()=>post({action:'open'});
el('copyOutput').onclick=()=>{
 const text=el('status').textContent;if(copying||!text.trim())return;
 copying=true;el('copyOutput').disabled=true;el('copyFeedback').textContent='';post({action:'copyOutput',text});
};
el('openChart').onclick=()=>{if(!locked()&&!el('openChart').disabled)post({action:'openChart'});};
el('run').onclick=()=>{if(locked()||!hasInput)return;busy=true;el('openChart').disabled=true;controls();post({action:'run'});};
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
 if(files.length>1){inputError('Drop one configuration file at a time.');return;}
 if(!files.length){const text=transfer?.getData('text/plain');if(text)loadText(text,'Dropped configuration');return;}
 const file=files[0];
 if(file.size>MAX_INPUT_BYTES){inputError('Configuration exceeds the 20 MB limit.');return;}
 loading=true;controls();el('openChart').disabled=true;status('status','Reading configuration...');
 try{
  const bytes=new Uint8Array(await file.arrayBuffer());
  if(bytes.length>=4&&bytes[0]===0x88&&bytes[1]===0xac&&bytes[2]===0xa1&&bytes[3]===0xb1){
   let binary='';for(let offset=0;offset<bytes.length;offset+=8192)binary+=String.fromCharCode(...bytes.subarray(offset,offset+8192));
   post({action:'loadBackup',text:btoa(binary),name:file.name});return;
  }
  if(bytes.length>=4&&bytes[0]===0xef&&bytes[1]===0xa8&&bytes[2]===0x91){inputError('This MikroTik backup is encrypted. Load an unencrypted backup or a RouterOS .rsc export.');return;}
  const encoding=bytes[0]===255&&bytes[1]===254?'utf-16le':bytes[0]===254&&bytes[1]===255?'utf-16be':'utf-8';
  const text=new TextDecoder(encoding).decode(bytes);
  loading=false;loadText(text,file.name);
 }catch{inputError('The dropped file could not be read. Choose or drop a text configuration file.');}
});
window.addEventListener('blur',clearDrag);
window.chrome.webview.addEventListener('message',event=>{
 const m=event.data;
 if(m.type==='updateState'){renderUpdate(m);return;}
 if(['devices','devicesSaved','deviceError'].includes(m.type)){
  if(m.requestId!==deviceRequest)return;deviceLoading=false;deviceSaving=false;
  if(m.type==='devices'){el('deviceError').textContent='';renderDevices(m.devices);el('deviceRows').querySelector('input')?.focus();}
  if(m.type==='deviceError'){el('deviceError').textContent=m.message;if(!deviceDraft)el('deviceRows').textContent='No devices to edit.';controls();}
  if(m.type==='devicesSaved'){el('deviceDialog').close();el('openChart').disabled=true;status('status',m.count?'Device details saved. Build reports to apply them.':'Device details cleared. Build reports to apply.');controls();}
  return;
 }
 if(m.type==='outputCopied'||m.type==='outputCopyFailed'){
  copying=false;el('copyOutput').disabled=!el('status').textContent;el('copyFeedback').textContent=m.message;return;
 }
 if(m.type==='logoSelected'){
  hasLogo=!!m.preview;el('logoName').textContent=m.name||'No report logo';el('logoName').title=m.name||'';
  el('logoPreview').hidden=!hasLogo;el('removeLogo').hidden=!hasLogo;
  if(hasLogo)el('logoPreview').src=m.preview;else el('logoPreview').removeAttribute('src');
  el('openChart').disabled=true;status('','');controls();return;
 }
 if(m.type==='models'){modelChoices=m.models;return;}
 if(m.type==='outputSelected'){el('outputFolder').textContent=m.path;el('openChart').disabled=true;return;}
 if(m.type==='selected'){
  hasInput=true;loading=false;
  if(el('deviceDialog').open)el('deviceDialog').close();deviceDraft=null;deviceLoading=false;deviceSaving=false;deviceRequest++;
  el('file').textContent=m.hostname?m.hostname+' - '+m.name:m.name;el('file').title=m.path||m.name;
  el('openChart').disabled=true;el('pasteError').textContent='';
  if(el('pasteDialog').open)el('pasteDialog').close();
  status('','');controls();return;
 }
 if(m.type==='complete')el('openChart').disabled=!m.chartAvailable;
 if(['blocked','error','chartUnavailable'].includes(m.type))el('openChart').disabled=true;
 status(m.type,[m.message,m.details,m.output?'Output: '+m.output:''].filter(Boolean).join('\n\n'));
 if(['complete','blocked','error'].includes(m.type)){
  busy=false;loading=false;controls();
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
 el('installUpdate').disabled=!m.canInstall||locked()||el('deviceDialog').open||el('pasteDialog').open;
 el('reviewUpdate').hidden=!m.releaseUrl;el('reviewUpdate').disabled=updating;
 el('laterUpdate').hidden=!m.availableVersion;el('laterUpdate').disabled=updating;
 el('closeUpdates').disabled=updating;
 el('updateProgress').hidden=!m.downloading;el('updateProgress').value=m.progress||0;
 el('updateBadge').hidden=!m.availableVersion;
 if(m.reminder&&m.availableVersion){el('updateNotice').textContent='Version '+m.availableVersion+' is available.';el('updateReminder').hidden=false;}
 if(!m.availableVersion)el('updateReminder').hidden=true;
}
function showUpdates(){
 if(el('deviceDialog').open||el('pasteDialog').open||el('updateDialog').open)return;
 el('updateDialog').showModal();post({action:'updateState'});
}
el('updates').onclick=showUpdates;
el('reviewUpdateNotice').onclick=showUpdates;
el('laterUpdateNotice').onclick=()=>{el('updateReminder').hidden=true;post({action:'updateLater'});};
el('closeUpdates').onclick=()=>{if(!updating)el('updateDialog').close();};
el('updateDialog').addEventListener('cancel',event=>{if(updating)event.preventDefault();});
el('checkUpdate').onclick=()=>post({action:'updateCheck'});
el('downloadUpdate').onclick=()=>{if(updateState.canDownload)post({action:'updateDownload'});};
el('installUpdate').onclick=()=>{if(updateState.canInstall&&!locked()&&!el('deviceDialog').open&&!el('pasteDialog').open)post({action:'updateInstall'});};
el('reviewUpdate').onclick=()=>post({action:'updateReview'});
el('autoUpdates').onchange=()=>post({action:'updatePreference',enabled:el('autoUpdates').checked});
el('laterUpdate').onclick=()=>{el('updateReminder').hidden=true;el('updateDialog').close();post({action:'updateLater'});};
post({action:'ready'});
