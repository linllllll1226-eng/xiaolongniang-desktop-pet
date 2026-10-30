using System;
namespace WhiteDragon {
// Preserve every source pose and the final reading tail, while allowing
// more time for hand contact without a frozen hold.
public static class ReadingExchangeTiming {
 static readonly double[] live={0,600,1150,1775,2600,3350,4200};
 static readonly double[] source={0,750,1350,1950,2500,3250,4200};
 static readonly double[] slopes=BuildSlopes();
 static double[] BuildSlopes(){var m=new double[live.Length];m[0]=m[m.Length-1]=1;for(int i=1;i<m.Length-1;i++){double a=(source[i]-source[i-1])/(live[i]-live[i-1]),b=(source[i+1]-source[i])/(live[i+1]-live[i]);m[i]=2*a*b/(a+b);}return m;}
 public static double SourceTime(double time){
  if(Double.IsNaN(time)||time<=0)return 0;if(time>=4200)return time;
  int i=0;while(i+1<live.Length-1&&time>live[i+1])i++;
  double h=live[i+1]-live[i],u=(time-live[i])/h,u2=u*u,u3=u2*u;
  return (2*u3-3*u2+1)*source[i]+(u3-2*u2+u)*h*slopes[i]+(-2*u3+3*u2)*source[i+1]+(u3-u2)*h*slopes[i+1];
 }
}
// Counts actual, unfrozen reading time. It never schedules by wall-clock sleep.
public sealed class ReadingLibrary {
 readonly Random random;long last=-1;bool wasReading;
 public double RemainingMilliseconds{get;private set;}public double LastIntervalMilliseconds{get;private set;}
 public bool InExchange{get;private set;}public int Started{get;private set;}public int Completed{get;private set;}public int Canceled{get;private set;}
 public bool Due{get{return !InExchange&&RemainingMilliseconds<=0;}}
 public ReadingLibrary(int seed){random=seed<0?new Random():new Random(seed);ResetInterval();}
 void ResetInterval(){RemainingMilliseconds=LastIntervalMilliseconds=120000+random.NextDouble()*180000;wasReading=false;}
 public void Update(long now,bool reading,bool frozen){
  long delta=last<0?0:Math.Max(0,now-last);last=now;
  if(frozen||!reading||InExchange){wasReading=false;return;}
  if(wasReading&&delta<=150)RemainingMilliseconds-=delta;
  wasReading=true;
 }
 public void Begin(){if(InExchange)return;InExchange=true;Started++;wasReading=false;}
 public void Finish(bool completed){if(!InExchange)return;InExchange=false;if(completed)Completed++;else Canceled++;ResetInterval();}
 public void TaskEnded(){if(InExchange)Finish(false);else ResetInterval();}
}
}
