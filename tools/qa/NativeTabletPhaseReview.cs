using System;using System.IO;using System.Reflection;using System.Collections;using System.Collections.Generic;using System.Diagnostics;using System.Security.Cryptography;using System.Web.Script.Serialization;using System.Windows;using System.Windows.Threading;using System.Windows.Controls;using System.Windows.Media.Imaging;
public static class NativeTabletPhaseReview {
 static BindingFlags F=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance;static List<string> failures=new List<string>();static List<object> events=new List<object>();static Dictionary<string,object> report=new Dictionary<string,object>();
 static object Field(object o,string n){return o.GetType().GetField(n,F).GetValue(o);}static void Set(object o,string n,object v){o.GetType().GetField(n,F).SetValue(o,v);}static object Prop(object o,string n){return o.GetType().GetProperty(n,F).GetValue(o,null);}static object Call(object o,string n,params object[] a){return o.GetType().GetMethod(n,F).Invoke(o,a);}static int I(object o,string n){return Convert.ToInt32(Prop(o,n));}static void Check(bool b,string n){if(!b)failures.Add(n);}
 [STAThread]public static int Main(string[] args){
  string output=Path.GetFullPath(args[1]);Directory.CreateDirectory(output);string target=Path.GetFullPath(args[0]);using(var s=File.OpenRead(target))using(var h=SHA256.Create())report["candidate_sha256"]=BitConverter.ToString(h.ComputeHash(s)).Replace("-","").ToLowerInvariant();
  report["scope"]="Real timed hidden WPF rendering with synthetic phase input; no installed settings, state files or native event claim";
  var a=Assembly.LoadFrom(target);var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};var w=(Window)Activator.CreateInstance(a.GetType("WhiteDragon.PetWindow"),new object[]{true});w.Opacity=0;w.Topmost=false;w.ShowActivated=false;
  var behavior=Field(w,"behavior");var pref=Field(w,"preferences");var smooth=Field(w,"smooth");var phases=Field(w,"tabletPhases");var clock=(Stopwatch)Field(w,"clock");
  foreach(string n in new[]{"Follow","AutoNap","LinkCodex","LinkChatGPT","DailyRoutine","SmallTalkEnabled","FocusReading"}){var f=pref.GetType().GetField(n,F);if(f!=null)f.SetValue(pref,false);}Set(behavior,"AutoNap",false);Set(w,"bridgeNextRead",long.MaxValue);Set(w,"bridgeSnooze",0L);
  var durations=(Dictionary<int,int>)Prop(smooth,"MediaDurations");int stage=0;long began=0,loopStarted=0;int lastRow=-999,lastId=-1,lastSample=-1,wraps=0;var rendered=new HashSet<int>();var loopIds=new HashSet<int>();var cueIds=new HashSet<int>();bool exitingSeen=false,readingAfterEarlyEnd=false,pixelsSaved=false;int lastLogic=-1;
  Action<string> force=status=>{Set(w,"bridgeStatus",status);Set(w,"bridgeSource","phase-review-synthetic");Set(w,"bridgeNextRead",long.MaxValue);Call(w,"UpdateBridge",clock.ElapsedMilliseconds);};
  var timer=new DispatcherTimer(DispatcherPriority.Normal){Interval=TimeSpan.FromMilliseconds(20)};
  Action save=()=>{report["ok"]=failures.Count==0;report["failures"]=failures;report["events"]=events;report["rendered_rows"]=new List<int>(rendered);report["loop_wraps"]=wraps;report["loop_playback_ids"]=new List<int>(loopIds);report["completion_playback_ids"]=new List<int>(cueIds);report["entries"]=I(phases,"Entries");report["exits"]=I(phases,"Exits");report["reading_after_early_end"]=readingAfterEarlyEnd;File.WriteAllText(Path.Combine(output,"native-tablet-phases.json"),new JavaScriptSerializer{MaxJsonLength=2000000}.Serialize(report));};
  timer.Tick+=delegate{try{
   long now=clock.ElapsedMilliseconds;int row=Convert.ToInt32(Field(w,"shownRow")),id=I(behavior,"PlaybackId"),logic=I(behavior,"Animation"),sample=Convert.ToInt32(Field(w,"presentedSampleIndex"));rendered.Add(row);
   if(row!=lastRow||id!=lastId||logic!=lastLogic){events.Add(new{ms=now,row=row,logical_row=logic,playback_id=id,sample=sample,stage=stage});lastRow=row;lastId=id;lastLogic=logic;}
   if(stage==0&&now>500){force("executing");began=now;stage=1;}
   else if(stage==1){
    force("executing");if(row==28){if(loopStarted==0)loopStarted=now;loopIds.Add(id);if(lastSample>sample+50)wraps++;lastSample=sample;
     if(!pixelsSaved&&now-loopStarted>350){var bitmap=(BitmapSource)((Image)Field(w,"image")).Source;var data=new byte[bitmap.PixelWidth*bitmap.PixelHeight*4];bitmap.CopyPixels(data,bitmap.PixelWidth*4,0);int opaque=0;for(int k=3;k<data.Length;k+=4)if(data[k]>128)opaque++;Check(opaque>1000,"Actual WPF tapping surface is transparent");report["actual_tapping_surface_opaque_pixels"]=opaque;var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using(var stream=File.Create(Path.Combine(output,"actual-wpf-tapping.png")))encoder.Save(stream);pixelsSaved=true;}
    }
    if(loopStarted>0&&now-loopStarted>durations[28]*2+500){Check(rendered.Contains(29),"Pickup row was not actually rendered");Check(loopIds.Count==1,"Repeated execution restarted the tapping playback");Check(wraps>=2,"Did not cross two actual tapping loop boundaries");force("ready");began=now;stage=2;}
   }else if(stage==2){
    force("ready");if(row==30)exitingSeen=true;if(row==21)cueIds.Add(id);
    if(exitingSeen&&cueIds.Count>0&&row==0&&logic==0){Check(cueIds.Count==1,"Completion cue replayed");Check(I(phases,"Entries")==1&&I(phases,"Exits")==1,"First execution did not have one pickup and one putdown");force("executing");began=now;stage=3;}
   }else if(stage==3){
    if(now-began>150)force("working");else force("executing");
    if(now-began>150&&row==19&&logic==19){readingAfterEarlyEnd=true;Check(I(phases,"Entries")==2&&I(phases,"Exits")==2,"Short execution repeated a transition");force("ready");began=now;stage=4;}
   }else if(stage==4){
    if(logic==21){force("executing");Check(I(behavior,"Animation")==29,"A new task failed to preempt completion with pickup");Check(I(phases,"Entries")==3,"New phase pickup count incorrect");timer.Stop();save();w.Close();app.Shutdown(failures.Count==0?0:1);}
   }
   if(now>35000){failures.Add("Timed WPF review expired in stage "+stage);timer.Stop();save();w.Close();app.Shutdown(1);}
  }catch(Exception e){failures.Add(e.ToString());timer.Stop();save();w.Close();app.Shutdown(1);}};
  w.Show();timer.Start();app.Run();report["decoder_worker_stopped"]=Prop(smooth,"WorkerStopped");save();return failures.Count==0?0:1;
 }
}
