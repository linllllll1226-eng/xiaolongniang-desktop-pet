using System;
using System.IO;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Web.Script.Serialization;
using Forms=System.Windows.Forms;

[assembly: AssemblyTitle("GPT白龙娘互动桌宠")]
#if CLICK_BOUNCE
[assembly: AssemblyVersion("10.15.1.0")]
[assembly: AssemblyFileVersion("10.15.1.0")]
#elif SMALL_TALK
[assembly: AssemblyVersion("10.13.0.0")]
[assembly: AssemblyFileVersion("10.13.0.0")]
#elif FOCUS_COMPANION
[assembly: AssemblyVersion("10.12.1.0")]
[assembly: AssemblyFileVersion("10.12.1.0")]
#elif DAILY_ROUTINE
[assembly: AssemblyVersion("10.11.0.0")]
[assembly: AssemblyFileVersion("10.11.0.0")]
#elif WINDOW_PERCH
[assembly: AssemblyVersion("10.10.2.0")]
[assembly: AssemblyFileVersion("10.10.2.0")]
#elif STYLE_V10
[assembly: AssemblyVersion("10.9.3.0")]
[assembly: AssemblyFileVersion("10.9.3.0")]
#else
[assembly: AssemblyVersion("9.2.0.0")]
[assembly: AssemblyFileVersion("9.2.0.0")]
#endif

namespace WhiteDragon {
public sealed class FallbackSpec {public Dictionary<string,int> posters;}
public sealed partial class PetWindow:Window {
#if STYLE_V10
    const int CW=640,CH=360;
#else
    const int CW=384,CH=416;
#endif
    readonly Canvas canvas=new Canvas();
    readonly Image image=new Image();
    readonly Image transitionImage=new Image{IsHitTestVisible=false};
    readonly BitmapSource[,] frames=new BitmapSource[PetBehavior.Counts.Length,8];
    readonly byte[,][] masks=new byte[PetBehavior.Counts.Length,8][];
    readonly GrabMotion motion=new GrabMotion();
    readonly ThrowGesture gesture=new ThrowGesture();
    readonly GentleFlight flight=new GentleFlight();
    readonly SmoothMotion smooth=new SmoothMotion();
    readonly ReadingLibrary readingLibrary=new ReadingLibrary(-1);
    bool automaticBookChange;
#if STYLE_V10
    readonly IdleBlink idleBlink=new IdleBlink();
#endif
    readonly byte[] hitPixel=new byte[4];
    long transitionStarted;
    bool transitioning;
    readonly FrameTransition frameTransition=new FrameTransition();
#if STYLE_V10
    readonly TransitionFlowArchive transitionFlows=new TransitionFlowArchive();
#endif
    bool transitionHoldsTimeline,transitionMediaReady;
    readonly byte[] livePixels=new byte[CW*CH*4];
    double transitionElapsed;
    long lastVisualRender;
    int presentedPlaybackId=-1,presentedSampleIndex=-1;
    double presentedVisualTime;
    BitmapSource lastDecodedImage;
    int mediaWaits,committedTransitions;
    double maxRenderMilliseconds,maxTickMilliseconds;
    readonly List<long[]> renderSubmissionTrace=new List<long[]>();
    long lastLogicTick;
    string bridgeStatus=null,bridgeApplied=null;
    string bridgeFolder;
    string bridgeSource;
#if STYLE_V10
    readonly ClientLink clientLink=new ClientLink();
    readonly ThinkingIndicator thinkingIndicator=new ThinkingIndicator();
    readonly ReadingBookProps readingBookProps=new ReadingBookProps();
    PetStatus lastCodexState;
    string connectionAuditPath,connectionAuditKey;long connectionAuditDeadline;bool connectionAuditFinished;
    readonly List<Dictionary<string,object>> connectionAuditEvents=new List<Dictionary<string,object>>();
    public void BeginConnectionAudit(){string folder=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"联动验收");Directory.CreateDirectory(folder);connectionAuditPath=Path.Combine(folder,"desktop-events.json");connectionAuditDeadline=clock.ElapsedMilliseconds+180000;}
    void CaptureConnectionAudit(){
        if(connectionAuditPath==null||connectionAuditFinished)return;
        bool finished=clock.ElapsedMilliseconds>=connectionAuditDeadline;
        string key=bridgeStatus+"|"+bridgeSource+"|"+behavior.Animation+"|"+behavior.PlaybackId;
        if(key==connectionAuditKey&&!finished)return;connectionAuditKey=key;
        if(connectionAuditEvents.Count<128)connectionAuditEvents.Add(new Dictionary<string,object>{{"utc",DateTime.UtcNow.ToString("o")},{"status",bridgeStatus},{"source",bridgeSource},{"row",behavior.Animation},{"playback_id",behavior.PlaybackId},{"single",behavior.SingleAction}});
        connectionAuditFinished=finished;
        File.WriteAllText(connectionAuditPath,new JavaScriptSerializer().Serialize(new Dictionary<string,object>{{"version","10.15.1"},{"finished",finished},{"scope","Actual running desktop phase and playback identifiers; no chat content or tool inputs"},{"events",connectionAuditEvents}}));
    }
#endif
    long bridgeNextRead,bridgeSnooze;
    int smoothChanges;
    readonly List<double> renderIntervals=new List<double>();
    double lastComposition;
    string releaseKind="none";
    bool pendingWingFold;
    long wingFoldDue,lastWingFoldCheck;
    long hiddenStarted;
    readonly ScaleTransform stretch=new ScaleTransform();
#if CLICK_BOUNCE
    readonly ClickBounce clickBounce=new ClickBounce();
#endif
    readonly RotateTransform swing=new RotateTransform();
    readonly DispatcherTimer timer=new DispatcherTimer(DispatcherPriority.Render);
    readonly Stopwatch clock=Stopwatch.StartNew();
    readonly PetPreferences preferences;
    readonly PetBehavior behavior;
    readonly bool testing;
    readonly string settingsPath;
    ContextMenu menu;
    MenuItem followItem,topItem,pauseItem,napItem;
    Forms.NotifyIcon tray;
    System.Drawing.Icon trayIcon;
    bool initialized,closed;
    int shownRow=-1,shownColumn=-1,timerHandlers,frameChanges;
    POINT lastPointer,pressPoint,grabPointer;double pressLeft,pressTop;
    Point pressLocal;
    Point activeGrip;
    long pickupStarted=-1;
    double bodyWidth;
    double BodyHeight{get{return bodyWidth*CH/CW;}}
    double RigPadding{get{return bodyWidth*GrabMotion.Padding;}}
    long lastPointerMotion;
    public bool CursorPollWorked{get;private set;}
    [StructLayout(LayoutKind.Sequential)] public struct POINT{public int X,Y;}
    [DllImport("user32.dll")] static extern bool GetCursorPos(out POINT p);
    [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr h,int msg,IntPtr w,IntPtr l);
    [DllImport("user32.dll",EntryPoint="GetWindowLong")] static extern int GetWindowLong32(IntPtr h,int n);
    [DllImport("user32.dll",EntryPoint="SetWindowLong")] static extern int SetWindowLong32(IntPtr h,int n,int v);

    public PetWindow(bool test) {
        testing=test;settingsPath=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"白龙娘设置.json");
        bridgeFolder=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"联动状态");
        preferences=test?new PetPreferences():PreferenceStore.Load(settingsPath);
#if STYLE_V10
        if(!File.Exists(settingsPath))preferences.Size=350;
        preferences.Follow=false;
        behavior=new PetBehavior(Forms.SystemInformation.DoubleClickTime,smooth.MediaDurations);
#if WINDOW_PERCH
#if SMALL_TALK
#if CLICK_BOUNCE
        Title="小龙娘 · 全量合并 v10.15.1";
#else
        Title="小龙娘 · 轻语气泡 v10.13";
#endif
#elif FOCUS_COMPANION
        Title="小龙娘 · 提示衔接修正 v10.12.1";
#elif DAILY_ROUTINE
        Title="小龙娘 · 自己安排小日常 v10.11";
#else
        Title="小龙娘 · 菜单移入修正 v10.10.2";
#endif
#else
        Title="小龙娘 · 开合书流畅修正 v10.9.3";
#endif
#else
        behavior=new PetBehavior(Forms.SystemInformation.DoubleClickTime);
        Title="小龙娘 · 动作流畅修正版 v9.2";
#endif
#if DAILY_ROUTINE
        preferences.DailyRhythm=Math.Max(0,Math.Min(2,preferences.DailyRhythm));dailyRoutine.Configure(!testing&&preferences.DailyRoutine,preferences.DailyRhythm,DateTime.Now.Hour);
#endif
        behavior.AutoNap=preferences.AutoNap;WindowStyle=WindowStyle.None;AllowsTransparency=true;Background=Brushes.Transparent;
        ResizeMode=ResizeMode.NoResize;ShowInTaskbar=false;ShowActivated=false;Focusable=false;Topmost=!test&&preferences.Topmost;
        bodyWidth=preferences.Size;Content=canvas;canvas.Children.Add(transitionImage);canvas.Children.Add(image);SetBodySize();image.Stretch=transitionImage.Stretch=Stretch.Fill;image.Focusable=false;Cursor=Cursors.Hand;
        var transforms=new TransformGroup();transforms.Children.Add(stretch);transforms.Children.Add(swing);image.RenderTransform=transitionImage.RenderTransform=transforms;
        RenderOptions.SetBitmapScalingMode(image,BitmapScalingMode.HighQuality);
        RenderOptions.SetBitmapScalingMode(transitionImage,BitmapScalingMode.HighQuality);
        image.ToolTip="轻戳脸颊躲开 · 单击身体跳跃 · 双击挥手 · 长按头部摸摸头 · 拎起甩出后扇翼缓降";
