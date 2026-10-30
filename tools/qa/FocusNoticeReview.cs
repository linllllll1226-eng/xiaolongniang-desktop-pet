using System;using System.IO;using System.Reflection;using System.Diagnostics;using System.Collections.Generic;using System.Security.Cryptography;using System.Web.Script.Serialization;using System.Windows;using System.Windows.Controls;using System.Windows.Threading;
public static class FocusNoticeReview {
 static BindingFlags F=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;static List<object> cases=new List<object>();static int failed;
 static object G(object o,string n){return o.GetType().GetField(n,F).GetValue(o);}static object P(object o,string n){return o.GetType().GetProperty(n,F).GetValue(o,null);}static void S(object o,string n,object v){o.GetType().GetField(n,F).SetValue(o,v);}static object C(object o,string n,params object[] args){try{foreach(var m in o.GetType().GetMethods(F))if(m.Name==n&&m.GetParameters().Length==args.Length)return m.Invoke(o,args);throw new MissingMethodException(n);}catch(TargetInvocationException e){throw e.InnerException??e;}}
 static void Check(bool ok,string name,object detail){cases.Add(new{ok=ok,name=name,detail=detail});if(!ok)failed++;}
 static string Hash(string p){using(var h=SHA256.Create())using(var s=File.OpenRead(p))return BitConverter.ToString(h.ComputeHash(s)).Replace("-","").ToLowerInvariant();}
 static void Reset(Window w,object b,object session,long now){C(b,"SetPaused",false,now);C(b,"SetMenu",false,now);C(b,"CancelPointer",now);C(b,"Play",0,false,now);C(session,"End",now);S(w,"focusNoticeUntil",0L);foreach(string n in new string[]{"focusNoticeRemaining","focusNoticeLast"}){var field=w.GetType().GetField(n,F);if(field!=null)field.SetValue(w,n=="focusNoticeLast"?-1L:0L);}var visible=w.GetType().GetField("focusNoticeWasVisible",F);if(visible!=null)visible.SetValue(w,false);}
 [STAThread]public static int Main(string[] args){
  string target=Path.GetFullPath(args[0]),output=Path.GetFullPath(args[1]);Directory.CreateDirectory(output);var report=new Dictionary<string,object>{{"candidate_sha256",Hash(target)},{"cases",cases}};Window w=null;object smooth=null;bool completed=false;
  try{
   var a=Assembly.LoadFrom(target);var app=new Application{ShutdownMode=ShutdownMode.OnMainWindowClose};w=(Window)Activator.CreateInstance(a.GetType("WhiteDragon.PetWindow"),new object[]{true});w.Opacity=0;w.ShowActivated=false;w.Topmost=false;w.Show();((DispatcherTimer)G(w,"timer")).Stop();smooth=G(w,"smooth");var b=G(w,"behavior");var session=G(w,"focusSession");var prefs=G(w,"preferences");foreach(string n in new string[]{"AutoNap","LinkCodex","LinkChatGPT","Follow"})S(prefs,n,false);S(b,"AutoNap",false);S(w,"bridgeNextRead",long.MaxValue);
   foreach(int row in new int[]{14,15,16,17}){
    Reset(w,b,session,0);C(b,"Play",row,false,0L);C(b,"SetPaused",true,0L);C(session,"Start",1,false,0L);C(w,"AdvanceFocusClock",1000L);C(w,"UpdateFocusBadge",1000L);var layer=(FrameworkElement)G(G(w,"focusBadge"),"Layer");
    Check((bool)P(session,"NoticePending")&&(long)G(w,"focusNoticeUntil")==0,"arrival during wing action waits until badge can be shown: "+row,new{pending=P(session,"NoticePending"),deadline=G(w,"focusNoticeUntil"),badge_visible=layer.IsVisible});
    C(w,"AdvanceFocusClock",6000L);C(w,"AdvanceFocusClock",11000L);C(b,"SetPaused",false,11000L);C(b,"Play",0,false,11000L);C(w,"AdvanceFocusClock",11000L);
    Check((long)G(w,"focusNoticeUntil")>=18990,"deferred wing notice gets a full display window: "+row,new{deadline=G(w,"focusNoticeUntil")});
   }
   foreach(string kind in new string[]{"menu","pointer"}){
    Reset(w,b,session,0);C(session,"Start",1,false,0L);C(w,"AdvanceFocusClock",1000L);C(w,"AdvanceFocusClock",2000L);
    if(kind=="menu")C(b,"SetMenu",true,2000L);else C(b,"BeginPress",false,1,2000L);
    C(w,"AdvanceFocusClock",2000L);foreach(long now in new long[]{5000,9000,12000})C(w,"AdvanceFocusClock",now);
    if(kind=="menu")C(b,"SetMenu",false,12000L);else{C(b,"CancelPointer",12000L);C(b,"Play",0,false,12000L);}C(w,"AdvanceFocusClock",12000L);
    Check((long)G(w,"focusNoticeUntil")>=18900,"existing notice keeps unseen time during "+kind,new{deadline=G(w,"focusNoticeUntil"),pending=P(session,"NoticePending")});
    C(w,"AdvanceFocusClock",13000L);C(w,"AdvanceFocusClock",19010L);Check((long)G(w,"focusNoticeUntil")<=19010,"notice still expires after its visible allowance: "+kind,new{deadline=G(w,"focusNoticeUntil")});
   }
   Reset(w,b,session,0);C(session,"Start",1,false,0L);C(w,"AdvanceFocusClock",1000L);C(w,"AdvanceFocusClock",2000L);C(session,"SetSuspended",true,2000L);C(w,"AdvanceFocusClock",2000L);C(w,"AdvanceFocusClock",9000L);C(session,"SetSuspended",false,12000L);C(w,"AdvanceFocusClock",12000L);Check((long)G(w,"focusNoticeUntil")>=18900,"computer suspend preserves an already delivered notice",new{deadline=G(w,"focusNoticeUntil")});
   Reset(w,b,session,0);C(session,"Start",1,false,0L);S(prefs,"FocusShowTimer",false);C(w,"AdvanceFocusClock",1000L);C(w,"UpdateFocusBadge",1000L);Check((long)G(w,"focusNoticeUntil")>=8990,"completion still appears when countdown display is off",new{deadline=G(w,"focusNoticeUntil")});
   Reset(w,b,session,0);C(session,"Start",1,false,0L);S(w,"bridgeStatus","needs_input");C(b,"Play",20,false,0L);C(w,"AdvanceFocusClock",1000L);C(w,"UpdateFocusCompanion",1000L);Check((int)P(b,"Animation")==20,"completion notice does not replace task question pose",new{row=P(b,"Animation")});S(w,"bridgeStatus",null);S(w,"bridgeApplied",null);S(prefs,"FocusShowTimer",true);
   var clock=(Stopwatch)G(w,"clock");Reset(w,b,session,clock.ElapsedMilliseconds);C(w,"StartFocusPeriod",1,true);var timer=(DispatcherTimer)G(w,"timer");timer.Start();var observer=new DispatcherTimer(DispatcherPriority.Background){Interval=TimeSpan.FromMilliseconds(40)};int phase=0;long began=clock.ElapsedMilliseconds,testStart=began,beforeHide=0;double remainingBefore=0;
   observer.Tick+=delegate{try{
    long now=clock.ElapsedMilliseconds;if(now-testStart>15000)throw new Exception("Native notice visibility check timeout");
    if(phase==0&&!(bool)P(session,"Active")&&(long)G(w,"focusNoticeUntil")>now){phase=1;began=now;}
    else if(phase==1&&now-began>=800){remainingBefore=(long)G(w,"focusNoticeUntil")-now;beforeHide=now;C(w,"SetHidden",true);Check(!timer.IsEnabled,"render clock stops for hidden completed session",new{render=timer.IsEnabled,focus=((DispatcherTimer)G(w,"focusTimer")).IsEnabled});phase=2;began=now;}
    else if(phase==2&&now-began>=2200){Check(!((DispatcherTimer)G(w,"focusTimer")).IsEnabled,"background clock stops after completed hidden session",null);C(w,"SetHidden",false);phase=3;began=now;}
    else if(phase==3&&now-began>=200){double remaining=(long)G(w,"focusNoticeUntil")-now;Check(remaining>=remainingBefore-600,"real hide/show preserves remaining notice with both clocks stopped",new{before_ms=remainingBefore,after_ms=remaining,hidden_elapsed_ms=now-beforeHide});completed=true;observer.Stop();w.Close();}
   }catch(Exception e){report["native_error"]=e.ToString();observer.Stop();w.Close();}};observer.Start();app.Run();
  }catch(Exception e){report["error"]=e.ToString();failed++;}finally{if(w!=null)w.Close();if(smooth!=null)report["worker_stopped"]=P(smooth,"WorkerStopped");report["failed_cases"]=failed;report["ok"]=completed&&failed==0;report["scope"]="Isolated exact-candidate WPF window. Controlled monotonic times cover wing/menu/pointer/suspend cases; real hide/show checks a delivered notice while both production timers are stopped. No installed settings, screen power or real cursor mutations.";File.WriteAllText(Path.Combine(output,"focus-notice-review.json"),new JavaScriptSerializer{MaxJsonLength=2000000}.Serialize(report));}
  return completed&&failed==0?0:1;
 }
}
