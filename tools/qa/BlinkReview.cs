using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
public static class BlinkReview {
 static readonly BindingFlags F=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance;
 static readonly List<string> errors=new List<string>();
 static object Field(object o,string n){return o.GetType().GetField(n,F).GetValue(o);}
 static object Prop(object o,string n){return o.GetType().GetProperty(n,F).GetValue(o,null);}
 static object Call(object o,string n,params object[] a){try{return o.GetType().GetMethod(n,F).Invoke(o,a);}catch(TargetInvocationException e){throw e.InnerException??e;}}
 static int Number(object o,string n){return Convert.ToInt32(Prop(o,n));}
 static void Check(bool ok,string message){if(!ok&&!errors.Contains(message))errors.Add(message);}
 static string Hash(string p){using(var h=SHA256.Create())using(var s=File.OpenRead(p))return BitConverter.ToString(h.ComputeHash(s)).Replace("-","").ToLowerInvariant();}
 static byte[] Pixels(Image image){var p=new byte[640*360*4];((BitmapSource)image.Source).CopyPixels(p,2560,0);return p;}
 static bool Equal(byte[] a,byte[] b){for(int i=0;i<a.Length;i++)if(a[i]!=b[i])return false;return true;}
 [STAThread]public static int Main(string[] args){
  string target=Path.GetFullPath(args[0]),output=Path.GetFullPath(args[1]);Directory.CreateDirectory(output);
  var report=new Dictionary<string,object>{{"candidate_sha256",Hash(target)}};
  try{
   var assembly=Assembly.LoadFrom(target);var type=assembly.GetType("WhiteDragon.BlinkSchedule",true);
   object simulated=Activator.CreateInstance(type,new object[]{12345,13,2800,7200,220,280});
   for(int i=0;i<30000;i++)Call(simulated,"Advance",10.0,true,false);
   var history=(IList)Field(simulated,"History");var intervals=new HashSet<int>();var phases=new HashSet<int>();
   foreach(double[] e in history){Check(e[1]>=2800&&e[1]<=7200,"Random interval out of range");Check(e[2]>=220&&e[2]<=280,"Blink duration out of range");intervals.Add((int)e[1]);phases.Add((int)(e[0]%5000/500));}
   Check(history.Count>=30&&intervals.Count>=20&&phases.Count>=7,"Blinks remain fixed or locked to five-second body loop");
   int before=Number(simulated,"Frame");for(int i=0;i<100;i++)Call(simulated,"Advance",0.0,true,false);Check(Number(simulated,"Frame")==before,"Zero idle time changes blink phase");Call(simulated,"Advance",10.0,false,false);Check(Number(simulated,"Frame")==-1,"Eye animation continues in another action");
   report["simulated_idle_ms"]=300000;report["random_intervals"]=history;report["body_loop_phase_bins"]=phases.Count;
   var app=new Application{ShutdownMode=ShutdownMode.OnMainWindowClose};var window=(Window)Activator.CreateInstance(assembly.GetType("WhiteDragon.PetWindow"),new object[]{true});window.Opacity=0;window.Topmost=false;window.ShowActivated=false;
   object behavior=Field(window,"behavior"),blink=Field(window,"idleBlink"),schedule=Field(blink,"Schedule"),smooth=Field(window,"smooth"),preferences=Field(window,"preferences");var image=(Image)Field(window,"image");var clock=(Stopwatch)Field(window,"clock");
   foreach(string p in new string[]{"Follow","AutoNap","LinkCodex"})preferences.GetType().GetField(p).SetValue(preferences,false);behavior.GetType().GetField("AutoNap").SetValue(behavior,false);
   var timer=new DispatcherTimer(DispatcherPriority.Input){Interval=TimeSpan.FromMilliseconds(16)};
   int frozenKind=0,completedFreeze=0,frozenFrame=-1,loops=0,lastSample=-1,eyePixelProbes=0,outsidePixelProbes=0;long freezeAt=0,began=0,switchedAt=0;byte[] frozen=null,expected=new byte[640*360*4];
   var observed=new List<int>();int lastEye=-2;
   window.Loaded+=delegate{began=clock.ElapsedMilliseconds;timer.Start();};
   timer.Tick+=delegate{
    try{
     long now=clock.ElapsedMilliseconds,elapsed=now-began;int eye=Number(schedule,"Frame"),started=Number(schedule,"Started");
     if(eye!=lastEye){observed.Add(eye);lastEye=eye;}
     if(frozenKind>0){
      Check(eye==frozenFrame&&Equal(frozen,Pixels(image)),"Blink pixels or phase advance during freeze "+frozenKind);
      if(now-freezeAt>=600){if(frozenKind==1)Call(behavior,"SetPaused",false,now);else if(frozenKind==2)Call(behavior,"SetMenu",false,now);else Call(behavior,"ResumeAfterHidden",now);completedFreeze=frozenKind;frozenKind=0;}
      return;
     }
     if(completedFreeze<3&&started>completedFreeze&&eye>=1&&eye<=3){
      frozenKind=completedFreeze+1;freezeAt=now;if(frozenKind==1)Call(behavior,"SetPaused",true,now);else if(frozenKind==2)Call(behavior,"SetMenu",true,now);else Call(behavior,"SuspendHidden",now);Call(window,"RenderFrame");frozenFrame=Number(schedule,"Frame");frozen=Pixels(image);return;
     }
     if(Convert.ToInt32(Field(window,"shownRow"))==0&&elapsed>500){
      int sample=Convert.ToInt32(Field(window,"presentedSampleIndex"));if(lastSample>270&&sample<30)loops++;lastSample=sample;
      if(eye>=1&&eye<=11){
       var visible=(byte[])Field(window,"livePixels");double time=Convert.ToDouble(Field(window,"presentedVisualTime"));bool ready=(bool)Call(smooth,"TryCopyExact",0,time,expected);
       if(ready){int outside=0,inside=0;for(int y=0;y<360;y++)for(int x=0;x<640;x++)for(int c=0;c<4;c++){int p=(y*640+x)*4+c;if(visible[p]!=expected[p]){if(x>=263&&x<373&&y>=153&&y<217)inside++;else outside++;}}
        Check(outside==0,"Blink changes body, hands, hair, or colours outside eye rectangle");outsidePixelProbes++;if(inside>20)eyePixelProbes++;
       }
      }
     }
     if(elapsed>=35000&&switchedAt==0){Check(completedFreeze==3,"Pause, menu and hidden blink freezes not all tested");Check(started>=3&&loops>=4,"Too few real blink or body-loop boundaries");Call(window,"Choose",3);switchedAt=now;return;}
     if(switchedAt>0&&now-switchedAt>=500){Check(Number(schedule,"Frame")==-1&&Number(behavior,"Animation")==3,"Eye overlay continues after changing to wave");Check(eyePixelProbes>=12&&outsidePixelProbes>=12,"Too few real eye-only surface probes");timer.Stop();window.Close();}
    }catch(Exception e){errors.Add(e.ToString());timer.Stop();window.Close();}
   };
   app.Run(window);report["real_idle_duration_ms"]=clock.ElapsedMilliseconds;report["real_blink_history"]=Field(schedule,"History");report["real_started"]=Number(schedule,"Started");report["real_completed"]=Number(schedule,"Completed");report["body_loop_boundaries"]=loops;report["freeze_cases_passed"]=completedFreeze;report["eye_pixel_probes"]=eyePixelProbes;report["outside_eye_unchanged_probes"]=outsidePixelProbes;report["observed_eye_frames"]=observed;report["worker_stopped"]=Prop(smooth,"WorkerStopped");Check((bool)report["worker_stopped"],"Blink QA decoder worker remains alive");
  }catch(Exception e){errors.Add(e.ToString());}
  report["ok"]=errors.Count==0;report["failures"]=errors;report["scope"]="Independent seeded five-minute schedule and 35-second hidden native WPF playback; freeze behavior and eye-only surface changes inspected. No OS cursor movement or installed settings.";
  File.WriteAllText(Path.Combine(output,"blink-review.json"),new JavaScriptSerializer{MaxJsonLength=4000000}.Serialize(report));return errors.Count==0?0:1;
 }
}
