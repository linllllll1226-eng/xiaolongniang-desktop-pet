#if TABLET_PHASES
using System;
namespace WhiteDragon {
public partial class PetWindow {
 readonly TabletPhases tabletPhases=new TabletPhases();
 bool RouteTabletPhases(long now,bool canLeaveBook){
  bool allowed=canLeaveBook||AssistantStates.MayUpdate(behavior,flight.Active||pendingWingFold,bridgeSnooze,now)||AssistantStates.MayReplaceCompletion(behavior,flight.Active||pendingWingFold,bridgeApplied,bridgeStatus,bridgeSnooze,now);
  bool owned=tabletPhases.Route(bridgeStatus,behavior,allowed,now);
  if(tabletPhases.FinishedExit)bridgeApplied=null;
  if(!owned)return false;
  if(tabletPhases.Current==TabletPhases.Stage.Entering||tabletPhases.Current==TabletPhases.Stage.Looping)bridgeApplied="executing";
  if(automaticBookChange&&tabletPhases.Current==TabletPhases.Stage.Entering){automaticBookChange=false;readingLibrary.Finish(false);}
  image.ToolTip="小龙娘 · "+SourceName(bridgeSource)+" · "+(tabletPhases.Current==TabletPhases.Stage.Exiting?"收起平板":StateName(bridgeStatus))+"\n慢拖拎起 · 甩出缓降 · 右键打开菜单";
  return true;
 }
}
}
#endif
