using System;using System.IO;using System.Collections.Generic;using System.Web.Script.Serialization;using WhiteDragon;
public static class TabletPhaseReview {
 static List<string> checks=new List<string>();static void Check(bool ok,string name){if(!ok)throw new Exception(name);checks.Add(name);}
 static PetBehavior B(){var b=new PetBehavior(500,new Dictionary<int,int>{{29,800},{28,3200},{30,800}});b.AutoNap=false;return b;}
 public static int Main(string[] args){try{
  var b=B();var p=new TabletPhases();
  Check(p.Route("executing",b,true,100)&&b.Animation==29&&p.Entries==1,"Execution starts one pickup");int first=b.PlaybackId;
  for(int i=0;i<10;i++)p.Route("executing",b,false,150+i*30);
  Check(b.PlaybackId==first&&p.Entries==1,"Repeated commands during pickup do not restart it");
  b.Tick(901,null,false,false);Check(b.Animation==28&&!b.SingleAction&&p.BoundaryNeedsRouting(b),"Pickup ends directly in tapping without an idle frame");p.Route("executing",b,true,901);int loop=b.PlaybackId;
  for(int i=0;i<300;i++){long now=1000+i*250;b.Tick(now,null,false,false);p.Route("executing",b,true,now);}
  Check(b.Animation==28&&b.PlaybackId==loop&&p.Entries==1&&p.Exits==0,"Many loop boundaries and command updates keep one tapping playback");
  p.Route("working",b,true,80000);Check(b.Animation==30&&p.Exits==1,"Leaving execution starts one putdown");int ending=b.PlaybackId;
  for(int i=0;i<5;i++)p.Route("working",b,false,80100+i*50);
  Check(b.PlaybackId==ending&&p.Exits==1,"Repeated next-stage events do not restart putdown");
  b.Tick(80801,null,false,false);Check(p.BoundaryNeedsRouting(b),"Putdown boundary requests immediate next-phase routing");bool held=p.Route("working",b,true,80801);Check(!held&&p.FinishedExit&&p.Current==TabletPhases.Stage.None,"Putdown releases the bridge for reading in the same rendering tick");b.Play(19,false,80801);
  Check(b.Animation==19&&p.Entries==1&&p.Exits==1,"One execution phase has exactly one pickup and one putdown");
  b=B();p=new TabletPhases();p.Route("executing",b,true,0);first=b.PlaybackId;p.Route("ready",b,false,200);Check(b.Animation==29&&b.PlaybackId==first,"An early reply end queues behind the pickup");b.Tick(801,null,false,false);p.Route("ready",b,true,801);Check(b.Animation==30&&p.Entries==1&&p.Exits==1,"An early end puts the tablet down without an extra looping or pickup action");b.Tick(1602,null,false,false);held=p.Route("ready",b,true,1602);Check(!held&&p.FinishedExit,"Reply completion can follow the putdown");
  b=B();p=new TabletPhases();p.Route("executing",b,true,0);b.Tick(200,null,false,false);b.SetPaused(true,200);b.Tick(9000,null,false,false);p.Route("executing",b,false,9000);Check(b.Animation==29&&p.Entries==1,"Pause holds pickup instead of restarting or skipping it");b.SetPaused(false,10000);b.Tick(10601,null,false,false);p.Route("executing",b,true,10601);Check(b.Animation==28,"Unpause continues to the same tapping phase");
  p.Route("working",b,true,11000);p.Route("executing",b,false,11200);Check(b.Animation==30,"A new execution request during putdown does not tear the clip");b.Tick(11801,null,false,false);p.Route("executing",b,true,11801);Check(b.Animation==29&&p.Entries==2&&p.Exits==1,"A genuinely new execution phase gets one new pickup after putdown");
  b=B();p=new TabletPhases();Check(!p.Route("executing",b,false,0)&&b.Animation==0,"Pointer and physics gates can postpone pickup");Check(!p.Route("ready",b,true,100)&&p.Entries==0,"A phase that passed during user interaction does not invent pickup and putdown");
  File.WriteAllText(args[0],new JavaScriptSerializer().Serialize(new{ok=true,checks=checks,scope="Deterministic real PetBehavior clock and phase routing; no media or visible desktop claim"}));Console.WriteLine("PASS "+checks.Count+" tablet phase cases");return 0;
 }catch(Exception e){Console.WriteLine(e.GetType().Name+": "+e.Message);return 1;}}
}
