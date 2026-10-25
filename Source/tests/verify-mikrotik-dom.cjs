const {JSDOM}=require('jsdom');
const fs=require('fs'),vm=require('vm'),assert=require('assert');
const ui=require('path').join(__dirname,'../SwitchportDiagramBuilder/wwwroot/');
const dom=new JSDOM(fs.readFileSync(ui+'index.html','utf8'),{url:'https://switchport.local',runScripts:'outside-only'});
const w=dom.window;let listener;const messages=[];
w.TextEncoder=TextEncoder;w.TextDecoder=TextDecoder;
w.HTMLDialogElement.prototype.showModal=function(){this.open=true;};
w.HTMLDialogElement.prototype.close=function(){this.open=false;this.dispatchEvent(new w.Event('close'));};
w.chrome={webview:{addEventListener:(_,fn)=>listener=fn,postMessage:m=>{messages.push(m);if(m.action==='loadBackup'||m.action==='loadText')listener({data:{type:'selected',name:m.name,hostname:m.action==='loadBackup'?'ScaleFiberSwitch2':'TEXT-TEST'}});}}};
vm.runInContext(fs.readFileSync(ui+'app.js','utf8'),dom.getInternalVMContext());
async function drop(data,name){const event=new w.Event('drop',{bubbles:true,cancelable:true});Object.defineProperty(event,'dataTransfer',{value:{files:[{name,size:data.length,arrayBuffer:async()=>Uint8Array.from(data).buffer}],types:['Files']}});w.document.body.dispatchEvent(event);await new Promise(r=>setImmediate(r));}
(async()=>{
 const bytes=fs.readFileSync(process.argv[2]);await drop(bytes,'Winthrop-switch2.backup');
 let message=messages.find(m=>m.action==='loadBackup');assert(message&&Buffer.from(message.text,'base64').equals(bytes));
 assert(w.document.getElementById('file').textContent.includes('ScaleFiberSwitch2'));assert(!w.document.getElementById('run').disabled);
 await drop([239,168,145,115,0,0,0,0],'encrypted.backup');assert(w.document.getElementById('status').textContent.includes('encrypted'));assert(!w.document.getElementById('choose').disabled);
 await drop(Buffer.from('/system identity\nset name=TEXT-TEST'),'config.rsc');assert(messages.at(-1).action==='loadText');
 w.document.getElementById('devices').click();let request=messages.at(-1);assert(request.action==='inspectDevices');
 listener({data:{type:'models',models:[{sku:'CRS309-1G-8S+IN',name:'CRS309-1G-8S+IN',kind:'Switch'}]}});
 listener({data:{type:'devices',requestId:request.requestId,devices:[{Id:'1',Label:'Switch',Kind:'Switch',Model:'',DetectedModel:'',Name:'',SerialNumber:'',AssetTag:'',Notes:'',ModelSelection:''}]}});
 const model=w.document.querySelector('[id$="-ModelSelection"]');assert(model);model.value='CRS309-1G-8S+IN';model.dispatchEvent(new w.Event('input'));
 w.document.getElementById('saveDevices').click();assert(messages.at(-1).devices[0].ModelSelection==='CRS309-1G-8S+IN');
 console.log('PASS DOM checks: exact binary file-drop bytes, hostname/generation state, encrypted backup message/recovery, RSC text loading and per-device MikroTik model save.');
})().catch(error=>{console.error(error);process.exitCode=1;});
