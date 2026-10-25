using System;
using System.Collections.Generic;
namespace WhiteDragon {
public static class AssistantChecks {
    static void Check(bool x,string why){if(!x)throw new Exception(why);}
    public static string[] Run(){
        var passed=new List<string>();PetBehavior b;
        foreach(int row in new int[]{18,19,20,21}){
            b=new PetBehavior(500);b.Select(row,100);Check(b.Row==row&&b.SingleAction,"assistant selection");
            int duration=0;foreach(int t in PetBehavior.Durations[row])duration+=t;
            b.Tick(duration+101,null,true,false);Check(b.Animation==0&&!b.SingleAction,"assistant action never settles");
        }passed.Add("four_new_actions_complete_before_returning_idle");
        b=new PetBehavior(500);b.Play(18,false,100);b.Tick(1000,45,true,false);Check(b.Row==18,"gaze replaces thinking");passed.Add("linked_assistant_loop_is_not_replaced_by_gaze");
        b=new PetBehavior(500);b.BeginPress(false,1,100);b.EndPress(140);Check(!AssistantStates.MayUpdate(b,false,0,200),"bridge cancels pending click");b.Tick(640,null,false,false);Check(b.Animation==4&&!AssistantStates.MayUpdate(b,false,0,700),"bridge interrupts jump");b.Tick(1600,null,false,false);Check(AssistantStates.MayUpdate(b,false,0,1600),"bridge never resumes after jump");passed.Add("pending_click_and_complete_jump_take_priority_over_status");
        b=new PetBehavior(500);b.Select(19,100);Check(!AssistantStates.MayUpdate(b,false,0,200),"manual reading interrupted");passed.Add("manual_assistant_action_finishes_before_new_status");
        b=new PetBehavior(500);b.Play(18,false,100);b.SuspendHidden(245);double position=b.VisualTime(245);b.Tick(10000,null,false,false);Check(b.VisualTime(10000)==position,"hidden pose advances");b.ResumeAfterHidden(10245);Check(b.VisualTime(10245)==position,"hidden resume rewinds");passed.Add("hiding_preserves_exact_subframe_and_resumes_without_rewind");
        b.SuspendHidden(10300);position=b.VisualTime(10300);b.SetPaused(true,14000);b.ResumeAfterHidden(18000);Check(b.VisualTime(20000)==position&&b.Paused,"pause during hide loses pose");b.SetPaused(false,22000);Check(b.VisualTime(22000)==position,"unpause after hidden jumps");passed.Add("pause_hidden_and_resume_can_overlap_without_time_jump");
        b=new PetBehavior(500);b.Play(19,false,0);b.SetMenu(true,120);position=b.VisualTime(120);b.SuspendHidden(500);b.SetMenu(false,2000);b.ResumeAfterHidden(4000);Check(b.VisualTime(4000)==position,"menu plus hidden changes pose");passed.Add("menu_and_hidden_freezes_compose_correctly");
        b=new PetBehavior(500);foreach(int row in new int[]{16,17}){b.Play(row,true,0);Check(!AssistantStates.MayUpdate(b,false,0,10),"wing transition interrupted");}Check(!AssistantStates.MayUpdate(new PetBehavior(500),true,0,100),"flight interrupted");passed.Add("flight_and_wing_transitions_protect_their_animation");
        Check(AssistantStates.Row("working")==19&&AssistantStates.Row("thinking")==18&&AssistantStates.Row("review")==19&&AssistantStates.Row("needs_input")==20&&AssistantStates.Row("ready")==21&&AssistantStates.Once("ready")&&!AssistantStates.Once("working")&&!AssistantStates.Once("thinking"),"state mapping");passed.Add("status_mapping_and_one_shot_delivery_are_explicit");
        return passed.ToArray();
    }
}
}
