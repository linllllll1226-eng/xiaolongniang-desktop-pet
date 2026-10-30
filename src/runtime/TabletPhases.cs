namespace WhiteDragon {
// Owns the three tablet parts, independent of repeated status callbacks.
public sealed class TabletPhases {
 public enum Stage {None,Entering,Looping,Exiting}
 public Stage Current{get;private set;}
 public bool FinishedExit{get;private set;}
 public int Entries{get;private set;}public int Exits{get;private set;}
 public bool Route(string status,PetBehavior behavior,bool mayApply,long now){
  FinishedExit=false;
  if(Current==Stage.Entering&&behavior.Animation!=29)Current=behavior.Animation==28?Stage.Looping:Stage.None;
  if(Current==Stage.Looping&&behavior.Animation!=28)Current=Stage.None;
  if(Current==Stage.Exiting&&behavior.Animation!=30){FinishedExit=behavior.Animation==0&&behavior.NaturalIdleTransition;Current=Stage.None;}
  if(Current==Stage.None){
   if(status!="executing"||!mayApply)return false;
   behavior.Play(29,true,now);Current=Stage.Entering;Entries++;return true;
  }
  if(Current==Stage.Entering||Current==Stage.Exiting)return true;
  if(status=="executing")return true;
  if(mayApply){behavior.Play(30,true,now);Current=Stage.Exiting;Exits++;}
  return true;
 }
 public bool BoundaryNeedsRouting(PetBehavior behavior){return Current==Stage.Entering&&behavior.Animation!=29||Current==Stage.Exiting&&behavior.Animation!=30;}
}
}
