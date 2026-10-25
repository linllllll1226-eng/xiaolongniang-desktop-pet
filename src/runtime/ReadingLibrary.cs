using System;
namespace WhiteDragon {
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
