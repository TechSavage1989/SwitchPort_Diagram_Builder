const {chromium}=require('playwright');
const path=require('path'),assert=require('assert'),fs=require('fs');

(async()=>{
 const browser=await chromium.launch({headless:true,args:['--no-sandbox'],...(process.env.SWITCHPORT_TEST_BROWSER?{executablePath:process.env.SWITCHPORT_TEST_BROWSER}:{})});
 try{
  const page=await browser.newPage({viewport:{width:1060,height:740}}),errors=[];
  page.on('pageerror',error=>errors.push(error.message));
  await page.addInitScript(()=>{
   let listener;window.__messages=[];
   window.__deliver=data=>listener({data});
   window.chrome={webview:{addEventListener:(event,handler)=>listener=handler,postMessage:message=>window.__messages.push(message)}};
  });
  await page.goto('file://'+path.resolve(__dirname,'../SwitchportDiagramBuilder/wwwroot/index.html'));
  const deliver=data=>page.evaluate(data=>window.__deliver(data),data);
  const last=()=>page.evaluate(()=>window.__messages.at(-1));
  assert(await page.locator('#supplementary').isDisabled(),'Supplementary import requires a selected configuration');
  await deliver({type:'workspace',items:[{id:'lab',name:'lab.cfg',hostname:'LAB-SW-01',model:'Aruba 6100',supplementCount:0}],selectedId:'lab',dirty:false});
  await deliver({type:'selected',id:'lab',name:'lab.cfg',hostname:'LAB-SW-01',supplementary:[]});
  await page.click('#supplementary');
  assert(await page.locator('#supplementaryDialog').isVisible(),'Supplementary dialog did not open');
  assert.equal(await page.locator('#supplementaryEmpty').isVisible(),true);
  await page.click('#suppChoose');assert.deepEqual(await last(),{action:'chooseSupplementary'});
  const first={id:'0'.repeat(32),name:'fictional-auvik.xlsx',kind:'Auvik workbook',device:'LAB-SW-01',count:30,categories:['Interface','Neighbor','Route']};
  const second={id:'1'.repeat(32),name:'<script>unsafe</script>.txt',kind:'Command output',device:'LAB-SW-01',count:12,categories:['DHCP relay','Route']};
  await deliver({type:'selected',id:'lab',name:'lab.cfg',hostname:'LAB-SW-01',supplementary:[first,second]});
  assert.equal(await page.locator('#supplementaryList li').count(),2);
  assert.equal(await page.locator('#supplementary').innerText(),'Supplementary (2)');
  assert.equal(await page.locator('#supplementary').getAttribute('aria-label'),'Supplementary data (2)');
  assert.equal(await page.locator('#supplementaryList script').count(),0,'Source names are inserted as literal text only');
  assert((await page.locator('#supplementaryList').innerText()).includes('DHCP relay'));
  if(process.env.SUPPLEMENTARY_TEST_SCREENSHOT){
   fs.mkdirSync(path.dirname(process.env.SUPPLEMENTARY_TEST_SCREENSHOT),{recursive:true});
   await page.screenshot({path:process.env.SUPPLEMENTARY_TEST_SCREENSHOT});
  }
  await page.click('#suppPaste');await page.locator('#supplementaryText').fill('SW#show lldp neighbors detail\nLocal Intf: 1/1/1\nSystem Name: LAB-CORE\nPort id: 1/1/48');
  await page.locator('#supplementaryPasteName').fill('Live CLI capture');
  await page.click('#suppUsePaste');
  const pasted=await last();assert.equal(pasted.action,'importSupplementaryText');assert.equal(pasted.name,'Live CLI capture');
  assert(pasted.text.includes('System Name: LAB-CORE'));
  assert(await page.locator('#suppUsePaste').isDisabled(),'Duplicate submission must be blocked during import');
  await deliver({type:'supplementaryResult',success:false,message:'Invalid command output.',errors:[]});
  assert((await page.locator('#supplementaryMessage').innerText()).includes('Invalid'));
  assert(await page.locator('#supplementaryText').isVisible(),'Failed import must retain pasted data');
  await page.click('#suppUsePaste');assert.equal((await last()).action,'importSupplementaryText');
  await deliver({type:'supplementaryResult',success:true,message:'Added 3 observations.',errors:[]});
  assert(await page.locator('#supplementaryPasteBox').isHidden(),'Successful paste should reset the editor');
  assert.equal(await page.locator('#supplementaryText').inputValue(),'');
  await page.locator('#supplementaryList button').first().click();
  assert.deepEqual(await last(),{action:'removeSupplementary',id:first.id});
  await deliver({type:'selected',id:'lab',name:'lab.cfg',hostname:'LAB-SW-01',supplementary:[second]});
  await deliver({type:'supplementaryResult',success:true,message:'Supplementary source removed.',errors:[]});
  assert.equal(await page.locator('#supplementaryList li').count(),1);
  await page.click('#suppClose');
  assert(await page.locator('#supplementaryDialog').isHidden());
  assert(!errors.length,'Embedded application JavaScript errors: '+errors.join('; '));
  console.log('PASS supplementary import dialog, source ownership, literal labels, select/paste/remove actions, error recovery and host-driven updates.');
 }finally{await browser.close();}
})().catch(error=>{console.error(error);process.exitCode=1;});
