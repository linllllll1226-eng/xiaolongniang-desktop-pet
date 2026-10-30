using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;

namespace WhiteDragon {
// Only window geometry and identifiers are requested. No title or client content.
public sealed class PerchSurface {
    public IntPtr Handle;
    public uint ProcessId;
    public Rect Bounds;
}
public static class PerchWindows {
    [StructLayout(LayoutKind.Sequential)] struct NativeRect {public int Left,Top,Right,Bottom;}
    delegate bool EnumWindow(IntPtr h,IntPtr parameter);
    [DllImport("user32.dll")] static extern bool EnumWindows(EnumWindow callback,IntPtr parameter);
    [DllImport("user32.dll")] static extern bool IsWindow(IntPtr h);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] static extern bool IsIconic(IntPtr h);
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h,out NativeRect rect);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h,out uint pid);
    [DllImport("user32.dll")] static extern int GetWindowLong(IntPtr h,int index);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern int GetClassName(IntPtr h,StringBuilder text,int count);
    [DllImport("dwmapi.dll",EntryPoint="DwmGetWindowAttribute")] static extern int DwmBounds(IntPtr h,int attribute,out NativeRect value,int size);
    [DllImport("dwmapi.dll",EntryPoint="DwmGetWindowAttribute")] static extern int DwmCloaked(IntPtr h,int attribute,out int value,int size);
    static readonly uint OwnProcess=(uint)Process.GetCurrentProcess().Id;
    public static PerchSurface Read(IntPtr handle) {
        if(handle==IntPtr.Zero||!IsWindow(handle)||!IsWindowVisible(handle)||IsIconic(handle))return null;
        uint pid;GetWindowThreadProcessId(handle,out pid);
        if(pid==0||pid==OwnProcess||(GetWindowLong(handle,-20)&0x80)!=0)return null;
        var name=new StringBuilder(128);GetClassName(handle,name,name.Capacity);
        string cls=name.ToString();
        if(cls=="Progman"||cls=="WorkerW"||cls=="Shell_TrayWnd"||cls=="Shell_SecondaryTrayWnd"||cls=="DV2ControlHost")return null;
        NativeRect bounds;if(!GetWindowRect(handle,out bounds))return null;
        try {
            int cloaked;if(DwmCloaked(handle,14,out cloaked,4)==0&&cloaked!=0)return null;
            NativeRect visible;if(DwmBounds(handle,9,out visible,16)==0)bounds=visible;
        }catch(DllNotFoundException){}catch(EntryPointNotFoundException){}
        if(bounds.Right-bounds.Left<120||bounds.Bottom-bounds.Top<80)return null;
        return new PerchSurface{Handle=handle,ProcessId=pid,Bounds=new Rect(bounds.Left,bounds.Top,bounds.Right-bounds.Left,bounds.Bottom-bounds.Top)};
    }
    public static List<PerchSurface> Visible() {
        var result=new List<PerchSurface>();
        EnumWindows(delegate(IntPtr h,IntPtr unused){var surface=Read(h);if(surface!=null)result.Add(surface);return true;},IntPtr.Zero);
        return result;
    }
}
public sealed class WindowPerch {
    // Source coordinates measured on the retained, fully seated part of action 22.
    public const double SeatY=306,HeadY=83;
    // One closed-to-open swing, measured on the seated source. The wider
    // 1900..3100 interval contains several separate openings and repeats them
    // again on reversal. Ease this single swing out and back over 2.4 seconds.
    public const double PoseStartMilliseconds=2200,PoseEndMilliseconds=2500;
    public const double LegSwingCycleMilliseconds=2400;
    public bool Attached{get;private set;}
    public IntPtr Handle{get;private set;}
    public uint ProcessId{get;private set;}
    public double Fraction{get;private set;}
    public double PoseMilliseconds{get;private set;}
    public int Direction{get;private set;}
    public Point Origin{get;private set;}
    public bool Approaching{get{return Attached&&approachElapsed<ApproachDuration;}}
    public double ApproachDuration{get;private set;}
    Point approachFrom;
    double approachElapsed,poseElapsed;
    long lastTick;
    public static bool TryOrigin(Rect bounds,Rect area,double width,double fraction,out Point origin) {
        origin=new Point();
        if(bounds.IsEmpty||area.IsEmpty||width<160||width>480||bounds.Width<Math.Max(120,width*.40))return false;
        double margin=width*.19;
        double center=bounds.Left+bounds.Width*Math.Max(0,Math.Min(1,fraction));
        center=Math.Max(bounds.Left+margin,Math.Min(bounds.Right-margin,center));
        if(center-margin<area.Left||center+margin>area.Right)return false;
        // A maximized window has no room above its edge for this character.
        if(bounds.Top-(SeatY-HeadY)*width/640<area.Top+4||bounds.Top+54*width/640>area.Bottom)return false;
        origin=new Point(center-width*.5,bounds.Top-SeatY*width/640);
        return true;
    }
    public void Attach(PerchSurface surface,double fraction,Point from,long now) {
        Attached=true;Handle=surface.Handle;ProcessId=surface.ProcessId;Fraction=Math.Max(0,Math.Min(1,fraction));
        approachFrom=Origin=from;approachElapsed=poseElapsed=0;PoseMilliseconds=PoseStartMilliseconds;Direction=1;lastTick=now;
        ApproachDuration=380;
    }
    public void SetApproachDistance(double distance){ApproachDuration=distance<140?380:Math.Min(2400,Math.Max(900,distance/600*1000));}
    public void BeginSitting(){poseElapsed=0;PoseMilliseconds=PoseStartMilliseconds;Direction=1;}
    public void Step(long now,Point destination,bool frozen) {
        double elapsed=Math.Max(0,Math.Min(80,now-lastTick));lastTick=now;
        if(!frozen){approachElapsed+=elapsed;poseElapsed+=elapsed;}
        double u=Math.Min(1,approachElapsed/ApproachDuration);u=u*u*(3-2*u);
        Origin=new Point(approachFrom.X+(destination.X-approachFrom.X)*u,approachFrom.Y+(destination.Y-approachFrom.Y)*u);
        // One opening and one closing per cycle, with no abrupt turnaround.
        double phase=poseElapsed%LegSwingCycleMilliseconds/LegSwingCycleMilliseconds;
        Direction=phase<.5?1:-1;
        double eased=(1-Math.Cos(phase*2*Math.PI))*.5;
        PoseMilliseconds=PoseStartMilliseconds+(PoseEndMilliseconds-PoseStartMilliseconds)*eased;
    }
    public void Detach(){Attached=false;Handle=IntPtr.Zero;ProcessId=0;}
}
}
