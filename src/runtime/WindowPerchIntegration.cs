using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using Forms=System.Windows.Forms;

namespace WhiteDragon {
public sealed partial class PetWindow {
    readonly WindowPerch windowPerch=new WindowPerch();
    MenuItem perchCommand,perchLeave,perchToggle;
    string perchDescription="慢慢放到窗口顶边，也可以从这里坐下";
    long perchNextRead;
    PerchSurface perchSurface;
    Point perchDestination;
    int perchActionAfterLanding=-1;
    int perchArrival;
    IntPtr perchForeground;
    void BuildPerchMenu() {
        var parent=MenuEntry("窗边陪伴","\uE737",null);
        perchCommand=MenuEntry("坐到当前窗口上","\uE8A5",delegate{PerchOnForeground();});
        perchLeave=MenuEntry("飞回桌面","\uE80F",delegate{LeaveWindowPerch(clock.ElapsedMilliseconds,true);});
        perchToggle=MenuToggle("拖到窗口顶边时坐下","\uE73A",preferences.WindowPerch,delegate(bool enabled){preferences.WindowPerch=enabled;if(!enabled)LeaveWindowPerch(clock.ElapsedMilliseconds,true);Save();});
        parent.Items.Add(perchCommand);parent.Items.Add(perchLeave);parent.Items.Add(new Separator());parent.Items.Add(perchToggle);
        parent.SubmenuOpened+=delegate{RefreshPerchMenu();};menu.Items.Add(parent);
    }
    void RefreshPerchMenu() {
        if(perchCommand==null)return;
        perchLeave.IsEnabled=windowPerch.Attached;perchToggle.IsChecked=preferences.WindowPerch;
        perchCommand.ToolTip=perchDescription;
    }
    PerchSurface SurfaceInDips(PerchSurface surface) {
        if(surface==null)return null;
        var source=PresentationSource.FromVisual(this);if(source==null)return null;
        var matrix=source.CompositionTarget.TransformFromDevice;
        return new PerchSurface{Handle=surface.Handle,ProcessId=surface.ProcessId,Bounds=new Rect(matrix.Transform(surface.Bounds.TopLeft),matrix.Transform(surface.Bounds.BottomRight))};
    }
    bool PerchPlacement(PerchSurface surface,double fraction,out Point origin) {
        origin=new Point();if(surface==null)return false;
        return WindowPerch.TryOrigin(surface.Bounds,WorkAreaFor(Forms.Screen.FromHandle(surface.Handle)),bodyWidth,fraction,out origin);
    }
    bool AttachWindowPerch(IntPtr target,double fraction,long now) {
        var surface=SurfaceInDips(PerchWindows.Read(target));Point origin;
        if(!PerchPlacement(surface,fraction,out origin))return false;
#if FOCUS_COMPANION
        CancelFocusOwnership(now,false);
#endif
#if DAILY_ROUTINE
        CancelDailyRoutine(now,false,-1);
#endif
        CancelFlight(now);motion.Reset();behavior.CancelPointer(now);automaticBookChange=false;
        if(readingLibrary.InExchange)readingLibrary.Finish(false);
        // Decode asynchronously; the current frame is retained until seated media is ready.
        smooth.WarmFrom(22,WindowPerch.PoseStartMilliseconds);
        windowPerch.Attach(surface,fraction,new Point(Left+RigPadding,Top+RigPadding),now);
        double distance=(origin-windowPerch.Origin).Length;windowPerch.SetApproachDistance(distance);
        perchSurface=surface;perchDestination=origin;perchNextRead=now+60;
        perchArrival=distance<140?0:1;
        behavior.Play(perchArrival==0?22:16,false,now);bridgeApplied=null;bridgeSnooze=now+1000;
        perchDescription="坐在窗边陪着你 · 可拖走或选择飞回桌面";
        image.ToolTip=perchDescription;RenderFrame();return true;
    }
    void PerchOnForeground() {
        IntPtr target=perchForeground!=IntPtr.Zero?perchForeground:GetForegroundWindow();
        if(!AttachWindowPerch(target,.72,clock.ElapsedMilliseconds)) {
            perchDescription="先选一个普通窗口并向下挪一点，给头顶留出空间";
            image.ToolTip=perchDescription;
            if(tray!=null){tray.BalloonTipTitle="还坐不上去";tray.BalloonTipText=perchDescription;tray.ShowBalloonTip(2600);}
        }
    }
    void RememberPerchForeground(){IntPtr handle=GetForegroundWindow();perchForeground=PerchWindows.Read(handle)!=null?handle:IntPtr.Zero;}
    bool TryPerchNearPointer(long now) {
        if(!preferences.WindowPerch)return false;
        var source=PresentationSource.FromVisual(this);if(source==null)return false;
        Point pointer=PointerDip(grabPointer);
        foreach(var native in PerchWindows.Visible()) {
            var surface=SurfaceInDips(native);
            if(Math.Abs(pointer.Y-surface.Bounds.Top)>30||pointer.X<surface.Bounds.Left+12||pointer.X>surface.Bounds.Right-12)continue;
            double fraction=(pointer.X-surface.Bounds.Left)/surface.Bounds.Width;
            Point origin;if(!PerchPlacement(surface,fraction,out origin))continue;
            return AttachWindowPerch(surface.Handle,fraction,now);
        }
        return false;
    }
    void UpdateWindowPerch(long now) {
        if(!windowPerch.Attached)return;
        if(now>=perchNextRead) {
            perchNextRead=now+60;
            perchSurface=SurfaceInDips(PerchWindows.Read(windowPerch.Handle));
            if(perchSurface==null||perchSurface.ProcessId!=windowPerch.ProcessId||!PerchPlacement(perchSurface,windowPerch.Fraction,out perchDestination)) {
                LeaveWindowPerch(now,true);return;
            }
        }
        bool frozen=behavior.Paused||behavior.MenuOpen||behavior.Hidden||behavior.Pressed||behavior.MediaWaiting;
        windowPerch.Step(now,perchDestination,frozen);
        Left=windowPerch.Origin.X-RigPadding;Top=windowPerch.Origin.Y-RigPadding;
    }
    void LeaveWindowPerch(long now,bool descend) {
        if(!windowPerch.Attached)return;
        windowPerch.Detach();perchSurface=null;perchDescription="慢慢放到窗口顶边，也可以从这里坐下";
        motion.Reset();bridgeApplied=null;bridgeSnooze=now+1000;
        if(descend) {
            Rect area=CurrentWorkArea();
            activeGrip=new Point(.5,FlightAsset.Foot/CH);
            flight.Begin(now,new Point(Left+RigPadding,Top+RigPadding),new Vector(0,0),area,bodyWidth,bodyWidth*FlightAsset.Foot/CW);
            behavior.Play(16,true,now);smooth.Warm(16);releaseKind="perch-descent";
        }
    }
    bool ResizeWindowPerch(double width) {
        if(!windowPerch.Attached)return false;
        bodyWidth=width;SetBodySize();perchNextRead=0;UpdateWindowPerch(clock.ElapsedMilliseconds);Save();RenderFrame();return true;
    }
    void AdvancePerchArrival(long now) {
        if(!windowPerch.Attached||behavior.Pressed||behavior.Paused||behavior.MenuOpen||behavior.Hidden)return;
        if(perchArrival==1&&!windowPerch.Approaching&&behavior.Animation==15){perchArrival=2;StartLandingFold(now);}
        else if(perchArrival==2&&behavior.Animation==0){perchArrival=0;windowPerch.BeginSitting();smooth.WarmDirected(22,WindowPerch.PoseStartMilliseconds,1);behavior.Play(22,false,now);}
    }
    bool DeferPerchedAction(int row) {
        if(!windowPerch.Attached)return false;
        LeaveWindowPerch(clock.ElapsedMilliseconds,true);perchActionAfterLanding=row;RenderFrame();return true;
    }
    void CompletePerchDeparture() {
        if(perchActionAfterLanding<0||flight.Active||pendingWingFold||behavior.Animation!=0||behavior.Paused||behavior.MenuOpen||behavior.Hidden)return;
        int row=perchActionAfterLanding;perchActionAfterLanding=-1;Choose(row);
    }
}
}
