const {chromium}=require('playwright');
const path=require('path'),assert=require('assert');
(async()=>{
 const browser=await chromium.launch({headless:true,args:['--no-sandbox'],...(process.env.SWITCHPORT_TEST_BROWSER?{executablePath:process.env.SWITCHPORT_TEST_BROWSER}:{})});
 try{
  const page=await browser.newPage({viewport:{width:884,height:611}});
  await page.addInitScript(()=>{let listener;window.__switchportMessages=[];window.__switchportDeliver=data=>listener({data});window.chrome={webview:{addEventListener:(name,fn)=>listener=fn,postMessage:msg=>window.__switchportMessages.push(msg)}};});
  await page.goto('file://'+path.resolve(__dirname,'../SwitchportDiagramBuilder/wwwroot/index.html'));
  assert(!(await page.locator('#outputSection').isVisible()));
  const message={type:'blocked',message:'Generation blocked. Review the validation report.',details:'RENDER_BLOCKED=TRUE\nInvalid interface range.\nUnicode: Réseau <b>example</b>',output:'D:\\Desktop\\Configs\\STACK (8)'};
  await page.evaluate(m=>window.__switchportDeliver(m),message);
  const expected=message.message+'\n\n'+message.details+'\n\nOutput: '+message.output;
  assert(await page.locator('#copyOutput').isVisible());
  await page.click('#copyOutput');
  assert.deepStrictEqual(await page.evaluate(()=>window.__switchportMessages.at(-1)),{action:'copyOutput',text:expected});
  assert(await page.locator('#copyOutput').isDisabled());
  assert.equal(await page.locator('#status b').count(),0);
  await page.evaluate(()=>window.__switchportDeliver({type:'outputCopied',message:'Copied.'}));
  assert.equal(await page.locator('#copyFeedback').innerText(),'Copied.');
  assert.equal(await page.locator('#status').textContent(),expected);
  assert(!(await page.locator('#copyOutput').isDisabled()));
  await page.click('#copyOutput');
  await page.evaluate(()=>window.__switchportDeliver({type:'outputCopyFailed',message:'Clipboard is busy. Try Copy again.'}));
  assert.equal(await page.locator('#status').textContent(),expected);
  assert(!(await page.locator('#copyOutput').isDisabled()));
  await page.evaluate(()=>window.__switchportDeliver({type:'complete',message:'Reports created.',output:'C:/Reports',chartAvailable:true}));
  assert(await page.locator('footer').evaluate(el=>el.getBoundingClientRect().bottom<=innerHeight+1),'Completed output and footer fit the normal launch viewport');
  assert.equal(await page.locator('#copyFeedback').innerText(),'');
  await page.click('#copyOutput');
  assert.deepStrictEqual(await page.evaluate(()=>window.__switchportMessages.at(-1)),{action:'copyOutput',text:'Reports created.\n\nOutput: C:/Reports'});
  await page.evaluate(()=>{window.__switchportDeliver({type:'outputCopied',message:'Copied.'});window.__switchportDeliver({type:'selected',name:'Next.txt'});});
  assert(!(await page.locator('#outputSection').isVisible()));
  assert(await page.locator('#copyOutput').isDisabled());
  console.log('PASS Copy preserves displayed output, multiline errors, paths and Unicode; treats markup as text; handles success/retry without replacing results; resets for new input.');
 }finally{await browser.close();}
})().catch(error=>{console.error(error);process.exit(1)});
