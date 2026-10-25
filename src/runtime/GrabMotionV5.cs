using System;
using System.Windows;
namespace WhiteDragon {
// Input is expressed in the 384px cell coordinate system, independent of DPI
// and displayed mascot size. A damped spring follows filtered cursor velocity.
public sealed class GrabMotion {
#if STYLE_V10
    public const double PivotX=.5,PivotY=108.0/360,Padding=.2;
#else
    public const double PivotX=240.0/384,PivotY=174.0/416,Padding=128.0/384;
#endif
    public bool Active{get;private set;}
    public double Angle{get;private set;}
    public double Stretch{get;private set;}
    public bool Landing{get{return landingStart>=0;}}
    double angleVelocity,vertical,verticalVelocity,vx,vy,lastX,lastY,releaseAngle;
    long lastTime,landingStart=-1;
    public GrabMotion(){Reset();}
    static double Bound(double v,double m){return Math.Max(-m,Math.Min(m,v));}
    public void Reset(){Active=false;Angle=0;Stretch=1;angleVelocity=vertical=verticalVelocity=vx=vy=0;landingStart=-1;}
    public void Begin(long now,double x,double y){Reset();Active=true;lastTime=now;lastX=x;lastY=y;}
    public void End(long now){
#if STYLE_V10
        releaseAngle=Angle;Active=false;Stretch=1;angleVelocity=verticalVelocity=vx=vy=0;lastTime=now;landingStart=now;
#else
        Reset();lastTime=now;landingStart=now;
#endif
    }
    public void Step(long now,double x,double y){
        if(!Active){
            if(landingStart>=0){double t=(now-landingStart)/240.0;
#if STYLE_V10
                t=Math.Max(0,Math.Min(1,t));Angle=releaseAngle*(1-t*t*(3-2*t));Stretch=1;if(t>=1)landingStart=-1;
#else
                if(t>=1){landingStart=-1;Stretch=1;}else Stretch=1-.035*Math.Sin(Math.PI*Math.Max(0,t));
#endif
            }
            return;
        }
        double elapsed=(now-lastTime)/1000.0;if(elapsed<=0)return;
        if(elapsed>.5){lastX=x;lastY=y;lastTime=now;vx=vy=angleVelocity=verticalVelocity=0;return;}
        double dt=Math.Min(.12,elapsed),smooth=1-Math.Exp(-16*dt);
        vx+=(Bound((x-lastX)/elapsed,3000)-vx)*smooth;
        vy+=(Bound((y-lastY)/elapsed,3000)-vy)*smooth;
        lastX=x;lastY=y;lastTime=now;
        double target=Bound(vx*.025,22),verticalTarget=Bound(-vy*.012,11);
        int steps=(int)Math.Ceiling(dt/.008);double h=dt/steps;
        for(int i=0;i<steps;i++){
            angleVelocity+=(80*(target-Angle)-11*angleVelocity)*h;Angle+=angleVelocity*h;
            verticalVelocity+=(110*(verticalTarget-vertical)-17*verticalVelocity)*h;vertical+=verticalVelocity*h;
        }
        Angle=Bound(Angle,27);vertical=Bound(vertical,13);
#if STYLE_V10
        // A hanging body swings around its grip; it does not inflate or stretch.
        Stretch=1;
#else
        Stretch=1+vertical/300;
#endif
    }
    public static Point Transform(Point p,double angle,double stretch,Point pivot){
        double rad=angle*Math.PI/180,dx=p.X-pivot.X,dy=(p.Y-pivot.Y)*stretch;
        return new Point(pivot.X+dx*Math.Cos(rad)-dy*Math.Sin(rad),pivot.Y+dx*Math.Sin(rad)+dy*Math.Cos(rad));
    }
}
}
