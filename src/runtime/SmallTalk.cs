using System;
using System.Collections.Generic;
namespace WhiteDragon {
// Local short phrases only; no chat, clipboard, microphone or network input.
public sealed class SmallTalk {
 readonly Random random;long last=-1,lastShown=-180000,pendingUntil,shownAt;bool wasIdle,night;int pendingContext=-1,lastKey=-1;
 static readonly string[,] lines={{"我在这儿呢","你忙你的，我陪着你"},{"今天也一起加油","累了就歇一小会儿"},{"唔，好舒服","再摸一小会儿吧"},{"被你摸得想打盹啦","再一下，就一下"},{"你好呀","看到你啦，挥挥手"},{"收到你的招呼啦","今天也请多关照"},{"陪你偷个小闲","伸伸懒腰，再继续吧"},{"我就在旁边","忙完了再来找我玩"}};
 public bool Enabled{get;private set;}public bool Active{get;private set;}public string Title{get;private set;}public string Detail{get;private set;}
 public double RemainingMilliseconds{get;private set;}public double IdleRemainingMilliseconds{get;private set;}public int Shown{get;private set;}
 public int LastKey{get{return lastKey;}}public bool Pending{get{return pendingContext>=0;}}
 public SmallTalk(int seed){random=seed<0?new Random():new Random(seed);Title=Detail="";Plan(12);}
 static bool Night(int hour){return hour>=22||hour<8;}
 void Plan(int hour){night=Night(hour);IdleRemainingMilliseconds=(night?720000:360000)*(1+random.NextDouble());}
 public void Configure(bool enabled,int hour){Enabled=enabled;Drop(0);last=-1;Plan(hour);}
 public void Drop(long now){Active=false;pendingContext=-1;RemainingMilliseconds=0;last=now;wasIdle=false;}
 public void Request(int context,bool manual,long now){
  if(!Enabled||(!manual&&now-lastShown<180000))return;
  if(manual){Active=false;RemainingMilliseconds=0;}
  pendingContext=Math.Max(0,Math.Min(3,context));pendingUntil=now+12000;
 }
 public void DailyEvent(long now){if(random.Next(4)==0)Request(3,false,now);}
 void Show(int context,long now,int hour){
  int[] pool=context==1?new int[]{2,3}:context==2?new int[]{4,5}:context==3?new int[]{6,7}:new int[]{0,1,4,5};
  var options=new List<int>();foreach(int key in pool)if(key!=lastKey)options.Add(key);int chosen=options[random.Next(options.Count)];lastKey=chosen;Title=lines[chosen,0];Detail=lines[chosen,1];
  Active=true;RemainingMilliseconds=4500;shownAt=lastShown=now;Shown++;pendingContext=-1;wasIdle=false;Plan(hour);
 }
 public void Update(long now,int hour,bool idle,bool canShow){
  long delta=last<0?0:Math.Max(0,now-last);last=Math.Max(last,now);if(delta>1000)delta=0;
  if(!Enabled){Drop(now);return;}
  bool n=Night(hour);if(n!=night){night=n;IdleRemainingMilliseconds=Math.Max(IdleRemainingMilliseconds,n?720000:360000);}
  if(Pending&&now>pendingUntil)pendingContext=-1;
  if(Active){if(now-shownAt>12000){Drop(now);return;}RemainingMilliseconds=Math.Max(0,RemainingMilliseconds-delta);if(RemainingMilliseconds<=0)Active=false;}
  if(!Active&&Pending&&canShow){Show(pendingContext,now,hour);return;}
  bool eligible=idle&&canShow&&!Active&&!Pending;
  if(eligible&&wasIdle)IdleRemainingMilliseconds=Math.Max(0,IdleRemainingMilliseconds-delta);
  wasIdle=eligible;
  if(eligible&&IdleRemainingMilliseconds<=0&&now-lastShown>=180000)Show(0,now,hour);
 }
}
}
