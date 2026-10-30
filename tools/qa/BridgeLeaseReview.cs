using System;using System.IO;using System.Reflection;using System.Collections.Generic;using System.Security.Cryptography;using System.Web.Script.Serialization;using System.Windows;
public static class BridgeLeaseReview {
 static BindingFlags F=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance;static List<string> failures=new List<string>();
 static object Field(object o,string n){return o.GetType().GetField(n,F).GetValue(o);}static void Set(object o,string n,object v){o.GetType().GetField(n,F).SetValue(o,v);}static object Prop(object o,string n){return o.GetType().GetProperty(n,F).GetValue(o,null);}static object Call(object o,string n,params object[] a){return o.GetType().GetMethod(n,F).Invoke(o,a);}static void Check(bool b,string n){if(!b)failures.Add(n);}
 [STAThread]public static int Main(string[] args){
  string target=Path.GetFullPath(args[0]),folder=Path.GetFullPath(args[1]);Directory.CreateDirectory(folder);var report=new Dictionary<string,object>();using(var s=File.OpenRead(target))using(var h=SHA256.Create())report["candidate_sha256"]=BitConverter.ToString(h.ComputeHash(s)).Replace("-","").ToLowerInvariant();
  Window w=null;object smooth=null;int cases=0;
  try{
   var a=Assembly.LoadFrom(target);var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};w=(Window)Activator.CreateInstance(a.GetType("WhiteDragon.PetWindow"),new object[]{true});var b=Field(w,"behavior");var pref=Field(w,"preferences");smooth=Field(w,"smooth");
   foreach(string n in new[]{"Follow","AutoNap","LinkChatGPT","DailyRoutine","SmallTalkEnabled","FocusReading"}){var f=pref.GetType().GetField(n,F);if(f!=null)f.SetValue(pref,false);}Set(pref,"LinkCodex",true);Set(b,"AutoNap",false);Set(w,"bridgeFolder",folder);Set(w,"bridgeSnooze",0L);
   string path=Path.Combine(folder,"session-fixture.json");var json=new JavaScriptSerializer();DateTime utc=DateTime.UtcNow;
   Action<string> write=status=>File.WriteAllText(path,json.Serialize(new{version=1,source="codex-events",status=status,updated_utc=utc.ToString("o"),expires_utc=utc.AddSeconds(10).ToString("o")}));
   Action<long> read=now=>{Set(w,"bridgeNextRead",0L);Call(w,"UpdateBridge",now);};
   write("executing");read(100L);Check((int)Prop(b,"Animation")==29,"Initial native execution did not pick up");cases++;
   var durations=(Dictionary<int,int>)Prop(smooth,"MediaDurations");long tick=101+durations[29];Call(b,"Tick",tick,null,false,false);read(tick);Check((int)Prop(b,"Animation")==28,"Pickup did not enter tapping");cases++;
   int id=(int)Prop(b,"PlaybackId");string saved=path+".saved";if(File.Exists(saved))File.Delete(saved);File.Move(path,saved);
   for(int i=0;i<8;i++){read(tick+i*500);Check((string)Field(w,"bridgeStatus")=="executing"&&(string)Field(w,"bridgeSource")=="codex-events"&&(int)Prop(b,"PlaybackId")==id,"Temporary unavailable state reset phase at poll "+i);cases++;}
   File.Move(saved,path);read(tick+4000);Check((int)Prop(b,"PlaybackId")==id,"Restored file restarted tapping");cases++;
   var bridge=a.GetType("WhiteDragon.PetBridge");var retain=bridge.GetMethod("RetainEventLease");object cached=Field(w,"lastCodexState");
   Check(retain.Invoke(null,new object[]{null,cached,utc.AddSeconds(10)})==null,"Expired state retained at exact lease boundary");cases++;
   Check(retain.Invoke(null,new object[]{null,cached,utc.AddSeconds(11)})==null,"Expired state retained after lease");cases++;
   Set(cached,"expires_utc",utc.AddHours(2).ToString("o"));Check(retain.Invoke(null,new object[]{null,cached,utc})==null,"Invalid long lease retained");cases++;Set(cached,"expires_utc",utc.AddSeconds(10).ToString("o"));
   Set(cached,"source","chatgpt-client");Check(retain.Invoke(null,new object[]{null,cached,utc})==null,"Fallback observer state cached as native");cases++;Set(cached,"source","codex-events");
   write("working");read(tick+4100);Check((string)Field(w,"bridgeStatus")=="working"&&(int)Prop(b,"Animation")==30,"Explicit next phase failed to put down");cases++;
   Set(pref,"LinkCodex",false);read(tick+4200);Check(Field(w,"bridgeStatus")==null&&Field(w,"lastCodexState")==null,"Disabled link retained cached state");cases++;
  }catch(Exception e){failures.Add(e.ToString());}finally{if(w!=null)w.Close();if(smooth!=null)report["decoder_worker_stopped"]=Prop(smooth,"WorkerStopped");report["scope"]="Exact candidate WPF bridge reads with temporarily unavailable fixture files and synthetic runtime ticks; not native session callbacks";report["cases"]=cases;report["ok"]=failures.Count==0;report["failures"]=failures;File.WriteAllText(Path.Combine(folder,"bridge-lease-review.json"),new JavaScriptSerializer().Serialize(report));}
  return failures.Count==0?0:1;
 }
}
