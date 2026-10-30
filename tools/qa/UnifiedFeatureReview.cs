using System;using System.IO;using System.Reflection;using System.Collections.Generic;using System.Security.Cryptography;using System.Web.Script.Serialization;using System.Windows;
public static class UnifiedFeatureReview {
 static BindingFlags F=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance;static List<string> checks=new List<string>();
 static object G(object o,string n){return o.GetType().GetField(n,F).GetValue(o);}static void S(object o,string n,object v){o.GetType().GetField(n,F).SetValue(o,v);}static object P(object o,string n){return o.GetType().GetProperty(n,F).GetValue(o,null);}
 static object C(object o,string n,params object[] v){foreach(var m in o.GetType().GetMethods(F))if(m.Name==n&&m.GetParameters().Length==v.Length)return m.Invoke(o,v);throw new Exception(n);}
 static void Check(bool b,string n){if(!b)throw new Exception(n);checks.Add(n);}static string Hash(Stream s){using(var h=SHA256.Create())return BitConverter.ToString(h.ComputeHash(s)).Replace("-","").ToLowerInvariant();}
 [STAThread]public static int Main(string[] args){var report=new Dictionary<string,object>();Window w=null;object smooth=null;string folder=Path.GetFullPath(args[1]);Directory.CreateDirectory(folder);
 try{
  var a=Assembly.LoadFile(Path.GetFullPath(args[0]));using(var s=File.OpenRead(args[0]))report["candidate_sha256"]=Hash(s);
  var book=Assembly.LoadFile(Path.GetFullPath(args[2]));var tablet=Assembly.LoadFile(Path.GetFullPath(args[3]));int resources=0;
  foreach(string name in book.GetManifestResourceNames()){
   if(name=="WhiteDragon.MotionSpec"||name=="WhiteDragon.FlowSpec"||name=="WhiteDragon.FallbackSpec"||name=="WhiteDragon.Posters")continue;
   using(var old=book.GetManifestResourceStream(name))using(var current=a.GetManifestResourceStream(name)){Check(current!=null&&Hash(old)==Hash(current),"Retained book resource "+name);resources++;}
  }
  foreach(int row in new[]{28,29,30})foreach(string kind in new[]{"Motion","Flow"}){
   string name="WhiteDragon."+kind+row;using(var old=tablet.GetManifestResourceStream(name))using(var current=a.GetManifestResourceStream(name)){Check(current!=null&&Hash(old)==Hash(current),"Retained tablet resource "+name);resources++;}
  }
  Check(resources==61&&a.GetManifestResourceNames().Length==65,"All 61 branch payloads and four merged descriptors are present");report["retained_resource_hashes"]=resources;
  var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};w=(Window)Activator.CreateInstance(a.GetType("WhiteDragon.PetWindow"),new object[]{true});smooth=G(w,"smooth");var b=G(w,"behavior");var prefs=G(w,"preferences");
  foreach(string n in new[]{"Follow","AutoNap","LinkChatGPT","DailyRoutine","SmallTalkEnabled","FocusReading"})S(prefs,n,false);S(prefs,"LinkCodex",true);S(b,"AutoNap",false);
  var durations=(Dictionary<int,int>)P(smooth,"MediaDurations");var bt=a.GetType("WhiteDragon.PetBehavior");
  foreach(int row in new[]{22,24}){
   var pose=Activator.CreateInstance(bt,new object[]{500,durations});C(pose,"Play",row,false,0L);C(pose,"Tick",1500L,null,false,false);C(pose,"SelectAfterExit",28,1500L);
   Check((bool)P(pose,"RecoveringPose"),"Manual tablet selection retains pose recovery from "+row);
   for(long tick=1550;tick<=18000;tick+=50)C(pose,"Tick",tick,null,false,false);
   Check((int)P(pose,"Animation")==28&&!(bool)P(pose,"SingleAction")&&!(bool)P(pose,"RecoveringPose")&&(double)P(pose,"PlaybackRate")==1,"Manual tablet loops at normal rate after recovery from "+row);
  }
  var library=G(w,"readingLibrary");string fixture=Path.Combine(folder,"bridge-fixture");S(w,"bridgeFolder",fixture);S(w,"bridgeSnooze",0L);var bridge=a.GetType("WhiteDragon.PetBridge");
  Action<string,long> write=(status,now)=>{bridge.GetMethod("Write").Invoke(null,new object[]{fixture,"unified",status,"codex-events",DateTime.UtcNow});S(w,"bridgeNextRead",0L);C(w,"UpdateBridge",now);};
  write("working",1000);C(b,"Play",19,false,2000L);library.GetType().GetProperty("RemainingMilliseconds").GetSetMethod(true).Invoke(library,new object[]{0.0});C(w,"UpdateReadingLibrary",2000L);
  Check((int)P(b,"Animation")==25&&(bool)G(w,"automaticBookChange"),"Task reading retains automatic book exchange");int swapId=(int)P(b,"PlaybackId");
  write("review",2001);Check((int)P(b,"Animation")==25&&(int)P(b,"PlaybackId")==swapId,"Same continuous reading status preserves active book exchange");
  write("executing",2010);Check((int)P(b,"Animation")==29&&!(bool)P(library,"InExchange")&&!(bool)G(w,"automaticBookChange"),"A real command safely preempts book exchange with one pickup");
  C(b,"Tick",2011L+durations[29],null,false,false);write("executing",2012+durations[29]);int loopId=(int)P(b,"PlaybackId");
  Check((int)P(b,"Animation")==28,"Pickup enters tablet loop after merged book path");write("executing",2013+durations[29]);Check((int)P(b,"PlaybackId")==loopId,"Repeated command callback does not replay pickup");
  write("ready",2020+durations[29]);Check((int)P(b,"Animation")==30,"Reply completion puts tablet away before the cue");
  long after=2021L+durations[29]+durations[30];C(b,"Tick",after,null,false,false);write("ready",after+1);Check((int)P(b,"Animation")==21,"Merged tablet exit reaches completion cue");int cue=(int)P(b,"PlaybackId");write("ready",after+2);Check((int)P(b,"PlaybackId")==cue,"Completion heartbeat does not replay cue");
  report["ok"]=true;
 }catch(Exception e){report["ok"]=false;report["error"]=e.ToString();}finally{if(w!=null)w.Close();if(smooth!=null)C(smooth,"Dispose");report["checks"]=checks;report["scope"]="Exact merged runtime, branch resource hashes, recovery to manual tablet, working/review book exchange and command/completion interoperation. Synthetic clocks and fixtures; live display is separate.";File.WriteAllText(Path.Combine(folder,"unified-feature-review.json"),new JavaScriptSerializer().Serialize(report));}
 return (bool)report["ok"]?0:1;
 }
}
