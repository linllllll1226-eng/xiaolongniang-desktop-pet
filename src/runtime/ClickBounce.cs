using System;
namespace WhiteDragon {
// Bounded time-based spring; affects only the temporary visual rig.
public sealed class ClickBounce {
 double position,velocity;long previous=-1;bool suspended;
 public double Compression{get{return position;}}
 public double ScaleY{get{return 1-position;}}
 public double ScaleX{get{return 1/(1-position);}}
 public bool Active{get{return position!=0||velocity!=0;}}
 public void Reset(){position=velocity=0;previous=-1;suspended=false;}
 public void Punch(long now){Step(now,false);position=Math.Min(.085,position+.008);velocity=Math.Min(1.65,Math.Max(0,velocity)+1.10);previous=now;}
 public void Step(long now,bool frozen){
  if(previous<0){previous=now;return;}long elapsed=now-previous;previous=now;
  if(frozen||suspended){suspended=frozen;return;}if(elapsed<=0)return;
  if(elapsed>300){position=velocity=0;return;}
  const double frequency=23,damping=.38;double decay=frequency*damping,omega=frequency*Math.Sqrt(1-damping*damping),t=elapsed/1000.0;
  double c=Math.Cos(omega*t),s=Math.Sin(omega*t),a=position,b=(velocity+decay*position)/omega,e=Math.Exp(-decay*t);
  position=e*(a*c+b*s);velocity=e*((-a*omega*s+b*omega*c)-decay*(a*c+b*s));
  if(position>.10){position=.10;velocity=Math.Min(0,velocity);}if(position<-.055){position=-.055;velocity=Math.Max(0,velocity);}
  if(Math.Abs(position)<.00035&&Math.Abs(velocity)<.006){position=velocity=0;}
 }
}
}
