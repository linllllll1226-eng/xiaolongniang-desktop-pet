using System;
using System.Collections.Generic;
namespace WhiteDragon {
// Schedules small activities from eligible, visible idle time. Animation
// completion remains owned by PetBehavior and its decoded media timeline.
public sealed class DailyRoutine {
 readonly Random random;long last=-1;bool wasEligible,wasRunning,night;int lastRow=-1;
 public bool Enabled{get;private set;}
 public int Rhythm{get;private set;}
 public int ActiveRow{get;private set;}
 public double RemainingMilliseconds{get;private set;}
 public double ReadingRemainingMilliseconds{get;private set;}
 public double LastIntervalMilliseconds{get;private set;}
 public bool Requested{get;private set;}
 public int Started{get;private set;}public int Completed{get;private set;}public int Interrupted{get;private set;}
 public bool Night{get{return night;}}
 public bool Due{get{return Enabled&&ActiveRow<0&&RemainingMilliseconds<=0;}}
 public DailyRoutine(int seed){random=seed<0?new Random():new Random(seed);ActiveRow=-1;Rhythm=1;Plan(12);}
 public static bool IsNight(int hour){return hour>=22||hour<8;}
 public static double MinimumInterval(int hour,int rhythm){bool n=IsNight(hour);return (rhythm==0?(n?360:180):rhythm==2?(n?120:45):(n?240:90))*1000.0;}
 double MaximumInterval(int hour){return (Rhythm==0?(IsNight(hour)?720:360):Rhythm==2?(IsNight(hour)?240:90):(IsNight(hour)?480:180))*1000.0;}
 void Plan(int hour){night=IsNight(hour);double min=MinimumInterval(hour,Rhythm);RemainingMilliseconds=LastIntervalMilliseconds=min+random.NextDouble()*(MaximumInterval(hour)-min);ResetClock();}
 public void Configure(bool enabled,int rhythm,int hour){Enabled=enabled;Rhythm=Math.Max(0,Math.Min(2,rhythm));if(!enabled)Requested=false;Plan(hour);}
 public void SetEnabled(bool enabled,int hour){Enabled=enabled;if(!enabled)Requested=false;Plan(hour);}
 public void SetRhythm(int rhythm,int hour){Rhythm=Math.Max(0,Math.Min(2,rhythm));Plan(hour);}
 public void ResetClock(){last=-1;wasEligible=wasRunning=false;}
 public void Update(long now,int hour,bool eligible,bool frozen){
  long delta=last<0?0:Math.Max(0,now-last);last=now;if(delta>1000)delta=0;
  bool n=IsNight(hour);if(n!=night){night=n;if(ActiveRow<0&&!Requested)RemainingMilliseconds=Math.Max(RemainingMilliseconds,MinimumInterval(hour,Rhythm));}
  bool running=ActiveRow>=0&&!frozen;
  if(running&&wasRunning&&ActiveRow==19)ReadingRemainingMilliseconds=Math.Max(0,ReadingRemainingMilliseconds-delta);
  bool idle=Enabled&&ActiveRow<0&&eligible&&!frozen;
  if(idle&&wasEligible)RemainingMilliseconds=Math.Max(0,RemainingMilliseconds-delta);
  wasEligible=idle;wasRunning=running;
 }
 public void RequestNow(){if(!Enabled||ActiveRow>=0)return;Requested=true;RemainingMilliseconds=0;ResetClock();}
 public int Pick(int hour,bool hasSkirt){
  var choices=new List<int>();bool n=IsNight(hour);
  int[] rows=hasSkirt?new int[]{13,22,23,19,26}:new int[]{13,22,23,19};
  int[] weights=n?new int[]{2,1,5,1,2}:new int[]{3,3,1,3,2};
  for(int i=0;i<rows.Length;i++)if(rows[i]!=lastRow)for(int j=0;j<weights[i];j++)choices.Add(rows[i]);
  return choices[random.Next(choices.Count)];
 }
 public void Begin(int row,int hour){if(ActiveRow>=0)throw new InvalidOperationException("A daily activity is already active");ActiveRow=row;lastRow=row;Requested=false;Started++;ReadingRemainingMilliseconds=row==19?(IsNight(hour)?18000:30000)+random.NextDouble()*(IsNight(hour)?12000:25000):0;ResetClock();}
 public void Finish(int hour){if(ActiveRow<0)return;ActiveRow=-1;ReadingRemainingMilliseconds=0;Requested=false;Completed++;Plan(hour);}
 public void Cancel(int hour){if(ActiveRow>=0)Interrupted++;ActiveRow=-1;ReadingRemainingMilliseconds=0;Requested=false;Plan(hour);}
 public void Defer(int hour,int manualRow){if(manualRow==13||manualRow==22||manualRow==23||manualRow==19||manualRow==26)lastRow=manualRow;Cancel(hour);}
}
}