#if CLICK_BOUNCE
        image.ToolTip="连续点三下 · 鼓脸护头、duangduang回弹\n单击身体跳跃 · 双击挥手 · 长按头部摸头 · 拖动拎起";
#endif
        var area=SystemParameters.WorkArea;Left=(double.IsNaN(preferences.Left)?area.Right-bodyWidth-24:preferences.Left)-RigPadding;
        Top=(double.IsNaN(preferences.Top)?area.Bottom-bodyWidth*FlightAsset.Foot/CW:preferences.Top)-RigPadding;
#if WINDOW_PERCH
        if(double.IsNaN(preferences.Left))preferences.Left=Left+RigPadding;
        if(double.IsNaN(preferences.Top))preferences.Top=Top+RigPadding;
#endif
#if !STYLE_V10
        LoadSheet("WhiteDragon.Gaze",CW*8,CH*2,9,2,8);
#endif
        LoadPosters();
#if STYLE_V10
        canvas.Children.Insert(0,readingBookProps.Layer);readingBookProps.Place(bodyWidth,RigPadding);
        canvas.Children.Add(readingBookProps.FrontLayer);
        canvas.Children.Add(thinkingIndicator.Layer);thinkingIndicator.Place(bodyWidth,RigPadding);
#endif
#if FOCUS_COMPANION
        InitializeFocus();
#endif
#if SMALL_TALK
        InitializeSmallTalk();
#endif
        smooth.Warm(0);BuildMenu();RenderFrame();
        SourceInitialized+=delegate {
            var handle=new WindowInteropHelper(this).Handle;
            // Tool window keeps the mascot out of Alt-Tab. Mouse clicks do not activate it.
            SetWindowLong32(handle,-20,GetWindowLong32(handle,-20)|0x80);
            HwndSource.FromHwnd(handle).AddHook(WindowHook);
        };
        Loaded+=delegate {
            if(!initialized){initialized=true;timer.Interval=TimeSpan.FromMilliseconds(1000.0/60);timer.Tick+=delegate{Tick();};timerHandlers++;CompositionTarget.Rendering+=Composition;if(!testing)CreateTray(true);}
            ClampPosition();GetCursorPos(out lastPointer);lastPointerMotion=clock.ElapsedMilliseconds;timer.Start();
#if STYLE_V10
            if(!testing){clientLink.Enabled=preferences.LinkChatGPT;clientLink.Start();}
#endif
        };
        MouseLeftButtonDown+=Press;MouseMove+=Move;MouseLeftButtonUp+=Release;
#if WINDOW_PERCH
        PreviewMouseRightButtonDown+=delegate{RememberPerchForeground();};
#endif
        LostMouseCapture+=delegate{if(behavior.Pressed){behavior.CancelPointer(clock.ElapsedMilliseconds);motion.Reset();ClampPosition();RenderFrame();}};
        Closed+=delegate {
            closed=true;timer.Stop();CompositionTarget.Rendering-=Composition;smooth.Dispose();Save();
#if FOCUS_COMPANION
            DisposeFocus();
#endif
#if STYLE_V10
            clientLink.Dispose();
#endif
            if(tray!=null){tray.Visible=false;tray.Dispose();}if(trayIcon!=null)trayIcon.Dispose();
            Microsoft.Win32.SystemEvents.DisplaySettingsChanged-=DisplaysChanged;
        };
        Microsoft.Win32.SystemEvents.DisplaySettingsChanged+=DisplaysChanged;
    }
    void LoadSheet(string resource,int width,int height,int startRow,int rows,int columns) {
        using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream(resource)) {
            if(stream==null)throw new InvalidDataException("缺少角色素材："+resource);
            var bitmap=BitmapFrame.Create(stream,BitmapCreateOptions.PreservePixelFormat,BitmapCacheOption.OnLoad);
            if(bitmap.PixelWidth!=width||bitmap.PixelHeight!=height)throw new InvalidDataException("角色图集尺寸不正确");
            var bgra=new FormatConvertedBitmap(bitmap,PixelFormats.Pbgra32,null,0);bgra.Freeze();
            for(int r=0;r<rows;r++)for(int c=0;c<PetBehavior.Counts[startRow+r];c++) {
                var frame=new CroppedBitmap(bgra,new Int32Rect(c*CW,r*CH,CW,CH));frame.Freeze();frames[startRow+r,c]=frame;
                var bytes=new byte[CW*CH*4];frame.CopyPixels(bytes,CW*4,0);var alpha=new byte[CW*CH];int painted=0;
                for(int i=0;i<alpha.Length;i++){alpha[i]=bytes[i*4+3];if(alpha[i]>16)painted++;}
                if(painted<100)throw new InvalidDataException("角色动作帧为空");masks[startRow+r,c]=alpha;
            }
        }
    }
    void LoadPosters(){
        FallbackSpec data;using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("WhiteDragon.FallbackSpec"))using(var reader=new StreamReader(stream))data=new JavaScriptSerializer().Deserialize<FallbackSpec>(reader.ReadToEnd());
        using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("WhiteDragon.Posters")){
            var bitmap=BitmapFrame.Create(stream,BitmapCreateOptions.PreservePixelFormat,BitmapCacheOption.OnLoad);var bgra=new FormatConvertedBitmap(bitmap,PixelFormats.Pbgra32,null,0);bgra.Freeze();
            foreach(var kv in data.posters){int row=int.Parse(kv.Key),i=kv.Value;var frame=new CroppedBitmap(bgra,new Int32Rect(i%8*CW,i/8*CH,CW,CH));frame.Freeze();for(int col=0;col<PetBehavior.Counts[row];col++)frames[row,col]=frame;}
        }
    }
#if STYLE_V10
    const double LandingFoldStart=550;
#else
    const double LandingFoldStart=0;
