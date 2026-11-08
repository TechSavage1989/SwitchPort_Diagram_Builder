const {chromium}=require('playwright'),path=require('path'),assert=require('assert');
(async()=>{
 const browser=await chromium.launch({headless:true,args:['--no-sandbox'],...(process.env.SWITCHPORT_TEST_BROWSER?{executablePath:process.env.SWITCHPORT_TEST_BROWSER}:{})});
 try{
  for(const viewport of [{width:1180,height:760},{width:884,height:611},{width:600,height:430}]){
   const page=await browser.newPage({viewport}),errors=[];
   page.on('pageerror',error=>errors.push(error.message));
   await page.addInitScript(()=>{let listener;window.__messages=[];
    window.__deliver=m=>listener({data:m});
    window.chrome={webview:{addEventListener:(_event,handler)=>listener=handler,postMessage:message=>window.__messages.push(message)}};
   });
   await page.goto('file://'+path.resolve(__dirname,'../SwitchportDiagramBuilder/wwwroot/index.html'));
   assert.equal(await page.locator('#paperSize').inputValue(),'a4');
   assert.equal(await page.locator('#paperOrientation').inputValue(),'landscape');
   await page.selectOption('#paperSize','letter');
   assert.deepEqual(await page.evaluate(()=>window.__messages.at(-1)),{action:'setReportPaper',preset:'letter',orientation:'landscape'});
   await page.evaluate(()=>window.__deliver({type:'workspace',items:[],dirty:true,selectedId:null,
    paper:{preset:'letter',orientation:'landscape',description:'US Letter landscape'}}));
   assert.equal(await page.locator('#sheetPaperLabel').innerText(),'(US Letter landscape)');
   await page.selectOption('#paperOrientation','portrait');
   assert.deepEqual(await page.evaluate(()=>window.__messages.at(-1)),{action:'setReportPaper',preset:'letter',orientation:'portrait'});
   await page.evaluate(()=>window.__deliver({type:'workspace',items:[],dirty:true,selectedId:null,
    paper:{preset:'letter',orientation:'portrait',description:'US Letter portrait'}}));
   assert.equal(await page.locator('#paperOrientation').inputValue(),'portrait');
   assert.equal(await page.locator('#sheetPaperLabel').innerText(),'(US Letter portrait)');
   await page.evaluate(()=>window.__deliver({type:'workspace',items:[],dirty:false,selectedId:null,
    paper:{preset:'a3',orientation:'landscape',description:'A3 landscape'}}));
   assert.equal(await page.locator('#paperSize').inputValue(),'a3');
   const count=await page.evaluate(()=>window.__messages.length);
   await page.evaluate(()=>window.__deliver({type:'selected',name:'LAB',hostname:'LAB'}));
   await page.click('#run');
   assert(await page.locator('#paperSize').isDisabled());
   assert(await page.locator('#paperOrientation').isDisabled());
   assert.equal(await page.evaluate(()=>window.__messages.length),count+1);
   await page.evaluate(()=>window.__deliver({type:'complete',message:'Report complete.'}));
   assert(!(await page.locator('#paperSize').isDisabled()));
   if(process.env.SWITCHPORT_PAPER_SCREENSHOT&&viewport.width===1180)
    await page.screenshot({path:process.env.SWITCHPORT_PAPER_SCREENSHOT});
   const dimensions=await page.evaluate(()=>({scrollWidth:document.documentElement.scrollWidth,scrollHeight:document.documentElement.scrollHeight,width:innerWidth,height:innerHeight}));
   assert(dimensions.scrollWidth<=dimensions.width+1,'Paper selection introduced horizontal overflow: '+JSON.stringify(dimensions));
   assert.equal(errors.length,0,'Browser errors: '+errors.join('; '));
   console.log('PASS persisted report paper, user changes, build locking and viewport '+viewport.width+'x'+viewport.height);
   await page.close();
  }
 }finally{await browser.close();}
})().catch(error=>{console.error(error);process.exitCode=1;});
