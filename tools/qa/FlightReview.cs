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
using System.Windows.Media;
using System.Windows.Threading;
public static class FlightReview {
 static readonly BindingFlags F=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance;
 static readonly List<string> errors=new List<string>();static Assembly assembly;
 static object Field(object o,string n){return o.GetType().GetField(n,F).GetValue(o);}
 static object Prop(object o,string n){return o.GetType().GetProperty(n,F).GetValue(o,null);}
 static double N(object o,string n){return Convert.ToDouble(Prop(o,n));}
 static object Call(object o,string n,params object[] args){try{foreach(var m in o.GetType().GetMethods(F))if(m.Name==n&&m.GetParameters().Length==args.Length)return m.Invoke(o,args);throw new MissingMethodException(n);}catch(TargetInvocationException e){throw e.InnerException??e;}}
 static void Check(bool ok,string message){if(!ok&&!errors.Contains(message))errors.Add(message);}
 static string Hash(string p){using(var h=SHA256.Create())using(var s=File.OpenRead(p))return BitConverter.ToString(h.ComputeHash(s)).Replace("-","").ToLowerInvariant();}
 static object Model(){return Activator.CreateInstance(assembly.GetType("WhiteDragon.GentleFlight",true));}
 static void Begin(object m,Point p,Vector v,Rect a,double angle){Call(m,"Begin",0L,p,v,a,350.0,180.0,angle);}
 static object Pointer(Type type,int x,int y){object p=Activator.CreateInstance(type);type.GetField("X").SetValue(p,x);type.GetField("Y").SetValue(p,y);return p;}
 static List<Dictionary<string,object>> Models(){
  var results=new List<Dictionary<string,object>>();
  var names=new string[]{"right","left","up_right","down","near_floor","left_monitor","portrait_monitor","outside_release"};
  var areas=new Rect[]{new Rect(0,0,1500,880),new Rect(0,0,1500,880),new Rect(0,0,1500,880),new Rect(0,0,1500,880),new Rect(0,0,1500,880),new Rect(-1920,0,1920,1040),new Rect(1920,-1000,1080,1920),new Rect(0,0,1500,880)};
  var origins=new Point[]{new Point(400,120),new Point(700,120),new Point(400,220),new Point(400,120),new Point(400,690),new Point(-1500,80),new Point(2200,-800),new Point(-40,730)};
  var velocities=new Vector[]{new Vector(1200,0),new Vector(-1200,0),new Vector(900,-1200),new Vector(100,1800),new Vector(900,0),new Vector(-1800,-600),new Vector(1500,-800),new Vector(-900,500)};
  for(int k=0;k<names.Length;k++){
   object m=Model();Begin(m,origins[k],velocities[k],areas[k],7);Call(m,"Step",0L);
   Check(Math.Abs(N(m,"X")-origins[k].X)<1e-9&&Math.Abs(N(m,"Y")-origins[k].Y)<1e-9&&Math.Abs(N(m,"Angle")-7)<1e-9,"Release position or angle jumps: "+names[k]);
   double duration=N(m,"Duration"),minY=origins[k].Y,lastY=origins[k].Y,lastX=origins[k].X,landingStep=0;bool descending=false,measuredLanding=false;
   var trace=new List<double[]>();int frames=(int)Math.Ceiling(duration*1000/16)+1;
   for(int i=0;i<=frames;i++){
    long t=i*16L;Call(m,"Step",t);double x=N(m,"X"),y=N(m,"Y"),vy=N(m,"VelocityY");minY=Math.Min(minY,y);if(vy>2)descending=true;
    if(k!=7){Check(x>=areas[k].Left-.01&&x+350<=areas[k].Right+.01,"Trajectory exceeds its own monitor: "+names[k]);Check(y>=areas[k].Top-.01&&y+180<=areas[k].Bottom+.01,"Vertical monitor bounds: "+names[k]);}
    if(i>0&&!measuredLanding&&!((bool)Prop(m,"Active"))){landingStep=Math.Sqrt((x-lastX)*(x-lastX)+(y-lastY)*(y-lastY));measuredLanding=true;}
    if(i%6==0||i==frames)trace.Add(new double[]{t,x,y,N(m,"Angle"),N(m,"VelocityX"),vy});lastY=y;lastX=x;
   }
   Check(!(bool)Prop(m,"Active")&&Math.Abs(N(m,"Y")+180-areas[k].Bottom)<1e-9&&Math.Abs(N(m,"Angle"))<1e-9,"Exact settled floor: "+names[k]);Check(landingStep<.6,"Touchdown snaps more than .6 logical pixels: "+names[k]);
   if(k==0)Check(N(m,"X")-origins[k].X>180&&descending,"Horizontal inertia or gravity missing");
   if(k==1)Check(N(m,"X")-origins[k].X< -180,"Left throw loses direction");
   if(k==2)Check(minY<origins[k].Y-70&&descending,"Upward throw does not rise and turn downward");
   if(k<4)Check(duration>2,"Descent does not slow down");
   results.Add(new Dictionary<string,object>{{"name",names[k]},{"origin",new double[]{origins[k].X,origins[k].Y}},{"velocity",new double[]{velocities[k].X,velocities[k].Y}},{"area",new double[]{areas[k].Left,areas[k].Top,areas[k].Right,areas[k].Bottom}},{"width",350},{"foot",180},{"angle",7},{"duration",duration},{"trace",trace},{"minimum_y",minY},{"landing_step_dip",landingStep}});
  }
  object f=Model();Begin(f,new Point(400,120),new Vector(1100,-600),areas[0],7);Call(f,"Step",400L);double px=N(f,"X"),py=N(f,"Y"),pa=N(f,"Angle");Call(f,"SetPaused",true,400L);Call(f,"Step",10400L);Check(N(f,"X")==px&&N(f,"Y")==py&&N(f,"Angle")==pa,"Paused physical model moves");Call(f,"SetPaused",false,10400L);Call(f,"Step",10400L);Check(N(f,"X")==px&&N(f,"Y")==py,"Model catches up paused distance");Call(f,"Cancel");Call(f,"Step",50000L);Check(N(f,"X")==px&&N(f,"Y")==py,"Canceled flight continues moving");
  return results;
 }
 static void Gesture(){
  var type=assembly.GetType("WhiteDragon.ThrowGesture",true);object gesture=Activator.CreateInstance(type);var release=type.GetMethod("TryRelease");
  foreach(int speed in new int[]{180,650,1500}){Call(gesture,"Begin",0L,0.0,0.0);for(int i=1;i<=8;i++)Call(gesture,"Add",i*20L,speed*i*.02,0.0);object[] args={165L,new Vector()};bool thrown=(bool)release.Invoke(gesture,args);Check(thrown==(speed>=450),"Moderate or slow throw misclassified: "+speed);}
  Call(gesture,"Begin",0L,0.0,0.0);for(int i=1;i<=8;i++)Call(gesture,"Add",i*20L,i*40.0,0.0);object[] held={330L,new Vector()};Check(!(bool)release.Invoke(gesture,held),"Moving then holding still throws");
 }
 [STAThread]public static int Main(string[] args){
  string target=Path.GetFullPath(args[0]),output=Path.GetFullPath(args[1]);Directory.CreateDirectory(output);var report=new Dictionary<string,object>{{"candidate_sha256",Hash(target)}};
  try{
   assembly=Assembly.LoadFrom(target);report["models"]=Models();Gesture();report["moderate_flick_detected"]=true;report["model_pause_and_cancel"]=true;
   var resources=new Dictionary<string,string>();foreach(string name in assembly.GetManifestResourceNames())using(var stream=assembly.GetManifestResourceStream(name))using(var hash=SHA256.Create())resources[name]=BitConverter.ToString(hash.ComputeHash(stream)).Replace("-","").ToLowerInvariant();report["embedded_resources"]=resources;
   var app=new Application{ShutdownMode=ShutdownMode.OnMainWindowClose};Type wt=assembly.GetType("WhiteDragon.PetWindow"),pt=wt.GetNestedType("POINT");var w=(Window)Activator.CreateInstance(wt,new object[]{true});w.Opacity=0;w.Topmost=false;w.ShowActivated=false;
   var image=(Image)Field(w,"image");object behavior=Field(w,"behavior"),flight=Field(w,"flight"),motion=Field(w,"motion"),prefs=Field(w,"preferences"),smooth=Field(w,"smooth");var clock=(Stopwatch)Field(w,"clock");var production=(DispatcherTimer)Field(w,"timer");
   foreach(string p in new string[]{"Follow","AutoNap","LinkCodex"})prefs.GetType().GetField(p).SetValue(prefs,false);behavior.GetType().GetField("AutoNap").SetValue(behavior,false);
   var timer=new DispatcherTimer(DispatcherPriority.Input){Interval=TimeSpan.FromMilliseconds(16)};int phase=0,pausePhase=0,probes=0;long began=0,flightAt=0,pauseAt=0,landedAt=0;Point pointer=new Point();int lastRow=-1;var rows=new List<int>();var trace=new List<double[]>();Rect area=new Rect();double lastX=0,lastY=0,maxStep=0,maxScale=0,maxFloor=0,releaseJump=0;double frozenX=0,frozenY=0,frozenAngle=0;
   Action begin=delegate{area=(Rect)Call(w,"CurrentWorkArea");double pad=(double)Prop(w,"RigPadding");w.Left=area.Left+area.Width*.35-pad;w.Top=area.Top+Math.Max(30,(area.Height-180)*.32)-pad;Point local=new Point(image.Width*.5,image.Height*.3);pointer=image.PointToScreen(local);Call(w,"BeginPointer",local,1,Pointer(pt,(int)Math.Round(pointer.X),(int)Math.Round(pointer.Y)),clock.ElapsedMilliseconds);};
   Action<long> tick=delegate(long now){Call(w,"AdvanceFlight",now);Call(behavior,"Tick",now,null,false,false);Call(w,"FoldAfterDownstroke",now);Call(w,"RenderFrame");};
   w.Loaded+=delegate{production.Stop();began=clock.ElapsedMilliseconds;timer.Start();};
   timer.Tick+=delegate{
    try{
     long now=clock.ElapsedMilliseconds,elapsed=now-began;
     if(phase==0){tick(now);if(elapsed>500){begin();began=now;phase=1;}return;}
     if(phase==1||phase==4){
      int x=(int)Math.Round(pointer.X+elapsed*1.8),y=(int)Math.Round(pointer.Y-elapsed*.65);Call(w,"MovePointer",Pointer(pt,x,y),now);Call(behavior,"Tick",now,null,false,false);Call(w,"RenderFrame");
      if(elapsed>=240){double a=N(motion,"Angle"),oldX=w.Left,oldY=w.Top;Call(w,"EndPointer",now);Check((bool)Prop(flight,"Active")&&N(behavior,"Animation")==16,"Native pointer release does not start wings and physics");releaseJump=Math.Max(releaseJump,Math.Abs(N(flight,"Angle")-a));Check(Math.Abs(w.Left-oldX)<.01&&Math.Abs(w.Top-oldY)<.01&&releaseJump<.0001,"Native release position or swing snaps");flightAt=now;began=now;lastX=w.Left;lastY=w.Top;phase=phase==1?2:5;}return;
     }
     tick(now);
     if(phase==2){
      int row=(int)N(behavior,"Animation");if(row!=lastRow){rows.Add(row);lastRow=row;}
      var scale=(ScaleTransform)Field(w,"stretch");maxScale=Math.Max(maxScale,Math.Max(Math.Abs(scale.ScaleX-1),Math.Abs(scale.ScaleY-1)));Check(maxScale==0,"Physics scales the character");
      double step=Math.Sqrt((w.Left-lastX)*(w.Left-lastX)+(w.Top-lastY)*(w.Top-lastY));maxStep=Math.Max(maxStep,step);lastX=w.Left;lastY=w.Top;probes++;
      if(probes%3==0)trace.Add(new double[]{now-flightAt,w.Left,w.Top,N(flight,"Angle"),N(flight,"VelocityX"),N(flight,"VelocityY"),row});
      if(pausePhase==0&&now-flightAt>650&&(bool)Prop(flight,"Active")){Call(behavior,"SetPaused",true,now);tick(now);frozenX=w.Left;frozenY=w.Top;frozenAngle=N(flight,"Angle");pauseAt=now;pausePhase=1;}
      else if(pausePhase==1){Check(w.Left==frozenX&&w.Top==frozenY&&N(flight,"Angle")==frozenAngle,"Native pause moves the trajectory");if(now-pauseAt>1000){Call(behavior,"SetPaused",false,now);tick(now);Check(w.Left==frozenX&&w.Top==frozenY,"Native resume catches up paused movement");pausePhase=2;}}
      double pad=(double)Prop(w,"RigPadding"),width=(double)Field(w,"bodyWidth"),foot=width*330/640;
      maxFloor=Math.Max(maxFloor,w.Top+pad+foot-area.Bottom);Check(maxFloor<.01,"Native flight crosses taskbar floor");
      if(!(bool)Prop(flight,"Active")&&!(bool)Field(w,"pendingWingFold")&&row==0&&now-flightAt>1500){Check(Math.Abs(w.Top+pad+foot-area.Bottom)<.01,"Native visible soles do not land on the work area");Check(rows.Contains(16)&&rows.Contains(15)&&rows.Contains(17)&&rows.Contains(0),"Native flight omits opening/flap/folding/idle");landedAt=now;phase=3;began=now;}
     }else if(phase==3&&elapsed>350){Check(Math.Abs(w.Top-lastY)<.01,"Landed window drifts");begin();began=now;phase=4;}
     else if(phase==5&&elapsed>600){Point local=new Point(image.Width*.5,image.Height*.3);Point p=image.PointToScreen(local);Call(w,"BeginPointer",local,1,Pointer(pt,(int)p.X,(int)p.Y),now);Check(!(bool)Prop(flight,"Active")&&(bool)Prop(behavior,"Pressed"),"Cannot catch the pet in midair");Call(behavior,"CancelPointer",now);Call(motion,"Reset");Call(w,"RenderFrame");report["midair_regrab_cancels_trajectory"]=true;timer.Stop();w.Close();}
     if(now-began>25000||clock.ElapsedMilliseconds>40000)throw new Exception("Native flight review timed out");
    }catch(Exception e){errors.Add(e.ToString());timer.Stop();w.Close();}
   };
   app.Run(w);report["native_rows"]=rows;report["native_trace"]=trace;report["native_pixel_probes"]=probes;report["native_pause_resume"]=pausePhase==2;report["release_angle_jump_degrees"]=releaseJump;report["runtime_scale_deviation"]=maxScale;report["max_floor_overshoot_dip"]=maxFloor;report["max_motion_step_dip"]=maxStep;report["worker_stopped"]=Prop(smooth,"WorkerStopped");Check((bool)report["worker_stopped"],"Flight worker remains alive");
  }catch(Exception e){errors.Add(e.ToString());}
  report["ok"]=errors.Count==0;report["failures"]=errors;report["scope"]="Exact candidate. Deterministic force-model paths and gesture classification plus real WPF pointer-release, pause/resume, complete landing chain and midair regrab in an invisible test window. System cursor and installed settings untouched.";
  File.WriteAllText(Path.Combine(output,"flight-review.json"),new JavaScriptSerializer{MaxJsonLength=6000000}.Serialize(report));return errors.Count==0?0:1;
 }
}
