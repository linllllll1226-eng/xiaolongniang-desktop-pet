using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

public static class WindowPerchReview {
 static readonly BindingFlags Flags=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
 static readonly List<string> cases=new List<string>();
 static object G(object o,string n){return o.GetType().GetField(n,Flags).GetValue(o);}
 static object P(object o,string n){return o.GetType().GetProperty(n,Flags).GetValue(o,null);}
 static void S(object o,string n,object v){o.GetType().GetField(n,Flags).SetValue(o,v);}
 static object C(object o,string n,params object[] args){try{foreach(var m in o.GetType().GetMethods(Flags))if(m.Name==n&&m.GetParameters().Length==args.Length)return m.Invoke(o,args);throw new MissingMethodException(n);}catch(TargetInvocationException e){throw e.InnerException??e;}}
 static void Check(bool ok,string name){if(!ok)throw new Exception(name);if(!cases.Contains(name))cases.Add(name);}
 static string Hash(Stream s){using(var h=SHA256.Create())return BitConverter.ToString(h.ComputeHash(s)).Replace("-","").ToLowerInvariant();}
 static string Hash(string p){using(var s=File.OpenRead(p))return Hash(s);}
 [DllImport("user32.dll")]static extern bool SetWindowPos(IntPtr h,IntPtr after,int x,int y,int width,int height,uint flags);
 [DllImport("user32.dll")]static extern bool ShowWindow(IntPtr h,int command);
 [DllImport("user32.dll")]static extern bool PostMessage(IntPtr h,int message,IntPtr w,IntPtr l);
 [DllImport("user32.dll")]static extern uint GetWindowThreadProcessId(IntPtr h,out uint id);
 static IntPtr fixtureHandle;static Process fixture;static Window coordinateWindow;
 static void MoveFixture(int x,int y,int width,int height){uint id;GetWindowThreadProcessId(fixtureHandle,out id);Check(fixture!=null&&id==(uint)fixture.Id,"fixture mutations confined to the owned test process");Point p=coordinateWindow.PointToScreen(new Point(x-coordinateWindow.Left,y-coordinateWindow.Top));var matrix=PresentationSource.FromVisual(coordinateWindow).CompositionTarget.TransformToDevice;Check(SetWindowPos(fixtureHandle,IntPtr.Zero,(int)Math.Round(p.X),(int)Math.Round(p.Y),(int)Math.Round(width*matrix.M11),(int)Math.Round(height*matrix.M22),0x14),"fixture move accepted");}
 static FrameworkElement FixtureContent(){
  var root=new Border{Background=new SolidColorBrush(Color.FromRgb(252,249,255)),BorderBrush=new SolidColorBrush(Color.FromRgb(207,193,225)),BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(12)};
  var stack=new StackPanel();root.Child=stack;
  stack.Children.Add(new TextBlock{Text="小龙娘的窗边座位",Foreground=new SolidColorBrush(Color.FromRgb(87,63,107)),FontSize=18,Margin=new Thickness(24,22,24,12)});
  stack.Children.Add(new TextBlock{Text="这是专用测试窗口，可以移动、缩放或最小化。",FontSize=13,Foreground=Brushes.SlateGray,Margin=new Thickness(24,0,24,24)});
  for(int i=0;i<3;i++)stack.Children.Add(new Border{Height=12,Width=480-i*95,HorizontalAlignment=HorizontalAlignment.Left,Background=new SolidColorBrush(Color.FromRgb(235,226,245)),CornerRadius=new CornerRadius(6),Margin=new Thickness(24,0,24,14)});
  return root;
 }
 static int Fixture(string path){var app=new Application();Rect area=SystemParameters.WorkArea;var w=new Window{Title="Window perch owned test fixture",Width=620,Height=285,Left=area.Left+100,Top=area.Top+Math.Min(240,area.Height*.35),WindowStyle=WindowStyle.None,ResizeMode=ResizeMode.NoResize,ShowActivated=false,ShowInTaskbar=false,Background=Brushes.White,Content=FixtureContent()};w.Loaded+=delegate{File.WriteAllText(path,new WindowInteropHelper(w).Handle.ToInt64().ToString(CultureInfo.InvariantCulture));};var expiry=new DispatcherTimer{Interval=TimeSpan.FromSeconds(100)};expiry.Tick+=delegate{w.Close();};expiry.Start();app.Run(w);return 0;}
 static bool Origin(Type type,Rect bounds,Rect area,double width,double fraction,out Point origin){object[] args={bounds,area,width,fraction,new Point()};bool ok=(bool)type.GetMethod("TryOrigin").Invoke(null,args);origin=(Point)args[4];return ok;}
 static void ModelChecks(Assembly a){
  Type type=a.GetType("WhiteDragon.WindowPerch",true);Point origin;
  Check(Origin(type,new Rect(100,230,650,320),new Rect(0,0,1400,860),350,.72,out origin),"ordinary window accepts a seated placement");
  Check(Math.Abs(origin.Y+306*350.0/640-230)<.000001,"seat meets the exact top edge");
  Check(!Origin(type,new Rect(0,0,1400,860),new Rect(0,0,1400,860),350,.5,out origin),"maximized top edge is rejected for headroom");
  Check(!Origin(type,new Rect(100,230,110,320),new Rect(0,0,1400,860),350,.5,out origin),"narrow windows are rejected");
  Check(Origin(type,new Rect(-1700,250,700,400),new Rect(-1920,0,1920,1040),420,.6,out origin),"negative monitor coordinates supported by placement math");
  Check(Origin(type,new Rect(2050,-750,700,500),new Rect(1920,-1000,1080,1920),480,.5,out origin),"portrait monitor coordinates supported by placement math");
  object model=Activator.CreateInstance(type);object surface=Activator.CreateInstance(a.GetType("WhiteDragon.PerchSurface"));S(surface,"Handle",new IntPtr(123));S(surface,"ProcessId",(uint)1);
  C(model,"Attach",surface,.72,new Point(0,0),0L);double min=4000,max=0;
  for(long t=16;t<=20000;t+=16){C(model,"Step",t,new Point(100,200),false);double time=(double)P(model,"PoseMilliseconds");min=Math.Min(min,time);max=Math.Max(max,time);}
  Check(min>=1900&&max<=3100,"long seated loop excludes standing-up frames");
  Point before=(Point)P(model,"Origin");double pose=(double)P(model,"PoseMilliseconds");C(model,"Step",30000L,new Point(100,200),true);
  Check(pose==(double)P(model,"PoseMilliseconds")&&before==(Point)P(model,"Origin"),"seated loop pauses without consuming time");
  C(model,"Detach");Check(!(bool)P(model,"Attached"),"detach clears the native handle");
 }
 static void Capture(Window window,string output){
  var surface=G(window,"perchSurface");Rect bounds=(Rect)G(surface,"Bounds");double pad=(double)P(window,"RigPadding");
  var visual=new DrawingVisual();using(var dc=visual.RenderOpen()){
   dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(241,235,248)),null,new Rect(0,0,900,520));
   var content=FixtureContent();content.Measure(new Size(bounds.Width,bounds.Height));content.Arrange(new Rect(0,0,bounds.Width,bounds.Height));content.UpdateLayout();
   Point target=new Point(75,210);dc.DrawRectangle(new VisualBrush(content),null,new Rect(target,new Size(bounds.Width,bounds.Height)));
   var canvas=(FrameworkElement)G(window,"canvas");window.UpdateLayout();var petBitmap=new RenderTargetBitmap((int)Math.Ceiling(window.Width),(int)Math.Ceiling(window.Height),96,96,PixelFormats.Pbgra32);petBitmap.Render(canvas);dc.DrawImage(petBitmap,new Rect(target.X+window.Left-bounds.Left,target.Y+window.Top-bounds.Top,petBitmap.PixelWidth,petBitmap.PixelHeight));
   dc.DrawText(new FormattedText("窗边陪伴 · 原生素材渲染预览",CultureInfo.GetCultureInfo("zh-CN"),FlowDirection.LeftToRight,new Typeface("Microsoft YaHei"),16,Brushes.SlateGray),new Point(28,24));
  }
  var bitmap=new RenderTargetBitmap(900,520,96,96,PixelFormats.Pbgra32);bitmap.Render(visual);using(var stream=File.Create(output)){var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(bitmap));png.Save(stream);}
 }
 static object Pointer(Type type,Point value){object p=Activator.CreateInstance(type);S(p,"X",(int)Math.Round(value.X));S(p,"Y",(int)Math.Round(value.Y));return p;}
 static void RecordScene(Window window,string folder,int index,int phase,long elapsed,Dictionary<string,object> metrics){
  Rect area=(Rect)C(window,"CurrentWorkArea");double scale=Math.Min(870/area.Width,580/area.Height);
  string[] names={"准备","展翼 → 飞到窗边 → 收翼 → 坐下","跟随窗口移动","跟随窗口缩放","主动调整角色大小","暂停","暂时隐藏","恢复与拖动","重新坐下","窗口最小化 → 展翼缓降 → 收翼落地","检查顶部空间","恢复窗口","重新飞到窗边","关闭窗口 → 缓降","坐着时选择读书","先回到桌面再继续阅读"};
  var visual=new DrawingVisual();using(var dc=visual.RenderOpen()){
   dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(241,235,248)),null,new Rect(0,0,1280,720));
   dc.DrawText(new FormattedText("窗边陪伴 · "+names[Math.Min(phase,names.Length-1)],CultureInfo.GetCultureInfo("zh-CN"),FlowDirection.LeftToRight,new Typeface("Microsoft YaHei"),21,new SolidColorBrush(Color.FromRgb(78,57,95))),new Point(25,20));
   dc.PushTransform(new TranslateTransform(20,82));dc.PushTransform(new ScaleTransform(scale,scale));dc.PushTransform(new TranslateTransform(-area.Left,-area.Top));
   dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromRgb(251,249,253)),new Pen(new SolidColorBrush(Color.FromRgb(215,203,227)),1),area,12,12);
   if(fixtureHandle!=IntPtr.Zero){var native=window.GetType().Assembly.GetType("WhiteDragon.PerchWindows").GetMethod("Read").Invoke(null,new object[]{fixtureHandle});if(native!=null){var dip=C(window,"SurfaceInDips",native);Rect b=(Rect)G(dip,"Bounds");var content=FixtureContent();content.Measure(b.Size);content.Arrange(new Rect(new Point(),b.Size));content.UpdateLayout();dc.DrawRectangle(new VisualBrush(content),null,b);}}
   var canvas=(FrameworkElement)G(window,"canvas");window.UpdateLayout();var petBitmap=new RenderTargetBitmap((int)Math.Ceiling(window.Width),(int)Math.Ceiling(window.Height),96,96,PixelFormats.Pbgra32);petBitmap.Render(canvas);dc.DrawImage(petBitmap,new Rect(window.Left,window.Top,petBitmap.PixelWidth,petBitmap.PixelHeight));dc.Pop();dc.Pop();dc.Pop();
   dc.DrawText(new FormattedText("素材近看 · 固定像素比例",CultureInfo.GetCultureInfo("zh-CN"),FlowDirection.LeftToRight,new Typeface("Microsoft YaHei"),15,Brushes.SlateGray),new Point(925,112));
   dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromRgb(227,225,232)),null,new Rect(920,153,330,360),12,12);
   var image=(Image)G(window,"image");var source=image.Source as BitmapSource;
   if(source!=null){dc.DrawImage(new CroppedBitmap(source,new Int32Rect(160,20,320,340)),new Rect(925,163,320,340));
    var pixels=new byte[640*360*4];source.CopyPixels(pixels,2560,0);var reds=new List<int>();var greens=new List<int>();var blues=new List<int>();int minX=640,minY=360,maxX=0,maxY=0;
    for(int y=0;y<360;y++)for(int x=0;x<640;x++){int p=(y*640+x)*4;if(pixels[p+3]<240)continue;minX=Math.Min(minX,x);maxX=Math.Max(maxX,x);minY=Math.Min(minY,y);maxY=Math.Max(maxY,y);int b=pixels[p],g=pixels[p+1],r=pixels[p+2];if(r>=180&&g>=180&&b>=180&&Math.Max(r,Math.Max(g,b))-Math.Min(r,Math.Min(g,b))<45){reds.Add(r);greens.Add(g);blues.Add(b);}}
    if(reds.Count>50){reds.Sort();greens.Sort();blues.Sort();metrics["light_neutral_rgb"]=new int[]{reds[reds.Count/2],greens[greens.Count/2],blues[blues.Count/2]};}metrics["opaque_bounds"]=new int[]{minX,minY,maxX,maxY};
   }
   dc.DrawText(new FormattedText("连续播放检查 · 约 10 Hz 画面采样\n原生窗口与素材合成，不含其他桌面内容",CultureInfo.GetCultureInfo("zh-CN"),FlowDirection.LeftToRight,new Typeface("Microsoft YaHei"),13,Brushes.SlateGray),new Point(925,548));
  }
  var bitmap=new RenderTargetBitmap(1280,720,96,96,PixelFormats.Pbgra32);bitmap.Render(visual);string name="frame-"+index.ToString("0000")+".png";using(var stream=File.Create(Path.Combine(folder,name))){var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));encoder.Save(stream);}metrics["file"]=name;metrics["elapsed_ms"]=elapsed;metrics["phase"]=phase;metrics["row"]=P(G(window,"behavior"),"Animation");metrics["body_width"]=G(window,"bodyWidth");metrics["scale_x"]=((ScaleTransform)G(window,"stretch")).ScaleX;metrics["scale_y"]=((ScaleTransform)G(window,"stretch")).ScaleY;
 }
 [STAThread]public static int Main(string[] args){
  if(args.Length>0&&args[0]=="--fixture")return Fixture(args[1]);
  string target=Path.GetFullPath(args[0]),folder=Path.GetFullPath(args[1]);Directory.CreateDirectory(folder);
  var report=new Dictionary<string,object>{{"candidate_sha256",Hash(target)}};Window window=null;object smooth=null;bool completed=false;
  try{
   Assembly a=Assembly.LoadFrom(target);ModelChecks(a);Type perchType=a.GetType("WhiteDragon.WindowPerch"),native=a.GetType("WhiteDragon.PerchWindows");
   if(args.Length>2){var baseline=Assembly.LoadFile(Path.GetFullPath(args[2]));var resources=new Dictionary<string,string>();foreach(string name in baseline.GetManifestResourceNames().Where(name=>(a.GetName().Version.Minor!=11&&a.GetName().Version.Minor<14)||!(new string[]{"WhiteDragon.MotionSpec","WhiteDragon.FlowSpec","WhiteDragon.FallbackSpec","WhiteDragon.Posters"}).Contains(name)))using(var original=baseline.GetManifestResourceStream(name))using(var current=a.GetManifestResourceStream(name)){Check(current!=null&&Hash(original)==Hash(current),"unchanged media resource: "+name);current.Position=0;resources[name]=Hash(current);}report["unchanged_embedded_resources"]=resources;}
   string handleFile=Path.Combine(folder,"fixture-handle.txt");if(File.Exists(handleFile))File.Delete(handleFile);
   fixture=Process.Start(new ProcessStartInfo{FileName=Assembly.GetExecutingAssembly().Location,Arguments="--fixture \""+handleFile+"\"",UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden});
   var app=new Application{ShutdownMode=ShutdownMode.OnMainWindowClose};window=(Window)Activator.CreateInstance(a.GetType("WhiteDragon.PetWindow"),new object[]{true});coordinateWindow=window;window.Opacity=0;window.Topmost=false;window.ShowActivated=false;
   var clock=(Stopwatch)G(window,"clock");var behavior=G(window,"behavior");var prefs=G(window,"preferences");var perch=G(window,"windowPerch");var flight=G(window,"flight");smooth=G(window,"smooth");
   foreach(string name in new string[]{"AutoNap","LinkCodex","LinkChatGPT","Follow"})S(prefs,name,false);S(behavior,"AutoNap",false);
   var observer=new DispatcherTimer(DispatcherPriority.Background){Interval=TimeSpan.FromMilliseconds(40)};int phase=0,framesBefore=0;long began=0,testStart=0;double frozenPose=0;Point beforeMove=new Point();Rect firstBounds=new Rect();
   var trace=new List<double[]>();report["native_trace"]=trace;var recording=new List<Dictionary<string,object>>();report["visual_recording"]=recording;long lastCapture=-1000;string captureFolder=Path.Combine(folder,"flow-frames");Directory.CreateDirectory(captureFolder);
   Action next=delegate{phase++;began=clock.ElapsedMilliseconds;};
   Action settled=delegate{Point expected;Rect bounds=(Rect)G(G(window,"perchSurface"),"Bounds");Rect area=(Rect)C(window,"CurrentWorkArea");Check(Origin(perchType,bounds,area,(double)G(window,"bodyWidth"),(double)P(perch,"Fraction"),out expected),"native target has a valid placement");Check((new Point(window.Left+(double)P(window,"RigPadding"),window.Top+(double)P(window,"RigPadding"))-expected).Length<1,"native seat follows target geometry within one DIP");};
   window.Loaded+=delegate{began=testStart=clock.ElapsedMilliseconds;observer.Start();};
   observer.Tick+=delegate{try{
    long now=clock.ElapsedMilliseconds,elapsed=now-began;if(now-testStart>80000)throw new Exception("Window perch review timeout at phase "+phase);
    if(now-lastCapture>=100&&phase>0){lastCapture=now;var metrics=new Dictionary<string,object>();RecordScene(window,captureFolder,recording.Count,phase,now-testStart,metrics);recording.Add(metrics);Check((double)metrics["scale_x"]==1&&(double)metrics["scale_y"]==1,"runtime rig never enlarges or stretches a transition");}
    if((bool)P(perch,"Attached"))trace.Add(new double[]{now-testStart,phase,window.Left,window.Top,(double)P(perch,"PoseMilliseconds"),Convert.ToDouble(P(behavior,"Animation"))});
    if(phase==0){if(!File.Exists(handleFile))return;fixtureHandle=new IntPtr(long.Parse(File.ReadAllText(handleFile),CultureInfo.InvariantCulture));Check(native.GetMethod("Read").Invoke(null,new object[]{fixtureHandle})!=null,"real external window is detected");S(window,"perchForeground",fixtureHandle);C(window,"PerchOnForeground");Check((bool)P(perch,"Attached"),"current-window menu route attaches to the captured foreground target");framesBefore=(int)G(window,"frameChanges");next();}
    else if(phase==1&&elapsed>6500){
     Check((bool)P(perch,"Attached")&&(int)P(behavior,"Animation")==22,"native timer keeps her seated through a full sway cycle");Check((int)G(window,"frameChanges")-framesBefore>50,"native seated media keeps producing changing frames");settled();Capture(window,Path.Combine(folder,"window-perch-native.png"));
     firstBounds=(Rect)G(G(window,"perchSurface"),"Bounds");beforeMove=new Point(window.Left,window.Top);MoveFixture((int)firstBounds.Left+70,(int)firstBounds.Top+30,(int)firstBounds.Width,(int)firstBounds.Height);next();
    }else if(phase==2&&elapsed>550){settled();Check(Math.Abs(window.Left-beforeMove.X-70)<2&&Math.Abs(window.Top-beforeMove.Y-30)<2,"real moved window carries the seated pet");Rect b=(Rect)G(G(window,"perchSurface"),"Bounds");MoveFixture((int)b.Left,(int)b.Top,(int)b.Width-150,(int)b.Height);next();}
    else if(phase==3&&elapsed>550){settled();C(window,"ResizePet",420.0);next();}
    else if(phase==4&&elapsed>550){Check((bool)P(perch,"Attached"),"changing pet size preserves attachment");settled();C(behavior,"SetPaused",true,now);frozenPose=(double)P(perch,"PoseMilliseconds");next();}
    else if(phase==5&&elapsed>450){Check(Math.Abs(frozenPose-(double)P(perch,"PoseMilliseconds"))<.001,"native pause freezes seated media");C(behavior,"SetPaused",false,now);C(window,"SetHidden",true);next();}
    else if(phase==6&&elapsed>250){Check(!(bool)((DispatcherTimer)G(window,"timer")).IsEnabled,"hidden pet stops its production timer");C(window,"SetHidden",false);next();}
    else if(phase==7&&elapsed>550){
     Check((bool)P(perch,"Attached"),"hide and restore preserve a valid window seat");settled();
     Type pointerType=window.GetType().GetNestedType("POINT");var image=(Image)G(window,"image");Point local=new Point(image.Width*.5,image.Height*.4);Point screen=image.PointToScreen(local);
     C(window,"BeginPointer",local,1,Pointer(pointerType,screen),now);C(window,"EndPointer",now);Check((bool)P(perch,"Attached")&&(int)P(behavior,"Animation")==22,"a seated click does not leave her standing in midair");
     C(window,"BeginPointer",local,1,Pointer(pointerType,screen),now);var surface=G(window,"perchSurface");Rect b=(Rect)G(surface,"Bounds");Point drop=window.PointToScreen(new Point(b.Left+b.Width*.5-window.Left,b.Top-window.Top));
     C(window,"MovePointer",Pointer(pointerType,drop),now);Check(!(bool)P(perch,"Attached")&&(bool)P(behavior,"Dragged"),"dragging detaches through the production input helper");
     C(window,"EndPointer",now);Check((bool)P(perch,"Attached"),"slow release at a real window top snaps into a seat");next();
    }else if(phase==8&&elapsed>2650){ShowWindow(fixtureHandle,7);next();}
    else if(phase==9){
     if(elapsed>300)Check(!(bool)P(perch,"Attached"),"minimized target detaches the pet");
     if(elapsed>400&&!(bool)P(flight,"Active")&&!(bool)G(window,"pendingWingFold")&&(int)P(behavior,"Animation")==0){
      Rect area=(Rect)C(window,"CurrentWorkArea");double sole=window.Top+(double)P(window,"RigPadding")+(double)G(window,"bodyWidth")*330/640;
      report["landing_diagnostic"]=new{sole=sole,area=area.ToString(),top=window.Top,pad=P(window,"RigPadding"),width=G(window,"bodyWidth"),floor=P(flight,"Floor"),target_y=P(flight,"TargetY"),release_kind=G(window,"releaseKind")};
      Check(Math.Abs(sole-area.Bottom)<.01,"window loss completes wings and lands at the desktop floor");ShowWindow(fixtureHandle,4);MoveFixture((int)firstBounds.Left,(int)area.Top,(int)firstBounds.Width,(int)firstBounds.Height);next();
     }
    }else if(phase==10&&elapsed>350){Check(!(bool)C(window,"AttachWindowPerch",fixtureHandle,.5,now),"real top-flush window is refused without teleporting");MoveFixture((int)firstBounds.Left,(int)firstBounds.Top,(int)firstBounds.Width,(int)firstBounds.Height);next();}
    else if(phase==11&&elapsed>350){Check((bool)C(window,"AttachWindowPerch",fixtureHandle,.6,now),"restored window can be seated on again");next();}
    else if(phase==12&&elapsed>650){PostMessage(fixtureHandle,0x10,IntPtr.Zero,IntPtr.Zero);next();}
    else if(phase==13&&elapsed>350){
     Check(!(bool)P(perch,"Attached")&&(bool)P(flight,"Active"),"closing the target begins a real wing descent");
     var image=(Image)G(window,"image");Point local=new Point(image.Width*.5,image.Height*.3);C(window,"BeginPointer",local,1,Pointer(window.GetType().GetNestedType("POINT"),image.PointToScreen(local)),now);
     Check(!(bool)P(flight,"Active")&&(bool)P(behavior,"Pressed"),"pet remains catchable during window-loss descent");C(behavior,"CancelPointer",now);C(G(window,"motion"),"Reset");
     Check((bool)C(window,"AttachWindowPerch",fixtureHandle,.6,now)==false,"closed target cannot be reattached");
     C(window,"ResetPosition");if(File.Exists(handleFile))File.Delete(handleFile);fixture=Process.Start(new ProcessStartInfo{FileName=Assembly.GetExecutingAssembly().Location,Arguments="--fixture \""+handleFile+"\"",UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden});next();
    }else if(phase==14&&elapsed>500){
     long candidate=0;bool loaded=false;try{loaded=File.Exists(handleFile)&&long.TryParse(File.ReadAllText(handleFile),NumberStyles.Integer,CultureInfo.InvariantCulture,out candidate);}catch(IOException){}
     uint owner=0;if(loaded)GetWindowThreadProcessId(new IntPtr(candidate),out owner);
     if(!loaded||owner!=(uint)fixture.Id){Check(elapsed<5000,"new owned fixture completes startup within five seconds");return;}
     fixtureHandle=new IntPtr(candidate);report["new_fixture_startup_ms"]=elapsed;Check((bool)C(window,"AttachWindowPerch",fixtureHandle,.6,now),"new fixture accepts the menu-departure flow");next();}
    else if(phase==15&&elapsed>4500&&((int)G(window,"perchArrival"))==0){C(window,"Choose",19);Check((bool)P(flight,"Active")&&!(bool)P(perch,"Attached")&&(int)G(window,"perchActionAfterLanding")==19,"selecting reading from a seat waits for wing descent");next();}
    else if(phase==16&&elapsed>500&&!(bool)P(flight,"Active")&&!(bool)G(window,"pendingWingFold")&&(int)G(window,"perchActionAfterLanding")==-1&&(int)P(behavior,"Animation")==19){
     Check(!(bool)P(perch,"Attached"),"reading starts only after returning to the desktop");next();
    }else if(phase==17&&elapsed>1200){
     Check((int)P(behavior,"Animation")==19,"reading continues after the departure transition");report["native_trace"]=trace;report["frame_changes_during_first_cycle"]=(int)G(window,"frameChanges")-framesBefore;report["media_waits"]=G(window,"mediaWaits");report["decoder_error"]=P(smooth,"LastError");Check(P(smooth,"LastError")==null,"native decoder reports no error");completed=true;observer.Stop();window.Close();
    }
   }catch(Exception e){report["native_error"]=e.ToString();report["failed_phase"]=phase;observer.Stop();window.Close();}};
   app.Run(window);Check(completed,"all native window interaction phases completed");Check((bool)P(smooth,"WorkerStopped"),"native decoder worker stops on exit");report["ok"]=true;
  }catch(Exception e){report["ok"]=false;report["error"]=e.ToString();}
  finally{if(window!=null)window.Close();if(fixture!=null&&!fixture.HasExited){fixture.CloseMainWindow();if(!fixture.WaitForExit(1500))fixture.Kill();}report["cases"]=cases;report["scope"]="Exact candidate and unchanged embedded media. Real separate-process WPF target detection, movement, resizing, minimize, restore and close. Production WPF timers, media, synthetic pointer helper and full descent. Global cursor, existing apps and installed settings untouched. PNG is a WPF-rendered composition of the owned fixture and real pet canvas, not a desktop screenshot. Multi-monitor placement is simulated; no mixed-DPI hardware acceptance.";File.WriteAllText(Path.Combine(folder,"window-perch-review.json"),new JavaScriptSerializer{MaxJsonLength=6000000}.Serialize(report));}
  return report.ContainsKey("ok")&&(bool)report["ok"]?0:1;
 }
}
