using System;
using System.Collections.Generic;
using System.Windows;
namespace WhiteDragon {
public sealed class ThrowGesture {
    sealed class Sample {public long T;public double X,Y;public Sample(long t,double x,double y){T=t;X=x;Y=y;}}
    readonly List<Sample> samples=new List<Sample>();
    long lastMotion;double lastX,lastY;
    public const double SpeedThreshold=450;
    public void Begin(long now,double x,double y){samples.Clear();lastMotion=now;lastX=x;lastY=y;Add(now,x,y);}
    public void Add(long now,double x,double y){
        if(samples.Count>0&&now<samples[samples.Count-1].T)return;
        if(Math.Abs(x-lastX)+Math.Abs(y-lastY)>=1){lastMotion=now;lastX=x;lastY=y;}
        if(samples.Count>0&&now==samples[samples.Count-1].T){samples[samples.Count-1]=new Sample(now,x,y);return;}
        samples.Add(new Sample(now,x,y));while(samples.Count>2&&samples[1].T<now-140)samples.RemoveAt(0);
    }
    public bool TryRelease(long now,out Vector velocity){
        velocity=new Vector();if(samples.Count==0)return false;
        Sample last=samples[samples.Count-1];Add(now,last.X,last.Y);
        if(now-lastMotion>90||samples.Count<3)return false;
        Sample first=samples[0];last=samples[samples.Count-1];double duration=(last.T-first.T)/1000.0;
        if(duration<.04||duration>.26)return false;
        double dx=last.X-first.X,dy=last.Y-first.Y,distance=Math.Sqrt(dx*dx+dy*dy),path=0;
        for(int i=1;i<samples.Count;i++){double ax=samples[i].X-samples[i-1].X,ay=samples[i].Y-samples[i-1].Y;path+=Math.Sqrt(ax*ax+ay*ay);}
        if(distance<28||path==0||distance/path<.72)return false;
        double meanT=0,meanX=0,meanY=0;
        foreach(Sample s in samples){meanT+=(s.T-first.T)/1000.0;meanX+=s.X;meanY+=s.Y;}
        meanT/=samples.Count;meanX/=samples.Count;meanY/=samples.Count;
        double denominator=0,nx=0,ny=0;
        foreach(Sample s in samples){double t=(s.T-first.T)/1000.0-meanT;denominator+=t*t;nx+=t*(s.X-meanX);ny+=t*(s.Y-meanY);}
        if(denominator<=0)return false;velocity=new Vector(nx/denominator,ny/denominator);
        if(velocity.Length<SpeedThreshold){velocity=new Vector();return false;}return true;
    }
}
public sealed class GentleFlight {
    public bool Active{get;private set;}public bool Paused{get;private set;}
    public double X{get;private set;}public double Y{get;private set;}public double Angle{get;private set;}
    public double TargetY{get;private set;}public double Duration{get;private set;}
    public double Floor{get;private set;}
    public double VelocityX{get;private set;}public double VelocityY{get;private set;}
    public string Phase{get;private set;}
    const double Dt=1.0/240,Gravity=780,HorizontalDrag=1.55,TerminalSpeed=125,LandingHeight=60;
    sealed class Pose {public double X,Y,VX,VY;public Pose(double x,double y,double vx,double vy){X=x;Y=y;VX=vx;VY=vy;}}
    readonly List<Pose> path=new List<Pose>();
    long started,pauseStarted,pausedTotal;double minX,maxX,ceiling,releaseAngle;
    public void Cancel(){Active=false;Paused=false;Angle=0;VelocityX=VelocityY=0;Phase="fixed";}
    static double Bound(double v,double a,double b){return Math.Max(a,Math.Min(b,v));}
    public void Begin(long now,Point origin,Vector velocity,Rect area,double width,double footOffset){
        Begin(now,origin,velocity,area,width,footOffset,0);
    }
    public void Begin(long now,Point origin,Vector velocity,Rect area,double width,double footOffset,double initialAngle){
        minX=area.Left;maxX=area.Right-width;
        if(maxX<minX)minX=maxX=area.Left+(area.Width-width)/2;
        Floor=area.Bottom;TargetY=area.Bottom-footOffset;ceiling=Math.Min(area.Top,TargetY);
        // Preserve the exact release position and swing. Screen constraints
        // act during motion instead of teleporting the pet at release.
        X=origin.X;Y=origin.Y;releaseAngle=initialAngle;
        double x=X,y=Y,vx=Bound(velocity.X*.62,-1000,1000),vy=Bound(velocity.Y*.60,-700,480);
        if(TargetY-y<35&&Math.Abs(velocity.Y)<Math.Abs(velocity.X)*.25)vy=Math.Min(vy,-160);
        path.Clear();path.Add(new Pose(x,y,vx,vy));
        bool landed=false;
        for(int n=1;n<=240*60;n++){
            double t=(n-1)*Dt,height=TargetY-y;
            double ax=-HorizontalDrag*vx;
            if(x<minX)ax+=50*(minX-x)-10*vx;
            if(x>maxX)ax+=50*(maxX-x)-10*vx;
            double ay;
            if(y<ceiling){
                ay=20.25*(ceiling-y)-9*vy;
            }else if((vy>=0&&height<=LandingHeight)||height<0){
                // Wing braking becomes a critically damped approach. This
                // makes touchdown slow without a bounce or a position snap.
                const double omega=4.5;ay=omega*omega*height-2*omega*vy;
            }else{
                double wing=Bound((t-.15)/.4,0,1);wing=wing*wing*(3-2*wing);
                double drag=vy<0?.8:(.8+(Gravity/TerminalSpeed-.8)*wing);
                ay=Gravity-drag*vy;
            }
            vx+=ax*Dt;vy+=ay*Dt;x+=vx*Dt;y+=vy*Dt;
            if(x<minX&&path[path.Count-1].X>=minX){x=minX;vx=Math.Abs(vx)*.12;}
            if(x>maxX&&path[path.Count-1].X<=maxX){x=maxX;vx=-Math.Abs(vx)*.12;}
            if(y<ceiling&&path[path.Count-1].Y>=ceiling){y=ceiling;vy=Math.Abs(vy)*.12;}
            // A hard downward throw can use its stopping distance immediately.
            // The surface is solid even when it is released very close to it.
            if(y>TargetY&&path[path.Count-1].Y<=TargetY){y=TargetY;vy=0;}
            if(n*Dt>=.45&&Math.Abs(TargetY-y)<=.30&&Math.Abs(vy)<2&&Math.Abs(vx)<12&&x>=minX-.05&&x<=maxX+.05){
                y=TargetY;vy=vx=0;x=Bound(x,minX,maxX);landed=true;
            }
            path.Add(new Pose(x,y,vx,vy));if(landed)break;
        }
        if(!landed)throw new InvalidOperationException("Flight did not settle within its physical bounds");
        Duration=(path.Count-1)*Dt;started=now;pausedTotal=0;pauseStarted=0;Paused=false;Active=true;Angle=initialAngle;VelocityX=path[0].VX;VelocityY=path[0].VY;Phase=VelocityY<0?"throw":"descent";
    }
    public void SetPaused(bool value,long now){
        if(!Active||value==Paused)return;
        if(value){pauseStarted=now;Paused=true;}else{pausedTotal+=Math.Max(0,now-pauseStarted);Paused=false;}
    }
    public void Step(long now){
        if(!Active||Paused)return;
        double t=Math.Max(0,(now-started-pausedTotal)/1000.0),sample=Math.Min(path.Count-1,t/Dt);int i=(int)sample,j=Math.Min(path.Count-1,i+1);double u=sample-i;
        Pose a=path[i],b=path[j];X=a.X+(b.X-a.X)*u;Y=a.Y+(b.Y-a.Y)*u;VelocityX=a.VX+(b.VX-a.VX)*u;VelocityY=a.VY+(b.VY-a.VY)*u;
        double decay=Math.Exp(-5*t),taper=Bound((Duration-t)/.45,0,1);taper=taper*taper*(3-2*taper);
        Angle=(releaseAngle*decay+Bound(VelocityX/85,-10,10)*(1-decay))*taper;
        Phase=VelocityY< -2?"throw":TargetY-Y<LandingHeight?"landing":"descent";
        if(t>=Duration){Y=TargetY;Angle=0;VelocityX=VelocityY=0;Active=false;Phase="landed";}
    }
}
}
