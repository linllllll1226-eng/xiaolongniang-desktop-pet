using System;
using System.Collections.Generic;
using System.Windows;
namespace WhiteDragon {
public static class ThrowFlightChecks {
    static void Check(bool value,string name){if(!value)throw new Exception(name);}
    public static Dictionary<string,object> Run(){
        var cases=new List<string>();var g=new ThrowGesture();Vector v;
        g.Begin(0,0,0);for(int i=1;i<=8;i++)g.Add(i*20,i*4,0);Check(!g.TryRelease(165,out v),"slow drag misclassified");cases.Add("slow_drag_release_is_fixed");
        foreach(Vector direction in new Vector[]{new Vector(1,0),new Vector(-1,0),new Vector(0,1),new Vector(0,-1)}){g.Begin(0,0,0);for(int i=1;i<=8;i++)g.Add(i*20,direction.X*i*40,direction.Y*i*40);Check(g.TryRelease(165,out v)&&v.X*direction.X+v.Y*direction.Y>950,"throw direction classification");}cases.Add("fast_release_in_all_four_directions_is_a_throw");
        g.Begin(0,0,0);for(int i=1;i<=8;i++)g.Add(i*20,i*40,0);Check(!g.TryRelease(330,out v),"stopped flick still throws");cases.Add("fast_move_then_hold_before_release_is_fixed");
        g.Begin(0,0,0);for(int i=1;i<=12;i++)g.Add(i*10,i%2==0?2:-2,0);Check(!g.TryRelease(125,out v),"jitter throws");cases.Add("small_cursor_jitter_is_not_a_throw");
        g.Begin(0,0,0);g.Add(1,400,0);Check(!g.TryRelease(2,out v),"single teleport throws");cases.Add("one_event_teleport_is_not_a_throw");
        var f=new GentleFlight();var area=new Rect(0,0,1400,900);double width=288,foot=width*FlightAsset.Foot/384;
        f.Begin(0,new Point(500,70),new Vector(1800,-800),area,width,foot);double duration=f.Duration;f.Step(300);Check(f.Active&&f.Y<f.TargetY-30,"no gradual descent");cases.Add("throw_flies_with_limited_momentum_and_gradual_descent");
        f.Step((long)(duration*1000)+1);Check(!f.Active&&Math.Abs(f.Y+foot-area.Bottom)<1e-9,"taskbar contact");cases.Add("flight_finishes_with_visible_soles_on_taskbar_top");
        double y=f.Y,x=f.X;f.Step(50000);Check(f.X==x&&f.Y==y,"landed state drifts");cases.Add("completed_flight_remains_fixed");
        f.Begin(0,new Point(500,70),new Vector(1800,0),area,width,foot);f.Step(500);x=f.X;y=f.Y;f.SetPaused(true,500);f.Step(10500);Check(f.X==x&&f.Y==y,"paused flight moves");f.SetPaused(false,10500);f.Step(10500);Check(f.X==x&&f.Y==y,"resume flight jumps");f.Step(12000);Check(f.Y>y,"resumed flight remains frozen");cases.Add("pause_and_hidden_time_do_not_advance_or_jump_flight");
        foreach(Rect screen in new Rect[]{new Rect(-1920,0,1920,1040),new Rect(1920,-1000,1080,1920)}){f.Begin(0,new Point(screen.Left+300,screen.Top+30),new Vector(3000,3000),screen,width,foot);for(int i=1;i<=500;i++){f.Step(i*16);Check(f.X>=screen.Left&&f.X+width<=screen.Right&&f.Y+foot<=screen.Bottom+1e-8,"cross monitor flight");}Check(!f.Active&&Math.Abs(f.Y+foot-screen.Bottom)<.01,"wrong monitor floor");}cases.Add("negative_and_portrait_monitors_have_their_own_landing_floor");
        f.Begin(0,new Point(500,70),new Vector(1500,0),area,width,foot);f.Step(500);x=f.X;y=f.Y;f.Cancel();f.Step(10000);Check(!f.Active&&f.X==x&&f.Y==y,"canceled flight moves");cases.Add("grabbing_or_canceling_flight_stops_previous_trajectory");
        var trace=new List<double[]>();f.Begin(0,new Point(420,60),new Vector(1700,-700),area,width,foot);for(int i=1;i<=60;i++){f.Step(i*100);if(i%5==0)trace.Add(new double[]{i*100,f.X,f.Y,f.Angle,f.Active?1:0});}
        foreach(double a in new double[]{-10,10})foreach(Point p in new Point[]{new Point(0,0),new Point(384,0),new Point(384,416),new Point(0,416)}){Point t=GrabMotion.Transform(new Point(192+(p.X-192)*FlightAsset.Scale,402+(p.Y-402)*FlightAsset.Scale),a,1,new Point(192,402));Check(t.X>=-128&&t.X<=512&&t.Y>=-128&&t.Y<=544,"flight visual clipped by window");}cases.Add("flight_wing_expansion_and_roll_fit_inside_transparent_window");
        return new Dictionary<string,object>{{"cases",cases.ToArray()},{"flight_parity_trace",trace},{"throw_threshold_dip_per_second",ThrowGesture.SpeedThreshold}};
    }
}
}
