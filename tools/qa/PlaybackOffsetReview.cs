using System;using System.IO;using System.Reflection;using System.Collections.Generic;using System.Security.Cryptography;using System.Web.Script.Serialization;using System.Windows.Media;using System.Windows.Media.Imaging;
public static class PlaybackOffsetReview {
 static readonly BindingFlags F=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;static readonly List<string> checks=new List<string>();
 static object C(object o,string n,params object[] args){try{foreach(var m in o.GetType().GetMethods(F))if(m.Name==n&&m.GetParameters().Length==args.Length)return m.Invoke(o,args);throw new MissingMethodException(n);}catch(TargetInvocationException e){throw e.InnerException??e;}}
 static object P(object o,string n){return o.GetType().GetProperty(n,F).GetValue(o,null);}static void Check(bool ok,string name){if(!ok)throw new Exception(name);if(!checks.Contains(name))checks.Add(name);}
 static byte[] Pixels(BitmapSource b){var p=new byte[640*360*4];b.CopyPixels(p,2560,0);return p;}static bool Equal(byte[] a,byte[] b){for(int i=0;i<a.Length;i++)if(a[i]!=b[i])return false;return true;}
 static void Save(BitmapSource b,string path){var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(b));using(var f=File.Create(path))png.Save(f);}
 [STAThread]public static int Main(string[] args){string target=Path.GetFullPath(args[0]),folder=Path.GetFullPath(args[1]);Directory.CreateDirectory(folder);var report=new Dictionary<string,object>();object smooth=null;
  try{
   using(var h=SHA256.Create())using(var f=File.OpenRead(target))report["candidate_sha256"]=BitConverter.ToString(h.ComputeHash(f)).Replace("-","").ToLowerInvariant();var a=Assembly.LoadFrom(target);smooth=Activator.CreateInstance(a.GetType("WhiteDragon.SmoothMotion"));var durations=(Dictionary<int,int>)P(smooth,"MediaDurations");object b=Activator.CreateInstance(a.GetType("WhiteDragon.PetBehavior"),new object[]{500,durations});
   C(b,"PlayFrom",17,true,1000L,550.0);Check(Math.Abs((double)C(b,"VisualTime",1000L)-550)<.01,"offset playback starts at the retained moving pose");
   C(b,"StartVisualPlayback",1200L);Check(Math.Abs((double)C(b,"VisualTime",1200L)-550)<.01,"decoder startup preserves the offset");
   C(b,"SetMediaWaiting",true,1250L);Check(Math.Abs((double)C(b,"VisualTime",2250L)-600)<.01,"decoder wait freezes an offset timeline");C(b,"SetMediaWaiting",false,2250L);Check(Math.Abs((double)C(b,"VisualTime",2300L)-650)<.01,"decoder resume advances from the offset without rewinding");
   C(b,"SetPaused",true,2300L);Check(Math.Abs((double)C(b,"VisualTime",3300L)-650)<.01,"user pause freezes an offset timeline");C(b,"SetPaused",false,3300L);Check(Math.Abs((double)C(b,"VisualTime",3350L)-700)<.01,"user resume does not consume paused time");
   C(b,"PlayFrom",17,true,4000L,550.0);C(b,"Tick",4799L,null,false,false);Check((int)P(b,"Animation")==17,"retained fold does not complete early");C(b,"Tick",4801L,null,false,false);Check((int)P(b,"Animation")==0&&(bool)P(b,"NaturalIdleTransition"),"retained fold returns naturally after its remaining 800 ms");
   Check((double)P(b,"PlaybackStartMilliseconds")==0,"next normal action clears the old offset");
   object archive=Activator.CreateInstance(a.GetType("WhiteDragon.TransitionFlowArchive"));var records=new List<object>();
   foreach(int sourceTime in new int[]{80,400,850,1200}){
    Check((bool)C(smooth,"EnsureSample",15,(double)sourceTime,10000),"flap source decodes at "+sourceTime);var source=(BitmapSource)C(smooth,"Get",15,(double)sourceTime);var sourcePixels=Pixels(source);int sourceSample=(int)C(smooth,"GetSampleIndex",15,(double)sourceTime);
    Check((bool)C(smooth,"EnsureSample",17,550.0,10000),"retained fold opening decodes");var incoming=Pixels((BitmapSource)C(smooth,"Get",17,550.0));object transition=Activator.CreateInstance(a.GetType("WhiteDragon.FrameTransition"));bool warped=(bool)C(transition,"BeginAligned",source,incoming,C(archive,"Get",15,sourceSample),C(archive,"Get",17,33),140);
    for(int step=0;step<=10;step++){
     double elapsed=step*14.0,time=550+elapsed;Check((bool)C(smooth,"EnsureSample",17,time,10000),"moving fold target decodes");incoming=Pixels((BitmapSource)C(smooth,"Get",17,time));if(step>0)C(transition,"FollowIncoming",incoming,C(archive,"Get",17,(int)C(smooth,"GetSampleIndex",17,time)));
     var image=(BitmapSource)C(transition,"PresentPixels",incoming,elapsed,warped);var actual=Pixels(image);
     if(step==0)Check(Equal(actual,sourcePixels),"midcycle fold preserves its first visible pose at "+sourceTime);if(step==10)Check(Equal(actual,incoming),"midcycle fold ends at the playing target pose at "+sourceTime);
     for(int p=0;p<actual.Length;p+=4)if(actual[p]>actual[p+3]||actual[p+1]>actual[p+3]||actual[p+2]>actual[p+3])throw new Exception("Premultiplied color bounds violated");
     if(step==0||step==3||step==6||step==10)Save(image,Path.Combine(folder,"fold-"+sourceTime+"-"+step.ToString("00")+".png"));
    }
    records.Add(new{source_time_ms=sourceTime,target_start_ms=550,bridge_ms=140,warped=warped});
   }
   report["flow_pairs"]=records;report["premultiplied_color_bounds"]=true;report["decoder_error"]=P(smooth,"LastError");Check(P(smooth,"LastError")==null,"decoder reports no failure");report["ok"]=true;
  }catch(Exception e){report["ok"]=false;report["error"]=e.ToString();}finally{if(smooth!=null){C(smooth,"Dispose");report["worker_stopped"]=P(smooth,"WorkerStopped");}report["checks"]=checks;report["scope"]="Exact candidate. Offset timeline startup, waits, pause, natural completion, plus four real flap phases into the moving retained fold, exact first/last pixels and premultiplied color bounds. Static rendered flow surfaces, not monitor cadence.";File.WriteAllText(Path.Combine(folder,"playback-offset-review.json"),new JavaScriptSerializer().Serialize(report));}
  return (bool)report["ok"]?0:1;
 }
}
