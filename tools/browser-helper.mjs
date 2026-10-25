import {spawn} from 'node:child_process';
import {resolve} from 'node:path';
import {pathToFileURL} from 'node:url';
import {existsSync} from 'node:fs';

export async function openBrowser(html,profile){
 const binary=process.env.PET_BROWSER||['C:/Program Files/Google/Chrome/Application/chrome.exe','C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe','C:/Program Files/Microsoft/Edge/Application/msedge.exe'].find(existsSync);
 if(!binary)throw Error('Install Chrome/Edge or set PET_BROWSER to its executable path');
 const browser=spawn(binary,[
  '--headless=new','--disable-gpu','--no-first-run','--no-default-browser-check',
  '--disable-background-networking','--remote-debugging-port=0',
  '--user-data-dir='+resolve(profile),'--window-size=1200,1000',pathToFileURL(resolve(html)).href,
 ],{windowsHide:true,stdio:['ignore','ignore','pipe']});
 const endpoint=await new Promise((yes,no)=>{let err='';browser.stderr.on('data',d=>{err+=d;const m=err.match(/DevTools listening on (ws:\/\/\S+)/);if(m)yes(m[1]);});browser.once('error',no);browser.once('exit',c=>no(new Error('Chrome exited '+c+' '+err)));});
 const socket=new WebSocket(endpoint);await new Promise((yes,no)=>{socket.onopen=yes;socket.onerror=no;});
 let seq=0;const pending=new Map(),exceptions=[];
 socket.onmessage=e=>{const m=JSON.parse(e.data);if(m.id){const p=pending.get(m.id);pending.delete(m.id);if(m.error)p.reject(new Error(JSON.stringify(m.error)));else p.resolve(m.result);}if(m.method==='Runtime.exceptionThrown')exceptions.push(m.params);};
 const request=(method,params={},sessionId)=>new Promise((resolve,reject)=>{const id=++seq;pending.set(id,{resolve,reject});socket.send(JSON.stringify({id,method,params,...(sessionId?{sessionId}:{})}));});
 const {targetInfos}=await request('Target.getTargets');
 const target=targetInfos.find(t=>t.type==='page'&&t.url.startsWith('file:'));if(!target)throw Error('No local page');
 const {sessionId}=await request('Target.attachToTarget',{targetId:target.targetId,flatten:true});
 const call=(method,params={})=>request(method,params,sessionId);
 await call('Runtime.enable');
 const evaluate=async expression=>{const r=await call('Runtime.evaluate',{expression,awaitPromise:true,returnByValue:true});if(r.exceptionDetails)throw Error(JSON.stringify(r.exceptionDetails));return r.result.value;};
 const close=async()=>{try{await request('Browser.close');}catch{}finally{socket.close();browser.kill();}};
 return {browser,evaluate,call,close,exceptions};
}
export const wait=ms=>new Promise(r=>setTimeout(r,ms));
export const check=(ok,label)=>{if(!ok)throw Error(label);};
