using System;using System.IO;using System.Reflection;using System.Diagnostics;using System.Collections.Generic;using System.Security.Cryptography;using System.Web.Script.Serialization;using System.Windows;using System.Windows.Controls;using System.Windows.Media;using System.Windows.Media.Imaging;using System.Windows.Threading;
public static class LandingContinuityReview {
 static readonly BindingFlags F=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
 static object G(object o,string n){return o.GetType().GetField(n,F).GetValue(o);}
 static object P(object o,string n){return o.GetType().GetProperty(n,F).GetValue(o,null);}
 static void S(object o,string n,object v){o.GetType().GetField(n,F).SetValue(o,v);}
 static object C(object o,string n,params object[] args){try{foreach(var m in o.GetType().GetMethods(F))if(m.Name==n&&m.GetParameters().Length==args.Length)return m.Invoke(o,args);throw new MissingMethodException(n);}catch(TargetInvocationException e){throw e.InnerException??e;}}
 static string Hash(string p){using(var h=SHA256.Create())using(var f=File.OpenRead(p))return BitConverter.ToString(h.ComputeHash(f)).Replace("-","").ToLowerInvariant();}
 static double Percentile(List<long> data,double q){if(data.Count==0)return 0;data.Sort();return data[Math.Min(data.Count-1,(int)(data.Count*q))];}
 [STAThread]public static int Main(string[] args){
  string target=Path.GetFullPath(args[0]),folder=Path.GetFullPath(args[1]);Directory.CreateDirectory(folder);bool visible=args.Length>2&&args[2]=="visible";
  var report=new Dictionary<string,object>{{"candidate_sha256",Hash(target)}};var trials=new List<object>();report["trials"]=trials;Window w=null;object smooth=null;bool completed=false;
  try{
   var a=Assembly.LoadFrom(target);var app=new Application{ShutdownMode=ShutdownMode.OnMainWindowClose};w=(Window)Activator.CreateInstance(a.GetType("WhiteDragon.PetWindow"),new object[]{true});w.ShowActivated=false;w.Opacity=visible?1:0;w.Topmost=visible;
   smooth=G(w,"smooth");object behavior=G(w,"behavior"),flight=G(w,"flight");var image=(Image)G(w,"image");var clock=(Stopwatch)G(w,"clock");var prefs=G(w,"preferences");foreach(string key in new string[]{"AutoNap","Follow","LinkChatGPT","LinkCodex"})S(prefs,key,false);S(behavior,"AutoNap",false);
   int trial=-1,lastRow=-1,lastCount=-1,waits=0,misses=0;long began=0,landed=-1,fold=-1,idle=-1,lastPixel=0,lastPoll=0;bool previous=false;
   byte[] pixels=new byte[640*360*4],prior=new byte[640*360*4];var trace=new List<double[]>();var holds=new List<long[]>();var rows=new List<long[]>();
   var timer=new DispatcherTimer(DispatcherPriority.Background){Interval=TimeSpan.FromMilliseconds(5)};
   Action begin=delegate{
    trial++;if(trial>=4){completed=true;timer.Stop();w.Close();return;}
    C(w,"CancelFlight",clock.ElapsedMilliseconds);C(behavior,"Play",0,false,clock.ElapsedMilliseconds);C(w,"RenderFrame");
    Rect area=(Rect)C(w,"CurrentWorkArea");double width=(double)G(w,"bodyWidth"),pad=(double)P(w,"RigPadding"),foot=width*330/640;
    double x=area.Left+area.Width*.55-width/2,y=area.Bottom-foot-(trial==0?220:trial==1?120:trial==2?350:45);
    w.Left=x-pad;w.Top=y-pad;began=clock.ElapsedMilliseconds;landed=fold=idle=-1;lastRow=-1;lastCount=-1;lastPixel=began;lastPoll=began;previous=false;trace=new List<double[]>();holds=new List<long[]>();rows=new List<long[]>();waits=(int)G(w,"mediaWaits");misses=(int)P(smooth,"StreamMisses");
    C(flight,"Begin",began,new Point(x,y),new Vector(trial==1?400:0,0),area,width,foot);C(behavior,"Play",16,true,began);C(w,"RenderFrame");
   };
   w.Loaded+=delegate{if(args.Length>3&&args[3]=="fast")((DispatcherTimer)G(w,"timer")).Interval=TimeSpan.FromMilliseconds(8);begin();timer.Start();};timer.Tick+=delegate{try{
    long now=clock.ElapsedMilliseconds,elapsed=now-began;int row=(int)G(w,"shownRow"),count=(int)G(w,"frameChanges");bool active=(bool)P(flight,"Active"),waiting=(bool)P(behavior,"MediaWaiting"),pending=(bool)G(w,"pendingWingFold");
    if(!active&&landed<0)landed=elapsed;if(row==17&&fold<0)fold=elapsed;if(fold>=0&&row==0&&idle<0)idle=elapsed;
    if(row!=lastRow){rows.Add(new long[]{elapsed,row});lastRow=row;}
    if(now-lastPoll>=8&&image.Source!=null){lastPoll=now;((BitmapSource)image.Source).CopyPixels(pixels,2560,0);bool changed=!previous;
     if(previous)for(int i=0;i<pixels.Length;i++)if(pixels[i]!=prior[i]){changed=true;break;}
     if(changed){if(previous)holds.Add(new long[]{elapsed,now-lastPixel,row,(int)G(w,"presentedSampleIndex")});lastPixel=now;Buffer.BlockCopy(pixels,0,prior,0,pixels.Length);previous=true;}
    }
    if(count!=lastCount||waiting){trace.Add(new double[]{elapsed,row,Convert.ToDouble(G(w,"presentedSampleIndex")),Convert.ToDouble(G(w,"presentedVisualTime")),waiting?1:0,active?1:0,pending?1:0,Convert.ToDouble(G(w,"maxTickMilliseconds")),Convert.ToDouble(G(w,"maxRenderMilliseconds"))});lastCount=count;}
    if(idle>=0&&elapsed-idle>650){
     var foldHolds=new List<long>();var idleHolds=new List<long>();foreach(var h in holds){if(h[0]>=fold-200&&h[0]<=fold+700)foldHolds.Add(h[1]);if(h[0]>=idle-200&&h[0]<=idle+500)idleHolds.Add(h[1]);}
     trials.Add(new{trial=trial,visible=visible,elapsed_ms=elapsed,landed_at_ms=landed,fold_at_ms=fold,idle_at_ms=idle,landed_to_fold_ms=fold-landed,fold_boundary_max_hold_ms=Percentile(foldHolds,1),idle_boundary_max_hold_ms=Percentile(idleHolds,1),fold_p95_hold_ms=Percentile(foldHolds,.95),media_waits=(int)G(w,"mediaWaits")-waits,cache_misses=(int)P(smooth,"StreamMisses")-misses,rows=rows,holds=holds,trace=trace});begin();
    }
    if(elapsed>18000)throw new Exception("Landing review timed out at trial "+trial);
   }catch(Exception e){report["error"]=e.ToString();timer.Stop();w.Close();}};
   app.Run(w);report["completed"]=completed;report["decoder_error"]=P(smooth,"LastError");report["worker_stopped"]=P(smooth,"WorkerStopped");report["ok"]=completed&&P(smooth,"LastError")==null&&(bool)P(smooth,"WorkerStopped");
  }catch(Exception e){report["ok"]=false;report["error"]=e.ToString();}finally{if(w!=null)w.Close();report["scope"]="Exact executable, production WPF timers, physical descent, pixel-change intervals and flap/fold/idle boundaries. Four isolated descents with warm/cold caches. No cursor movement, installed settings or original app changes. Visible flag controls actual window visibility; not a monitor scanout guarantee.";File.WriteAllText(Path.Combine(folder,"landing-continuity.json"),new JavaScriptSerializer{MaxJsonLength=8000000}.Serialize(report));}
  return report.ContainsKey("ok")&&(bool)report["ok"]?0:1;
 }
}