#endif
    void PrefetchNext(){int next=shownRow==25?19:shownRow==19&&readingLibrary.RemainingMilliseconds<6000?25:shownRow==16?15:shownRow==15?17:shownRow==17?0:(behavior.SingleAction||shownRow==14)?0:-1;
        if(next==17){
            bool landing=flight.Active||pendingWingFold;
#if WINDOW_PERCH
            landing=landing||(windowPerch.Attached&&perchArrival==1);
#endif
            smooth.WarmFrom(17,landing?LandingFoldStart:0);
        }else if(next>=0)smooth.Warm(next);
    }
    IntPtr WindowHook(IntPtr h,int message,IntPtr w,IntPtr l,ref bool handled) {
        if(message==0x0021){handled=true;return new IntPtr(3);} // MA_NOACTIVATE
        if(message==0x0084&&!behavior.Pressed) {
            int x=unchecked((short)(l.ToInt64()&65535)),y=unchecked((short)((l.ToInt64()>>16)&65535));
            if(!Painted(PointFromScreen(new Point(x,y)))){handled=true;return new IntPtr(-1);}
        }
        return IntPtr.Zero;
    }
    bool Painted(Point p) {
        if(!IsLoaded)return false;
        var inverse=image.TransformToAncestor(this).Inverse;if(inverse==null)return false;p=inverse.Transform(p);
        int x=(int)Math.Floor(p.X/bodyWidth*CW),y=(int)Math.Floor(p.Y/BodyHeight*CH);
        if(x<0||x>=CW||y<0||y>=CH||image.Source==null)return false;
        // Reading only the pointed pixel avoids copying 38 MB/s for hit testing.
        ((BitmapSource)image.Source).CopyPixels(new Int32Rect(x,y,1,1),hitPixel,4,0);
        return hitPixel[3]>12;
    }
    void RenderFrame() {
        long start=Stopwatch.GetTimestamp();
        try{RenderFrameCore();}finally{
#if SMALL_TALK
            UpdateSmallTalkBadge(clock.ElapsedMilliseconds);
#endif
#if FOCUS_COMPANION
            UpdateFocusBadge(clock.ElapsedMilliseconds);
#endif
#if STYLE_V10
            thinkingIndicator.Update(clock.ElapsedMilliseconds,shownRow==18&&!behavior.Pressed&&!flight.Active,behavior.Paused||behavior.MenuOpen||behavior.Hidden);
            readingBookProps.Update(clock.ElapsedMilliseconds,(shownRow==18||shownRow==19||shownRow==25)&&!behavior.Pressed&&!flight.Active,behavior.Paused||behavior.MenuOpen||behavior.Hidden);
#endif
            maxRenderMilliseconds=Math.Max(maxRenderMilliseconds,(Stopwatch.GetTimestamp()-start)*1000.0/Stopwatch.Frequency);
        }
    }
    public readonly double[] RenderPhaseMaxMilliseconds=new double[9];
    void RenderMark(int phase,ref long stamp){long now=Stopwatch.GetTimestamp();RenderPhaseMaxMilliseconds[phase]=Math.Max(RenderPhaseMaxMilliseconds[phase],(now-stamp)*1000.0/Stopwatch.Frequency);stamp=now;}
    void RenderFrameCore() {
        long phaseStamp=Stopwatch.GetTimestamp();
        if(motion.Active&&!behavior.Dragged)motion.Reset();
        long now=clock.ElapsedMilliseconds;
        double elapsed=lastVisualRender==0?0:Math.Max(0,now-lastVisualRender);
#if CLICK_BOUNCE
        if(behavior.Hidden)clickBounce.Reset();else if(behavior.Paused||behavior.MenuOpen)clickBounce.Step(now,true);
#endif
        if((behavior.Paused||behavior.MenuOpen||behavior.Hidden)&&image.Source!=null){lastVisualRender=now;return;}
        ApplyRig();RenderMark(0,ref phaseStamp);
        int targetRow=behavior.Row;
        bool changed=shownRow!=targetRow||presentedPlaybackId!=behavior.PlaybackId;
        // Composition and the fallback timer can fire almost together. A
        // second full-body warp in that interval cannot reach another display
        // frame and delays the next one. Keep the accumulated animation time;
        // action changes and pointer transforms still take effect immediately.
        if(transitioning&&!changed&&elapsed<12)return;
        lastVisualRender=now;
        bool streamed=targetRow!=9&&targetRow!=10;
        double time=changed||transitionHoldsTimeline?behavior.PlaybackStartMilliseconds:behavior.VisualTime(now);
#if WINDOW_PERCH
        bool perchedPose=windowPerch.Attached&&targetRow==22;
        if(perchedPose)time=windowPerch.PoseMilliseconds;
#endif
        BitmapSource selected=null;
        int previousSample=presentedSampleIndex;
        if(streamed){
#if WINDOW_PERCH
            if(perchedPose)smooth.WarmDirected(targetRow,time,windowPerch.Direction);
            else if(behavior.RecoveringPose)smooth.WarmRecovery(targetRow,time);else smooth.WarmFrom(targetRow,time);
#else
            if(behavior.RecoveringPose)smooth.WarmRecovery(targetRow,time);else smooth.WarmFrom(targetRow,time);
#endif
            bool exact=smooth.TryCopyExact(targetRow,time,livePixels);
            bool ready=exact&&(!changed||smooth.IsReady(targetRow,time,2));
#if WINDOW_PERCH
            if(perchedPose)ready=exact;
#endif
            RenderMark(1,ref phaseStamp);transitionMediaReady=ready;
            if(!ready){
                if(!behavior.MediaWaiting){behavior.SetMediaWaiting(true,now);mediaWaits++;}
                // A cold action never replaces a moving pose with a poster. The
                // startup poster is used only before any image has been shown.
                if(image.Source==null)image.Source=frames[targetRow,0];
                return;
            }
            if(!transitionHoldsTimeline||changed)behavior.SetMediaWaiting(false,now);
            if(changed){behavior.StartVisualPlayback(now);
#if WINDOW_PERCH
                if(!perchedPose)time=behavior.PlaybackStartMilliseconds;
#else
                time=behavior.PlaybackStartMilliseconds;
#endif
            }
        }else{
            behavior.SetMediaWaiting(false,now);selected=frames[targetRow,behavior.Column];
        }
        RenderMark(2,ref phaseStamp);if(changed){
            // These two wing boundaries already share the same pose. Other
            // changes include a short, single-surface transition, including
            // folding back to idle, whose source drawing differs visibly.
            bool matched=(shownRow==16&&targetRow==15)||(shownRow==15&&targetRow==17&&behavior.PlaybackStartMilliseconds==0);
#if STYLE_V10
            // At a natural action end, the final video frames already follow
            // the moving idle sequence. Preserve that phase instead of fading
            // a held still over its next frames. Interruptions still blend.
            matched=matched||(targetRow==0&&shownRow>=0&&behavior.NaturalIdleTransition&&smooth.HasMovingIdleTail(shownRow));
            matched=matched||(targetRow==19&&shownRow==25&&behavior.NaturalReadingTransition&&smooth.HasMovingReadingTail(25));
#endif
            bool interrupted=transitioning;
            transitioning=!matched&&image.Source!=null;
            transitionImage.Source=null;transitionElapsed=0;transitionStarted=now;
            transitionHoldsTimeline=false;
            if(transitioning){
#if STYLE_V10
                if(transitionFlows.Enabled){
                    FlowField sourceFlow=interrupted?frameTransition.SnapshotFlow():transitionFlows.Get(shownRow,Math.Max(0,previousSample));
                    FlowField targetFlow=transitionFlows.Get(targetRow,smooth.GetSampleIndex(targetRow,time));
#if WINDOW_PERCH
                    if(perchedPose)targetFlow=transitionFlows.Get(targetRow,smooth.GetSampleIndex(targetRow,time));
#endif
                    int duration=shownRow==14||targetRow==14?280:targetRow==12||shownRow==12?260:240;
                    if(targetRow==17&&behavior.PlaybackStartMilliseconds>0)duration=140;
                    if(behavior.RecoveringPose&&shownRow==targetRow)duration=70;
                    // Book entry/exit needs a short moving bridge; a long
                    // snapshot of the previous body pose reads as a freeze.
                    if((shownRow==19&&(targetRow==0||targetRow==18))||(targetRow==19&&(shownRow==0||shownRow==18)))duration=180;
                    transitioning=frameTransition.BeginAligned((BitmapSource)image.Source,livePixels,sourceFlow,targetFlow,duration);
                    // Decoding waits still freeze time. A visual bridge itself
                    // follows the playing incoming clip without pausing it.
                    transitionHoldsTimeline=false;
                }else frameTransition.Begin((BitmapSource)image.Source,targetRow==0?160:120);
#else
                frameTransition.Begin((BitmapSource)image.Source,targetRow==0?160:120);
#endif
            }
            shownRow=targetRow;presentedPlaybackId=behavior.PlaybackId;committedTransitions++;
            elapsed=0;
        }
        RenderMark(3,ref phaseStamp);shownColumn=behavior.Column;presentedVisualTime=time;
        presentedSampleIndex=streamed?smooth.GetSampleIndex(targetRow,time):behavior.Column;
#if STYLE_V10
        if(transitioning&&streamed&&!changed&&previousSample!=presentedSampleIndex)frameTransition.FollowIncoming(livePixels,transitionFlows.Get(targetRow,presentedSampleIndex));
#endif
        RenderMark(4,ref phaseStamp);PrefetchNext();RenderMark(5,ref phaseStamp);
        int previousWeight=frameTransition.Weight;
#if STYLE_V10
        int previousBlink=idleBlink.Schedule.Frame;
        idleBlink.Schedule.Advance(transitionHoldsTimeline?0:Math.Min(50,elapsed),targetRow==0,changed);
        int blinkFrame=idleBlink.Schedule.Frame;
        bool eyeChanged=previousBlink!=blinkFrame;
        if(streamed&&targetRow==0)idleBlink.Apply(livePixels,blinkFrame);
#else
        bool eyeChanged=false;
#endif
        if(!changed&&!transitioning&&previousSample==presentedSampleIndex&&streamed&&!eyeChanged)return;
        RenderMark(6,ref phaseStamp);BitmapSource display=selected;
        if(transitioning)transitionElapsed+=elapsed;
        if(streamed)display=frameTransition.PresentPixels(livePixels,transitionElapsed,transitioning);
        else if(transitioning)display=frameTransition.Render(selected,transitionElapsed);
        RenderMark(7,ref phaseStamp);if(transitioning&&frameTransition.Weight>=256){
            transitioning=false;
            if(transitionHoldsTimeline){transitionHoldsTimeline=false;behavior.SetMediaWaiting(false,now);behavior.StartVisualPlayback(now);}
        }
        image.Opacity=1;
        if(changed||previousSample!=presentedSampleIndex||lastDecodedImage!=selected){lastDecodedImage=selected;smoothChanges++;}
        if(streamed||image.Source!=display||previousWeight!=frameTransition.Weight){
            image.Source=display;frameChanges++;
            RenderMark(8,ref phaseStamp);if(testing&&renderSubmissionTrace.Count<30000)renderSubmissionTrace.Add(new long[]{clock.ElapsedMilliseconds,shownRow,presentedPlaybackId,presentedSampleIndex,GC.CollectionCount(0),GC.CollectionCount(2)});
        }
    }
    void SetBodySize(){Width=bodyWidth+2*RigPadding;Height=BodyHeight+2*RigPadding;image.Width=transitionImage.Width=bodyWidth;image.Height=transitionImage.Height=BodyHeight;Canvas.SetLeft(image,RigPadding);Canvas.SetTop(image,RigPadding);Canvas.SetLeft(transitionImage,RigPadding);Canvas.SetTop(transitionImage,RigPadding);
#if STYLE_V10
        thinkingIndicator.Place(bodyWidth,RigPadding);
        readingBookProps.Place(bodyWidth,RigPadding);
#endif
    }
    void ApplyRig(){bool flying=behavior.Animation>=15&&behavior.Animation<=17;
#if STYLE_V10
        if(motion.Active&&behavior.Dragged)UpdateGrabPosition(clock.ElapsedMilliseconds);
        image.RenderTransformOrigin=transitionImage.RenderTransformOrigin=(motion.Active||flight.Active||(!flying&&motion.Landing))?activeGrip:new Point(.5,FlightAsset.Foot/CH);
        stretch.ScaleX=stretch.ScaleY=1;
#if CLICK_BOUNCE
        bool bounceAllowed=!motion.Active&&!flight.Active&&!pendingWingFold&&!windowPerch.Attached&&!behavior.Hidden&&!flying&&behavior.Animation!=14;
        if(!bounceAllowed)clickBounce.Reset();else clickBounce.Step(clock.ElapsedMilliseconds,behavior.Paused||behavior.MenuOpen);
        stretch.ScaleX=clickBounce.ScaleX;stretch.ScaleY=clickBounce.ScaleY;
#endif
#else
        image.RenderTransformOrigin=transitionImage.RenderTransformOrigin=motion.Active?new Point(GrabMotion.PivotX,GrabMotion.PivotY):new Point(.5,FlightAsset.Foot/CH);stretch.ScaleX=flying?FlightAsset.Scale:1;stretch.ScaleY=flying?FlightAsset.Scale:motion.Stretch;
#endif
        swing.Angle=flight.Active?flight.Angle:motion.Angle;}
    void Composition(object sender,EventArgs e){
        if(closed||!timer.IsEnabled||!IsVisible)return;
        var rendering=e as RenderingEventArgs;if(rendering!=null){double t=rendering.RenderingTime.TotalMilliseconds;if(t==lastComposition)return;if(lastComposition>0&&renderIntervals.Count<1000)renderIntervals.Add(t-lastComposition);lastComposition=t;}
        Tick();
    }
    void UpdateBridge(long now){
        if(now>=bridgeNextRead){
            bridgeNextRead=now+500;DateTime utc=DateTime.UtcNow;var state=preferences.LinkCodex?PetBridge.Read(bridgeFolder,utc):null;
#if STYLE_V10
            if(preferences.LinkCodex)state=PetBridge.RetainEventLease(state,lastCodexState,utc);
            lastCodexState=state;
            PetStatus legacyState=null;
            if(preferences.LinkCodex&&!testing){string installed=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Programs","GPTWhiteDragon");lastCodexState=PetBridge.ResolveSources(lastCodexState,PetBridge.Read(Path.Combine(installed,"联动状态-持续动作"),utc,"codex"),null);legacyState=PetBridge.Read(Path.Combine(installed,"联动状态"),utc,"codex");}
            lastCodexState=PetBridge.ResolveSources(lastCodexState,legacyState,null);
            state=PetBridge.ResolveSources(lastCodexState,null,preferences.LinkChatGPT?clientLink.State(utc):null);
#endif
            bridgeStatus=state==null?null:state.status;bridgeSource=state==null?null:state.source;
        }
#if DAILY_ROUTINE
        bool dailyPreempted=PreemptDailyForAssistant(now);
#else
        bool dailyPreempted=false;
#endif
#if WINDOW_PERCH
        if(windowPerch.Attached||perchActionAfterLanding>=0)return;
#endif
#if FOCUS_COMPANION
        bool focusPreempted=PreemptFocusForAssistant(now);
        if(!AssistantBusy&&FocusOwnsPose){bridgeApplied=bridgeStatus;return;}
#else
        bool focusPreempted=false;
#endif
        bool companionPreempted=dailyPreempted||focusPreempted;
        int desiredRow=AssistantStates.Row(bridgeStatus);
        bool sameContinuousPose=bridgeStatus!=bridgeApplied&&!AssistantStates.Once(bridgeStatus)&&desiredRow!=0&&desiredRow==AssistantStates.Row(bridgeApplied)&&((behavior.Animation==desiredRow&&!behavior.SingleAction)||(desiredRow==19&&automaticBookChange&&behavior.Animation==25));
        if(sameContinuousPose){bridgeApplied=bridgeStatus;
#if STYLE_V10
            image.ToolTip="小龙娘 · "+SourceName(bridgeSource)+" · "+StateName(bridgeStatus)+"\n慢拖拎起 · 甩出缓降 · 右键打开菜单";
#endif
            return;
        }
        bool canLeaveBook=automaticBookChange&&behavior.Animation==25&&desiredRow!=AssistantStates.Row(bridgeApplied)&&!behavior.Pressed&&!behavior.Paused&&!behavior.MenuOpen&&!behavior.Hidden;
#if TABLET_PHASES
        if(RouteTabletPhases(now,canLeaveBook))return;
#endif
        if(!companionPreempted&&!AssistantStates.MayUpdate(behavior,flight.Active||pendingWingFold,bridgeSnooze,now)&&!AssistantStates.MayReplaceCompletion(behavior,flight.Active||pendingWingFold,bridgeApplied,bridgeStatus,bridgeSnooze,now)&&!canLeaveBook)return;
        bool resumeAfterInteraction=behavior.Animation==0&&desiredRow!=0&&!AssistantStates.Once(bridgeStatus);
        if(bridgeStatus==bridgeApplied&&!resumeAfterInteraction&&!companionPreempted)return;
        if(canLeaveBook){automaticBookChange=false;readingLibrary.Finish(false);}
        if(bridgeStatus==null||bridgeStatus=="idle"||bridgeStatus=="ready"||bridgeStatus=="error")readingLibrary.TaskEnded();
        bridgeApplied=bridgeStatus;
        int row=AssistantStates.Row(bridgeStatus);
        behavior.PlayAfterExit(row,AssistantStates.Once(bridgeStatus),now);
#if STYLE_V10
        image.ToolTip="小龙娘 · "+(bridgeStatus==null?"安静陪伴":SourceName(bridgeSource)+" · "+StateName(bridgeStatus))+"\n慢拖拎起 · 甩出缓降 · 右键打开菜单";
#endif
    }
    Point CursorInCell(POINT p){var source=PresentationSource.FromVisual(this);Vector v=source.CompositionTarget.TransformFromDevice.Transform(new Vector(p.X,p.Y));return new Point(v.X*CW/bodyWidth,v.Y*CW/bodyWidth);}
    Point PointerDip(POINT p){Point q=CursorInCell(p);return new Point(q.X*bodyWidth/CW,q.Y*bodyWidth/CW);}
    void Tick() {
        long start=Stopwatch.GetTimestamp();
        try{TickCore();}finally{maxTickMilliseconds=Math.Max(maxTickMilliseconds,(Stopwatch.GetTimestamp()-start)*1000.0/Stopwatch.Frequency);
#if STYLE_V10
            try{CaptureConnectionAudit();}catch{}
#endif
        }
    }
    void TickCore() {
#if STYLE_V10
        // A 16.67 ms DispatcherTimer can round to two coarse Windows ticks.
        // Use an 8 ms request only during flight/folding; media time and the
        // normal render throttle still decide which source frame is shown.
        bool activeWings=flight.Active||pendingWingFold||behavior.Animation==17;
#if WINDOW_PERCH
        activeWings=activeWings||(windowPerch.Attached&&perchArrival>0);
#endif
        double requestedInterval=activeWings?8:1000.0/60;
        if(Math.Abs(timer.Interval.TotalMilliseconds-requestedInterval)>.01)timer.Interval=TimeSpan.FromMilliseconds(requestedInterval);
#endif
        long stamp=clock.ElapsedMilliseconds;if(stamp-lastLogicTick<8){RenderFrame();return;}lastLogicTick=stamp;
        if(closed)return;POINT p;CursorPollWorked=GetCursorPos(out p);long now=clock.ElapsedMilliseconds;double? angle=null;
        if(CursorPollWorked) {
            if(Math.Abs(p.X-lastPointer.X)+Math.Abs(p.Y-lastPointer.Y)>2){lastPointer=p;lastPointerMotion=now;}
            if(now-lastPointerMotion<1600) {
                var local=PointFromScreen(new Point(p.X,p.Y));double dx=local.X-RigPadding-bodyWidth/2,dy=local.Y-RigPadding-BodyHeight*.41;
                if(Math.Sqrt(dx*dx+dy*dy)>bodyWidth*.09)angle=(Math.Atan2(dy,dx)*180/Math.PI+450)%360;
            }
        }
        if(CursorPollWorked&&(motion.Active||motion.Landing)){Point q=CursorInCell(p);motion.Step(now,q.X,q.Y);}
        if(CursorPollWorked&&behavior.Dragged){grabPointer=p;Point q=PointerDip(p);gesture.Add(now,q.X,q.Y);}
#if WINDOW_PERCH
        UpdateWindowPerch(now);
#endif
        AdvanceFlight(now);
        UpdateBridge(now);
        bool linked=bridgeStatus!=null&&bridgeStatus!="idle";
#if FOCUS_COMPANION
        linked=linked||focusSession.Active&&!focusSession.IsBreak&&!focusSession.Paused;
#endif
#if WINDOW_PERCH
        linked=linked||windowPerch.Attached;
#endif
        behavior.Tick(now,angle,preferences.Follow&&!linked,CursorPollWorked&&!linked&&now-lastPointerMotion>=180000);
#if TABLET_PHASES
        if(tabletPhases.BoundaryNeedsRouting(behavior))UpdateBridge(now);
#endif
#if WINDOW_PERCH
        AdvancePerchArrival(now);
#endif
        FoldAfterDownstroke(now);
#if WINDOW_PERCH
        CompletePerchDeparture();
#endif
#if FOCUS_COMPANION
        UpdateFocusCompanion(now);
#endif
#if DAILY_ROUTINE
        UpdateDailyRoutine(now);
#endif
#if SMALL_TALK
        UpdateSmallTalk(now);
#endif
        UpdateReadingLibrary(now);RenderFrame();
    }
    void UpdateReadingLibrary(long now){
#if STYLE_V10
        if(readingLibrary.InExchange&&behavior.Animation!=25){readingLibrary.Finish(behavior.NaturalReadingTransition&&behavior.Animation==19);automaticBookChange=false;}
        bool frozen=behavior.Paused||behavior.MenuOpen||behavior.Hidden||behavior.Pressed||behavior.MediaWaiting||transitioning||flight.Active||pendingWingFold;
        readingLibrary.Update(now,behavior.Animation==19&&!behavior.SingleAction,frozen);
        bool allowBookExchange=true;
#if FOCUS_COMPANION
        allowBookExchange=!FocusOwnsPose||!focusStopping;
#endif
        if(readingLibrary.Due&&!frozen&&allowBookExchange&&behavior.Animation==19&&behavior.VisualTime(now)<100){readingLibrary.Begin();automaticBookChange=true;behavior.Play(25,true,now);smooth.Warm(25);}
#endif
    }
    void Press(object sender,MouseButtonEventArgs e) {
        Point local=e.GetPosition(this);if(!Painted(local))return;
        POINT p;GetCursorPos(out p);BeginPointer(e.GetPosition(image),e.ClickCount,p,clock.ElapsedMilliseconds);
        if(behavior.Pressed)CaptureMouse();else if(IsMouseCaptured)ReleaseMouseCapture();
        RenderFrame();e.Handled=true;
    }
    void Move(object sender,MouseEventArgs e) {
        long now=clock.ElapsedMilliseconds;
        if(behavior.Sleeping&&Painted(e.GetPosition(this))){behavior.Wake(now);RenderFrame();}
        if(!behavior.Pressed)return;POINT p;if(!GetCursorPos(out p))return;MovePointer(p,now);
    }
    void BeginPointer(Point local,int count,POINT p,long now){
#if FOCUS_COMPANION
        CancelFocusOwnership(now,false);
#endif
#if DAILY_ROUTINE
        CancelDailyRoutine(now,false,-1);
#endif
#if WINDOW_PERCH
        bool wasPerched=windowPerch.Attached;
#else
        bool wasPerched=false;
#endif
        bool wasFlying=flight.Active;if(!wasPerched)CancelFlight(now);motion.Reset();pressPoint=grabPointer=p;pressLeft=Left+RigPadding;pressTop=Top+RigPadding;pressLocal=local;Point q=PointerDip(p);gesture.Begin(now,q.X,q.Y);behavior.BeginPress(!wasPerched&&local.Y<BodyHeight*.52,wasFlying||wasPerched?1:count,now);
#if CLICK_BOUNCE
        if(!wasFlying&&!wasPerched&&!behavior.Paused&&!behavior.MenuOpen&&!behavior.Hidden&&!behavior.Sleeping)clickBounce.Punch(now);else clickBounce.Reset();
        if(wasFlying||wasPerched)behavior.ResetTapRun();
#endif
#if WINDOW_PERCH
        perchActionAfterLanding=-1;
#endif
#if STYLE_V10
        automaticBookChange=false;
        behavior.SetClickAction(PetBehavior.TapAction(local.X/bodyWidth*CW,local.Y/BodyHeight*CH));
#endif
#if STYLE_V10
        smooth.Warm(14);
#endif
    }
    void MovePointer(POINT p,long now){
        if(!behavior.Pressed)return;var source=PresentationSource.FromVisual(this);if(source==null)return;
        Vector delta=source.CompositionTarget.TransformFromDevice.Transform(new Vector(p.X-pressPoint.X,p.Y-pressPoint.Y));
        if(!behavior.Dragged&&Math.Abs(delta.X)<SystemParameters.MinimumHorizontalDragDistance&&Math.Abs(delta.Y)<SystemParameters.MinimumVerticalDragDistance)return;
#if WINDOW_PERCH
        windowPerch.Detach();
        perchActionAfterLanding=-1;
#endif
        grabPointer=p;Point q=CursorInCell(p);
        Point sample=PointerDip(p);gesture.Add(now,sample.X,sample.Y);
        if(!behavior.Dragged){motion.Begin(now,q.X,q.Y);pickupStarted=now;}
        behavior.Drag(14,now);motion.Step(now,q.X,q.Y);
#if STYLE_V10
        UpdateGrabPosition(now);
#else
        Left=pressLeft+delta.X+pressLocal.X-bodyWidth*GrabMotion.PivotX-RigPadding;
        Top=pressTop+delta.Y+pressLocal.Y-BodyHeight*GrabMotion.PivotY-RigPadding;
#endif
        RenderFrame();
    }
#if STYLE_V10
    void UpdateGrabPosition(long now){
        var source=PresentationSource.FromVisual(this);if(source==null)return;
        Vector delta=source.CompositionTarget.TransformFromDevice.Transform(new Vector(grabPointer.X-pressPoint.X,grabPointer.Y-pressPoint.Y));
        double u=Math.Max(0,Math.Min(1,(now-pickupStarted)/180.0));u=u*u*(3-2*u);
        activeGrip=new Point(pressLocal.X/bodyWidth+(GrabMotion.PivotX-pressLocal.X/bodyWidth)*u,pressLocal.Y/BodyHeight+(GrabMotion.PivotY-pressLocal.Y/BodyHeight)*u);
        // The pivot and window position move together. The original clicked
        // point stays at the cursor while the grip settles onto the hanging pose.
        Left=pressLeft+delta.X+pressLocal.X-bodyWidth*activeGrip.X-RigPadding;
        Top=pressTop+delta.Y+pressLocal.Y-BodyHeight*activeGrip.Y-RigPadding;
    }
#endif
    void Release(object sender,MouseButtonEventArgs e) {
        EndPointer(clock.ElapsedMilliseconds);if(IsMouseCaptured)ReleaseMouseCapture();e.Handled=true;
    }
    void EndPointer(long now){
#if WINDOW_PERCH
        if(windowPerch.Attached&&!behavior.Dragged){behavior.CancelPointer(now);behavior.Play(22,false,now);RenderFrame();return;}
#endif
        bool moved=behavior.Dragged;double releasedAngle=motion.Angle;Vector velocity=new Vector();bool thrown=moved&&gesture.TryRelease(now,out velocity);
        behavior.EndPress(now);
        bridgeSnooze=now+1000;bridgeApplied=null;
        if(moved){
#if WINDOW_PERCH
            if(!thrown&&TryPerchNearPointer(now)){RenderFrame();return;}
#endif
#if STYLE_V10
            motion.End(now);
#else
            motion.Reset();
#endif
            if(thrown){motion.Reset();releaseKind="throw";Rect area=WorkAreaFor(Forms.Screen.FromPoint(new System.Drawing.Point(grabPointer.X,grabPointer.Y)));flight.Begin(now,new Point(Left+RigPadding,Top+RigPadding),velocity,area,bodyWidth,bodyWidth*FlightAsset.Foot/CW,releasedAngle);behavior.Play(16,true,now);AdvanceFlight(now);}else{releaseKind="fixed";ClampPosition();Save();}}
        RenderFrame();
    }
    void CancelFlight(long now){
#if WINDOW_PERCH
        windowPerch.Detach();
        perchActionAfterLanding=-1;
#endif
        pendingWingFold=false;if(flight.Active){flight.Cancel();behavior.Play(0,false,now);}}
    void FoldAfterDownstroke(long now){
        if(!pendingWingFold)return;
        if(behavior.Paused||behavior.MenuOpen||behavior.MediaWaiting)wingFoldDue+=Math.Max(0,now-lastWingFoldCheck);
        lastWingFoldCheck=now;
        if(behavior.Paused||behavior.MenuOpen||behavior.MediaWaiting||now<wingFoldDue)return;
        pendingWingFold=false;StartLandingFold(now);bridgeSnooze=now+650;bridgeApplied=null;
    }
    void StartLandingFold(long now){behavior.PlayFrom(17,true,now,LandingFoldStart);smooth.WarmFrom(17,LandingFoldStart);}
    void AdvanceFlight(long now){
        if(!flight.Active)return;flight.SetPaused(behavior.Paused||behavior.MenuOpen||(behavior.MediaWaiting&&!(transitionHoldsTimeline&&transitionMediaReady)),now);flight.Step(now);Left=flight.X-RigPadding;Top=flight.Y-RigPadding;
        if(!flight.Active){
            pendingWingFold=true;lastWingFoldCheck=now;
#if STYLE_V10
            double delay=0;
#else
            double delay=250-behavior.VisualTime(now);if(delay< -25)delay+=660;
#endif
            wingFoldDue=now+(long)Math.Max(0,delay);motion.End(now);Save();
        }
    }
    void Choose(int row){
#if CLICK_BOUNCE
        clickBounce.Reset();
#endif
#if FOCUS_COMPANION
        CancelFocusOwnership(clock.ElapsedMilliseconds,false);
#endif
#if DAILY_ROUTINE
        CancelDailyRoutine(clock.ElapsedMilliseconds,false,row);
#endif
#if WINDOW_PERCH
        if(DeferPerchedAction(row))return;
#endif
        CancelFlight(clock.ElapsedMilliseconds);automaticBookChange=false;if(readingLibrary.InExchange)readingLibrary.Finish(false);if(row==25)readingLibrary.Begin();behavior.SelectAfterExit(row,clock.ElapsedMilliseconds);bridgeSnooze=clock.ElapsedMilliseconds+5000;bridgeApplied=null;RenderFrame();}
    void BuildMenu() {
#if STYLE_V10
        BuildStyledMenu();
#else
        menu=new ContextMenu();menu.Opened+=delegate{behavior.SetMenu(true,clock.ElapsedMilliseconds);};menu.Closed+=delegate{behavior.SetMenu(false,clock.ElapsedMilliseconds);};
#if STYLE_V10
        menu.Items.Add(new MenuItem{Header="小龙娘 v10 · 二维重制 · 18组动作",IsEnabled=false});
#else
        menu.Items.Add(new MenuItem{Header="GPT白龙娘 v9.2 · 18组动作",IsEnabled=false});
#endif
        var actions=new MenuItem{Header="播放动作"};
        for(int i=0;i<PetBehavior.ActionRows.Length;i++){int selected=PetBehavior.ActionRows[i];
#if STYLE_V10
            var item=new MenuItem{Header=smooth.ActionName(selected)};
#else
            var item=new MenuItem{Header=PetBehavior.ActionNames[i]};
#endif
            item.Click+=delegate{Choose(selected);};actions.Items.Add(item);}menu.Items.Add(actions);
#if !STYLE_V10
        followItem=Toggle("视线跟随鼠标",preferences.Follow,delegate(bool v){preferences.Follow=v;Choose(0);Save();});
#endif
        topItem=Toggle("显示在其他窗口上方",preferences.Topmost,delegate(bool v){preferences.Topmost=v;Topmost=v;Save();});
        napItem=Toggle("安静3分钟后打盹",preferences.AutoNap,delegate(bool v){preferences.AutoNap=v;behavior.AutoNap=v;behavior.Touch(clock.ElapsedMilliseconds);Save();});
        pauseItem=Toggle("暂停动画",false,delegate(bool v){behavior.SetPaused(v,clock.ElapsedMilliseconds);RenderFrame();});
        Toggle("关联 Codex 本地状态",preferences.LinkCodex,delegate(bool v){preferences.LinkCodex=v;bridgeNextRead=0;bridgeSnooze=0;Save();});
        var chat=new MenuItem{Header="打开 ChatGPT"};chat.Click+=delegate{Process.Start("https://chatgpt.com/");};menu.Items.Add(chat);
#if STYLE_V10
        var sizes=new MenuItem{Header="角色大小"};foreach(int n in new int[]{280,350,420,480}){int size=n;var item=new MenuItem{Header=n==350?"与 DeepSeek 桌宠同高":n==280?"小":n==420?"稍大":"大"};item.Click+=delegate{ResizePet(size);};sizes.Items.Add(item);}menu.Items.Add(sizes);
#else
        var sizes=new MenuItem{Header="角色大小"};foreach(int n in new int[]{192,240,288,384}){int size=n;var item=new MenuItem{Header=n==288?"标准（推荐）":n==192?"小":n==240?"中":"大"};item.Click+=delegate{ResizePet(size);};sizes.Items.Add(item);}menu.Items.Add(sizes);
#endif
        var reset=new MenuItem{Header="回到当前屏幕右下角"};reset.Click+=delegate{ResetPosition();};menu.Items.Add(reset);
        var hide=new MenuItem{Header="隐藏角色（从托盘找回）"};hide.Click+=delegate{SetHidden(true);};menu.Items.Add(hide);
        menu.Items.Add(new Separator());var exit=new MenuItem{Header="退出白龙娘"};exit.Click+=delegate{Close();};menu.Items.Add(exit);image.ContextMenu=menu;
#endif
    }
    MenuItem Toggle(string label,bool initial,Action<bool> callback) {
        var item=new MenuItem{Header=label,IsCheckable=true,IsChecked=initial};item.Click+=delegate{callback(item.IsChecked);};menu.Items.Add(item);return item;
    }
    void CreateTray(bool visible) {
        trayIcon=System.Drawing.Icon.ExtractAssociatedIcon(Assembly.GetExecutingAssembly().Location);
        tray=new Forms.NotifyIcon{Icon=trayIcon,Text="小龙娘 · 双击找回，右键菜单"};
        var ctx=new Forms.ContextMenuStrip();ctx.Items.Add("显示白龙娘",null,delegate{Dispatcher.BeginInvoke(new Action(delegate{SetHidden(false);if(!flight.Active)ResetPosition();}));});
        ctx.Items.Add("隐藏白龙娘",null,delegate{Dispatcher.BeginInvoke(new Action(delegate{SetHidden(true);}));});
        ctx.Items.Add("暂停 / 继续",null,delegate{Dispatcher.BeginInvoke(new Action(delegate{behavior.SetPaused(!behavior.Paused,clock.ElapsedMilliseconds);pauseItem.IsChecked=behavior.Paused;}));});
        ctx.Items.Add("退出白龙娘",null,delegate{Dispatcher.BeginInvoke(new Action(Close));});tray.ContextMenuStrip=ctx;
        tray.DoubleClick+=delegate{Dispatcher.BeginInvoke(new Action(delegate{SetHidden(false);if(!flight.Active)ResetPosition();}));};tray.Visible=visible;
    }
    void SetHidden(bool hidden) {
#if DAILY_ROUTINE
        dailyRoutine.ResetClock();
#endif
        long now=clock.ElapsedMilliseconds;
        if(hidden==behavior.Hidden)return;
#if SMALL_TALK
        if(hidden)SilenceSmallTalk(now);
#endif
#if FOCUS_COMPANION
        AdvanceFocusClock(now);
#endif
        if(flight.Active)flight.SetPaused(hidden||behavior.Paused||behavior.MenuOpen||(behavior.MediaWaiting&&!(transitionHoldsTimeline&&transitionMediaReady)),now);
        behavior.CancelPointer(now);motion.Reset();if(IsMouseCaptured)ReleaseMouseCapture();
        if(hidden){hiddenStarted=now;lastVisualRender=now;behavior.SuspendHidden(now);timer.Stop();Hide();}
        else {
            if(pendingWingFold){wingFoldDue+=Math.Max(0,now-hiddenStarted);lastWingFoldCheck=now;}
            lastVisualRender=now;Show();ClampPosition();behavior.ResumeAfterHidden(now);timer.Start();RenderFrame();
        }
#if FOCUS_COMPANION
        AdvanceFocusClock(now);UpdateFocusBadge(now);
#endif
    }
    Rect CurrentWorkArea() {
        return WorkAreaFor(Forms.Screen.FromHandle(new WindowInteropHelper(this).Handle));
    }
    Rect WorkAreaFor(Forms.Screen selected) {
        var source=PresentationSource.FromVisual(this);
        if(source==null)return SystemParameters.WorkArea;
        var screen=selected.WorkingArea;
#if WINDOW_PERCH
        // Use one stable screen transform. Adding the requested fractional
        // Window.Top to PointFromScreen can accumulate HWND rounding error.
        var matrix=source.CompositionTarget.TransformFromDevice;
        return new Rect(matrix.Transform(new Point(screen.Left,screen.Top)),matrix.Transform(new Point(screen.Right,screen.Bottom)));
#else
        Point start=PointFromScreen(new Point(screen.Left,screen.Top));Point end=PointFromScreen(new Point(screen.Right,screen.Bottom));
        return new Rect(Left+start.X,Top+start.Y,end.X-start.X,end.Y-start.Y);
#endif
    }
    void ClampPosition(){Point p=PetBehavior.Clamp(new Point(Left+RigPadding,Top+RigPadding),new Size(bodyWidth,bodyWidth*FlightAsset.Foot/CW),CurrentWorkArea());Left=p.X-RigPadding;Top=p.Y-RigPadding;}
    void ResetPosition(){CancelFlight(clock.ElapsedMilliseconds);behavior.CancelPointer(clock.ElapsedMilliseconds);motion.Reset();Rect area=CurrentWorkArea();Left=area.Right-bodyWidth-24-RigPadding;Top=area.Bottom-bodyWidth*FlightAsset.Foot/CW-RigPadding;ClampPosition();Save();RenderFrame();}
    void ResizePet(double width){
#if WINDOW_PERCH
        if(ResizeWindowPerch(width))return;
#endif
        CancelFlight(clock.ElapsedMilliseconds);behavior.CancelPointer(clock.ElapsedMilliseconds);motion.Reset();double center=Left+RigPadding+bodyWidth/2,bottom=Top+RigPadding+BodyHeight;bodyWidth=width;SetBodySize();Left=center-bodyWidth/2-RigPadding;Top=bottom-BodyHeight-RigPadding;ClampPosition();Save();RenderFrame();}
    void Save() {
        if(testing||!initialized)return;
#if WINDOW_PERCH
        if(!windowPerch.Attached){preferences.Left=Left+RigPadding;preferences.Top=Top+RigPadding;}
#else
        preferences.Left=Left+RigPadding;preferences.Top=Top+RigPadding;
#endif
        preferences.Size=bodyWidth;
        try{PreferenceStore.Save(settingsPath,preferences);}catch{if(tray!=null)tray.Text="GPT白龙娘 · 设置未保存，请解压到可写文件夹";}
    }
    void DisplaysChanged(object sender,EventArgs e){if(!closed)Dispatcher.BeginInvoke(new Action(delegate{if(!closed){CancelFlight(clock.ElapsedMilliseconds);ClampPosition();Save();}}));}
    public void SelfTest(string folder) {
        Directory.CreateDirectory(folder);preferences.Follow=false;behavior.AutoNap=false;
        var report=BehaviorChecks.Run(folder);CheckAsyncDecode(report);report["assistant_cases"]=AssistantChecks.Run();report["main_actions"]=PetBehavior.ActionRows.Length;report["native_window_loaded"]=IsLoaded;report["cursor_poll_worked"]=CursorPollWorked;
        Check(IsLoaded&&CursorPollWorked,"native-load");
        IntPtr handle=new WindowInteropHelper(this).Handle;
        Check(SendMessage(handle,0x21,IntPtr.Zero,IntPtr.Zero).ToInt64()==3,"mouse activation");
        Check(GetForegroundWindow()!=handle,"foreground focus");report["mouse_click_does_not_activate_window"]=true;
        report["actual_monitor_count"]=Forms.Screen.AllScreens.Length;report["actual_work_area"]=CurrentWorkArea().ToString();
        menu.Opacity=0;menu.IsOpen=true;Check(menu.IsOpen,"menu open");menu.IsOpen=false;menu.Opacity=1;
        // Popup closing animation completes asynchronously. The remaining
        // synchronous self-tests start from a logically closed menu fixture.
        behavior.SetMenu(false,clock.ElapsedMilliseconds);
        foreach(MenuItem item in ((MenuItem)menu.Items[1]).Items)item.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        Check(((MenuItem)menu.Items[1]).Items.Count==18,"18 actions");report["eighteen_menu_actions_dispatched"]=true;
        Choose(0);Check(!Painted(new Point(0,0))&&Painted(new Point(RigPadding+bodyWidth/2,RigPadding+BodyHeight*.48)),"alpha hit test");report["alpha_clickthrough"]=true;
        ResizePet(384);Check(bodyWidth==384&&BodyHeight==416&&Width==640&&Height==672,"resize and swing RigPadding");
        Rect area=CurrentWorkArea();Left=area.Right+400;Top=area.Bottom+400;ClampPosition();
        Check(Left+RigPadding+bodyWidth<=area.Right+1.1&&Top+RigPadding+bodyWidth*FlightAsset.Foot/CW<=area.Bottom+1.1,"foot and screen bounds");report["full_character_stays_in_work_area"]=true;
        CreateTray(false);Check(tray.ContextMenuStrip.Items.Count==4,"tray items");report["tray_created_without_showing_test_icon"]=true;
        SetHidden(true);Check(!timer.IsEnabled&&!IsVisible,"hidden pause");SetHidden(false);Check(timer.IsEnabled&&IsVisible&&timerHandlers==1,"restore once");report["hide_restore_no_duplicate_timer"]=true;
        CheckGrab(folder,report);CheckThrow(folder,report);CheckSmoothAndBridge(folder,report);RenderProof(folder);Choose(4);int before=frameChanges;
        var end=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(1800)};
        end.Tick+=delegate{end.Stop();try {
            Check(behavior.Animation==0&&!behavior.SingleAction&&frameChanges>before,"real timer completion");
            report["timer_action_returns_idle"]=true;report["timer_frame_changes"]=frameChanges-before;report["ok"]=true;
            report["composition_intervals_ms"]=renderIntervals.ToArray();report["smooth_source_changes"]=smoothChanges;report["decoded_motion_cache_rows"]=smooth.CachedRows;report["decoded_motion_pixel_bytes"]=smooth.CachedPixelBytes;
            CheckNewMedia(folder,report);
        }catch(Exception error){Fail(folder,error);}};end.Start();
    }
    void CheckAsyncDecode(Dictionary<string,object> report){
        using(var decoder=new SmoothMotion()){
            var watch=Stopwatch.StartNew();var pending=decoder.Get(8,0);watch.Stop();
            Check(pending==null&&watch.Elapsed.TotalMilliseconds<150,"cold action request blocked UI");
            report["cold_action_request_ms"]=watch.Elapsed.TotalMilliseconds;
            Check(decoder.EnsureReady(8,10000),"background decoder did not complete");Check(decoder.Get(8,0).IsFrozen,"worker frame is not thread safe");
            for(int i=0;i<20;i++){decoder.Warm(i%2==0?15:17);Check(decoder.CachedRows<=2,"prefetch cache exceeded limit");}
            Check(decoder.EnsureReady(17,10000),"latest prefetched action did not finish");
            report["background_action_decoder"]=true;report["background_decode_ms"]=decoder.MaxDecodeMilliseconds;report["cold_action_has_pose_fallback"]=frames[18,0]!=null&&frames[16,0]!=null;report["gaze_and_posters_replace_large_legacy_decode"]=true;
        }
        Choose(18);var pose=image.Source;behavior.SetPaused(true,clock.ElapsedMilliseconds);
        Check(smooth.EnsureReady(18,10000),"paused background load failed");RenderFrame();Check(Object.ReferenceEquals(pose,image.Source),"paused decode completion changed displayed pose");
        behavior.SetPaused(false,clock.ElapsedMilliseconds);RenderFrame();Check(!Object.ReferenceEquals(pose,image.Source),"resume did not publish decoded action");report["pause_freezes_pending_decode_pose"]=true;Choose(0);
    }
    void CheckNewMedia(string folder,Dictionary<string,object> report){
        int[] rows=PetBehavior.ActionRows;
        int[] durations=new int[rows.Length];
        for(int i=0;i<rows.Length;i++)durations[i]=behavior.TotalDuration(rows[i]);
        int[] allRows={0,1,2,3,4,5,6,7,8,11,12,13,14,15,16,17,18,19,20,21};
        var states=new List<Dictionary<string,object>>();
        // Check arbitrary seeks, PBGRA alpha hit tests and foot-sized native captures.
        foreach(int row in allRows){
            Choose(row);Check(smooth.EnsureReady(row,10000),"new media first frame");
            for(int j=0;j<4;j++){
                int duration=behavior.TotalDuration(row);double t=j*(duration-1)/3.0;
                Check(smooth.EnsureSample(row,t,10000),"new media random seek");
                image.Source=smooth.Get(row,t);image.Opacity=1;transitionImage.Source=null;transitioning=false;
                Check(((BitmapSource)image.Source).IsFrozen,"stream frame not frozen");
                Check(!Painted(new Point(0,0))&&Painted(new Point(RigPadding+bodyWidth/2,RigPadding+BodyHeight*.48)),"stream alpha hit test row "+row);
                CaptureRig(Path.Combine(folder,"native-new-"+row+"-"+j+".png"));
            }
            Check(smooth.CachedStreamFrames<=64,"stream cache exceeded bound");
        }
        int stage=-1,changes=0,requests=0,misses=0;long started=0;double peakPrivate=0;
        var playback=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(50)};
        Action next=delegate {
            stage++;if(stage>=rows.Length)return;
            Check(smooth.EnsureReady(rows[stage],10000),"playback warmup");
            behavior.Play(rows[stage],false,clock.ElapsedMilliseconds);RenderFrame();
            started=clock.ElapsedMilliseconds;changes=frameChanges;requests=smooth.StreamRequests;misses=smooth.StreamMisses;
        };
        next();
        playback.Tick+=delegate {
            try {
                peakPrivate=Math.Max(peakPrivate,Process.GetCurrentProcess().PrivateMemorySize64);
                Check(smooth.CachedRows<=2&&smooth.CachedStreamFrames<=64,"real playback cache exceeded bound");
                if(clock.ElapsedMilliseconds-started<durations[stage])return;
                states.Add(new Dictionary<string,object>{{"row",rows[stage]},{"elapsed_ms",clock.ElapsedMilliseconds-started},{"source_changes",frameChanges-changes},{"image_changes_per_second",(frameChanges-changes)*1000.0/(clock.ElapsedMilliseconds-started)},{"requests",smooth.StreamRequests-requests},{"cache_misses",smooth.StreamMisses-misses},{"cached_pixel_bytes",smooth.CachedPixelBytes}});
                if(stage+1<rows.Length){next();return;}
                playback.Stop();Check(smooth.LastError==null,"stream decoder error: "+smooth.LastError);
                report["new_media_native_playback"]=states;report["new_media_random_seeks_and_alpha_hit_test"]=true;
                report["random_seek_rows"]=allRows;report["all_public_actions_timed"]=states.Count==18;
                report["stream_cache_frames_limit_per_row"]=32;report["stream_lookahead_frames"]=12;
                report["stream_max_frame_decode_ms"]=smooth.MaxFrameDecodeMilliseconds;report["native_playback_peak_private_bytes"]=peakPrivate;
                report["composition_intervals_ms"]=renderIntervals.ToArray();report["verification_scope"]="Actual hidden WPF window, real timers, streamed media, synthetic input helpers. Not a claim that every AI frame is anatomically perfect or that the monitor presents every 60 Hz sample.";
                File.WriteAllText(Path.Combine(folder,"desktop-test.json"),new JavaScriptSerializer().Serialize(report));Close();
            }catch(Exception error){playback.Stop();Fail(folder,error);}
        };playback.Start();
    }
    void CaptureRig(string file){
        UpdateLayout();var bitmap=new RenderTargetBitmap((int)Math.Ceiling(Width),(int)Math.Ceiling(Height),96,96,PixelFormats.Pbgra32);bitmap.Render(canvas);
        var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(bitmap));using(var f=File.Create(file))png.Save(f);
    }
    void CaptureSettledRig(string file){image.Opacity=1;transitionImage.Source=null;transitioning=false;CaptureRig(file);}
    void CheckGrab(string folder,Dictionary<string,object> report){
        Choose(0);long start=clock.ElapsedMilliseconds;var p=new POINT{X=500,Y=500};
        BeginPointer(new Point(bodyWidth*.5,BodyHeight*.3),1,p,start);
        for(int i=1;i<=35;i++){p.X=500+i*8;MovePointer(p,start+i*20);}
        Check(behavior.Animation==14&&behavior.Dragged&&motion.Angle>5,"rightward pickup and lag");double right=motion.Angle;
        CaptureRig(Path.Combine(folder,"native-grab-right.png"));
        Point sample=image.TransformToAncestor(this).Transform(new Point(bodyWidth*.45,BodyHeight*.5));Check(Painted(sample),"inverse rotated alpha hit test");
        for(int i=1;i<=45;i++){p.X=780-i*8;MovePointer(p,start+700+i*20);}
        Check(motion.Angle< -5,"leftward pickup and lag");double left=motion.Angle;CaptureRig(Path.Combine(folder,"native-grab-left.png"));
        for(int i=1;i<=35;i++){p.Y=500-i*8;MovePointer(p,start+1600+i*20);}
        Check(motion.Stretch>1.01,"upward stretch");double up=motion.Stretch;CaptureRig(Path.Combine(folder,"native-grab-up.png"));
        for(int i=1;i<=45;i++){p.Y=220+i*8;MovePointer(p,start+2300+i*20);}
        Check(motion.Stretch<.99,"downward compression");double down=motion.Stretch;
        EndPointer(start+3210);Check(behavior.Animation==0&&behavior.PendingClick==0&&!motion.Active&&!motion.Landing&&!flight.Active&&releaseKind=="fixed","slow release pins without flight");
        motion.Step(start+3500,0,0);RenderFrame();ClampPosition();
        Check(!motion.Landing&&motion.Stretch==1&&motion.Angle==0,"landing settles");
        report["native_grab_input_path"]=true;report["native_grab_right_left_degrees"]=new double[]{right,left};report["native_grab_up_down_scale"]=new double[]{up,down};
        report["rotated_alpha_hit_test"]=true;report["grab_release_no_jump"]=true;report["grab_test_scope"]="Actual WPF input helper path with synthetic pointer coordinates; global mouse was not moved.";
    }
    void CheckThrow(string folder,Dictionary<string,object> report){
        Choose(0);Rect area=CurrentWorkArea();Left=area.Left+300-RigPadding;Top=area.Top+70-RigPadding;
        long now=clock.ElapsedMilliseconds;var p=new POINT{X=600,Y=400};BeginPointer(new Point(bodyWidth*.5,BodyHeight*.3),1,p,now);
        for(int i=1;i<=8;i++){p.X=600+i*52;p.Y=400-i*12;MovePointer(p,now+i*20);}
        EndPointer(now+165);Check(flight.Active&&behavior.Animation==16&&releaseKind=="throw"&&behavior.PendingClick==0,"fast release did not open wings");
        double target=flight.TargetY;long start=now+165;double duration=flight.Duration;int initial=behavior.Column;
        AdvanceFlight(start+400);behavior.Tick(start+400,null,false,false);RenderFrame();Check(behavior.Animation==15,"opening did not lead to wing loop");CaptureRig(Path.Combine(folder,"native-flight.png"));
        AdvanceFlight(start+(long)(duration*500));Check(flight.Active&&Top+RigPadding<target,"descent not gradual");
        AdvanceFlight(start+(long)(duration*1000)+1);RenderFrame();
        long landedAt=start+(long)(duration*1000)+1;for(int i=0;i<100;i++){long n=landedAt+i*16;behavior.Tick(n,null,false,false);FoldAfterDownstroke(n);}RenderFrame();
        double sole=Top+RigPadding+bodyWidth*FlightAsset.Foot/CW;Check(!flight.Active&&behavior.Animation==0&&Math.Abs(sole-area.Bottom)<.01,"feet do not meet taskbar edge");
        motion.Step(start+(long)(duration*1000)+300,0,0);RenderFrame();CaptureRig(Path.Combine(folder,"native-landed.png"));
        double fixedTop=Top,fixedLeft=Left;AdvanceFlight(start+20000);Check(Top==fixedTop&&Left==fixedLeft,"landed pet drifted");
        flight.Begin(start+21000,new Point(Left+RigPadding,area.Top+40),new Vector(1500,0),area,bodyWidth,bodyWidth*FlightAsset.Foot/CW);behavior.Play(15,false,start+21000);AdvanceFlight(start+21500);
        BeginPointer(new Point(bodyWidth*.5,BodyHeight*.3),1,p,start+21510);Check(!flight.Active&&behavior.Pressed,"cannot grab during flight");behavior.CancelPointer(start+21520);motion.Reset();RenderFrame();ClampPosition();
        report["throw_starts_wing_loop"]=true;report["flight_gradual"]=true;report["landed_sole_y"]=sole;report["taskbar_work_area_bottom"]=area.Bottom;report["taskbar_contact_error_dip"]=Math.Abs(sole-area.Bottom);report["landed_position_fixed"]=true;report["flight_can_be_grabbed_again"]=true;
    }
    void CheckSmoothAndBridge(string folder,Dictionary<string,object> report){
        Check(smooth.EnsureReady(3,10000),"background action decode");var first=smooth.Get(3,0);var retainedPixels=new byte[CW*CH*4];first.CopyPixels(retainedPixels,CW*4,0);
        Check(smooth.EnsureSample(3,60,10000),"streaming subframe decode");var middle=smooth.Get(3,60);Check(!Object.ReferenceEquals(first,middle),"subframes missing between retained keys");
        var immutablePixels=new byte[CW*CH*4];first.CopyPixels(immutablePixels,CW*4,0);for(int i=0;i<immutablePixels.Length;i++)Check(immutablePixels[i]==retainedPixels[i],"decoder scratch buffer changed a retained frozen frame");report["stream_frames_own_immutable_pixel_copies"]=true;
        var b=new PetBehavior(500);b.Play(3,false,100);double before=b.VisualTime(160);b.SetPaused(true,160);Check(Math.Abs(before-b.VisualTime(900))<.01,"paused subframe moves");b.SetPaused(false,1000);Check(Math.Abs(before-b.VisualTime(1000))<.01,"resumed subframe jumps");
        b.Play(3,false,1000);b.SetMenu(true,1060);before=b.VisualTime(1060);Check(b.VisualTime(1800)==before,"menu subframe moves");b.SetMenu(false,1900);Check(b.VisualTime(1900)==before,"menu resume jumps");
        string dir=Path.Combine(folder,"bridge-fixture");DateTime now=DateTime.UtcNow;
        PetBridge.Write(dir,"session-a","working","test",now);PetBridge.Write(dir,"session-b","needs_input","test",now);
        Check(PetBridge.Read(dir,now).status=="needs_input","multi-session priority");
        Check(PetBridge.Read(dir,now.AddHours(2))==null,"stale bridge status remains active");
        File.WriteAllText(Path.Combine(dir,"session-malformed.json"),"{bad json");Check(PetBridge.Read(dir,now).status=="needs_input","malformed bridge file breaks reader");
        Check(smooth.CachedRows<=2,"unbounded decoded motion cache");
        bridgeFolder=Path.Combine(folder,"native-bridge-fixture");preferences.LinkCodex=true;bridgeSnooze=bridgeNextRead=0;bridgeApplied=null;
        behavior.Play(0,false,clock.ElapsedMilliseconds);
        PetBridge.Write(bridgeFolder,"native-session","working","test",now);UpdateBridge(clock.ElapsedMilliseconds);RenderFrame();Check(behavior.Animation==19,"live bridge did not select working animation");CaptureSettledRig(Path.Combine(folder,"native-linked-working.png"));
        PetBridge.Write(bridgeFolder,"native-session","thinking","test",now);bridgeNextRead=0;UpdateBridge(clock.ElapsedMilliseconds);RenderFrame();Check(behavior.Animation==18,"live bridge did not select thinking animation");CaptureSettledRig(Path.Combine(folder,"native-linked-thinking.png"));
        PetBridge.Write(bridgeFolder,"native-session","review","test",now);bridgeNextRead=0;UpdateBridge(clock.ElapsedMilliseconds);RenderFrame();Check(behavior.Animation==19,"live bridge did not select reading animation");CaptureSettledRig(Path.Combine(folder,"native-linked-reading.png"));
        PetBridge.Write(bridgeFolder,"native-session","needs_input","test",now);bridgeNextRead=0;UpdateBridge(clock.ElapsedMilliseconds);RenderFrame();Check(behavior.Animation==20,"live bridge did not select note-card animation");CaptureSettledRig(Path.Combine(folder,"native-linked-waiting.png"));
        PetBridge.Write(bridgeFolder,"native-session","ready","test",now);bridgeNextRead=0;UpdateBridge(clock.ElapsedMilliseconds);Check(behavior.Animation==21&&behavior.SingleAction,"live bridge did not select delivery animation");
        behavior.Play(0,false,clock.ElapsedMilliseconds);bridgeApplied=null;PetBridge.Write(bridgeFolder,"native-session","working","test",now);bridgeNextRead=0;
        long inputAt=clock.ElapsedMilliseconds;behavior.BeginPress(false,1,inputAt);behavior.EndPress(inputAt+20);UpdateBridge(inputAt+30);Check(behavior.PendingClick>0,"real bridge canceled pending click");
        behavior.Tick(inputAt+20+behavior.ClickDelay,null,false,false);UpdateBridge(inputAt+20+behavior.ClickDelay+30);Check(behavior.Animation==4,"real bridge interrupted jump");
        behavior.Tick(inputAt+20+behavior.ClickDelay+900,null,false,false);UpdateBridge(inputAt+20+behavior.ClickDelay+901);Check(behavior.Animation==19,"bridge did not resume after interaction");
        preferences.LinkCodex=false;bridgeNextRead=0;UpdateBridge(clock.ElapsedMilliseconds);Check(behavior.Animation==0,"disabled link retained old state");bridgeFolder=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"联动状态");
        report["smooth_inbetweens_change_within_keyframe"]=true;report["pause_and_menu_freeze_subframes"]=true;report["bridge_priority_expiry_and_malformed_inputs"]=true;report["wing_open_and_fold_transitions"]=true;
        report["actual_window_bridge_file_to_action_switching"]=true;
        report["native_bridge_respects_pending_click_and_jump"]=true;
    }
    void RenderProof(string folder) {
        var v=new DrawingVisual();using(var d=v.RenderOpen()) {
            d.DrawRectangle(new SolidColorBrush(Color.FromRgb(53,54,66)),null,new Rect(0,0,1536,832));
            int[] rs={0,11,11,12,12,13,13,10};int[] cs={0,2,3,2,4,2,3,7};
            for(int i=0;i<rs.Length;i++)d.DrawImage(frames[rs[i],cs[i]],new Rect(i%4*CW,i/4*CH,CW,CH));
        }
        var bitmap=new RenderTargetBitmap(1536,832,96,96,PixelFormats.Pbgra32);bitmap.Render(v);
        var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(bitmap));using(var f=File.Create(Path.Combine(folder,"desktop-render.png")))png.Save(f);
    }
    static void Check(bool condition,string label){if(!condition)throw new Exception(label);}
    void Fail(string folder,Exception error){File.WriteAllText(Path.Combine(folder,"desktop-test-failed.txt"),error.ToString());Environment.ExitCode=1;Close();}
}
public static class Program {
    [STAThread]public static int Main(string[] args) {
        bool test=args.Length>=2&&args[0]=="--self-test";Mutex single=null;
        try {
            if(!test){bool first;single=new Mutex(true,"Local\\GPTWhiteDragonPet_6ad72d1bd8848191b510eb705b3b2805",out first);if(!first){MessageBox.Show("白龙娘已经在运行。请先从托盘退出旧版，再打开新版。","GPT白龙娘");return 0;}}
            var app=new Application{ShutdownMode=ShutdownMode.OnMainWindowClose};var window=new PetWindow(test);app.MainWindow=window;
#if STYLE_V10
            if(!test&&Array.IndexOf(args,"--connection-audit")>=0)window.BeginConnectionAudit();
#endif
            if(test){bool started=false;window.Opacity=0;window.Topmost=false;window.Loaded+=delegate {
                if(started)return;started=true;
                Directory.CreateDirectory(args[1]);File.WriteAllText(Path.Combine(args[1],"test-started.marker"),DateTime.UtcNow.ToString("o"));
                var delay=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(120)};delay.Tick+=delegate{delay.Stop();try{window.SelfTest(args[1]);}catch(Exception error){File.WriteAllText(Path.Combine(args[1],"desktop-test-failed.txt"),error.ToString());Environment.ExitCode=1;window.Close();}};delay.Start();};}
            app.Run(window);return Environment.ExitCode;
        }catch(Exception error){if(test){Directory.CreateDirectory(args[1]);File.WriteAllText(Path.Combine(args[1],"desktop-test-failed.txt"),error.ToString());return 1;}MessageBox.Show("白龙娘启动失败："+error.Message,"GPT白龙娘");return 1;}
        finally{if(single!=null)single.Dispose();}
    }
}
}
