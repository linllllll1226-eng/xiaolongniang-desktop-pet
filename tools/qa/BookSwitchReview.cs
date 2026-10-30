using System;
using System.IO;
using System.Reflection;
using System.Collections.Generic;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

// Measures the live renderer. Loading the candidate by reflection avoids Main,
// tray registration, saved settings and the user's assistant bridge.
public static class BookSwitchReview {
 static readonly BindingFlags F=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
 static object G(object o,string n){return o.GetType().GetField(n,F).GetValue(o);}
 static object P(object o,string n){return o.GetType().GetProperty(n,F).GetValue(o,null);}
 static object C(object o,string n,params object[] a){foreach(var m in o.GetType().GetMethods(F))if(m.Name==n&&m.GetParameters().Length==a.Length)return m.Invoke(o,a);throw new Exception(n);}
 static double Percentile(List<long> v,double q){if(v.Count==0)return 0;v.Sort();return v[Math.Min(v.Count-1,(int)(v.Count*q))];}
 [STAThread]public static int Main(string[] args){
  var report=new Dictionary<string,object>();var phases=new List<object>();report["phases"]=phases;
  Window w=null;object smooth=null;string output=Path.GetFullPath(args[1]);Directory.CreateDirectory(output);
  try{
   using(var h=SHA256.Create())using(var f=File.OpenRead(args[0]))report["candidate_sha256"]=BitConverter.ToString(h.ComputeHash(f)).Replace("-","").ToLowerInvariant();
   var assembly=Assembly.LoadFrom(Path.GetFullPath(args[0]));var app=new Application{ShutdownMode=ShutdownMode.OnMainWindowClose};
   w=(Window)Activator.CreateInstance(assembly.GetType("WhiteDragon.PetWindow"),new object[]{true});app.MainWindow=w;w.Opacity=0;w.ShowActivated=false;
   smooth=G(w,"smooth");object behavior=G(w,"behavior");var clock=(Stopwatch)G(w,"clock");
   var trace=(List<long[]>)G(w,"renderSubmissionTrace");var image=(Image)G(w,"image");
   int[] rows={0,19,0,19,18,19,18,19,25,19,0,19,18,19,0};
   int phase=-1,waits=0,misses=0,decode=0,pixelChanges=0;long start=0,lastPoll=0,lastPixel=0,maxPixelGap=0,first=-1;
   byte[] pixels=new byte[640*360*4];ulong lastHash=0;bool previous=false;var observer=new DispatcherTimer(DispatcherPriority.Normal){Interval=TimeSpan.FromMilliseconds(5)};
   Action next=delegate{
    phase++;if(phase>=rows.Length){observer.Stop();report["ok"]=true;w.Close();return;}
    trace.Clear();start=clock.ElapsedMilliseconds;lastPoll=start;lastPixel=start;maxPixelGap=0;first=-1;pixelChanges=0;previous=false;
    waits=(int)G(w,"mediaWaits");misses=(int)P(smooth,"StreamMisses");decode=(int)P(smooth,"DecodeCount");
    C(w,"Choose",rows[phase]);
   };
   w.Loaded+=delegate{next();observer.Start();};
   observer.Tick+=delegate{try{
    long now=clock.ElapsedMilliseconds;
    if(now-lastPoll>=12&&image.Source!=null){
     lastPoll=now;((BitmapSource)image.Source).CopyPixels(pixels,640*4,0);ulong hash=1469598103934665603UL;
     for(int p=0;p<pixels.Length;p+=64){hash^=pixels[p];hash*=1099511628211UL;hash^=pixels[p+3];hash*=1099511628211UL;}
     if(!previous||hash!=lastHash){if(previous){maxPixelGap=Math.Max(maxPixelGap,now-lastPixel);pixelChanges++;}lastPixel=now;lastHash=hash;previous=true;}
    }
    if(first<0&&trace.Count>0&&trace[0][1]==rows[phase])first=trace[0][0]-start;
    if(now-start<(rows[phase]==25?5400:900))return;
    var gaps=new List<long>();for(int i=1;i<trace.Count;i++)gaps.Add(trace[i][0]-trace[i-1][0]);
    phases.Add(new{row=rows[phase],from=phase>0?rows[phase-1]:-1,elapsed_ms=now-start,first_target_ms=first,
     submissions=trace.Count,p95_submission_gap_ms=Percentile(gaps,.95),max_submission_gap_ms=Percentile(gaps,1),
     pixel_changes=pixelChanges,max_visible_hold_ms=Math.Max(maxPixelGap,now-lastPixel),
     media_waits=(int)G(w,"mediaWaits")-waits,misses=(int)P(smooth,"StreamMisses")-misses,decoded=(int)P(smooth,"DecodeCount")-decode,
     max_tick_ms=G(w,"maxTickMilliseconds"),max_render_ms=G(w,"maxRenderMilliseconds"),max_blend_ms=P(G(w,"frameTransition"),"MaxBlendMilliseconds")});
    next();
   }catch(Exception e){observer.Stop();report["ok"]=false;report["error"]=e.ToString();w.Close();}};
   app.Run(w);report["decoder_error"]=P(smooth,"LastError");report["max_frame_decode_ms"]=P(smooth,"MaxFrameDecodeMilliseconds");
  }catch(Exception e){report["ok"]=false;report["error"]=e.ToString();}
  finally{if(w!=null)w.Close();if(smooth!=null)report["worker_stopped"]=P(smooth,"WorkerStopped");report["scope"]="Actual hidden WPF window and submitted/pixel surfaces; book open/close, repeated reading/thinking and full exchange. No monitor-level cadence claim or saved preference changes.";File.WriteAllText(Path.Combine(output,"book-switch-review.json"),new JavaScriptSerializer().Serialize(report));}
  return report.ContainsKey("ok")&&(bool)report["ok"]?0:1;
 }
}
