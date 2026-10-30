using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Web.Script.Serialization;
using System.Windows;

namespace WhiteDragon {
public sealed class PetPreferences {
    public double Size=288,Left=double.NaN,Top=double.NaN;
    public bool Follow=true,Topmost=true,AutoNap=true,LinkCodex=false;
    public bool LinkChatGPT=false;
#if SMALL_TALK
    public bool SmallTalkEnabled=true;
#endif
#if FOCUS_COMPANION
    public bool FocusReading=true,FocusShowTimer=true;
    public int FocusMinutes=25;
#endif
#if DAILY_ROUTINE
    public bool DailyRoutine=true;
    public int DailyRhythm=1;
#endif
#if WINDOW_PERCH
    public bool WindowPerch=true;
#endif
}
public static class PreferenceStore {
    public static PetPreferences Load(string path) {
        foreach(string file in new string[]{path,path+".bak"}) {
            if(!File.Exists(file))continue;
            try {
                var value=new JavaScriptSerializer().Deserialize<PetPreferences>(File.ReadAllText(file));
                if(value==null)continue;
                if(double.IsNaN(value.Size)||double.IsInfinity(value.Size)||value.Size<160||value.Size>480)value.Size=288;
                if(double.IsInfinity(value.Left))value.Left=double.NaN;
                if(double.IsInfinity(value.Top))value.Top=double.NaN;
                return value;
            }catch{}
        }
        return new PetPreferences();
    }
    public static void Save(string path,PetPreferences value) {
        string temp=path+".tmp";
        File.WriteAllText(temp,new JavaScriptSerializer().Serialize(value),new UTF8Encoding(false));
        if(File.Exists(path)) {
            try {File.Replace(temp,path,path+".bak",true);}
            catch(IOException){File.Copy(path,path+".bak",true);File.Copy(temp,path,true);File.Delete(temp);}
        } else File.Move(temp,path);
    }
}
// This controller is shared by the real input/timer path and deterministic edge-case checks.
public sealed class PetBehavior {
    public static readonly int[] Counts={6,8,8,4,5,8,6,6,6,8,8,6,6,6,4,4,1,1,6,6,6,6,6,6,6,6};
    public static readonly int[] ActionRows={0,1,2,3,4,5,6,7,8,11,12,13,14,15,18,19,20,21,22,23,24,25};
    public static readonly string[] ActionNames={"待机","向右移动","向左移动","挥手","跳跃","受挫","等待回应","专注工作","检查成果","摸摸头","打盹","伸懒腰","拎起悬空","扇翅膀","抱尾思考","翻书查资料","举便笺等你","递成果松口气","坐下晃腿","抱尾巴打哈欠","戳脸躲开","换一本书"};
    public static readonly int[][] Durations={
        new int[]{1333,1333,1333,1333,1333,1335},new int[]{120,120,120,120,120,120,120,220},new int[]{120,120,120,120,120,120,120,220},
        new int[]{850,850,850,1000},new int[]{140,140,140,140,280},new int[]{140,140,140,140,140,140,140,240},
        new int[]{150,150,150,150,150,260},new int[]{120,120,120,120,120,220},new int[]{750,750,750,750,750,917},
        new int[]{180,180,180,180,180,180,180,180},new int[]{180,180,180,180,180,180,180,180},
        new int[]{605,605,605,605,605,608},new int[]{650,320,380,800,380,650},new int[]{180,180,200,450,200,260},new int[]{1100,110,140,750},new int[]{160,90,160,250},new int[]{360},new int[]{400},
        new int[]{650,300,420,160,430,640},new int[]{700,360,240,320,400,680},new int[]{650,280,500,160,400,710},new int[]{450,300,460,500,320,570},
        new int[]{800,850,850,850,850,800},new int[]{800,850,850,850,850,800},new int[]{800,850,850,850,850,800},new int[]{800,850,850,850,850,800}
    };
    public readonly int[][] Timeline;
#if DAILY_ROUTINE
    static PetBehavior(){
        Array.Resize(ref Counts,27);Counts[26]=6;
        Array.Resize(ref Durations,27);Durations[26]=new int[]{800,850,850,850,850,800};
        Array.Resize(ref ActionRows,23);ActionRows[22]=26;
        Array.Resize(ref ActionNames,23);ActionNames[22]="轻轻整理裙摆";
#if CLICK_BOUNCE
        Array.Resize(ref Counts,28);Counts[27]=6;
        Array.Resize(ref Durations,28);Durations[27]=new int[]{800,850,850,850,850,800};
        Array.Resize(ref ActionRows,24);ActionRows[23]=27;
        Array.Resize(ref ActionNames,24);ActionNames[23]="连点鼓脸护头";
        Array.Resize(ref Counts,29);Counts[28]=6;
        Array.Resize(ref Durations,29);Durations[28]=new int[]{780,780,780,780,790,790};
        Array.Resize(ref ActionRows,25);ActionRows[24]=28;
        Array.Resize(ref ActionNames,25);ActionNames[24]="托平板轻点";
#if TABLET_PHASES
        Array.Resize(ref Counts,31);Counts[29]=Counts[30]=6;
        Array.Resize(ref Durations,31);Durations[29]=new int[]{130,130,130,130,130,150};Durations[30]=new int[]{130,130,130,130,130,150};
#endif
#endif
    }
#endif
    public int TotalDuration(int row){int total=0;foreach(int d in Timeline[row])total+=d;return total;}
    public int Animation{get;private set;} public int Frame{get;private set;}
    public int Row{get;private set;} public int Column{get;private set;}
    public bool Pressed{get;private set;} public bool Dragged{get;private set;} public bool Petting{get;private set;}
    public bool Sleeping{get;private set;} public bool SingleAction{get;private set;} public bool Paused{get;private set;}
    public bool MenuOpen{get;private set;} public bool AutoNap=true;
    public bool Hidden{get;private set;}
    public bool MediaWaiting{get;private set;}
    public int PlaybackId{get;private set;}
    public double PlaybackStartMilliseconds{get;private set;}
    public double PlaybackRate{get;private set;}
    public int ExitTarget{get;private set;}
    bool exitOnce;
    public bool RecoveringPose{get{return ExitTarget>=0;}}
    static readonly int[] Exit22={252,252,252,252,252,252,252,252,252,252,234,231,225,210,210,210,204,198,183,180,183,189,186,189,180,180,186,186,186,186,180,186,192,198,204,210,216,222,228,234,240,246,252,252,252,252,252,252,252,252};
    static readonly int[] Exit24={213,213,243,252,252,252,243,243,201,183,183,183,186,180,180,180,180,180,180,180,180,180,180,180,180,180,180,180,180,180,180,186,192,198,204,210,216,222,228,234,240,246,252,252,252,252,243,216,213,213};
    public void PlayAfterExit(int row,bool once,long now){
        bool urgent=row==14||row==15||row==16||row==17||row==27||Pressed||Dragged;
        if(RecoveringPose&&!urgent&&row!=Animation){ExitTarget=row;exitOnce=once;return;}
        if(!urgent&&!RecoveringPose&&(Animation==22||Animation==24)){
            double phase=VisualTime(now);bool interior=Animation==22?phase>=1100&&phase<3800:phase>=850&&phase<3400;
            if(interior){
                int source=Animation,index=Math.Max(0,Math.Min(49,(int)(phase/100)));
                int sample=(source==22?Exit22:Exit24)[index];
                if(sample<246){PlayFrom(source,true,now,sample*1000.0/60);ExitTarget=row;exitOnce=once;PlaybackRate=2.6;StartVisualPlayback(now);return;}
            }
        }
        Play(row,once,now);
    }
    public void SelectAfterExit(int row,long now){
        CancelPointer(now);Touch(now);
        if(row==15){Select(row,now);return;}
        PlayAfterExit(row,row!=0&&row!=12&&row!=18&&row!=19&&row!=28,now);
    }
    public bool NaturalIdleTransition{get;private set;}
    public bool NaturalReadingTransition{get;private set;}
    public long LastTouch{get;private set;} public long PendingClick{get;private set;}
    public readonly int ClickDelay;
    // A continuous position in the retained keyframe timeline. Pauses freeze subframes too.
    public double VisualTime(long now) {
        double time=0;for(int i=0;i<Frame;i++)time+=Timeline[Animation][i];
        double remaining=Remaining(now);
        return time+Math.Max(0,Math.Min(Timeline[Animation][Frame],Timeline[Animation][Frame]-remaining*PlaybackRate));
    }
    int look=-1,clickAction=4;bool headPress,wokeOnPress,previewFlight;long nextFrame,pressStarted,holdUntil,pauseRemaining,menuStarted,deadlineReset,hiddenRemaining,mediaRemaining;
#if CLICK_BOUNCE
    int tapRun;long lastCompletedTap=-1,doublePressStarted;bool doublePress,retainClickPose;
    public bool ClickReaction{get{return Animation==27&&SingleAction;}}
    public void ResetTapRun(){tapRun=0;lastCompletedTap=-1;doublePress=false;retainClickPose=false;}
    bool ContinuingTaps(long now){return lastCompletedTap>=0&&now>=lastCompletedTap&&now-lastCompletedTap<=600;}
    void CompleteTap(long now){tapRun=ContinuingTaps(now)?Math.Min(3,tapRun+1):1;lastCompletedTap=now;}
#endif
    // Coordinates use the fixed source canvas, so resizing the window does not
    // change which part of the character the user touched.
    public static int TapAction(double x,double y){return x>=265&&x<=375&&y>=178&&y<=215?24:4;}
    public void SetClickAction(int row){clickAction=row==24?24:4;}
    long Remaining(long now){return Paused?pauseRemaining:Hidden?hiddenRemaining:MediaWaiting?mediaRemaining:nextFrame-(MenuOpen?Math.Max(menuStarted,deadlineReset):now);}
    public PetBehavior(int clickDelay):this(clickDelay,null){}
    public PetBehavior(int clickDelay,Dictionary<int,int> mediaDurations) {
        Timeline=new int[Durations.Length][];
        for(int row=0;row<Durations.Length;row++){
            Timeline[row]=(int[])Durations[row].Clone();int duration;
            if(mediaDurations==null||!mediaDurations.TryGetValue(row,out duration))continue;
            int original=0;foreach(int d in Durations[row])original+=d;
            int cumulative=0,previous=0;
            for(int i=0;i<Timeline[row].Length;i++){cumulative+=Durations[row][i];int end=(int)Math.Round((double)cumulative*duration/original);Timeline[row][i]=Math.Max(1,end-previous);previous=end;}
        }
        ClickDelay=Math.Max(100,clickDelay);Play(0,false,0);
    }
    public void Touch(long now){LastTouch=now;}
    public void Play(int row,bool once,long now) {
        if(row<0||row>=Counts.Length||row==9||row==10)throw new ArgumentOutOfRangeException("row");
        ExitTarget=-1;PlaybackRate=1;Animation=row;Frame=0;look=-1;Row=row;Column=0;SingleAction=once||row==25;Sleeping=row==12;
        PlaybackId++;PlaybackStartMilliseconds=0;MediaWaiting=false;NaturalIdleTransition=false;NaturalReadingTransition=false;
        if(row!=15&&row!=16)previewFlight=false;
        PendingClick=0;nextFrame=now+Timeline[row][0];deadlineReset=now;holdUntil=0;
        if(Paused)pauseRemaining=Timeline[row][0];
        if(Hidden)hiddenRemaining=Timeline[row][0];
    }
    // Enter an existing clip at a retained moving pose. The controller owns
    // this offset so renderer startup and natural completion use one clock.
    public void PlayFrom(int row,bool once,long now,double milliseconds) {
        Play(row,once,now);PlaybackStartMilliseconds=Math.Max(0,Math.Min(TotalDuration(row)-1,milliseconds));StartVisualPlayback(now);
    }
    // A decoded pose must be available before its time is consumed. This is a
    // visual hold only: pointer gestures and pending clicks can still replace it.
    public void SetMediaWaiting(bool value,long now) {
        if(value==MediaWaiting)return;
        if(value){mediaRemaining=Math.Max(0,Remaining(now));MediaWaiting=true;}
        else {
            MediaWaiting=false;nextFrame=now+mediaRemaining;deadlineReset=now;
            if(Paused)pauseRemaining=mediaRemaining;
            if(Hidden)hiddenRemaining=mediaRemaining;
        }
    }
    public void StartVisualPlayback(long now) {
        Frame=0;Row=Animation;Column=0;
        double offset=PlaybackStartMilliseconds;
        while(Frame+1<Timeline[Animation].Length&&offset>=Timeline[Animation][Frame]){offset-=Timeline[Animation][Frame];Frame++;}
        Column=Frame;long remaining=Math.Max(1,(long)Math.Ceiling((Timeline[Animation][Frame]-offset)/PlaybackRate));
        nextFrame=now+remaining;deadlineReset=now;
        if(MediaWaiting)mediaRemaining=remaining;
        if(Paused)pauseRemaining=remaining;
        if(Hidden)hiddenRemaining=remaining;
    }
    public void Select(int row,long now){CancelPointer(now);Touch(now);if(row==15){Play(16,true,now);previewFlight=true;}else Play(row,row!=0&&row!=12&&row!=18&&row!=19&&row!=28,now);}
    public void Wake(long now){CancelPointer(now);Touch(now);if(Sleeping)Play(13,true,now);}
    public void BeginPress(bool head,int clickCount,long now) {
        Touch(now);PendingClick=0;clickAction=4;
        wokeOnPress=Sleeping||(Animation==13&&SingleAction);
        if(Sleeping)Play(13,true,now);
#if CLICK_BOUNCE
        bool third=ContinuingTaps(now)&&tapRun>=2;
        retainClickPose=ClickReaction||third;doublePress=false;
        if(wokeOnPress)ResetTapRun();
        if(clickCount>=2&&!wokeOnPress&&!retainClickPose){doublePress=true;doublePressStarted=now;Pressed=false;Dragged=false;Petting=false;Play(3,true,now);return;}
#else
        if(clickCount>=2&&!wokeOnPress){Pressed=false;Dragged=false;Petting=false;Play(3,true,now);return;}
#endif
        Pressed=true;Dragged=false;Petting=false;headPress=head&&!wokeOnPress;pressStarted=now;
    }
    public void Drag(int facing,long now) {
        if(!Pressed)return;
#if CLICK_BOUNCE
        ResetTapRun();
#endif
        PendingClick=0;Petting=false;
        if(!Dragged||Animation!=14)Play(14,false,now);
        Dragged=true;
    }
    public void EndPress(long now) {
#if CLICK_BOUNCE
        if(doublePress){doublePress=false;if(now-doublePressStarted<=350)CompleteTap(now);else ResetTapRun();return;}
#endif
        if(!Pressed)return;
        bool moved=Dragged,patted=Petting;Pressed=false;Dragged=false;Petting=false;Touch(now);
        if(moved){Play(0,false,now);holdUntil=now+650;}
        else if(patted){SingleAction=true;holdUntil=now+800;}
        else if(wokeOnPress){Play(13,true,now);}
        else {
#if CLICK_BOUNCE
            if(now-pressStarted<=350){
                CompleteTap(now);
                if(ClickReaction)return;
                if(tapRun>=3){Play(27,true,now);tapRun=0;return;}
            }else ResetTapRun();
            if(retainClickPose&&SingleAction)return;
#endif
            Play(0,false,now);PendingClick=now+ClickDelay;
        }
    }
    public void CancelPointer(long now) {
#if CLICK_BOUNCE
        ResetTapRun();
#endif
        PendingClick=0;
        if(Pressed){Pressed=false;Dragged=false;Petting=false;Play(0,false,now);}
    }
    public void SetPaused(bool value,long now) {
        if(value==Paused)return;
        CancelPointer(now);
        if(value){pauseRemaining=Math.Max(1,Remaining(now));Paused=true;}
        else {Paused=false;nextFrame=now+pauseRemaining;deadlineReset=now;if(Hidden)hiddenRemaining=pauseRemaining;if(MediaWaiting)mediaRemaining=pauseRemaining;Touch(now);}
    }
    public void SetMenu(bool value,long now) {
        if(value==MenuOpen)return;
        if(value){CancelPointer(now);menuStarted=now;MenuOpen=true;}
        else {MenuOpen=false;nextFrame+=now-Math.Max(menuStarted,deadlineReset);Touch(now);}
    }
    public void SuspendHidden(long now){if(Hidden)return;CancelPointer(now);hiddenRemaining=Math.Max(1,Remaining(now));Hidden=true;}
    public void ResumeAfterHidden(long now) {
        CancelPointer(now);Touch(now);nextFrame=now+(Hidden?(Paused?pauseRemaining:hiddenRemaining):Timeline[Animation][Frame]);Hidden=false;deadlineReset=now;
        if(Sleeping)Play(13,true,now);
    }
    public static int Direction(double dx,double dy){return ((int)Math.Round((Math.Atan2(dy,dx)*180/Math.PI+450)%360/22.5))%16;}
    public static int StableDirection(double angle,int previous) {
        int direction=(int)Math.Round(angle/22.5)%16;
        if(previous>=0&&direction!=previous){double d=Math.Abs(angle-previous*22.5);d=Math.Min(d,360-d);if(d<15)return previous;}
        return direction;
    }
    public void Tick(long now,double? gazeAngle,bool follow,bool pointerQuietForNap) {
        if(Paused||MenuOpen||Hidden)return;
        if(PendingClick>0){if(now<PendingClick)return;PendingClick=0;Play(clickAction,true,now);}
        if(Pressed) {
            if(!Dragged&&headPress&&!Petting&&now-pressStarted>=550){Petting=true;
#if CLICK_BOUNCE
                ResetTapRun();
#endif
                Play(11,false,now);}
            if(Dragged||Petting)Advance(now);
#if CLICK_BOUNCE
            else if(retainClickPose)Advance(now);
#endif
            return;
        }
        if(MediaWaiting)return;
        if(Sleeping||SingleAction||Animation==15||Animation>=16){Advance(now);return;}
        if(AutoNap&&Animation==0&&PendingClick==0&&pointerQuietForNap&&now-LastTouch>=180000){Play(12,false,now);return;}
        if(follow&&gazeAngle.HasValue&&now>=holdUntil){look=StableDirection(gazeAngle.Value,look);Row=9+look/8;Column=look%8;return;}
        if(look>=0){look=-1;Play(0,false,now);}
        Advance(now);
    }
    void Advance(long now) {
        if(MediaWaiting)return;
        int safety=0;
        while(now>=nextFrame&&safety++<24) {
            Frame++;
            if(Frame>=Counts[Animation]) {
                if(RecoveringPose){int target=ExitTarget;bool once=exitOnce;Play(target,once,now);if(target==0){NaturalIdleTransition=true;holdUntil=now+800;}return;}
#if TABLET_PHASES
                if(Animation==29){Play(28,false,now);return;}
#endif
                if(Animation==25){Play(19,false,now);NaturalReadingTransition=true;return;}
                if(Animation==16){Play(15,previewFlight,now);return;}
                if(Animation==15&&SingleAction){Play(17,true,now);return;}
                if(SingleAction){Play(0,false,now);NaturalIdleTransition=true;holdUntil=now+800;return;}
                Frame=0;
            }
            Row=Animation;Column=Frame;nextFrame+=(long)Math.Ceiling(Timeline[Animation][Frame]/PlaybackRate);
        }
        if(safety>=24)nextFrame=now+Timeline[Animation][Frame];
    }
    public static Point Clamp(Point point,Size size,Rect area) {
        double x=size.Width<=area.Width?Math.Max(area.Left,Math.Min(area.Right-size.Width,point.X)):area.Left+(area.Width-size.Width)/2;
        double y=size.Height<=area.Height?Math.Max(area.Top,Math.Min(area.Bottom-size.Height,point.Y)):area.Top+(area.Height-size.Height)/2;
        return new Point(x,y);
    }
}
}
