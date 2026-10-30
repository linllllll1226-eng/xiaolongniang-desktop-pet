using System;
namespace WhiteDragon {
// A user-started timer, independent from decoded animation time. A separate
// low-frequency clock keeps it running while the mascot window is hidden.
public sealed class FocusSession {
 long last=-1;
 public bool Active{get;private set;}public bool Paused{get;private set;}public bool Suspended{get;private set;}
 public bool IsBreak{get;private set;}public bool Finished{get;private set;}public bool NoticePending{get;private set;}
 public double RemainingMilliseconds{get;private set;}public int DurationSeconds{get;private set;}
 public int Generation{get;private set;}public int Completed{get;private set;}public int IgnoredLongGaps{get;private set;}
 public bool Running{get{return Active&&!Paused&&!Suspended;}}
 public string TimeText{get{int s=(int)Math.Ceiling(RemainingMilliseconds/1000);return (s/60).ToString("00")+":"+(s%60).ToString("00");}}
 public double Progress{get{return DurationSeconds<=0?0:Math.Max(0,Math.Min(1,1-RemainingMilliseconds/(DurationSeconds*1000.0)));}}
 public void Start(int seconds,bool rest,long now){
  if(seconds<1||seconds>10800)throw new ArgumentOutOfRangeException("seconds");
  Active=true;Paused=Suspended=Finished=NoticePending=false;IsBreak=rest;DurationSeconds=seconds;RemainingMilliseconds=seconds*1000.0;last=now;Generation++;
 }
 public void Update(long now){
  long delta=last<0?0:Math.Max(0,now-last);last=Math.Max(last,now);
  // Power notifications normally handle suspend/resume. This guard also
  // avoids silently spending a whole session after a missed notification.
  if(delta>10000){if(Running)IgnoredLongGaps++;return;}
  if(!Running)return;RemainingMilliseconds=Math.Max(0,RemainingMilliseconds-delta);
  if(RemainingMilliseconds<=0){Active=false;Paused=false;Finished=true;NoticePending=true;Completed++;}
 }
 public void SetPaused(bool value,long now){Update(now);if(Active)Paused=value;last=now;}
 public void SetSuspended(bool value,long now){if(value)Update(now);Suspended=value;last=now;}
 public void ConsumeNotice(){NoticePending=false;}
 public void End(long now){Active=Paused=Suspended=Finished=NoticePending=false;RemainingMilliseconds=0;DurationSeconds=0;last=now;Generation++;}
}
}
