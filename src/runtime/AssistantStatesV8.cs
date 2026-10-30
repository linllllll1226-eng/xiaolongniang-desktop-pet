namespace WhiteDragon {
public static class AssistantStates {
    public static int Row(string status){switch(status){case "working":case "review":return 19;case "executing":return 28;case "thinking":return 18;case "needs_input":return 20;case "ready":return 21;case "error":return 5;default:return 0;}}
    public static bool Once(string status){return status=="ready"||status=="error";}
    public static bool MayReplaceCompletion(PetBehavior b,bool physicsBusy,string previous,string next,long snooze,long now){
        return previous=="ready"&&next!=null&&next!="ready"&&next!="idle"&&b.Animation==21&&!b.Pressed&&b.PendingClick==0&&!physicsBusy&&!b.Paused&&!b.MenuOpen&&!b.Hidden&&now>=snooze;
    }
    public static bool MayUpdate(PetBehavior b,bool physicsBusy,long snooze,long now){
        return !b.Pressed&&!b.SingleAction&&b.PendingClick==0&&!physicsBusy&&b.Animation!=16&&b.Animation!=17&&!b.Paused&&!b.MenuOpen&&!b.Hidden&&now>=snooze;
    }
}
}
