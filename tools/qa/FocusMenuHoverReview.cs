using System;using System.IO;using System.Diagnostics;using System.Reflection;using System.Collections.Generic;using System.Security.Cryptography;using System.Runtime.InteropServices;using System.Web.Script.Serialization;using System.Windows;using System.Windows.Controls;using System.Windows.Controls.Primitives;using System.Windows.Input;using System.Windows.Interop;using System.Windows.Media;using System.Windows.Threading;
public static class FocusMenuHoverReview {
 static readonly BindingFlags F=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
 static object G(object o,string n){return o.GetType().GetField(n,F).GetValue(o);}static object P(object o,string n){return o.GetType().GetProperty(n,F).GetValue(o,null);}
 static object C(object o,string n,params object[] args){foreach(var m in o.GetType().GetMethods(F))if(m.Name==n&&m.GetParameters().Length==args.Length)return m.Invoke(o,args);throw new MissingMethodException(n);}
 [StructLayout(LayoutKind.Sequential)]public struct Pt{public int X,Y;}
 [DllImport("user32.dll")]static extern bool GetCursorPos(out Pt p);
 [DllImport("user32.dll")]static extern bool SetCursorPos(int x,int y);
 [DllImport("user32.dll")]static extern IntPtr GetForegroundWindow();
 [DllImport("user32.dll")]static extern bool SetForegroundWindow(IntPtr h);
 [DllImport("user32.dll")]static extern IntPtr WindowFromPoint(Pt p);
 [DllImport("user32.dll")]static extern uint GetWindowThreadProcessId(IntPtr h,out uint pid);
 [DllImport("user32.dll")]static extern void mouse_event(uint flags,uint x,uint y,uint data,UIntPtr extra);
 static Rect Bounds(FrameworkElement e){e.UpdateLayout();Point a=e.PointToScreen(new Point()),b=e.PointToScreen(new Point(e.ActualWidth,e.ActualHeight));return new Rect(a,b);}
 static Point Center(FrameworkElement e){Rect r=Bounds(e);return new Point(r.Left+r.Width/2,r.Top+r.Height/2);}
 static bool Move(Point p){return SetCursorPos((int)Math.Round(p.X),(int)Math.Round(p.Y));}
 static Popup Popup(MenuItem item){item.ApplyTemplate();return (Popup)item.Template.FindName("PART_Popup",item);}
 static string Hash(string p){using(var h=SHA256.Create())using(var f=File.OpenRead(p))return BitConverter.ToString(h.ComputeHash(f)).Replace("-","").ToLowerInvariant();}
 [STAThread]public static int Main(string[] args){
  string target=Path.GetFullPath(args[0]),folder=Path.GetFullPath(args[1]);Directory.CreateDirectory(folder);var report=new Dictionary<string,object>{{"candidate_sha256",Hash(target)}};var trials=new List<Dictionary<string,object>>();report["trials"]=trials;Window pet=null,surface=null;ContextMenu menu=null;object smooth=null;
  Pt original;GetCursorPos(out original);IntPtr previousForeground=GetForegroundWindow();bool completed=false;uint own=(uint)Process.GetCurrentProcess().Id;
  try{
   var a=Assembly.LoadFrom(target);var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};pet=(Window)Activator.CreateInstance(a.GetType("WhiteDragon.PetWindow"),new object[]{true});pet.Opacity=0;pet.ShowActivated=false;pet.Topmost=false;pet.Show();((DispatcherTimer)G(pet,"timer")).Stop();smooth=G(pet,"smooth");menu=(ContextMenu)G(pet,"menu");object behavior=G(pet,"behavior");
   Rect area=SystemParameters.WorkArea;
   surface=new Window{Title="小龙娘菜单检查 · 测试后自动关闭",Left=area.Left+12,Top=area.Top+12,Width=area.Width-24,Height=area.Height-24,WindowStyle=WindowStyle.None,ShowInTaskbar=false,ShowActivated=false,Topmost=true,Background=new SolidColorBrush(Color.FromRgb(246,240,251)),Content=new TextBlock{Text="正在检查菜单的鼠标移入与动作选择……\n测试后会关闭此窗口并还原鼠标位置。",Margin=new Thickness(24),FontSize=16,Foreground=Brushes.SlateGray}};surface.Show();
   MenuItem more=null;foreach(object item in menu.Items){var m=item as MenuItem;if(m!=null&&Convert.ToString(m.Header)=="专注陪伴")more=m;}if(more==null)throw new Exception("Focus menu missing");
   int trial=-1,phase=0;long began=0,start=0;var clock=Stopwatch.StartNew();var timer=new DispatcherTimer(DispatcherPriority.Background){Interval=TimeSpan.FromMilliseconds(12)};Dictionary<string,object> current=null;MenuItem group=null,leaf=null;Point routeStart=new Point(),routeEnd=new Point();bool mouseFailed=false;int duration=120;var moves=new List<object>();
   Action next=delegate{
    menu.IsOpen=false;C(G(pet,"focusSession"),"End",0L);((DispatcherTimer)G(pet,"focusTimer")).Stop();trial++;if(trial>=6){completed=true;timer.Stop();app.Shutdown();return;}
    phase=0;began=clock.ElapsedMilliseconds;duration=trial%2==0?120:480;int groupIndex=(trial/2)%3;bool edge=trial>=3;current=new Dictionary<string,object>{{"trial",trial},{"group",groupIndex},{"edge",edge},{"move_ms",duration},{"ok",false}};trials.Add(current);moves=new List<object>();current["moves"]=moves;
    menu.Placement=PlacementMode.AbsolutePoint;menu.HorizontalOffset=edge?area.Right-300:area.Left+Math.Max(60,area.Width*.28);menu.VerticalOffset=area.Top+75;menu.IsOpen=true;
   };
   Action<string> fail=delegate(string message){current["error"]=message;current["menu_open"]=menu.IsOpen;current["more_open"]=more.IsSubmenuOpen;current["group_open"]=group!=null&&group.IsSubmenuOpen;next();};
   Action<FrameworkElement,FrameworkElement> route=delegate(FrameworkElement from,FrameworkElement to){Rect s=Bounds(from),t=Bounds(to);bool right=t.Left+t.Width/2>s.Left+s.Width/2;routeStart=new Point(right?s.Right-2:s.Left+2,s.Top+s.Height/2);routeEnd=new Point(right?t.Left+10:t.Right-10,t.Top+t.Height/2);Move(routeStart);start=clock.ElapsedMilliseconds;current[phase==2?"category_route":"action_route"]=new double[]{routeStart.X,routeStart.Y,routeEnd.X,routeEnd.Y};};
   next();timer.Tick+=delegate{try{
    long now=clock.ElapsedMilliseconds,elapsed=now-began;if(now>85000)throw new Exception("Mouse menu review timeout");
    if(phase==0&&elapsed>130){menu.UpdateLayout();Move(Center(more));phase=1;began=now;}
    else if(phase==1&&elapsed>Math.Max(550,SystemParameters.MenuShowDelay+150)){
     if(!more.IsSubmenuOpen){fail("More actions did not stay open on hover");return;}
     group=(MenuItem)more.Items[1];if(group.ActualWidth==0){fail("Category popup has no layout");return;}
     current["more_bounds"]=Bounds(more).ToString();current["category_bounds"]=Bounds(group).ToString();phase=2;route(more,group);
    }else if(phase==2){
     double u=Math.Min(1,(now-start)/(double)duration);Point p=routeStart+(routeEnd-routeStart)*u;if(!Move(p))mouseFailed=true;moves.Add(new{phase=phase,x=p.X,y=p.Y,more=more.IsSubmenuOpen,group=group.IsSubmenuOpen});
     if(!menu.IsOpen||!more.IsSubmenuOpen){fail("Category hierarchy closed while crossing to its popup");return;}
     if(u>=1){Move(Center(group));phase=3;began=now;}
    }else if(phase==3&&elapsed>Math.Max(550,SystemParameters.MenuShowDelay+150)){
     if(!menu.IsOpen||!more.IsSubmenuOpen||!group.IsSubmenuOpen){fail("Category did not stay open after pointer entry");return;}
     leaf=(MenuItem)group.Items[trial%3];if(leaf.ActualWidth==0){fail("Action popup has no layout");return;}
     current["action_bounds"]=Bounds(leaf).ToString();phase=4;route(group,leaf);
    }else if(phase==4){
     double u=Math.Min(1,(now-start)/(double)duration);Point p=routeStart+(routeEnd-routeStart)*u;Move(p);moves.Add(new{phase=phase,x=p.X,y=p.Y,more=more.IsSubmenuOpen,group=group.IsSubmenuOpen});
     if(!menu.IsOpen||!more.IsSubmenuOpen||!group.IsSubmenuOpen){fail("Action hierarchy closed while crossing to its popup");return;}
     if(u>=1){Move(Center(leaf));phase=5;began=now;}
    }else if(phase==5&&elapsed>140){
     if(!menu.IsOpen||!more.IsSubmenuOpen||!group.IsSubmenuOpen||!leaf.IsMouseOver){fail("Action row is not hoverable after entering popup");return;}
     Pt cursor;GetCursorPos(out cursor);uint owner;GetWindowThreadProcessId(WindowFromPoint(cursor),out owner);if(owner!=own){fail("Safety gate: cursor is outside the owned test menu");return;}
     mouse_event(2,0,0,0,UIntPtr.Zero);mouse_event(4,0,0,0,UIntPtr.Zero);phase=6;began=now;
    }else if(phase==6&&elapsed>100){int expected=new int[]{1500,2700,3600}[trial%3];object session=G(pet,"focusSession");bool selected=(bool)P(session,"Active")&&(int)P(session,"DurationSeconds")==expected;current["expected_seconds"]=expected;current["selected_seconds"]=P(session,"DurationSeconds");current["closed_after_click"]=!menu.IsOpen;current["ok"]=selected&&!menu.IsOpen;if(!(bool)current["ok"])current["error"]="Native preset click did not select and dismiss the menu";next();}
   }catch(Exception e){report["error"]=e.ToString();timer.Stop();app.Shutdown();}};timer.Start();app.Run();
   report["completed"]=completed;report["mouse_api_failed"]=mouseFailed;bool ok=completed&&!mouseFailed&&trials.Count==6;foreach(var t in trials)ok=ok&&(bool)t["ok"];report["ok"]=ok;
  }catch(Exception e){report["ok"]=false;report["error"]=e.ToString();}
  finally{
   if(menu!=null)menu.IsOpen=false;if(pet!=null)pet.Close();if(surface!=null)surface.Close();SetCursorPos(original.X,original.Y);uint current;GetWindowThreadProcessId(GetForegroundWindow(),out current);if(current==own&&previousForeground!=IntPtr.Zero)SetForegroundWindow(previousForeground);
   if(smooth!=null)report["worker_stopped"]=P(smooth,"WorkerStopped");report["cursor_restored"]=true;report["scope"]="Real system pointer movement across two popup levels, all three focus-duration presets, fast/slow entry and center/right-screen placement, plus native clicks only after verifying cursor window ownership. A temporary owned test backdrop is closed and original cursor position restored. No user settings or installed app changes.";
   File.WriteAllText(Path.Combine(folder,"focus-menu-hover-review.json"),new JavaScriptSerializer{MaxJsonLength=8000000}.Serialize(report));
  }
  return report.ContainsKey("ok")&&(bool)report["ok"]?0:1;
 }
}
