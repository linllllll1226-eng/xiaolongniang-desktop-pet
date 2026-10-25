using System;using System.IO;using System.Collections;using System.Collections.Generic;using System.Diagnostics;using System.Reflection;using System.Security.Cryptography;using System.Web.Script.Serialization;using System.Windows.Media;using System.Windows.Media.Imaging;
public static class TransitionReview {
 static readonly BindingFlags F=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;static readonly List<string> errors=new List<string>();
 static object Call(object o,string n,params object[] a){try{foreach(var m in o.GetType().GetMethods(F))if(m.Name==n&&m.GetParameters().Length==a.Length)return m.Invoke(o,a);throw new MissingMethodException(n);}catch(TargetInvocationException e){throw e.InnerException??e;}}
 static object Prop(object o,string n){return o.GetType().GetProperty(n,F).GetValue(o,null);}static void Check(bool ok,string issue){if(!ok&&!errors.Contains(issue))errors.Add(issue);}
 static string Sha(string p){using(var h=SHA256.Create())using(var s=File.OpenRead(p))return BitConverter.ToString(h.ComputeHash(s)).Replace("-","").ToLowerInvariant();}
 static byte[] Pixels(BitmapSource image){var bytes=new byte[640*360*4];image.CopyPixels(bytes,2560,0);return bytes;}
 static bool Equal(byte[] a,byte[] b){for(int i=0;i<a.Length;i++)if(a[i]!=b[i])return false;return true;}
 static void Save(BitmapSource image,string p){using(var s=File.Create(p)){var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(image));encoder.Save(s);}}
 [STAThread]public static int Main(string[] args){
  string target=Path.GetFullPath(args[0]),folder=Path.GetFullPath(args[1]);Directory.CreateDirectory(folder);string captures=Path.Combine(folder,"transition-captures");Directory.CreateDirectory(captures);var report=new Dictionary<string,object>{{"candidate_sha256",Sha(target)}};object smooth=null;
  try{
   var a=Assembly.LoadFrom(target);smooth=Activator.CreateInstance(a.GetType("WhiteDragon.SmoothMotion"));object archive=Activator.CreateInstance(a.GetType("WhiteDragon.TransitionFlowArchive"));Type transitionType=a.GetType("WhiteDragon.FrameTransition");Check((bool)Prop(archive,"Enabled"),"Flow archive is disabled");
   int[] rows={22,23,24};var durations=(Dictionary<int,int>)Prop(smooth,"MediaDurations");int cases=0,warps=0,skips=0,renders=0;long beforeSoft=0,afterSoft=0;double total=0,maximum=0;var selected=new List<Dictionary<string,object>>();
   int[][] fixtures={new int[]{22,150,0},new int[]{23,120,0},new int[]{24,90,0},new int[]{22,150,23},new int[]{23,120,24},new int[]{24,90,19}};
   Action<int,int,int,int> review=delegate(int row,int sample,int next,int fixture){
    double time=sample*1000.0/60;Check((bool)Call(smooth,"EnsureSample",row,time,10000),"Source decode");var source=(BitmapSource)Call(smooth,"Get",row,time);byte[] from=Pixels(source);
    Check((bool)Call(smooth,"EnsureSample",next,0.0,10000),"Target decode");var incoming=(BitmapSource)Call(smooth,"Get",next,0.0);byte[] to=Pixels(incoming);object flowA=Call(archive,"Get",row,sample),flowB=Call(archive,"Get",next,0);Check(flowA!=null&&flowB!=null,"Missing transition correspondence");
    object transition=Activator.CreateInstance(transitionType);var diag=transitionType.GetField("RecordDiagnostics");if(diag!=null)diag.SetValue(transition,true);int duration=row==14||next==14?280:row==12||next==12?260:240;bool active=(bool)Call(transition,"BeginAligned",source,to,flowA,flowB,duration);cases++;if(active)warps++;else skips++;
    int count=fixture>=0?16:3;for(int i=0;i<=count;i++){
     double elapsed=duration*i/(double)count;var watch=Stopwatch.StartNew();var image=(BitmapSource)Call(transition,"PresentPixels",to,elapsed,active);double ms=watch.Elapsed.TotalMilliseconds;maximum=Math.Max(maximum,ms);total+=ms;renders++;byte[] actual=Pixels(image);
     Check(i!=0||!active||Equal(actual,from),"Visible pose jumps at transition start");Check(i!=count||Equal(actual,to),"Transition does not end at exact incoming pose");
     long alpha=0;for(int p=0;p<actual.Length;p+=4){Check(actual[p]<=actual[p+3]&&actual[p+1]<=actual[p+3]&&actual[p+2]<=actual[p+3],"Invalid premultiplied bridge");alpha+=actual[p+3];}Check(alpha>10000L*255,"Character vanishes in bridge");
     if(fixture>=0)Save(image,Path.Combine(captures,fixture+"-"+i.ToString("00")+".png"));
    }
    if(transitionType.GetProperty("BodySoftPixelsBefore")!=null){beforeSoft+=Convert.ToInt64(Prop(transition,"BodySoftPixelsBefore"));afterSoft+=Convert.ToInt64(Prop(transition,"BodySoftPixelsAfter"));}
    if(fixture>=0)selected.Add(new Dictionary<string,object>{{"fixture",fixture},{"row",row},{"sample",sample},{"next",next},{"duration_ms",duration},{"steps",count},{"uses_warp",active}});
    if(active){
     var middle=(BitmapSource)Call(transition,"PresentPixels",to,duration*.45,true);byte[] snapshot=Pixels(middle);object snapshotFlow=Call(transition,"SnapshotFlow");bool rerouted=(bool)Call(transition,"BeginAligned",middle,from,snapshotFlow,flowA,240);var restarted=(BitmapSource)Call(transition,"PresentPixels",from,0.0,rerouted);Check(!rerouted||Equal(Pixels(restarted),snapshot),"Interrupting a bridge changes its first visible frame");
    }
   };
   for(int i=0;i<rows.Length;i++)foreach(double phase in new double[]{.25,.6,.94}){int row=rows[i],sample=(int)(durations[row]*.06*phase);review(row,sample,0,-1);if(row!=0)review(row,sample,rows[(i+1)%rows.Length],-1);}
   for(int i=0;i<fixtures.Length;i++)review(fixtures[i][0],fixtures[i][1],fixtures[i][2],i);
   report["body_soft_pixel_probes_before"]=beforeSoft;report["body_soft_pixel_probes_after"]=afterSoft;report["cases"]=cases;report["warped_cases"]=warps;report["near_identical_skips"]=skips;report["render_probes"]=renders;report["mean_render_ms"]=total/renders;report["max_render_ms"]=maximum;report["fixtures"]=selected;report["flow_decodes"]=Prop(archive,"DecodeCount");report["flow_last_error"]=Prop(archive,"LastError");Check(Prop(archive,"LastError")==null,"Flow decoder error");Check(total/renders<12,"Warp compositor exceeds average frame budget");
  }catch(Exception e){errors.Add(e.ToString());}finally{if(smooth!=null){Call(smooth,"Dispose");report["worker_stopped"]=Prop(smooth,"WorkerStopped");Check((bool)report["worker_stopped"],"Transition review worker remains alive");}}
  report["ok"]=errors.Count==0;report["failures"]=errors;report["scope"]="Exact compiled candidate compositor, embedded flow fields and decoded pixels. Mid-action pairs, idle returns and interruption endpoints verified; six new-action native rendered sequences retained for visual inspection. Does not certify subjective perfection or physical monitor cadence.";
  File.WriteAllText(Path.Combine(folder,"transition-review.json"),new JavaScriptSerializer{MaxJsonLength=4000000}.Serialize(report));return errors.Count==0?0:1;
 }
}
