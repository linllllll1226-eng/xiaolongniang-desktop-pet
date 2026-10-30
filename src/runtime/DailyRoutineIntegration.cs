using System;
using System.Windows.Controls;
namespace WhiteDragon {
public sealed partial class PetWindow {
 readonly DailyRoutine dailyRoutine=new DailyRoutine(-1);
 int dailyPlayback=-1;bool dailyFinishing;long dailySnooze;
 MenuItem dailyToggle,dailyNow;System.Windows.Controls.TextBlock dailyStatus;
 bool AssistantBusy{get{return bridgeStatus!=null&&bridgeStatus!="idle";}}
 bool DailyOwnsPose{get{return dailyRoutine.ActiveRow>=0&&behavior.PlaybackId==dailyPlayback;}}
 void BuildDailyMenu(){
  var parent=MenuEntry("小日常","\uE708",null);
  dailyStatus=PetMenuTheme.Text("空闲时自己安排一点小事",11,"#9A85A9");parent.Items.Add(Card(new Border{Child=dailyStatus,Padding=new System.Windows.Thickness(12,7,12,7)}));
  dailyToggle=MenuToggle("自己安排小日常","\uE73A",preferences.DailyRoutine,delegate(bool value){preferences.DailyRoutine=value;dailyRoutine.SetEnabled(value,DateTime.Now.Hour);if(!value&&dailyRoutine.ActiveRow==19)dailyFinishing=true;Save();RefreshDailyMenu();});parent.Items.Add(dailyToggle);
  var rhythms=MenuEntry("活动节奏","\uE916",null);string[] labels={"安静一些","自然节奏","活泼一些"};
  for(int i=0;i<labels.Length;i++){int rhythm=i;var item=MenuEntry(labels[i],"\uE73E",delegate{preferences.DailyRhythm=rhythm;dailyRoutine.SetRhythm(rhythm,DateTime.Now.Hour);Save();RefreshDailyMenu();});item.Tag=rhythm;rhythms.Items.Add(item);}
  rhythms.SubmenuOpened+=delegate{foreach(MenuItem item in rhythms.Items){item.IsCheckable=true;item.IsChecked=(int)item.Tag==preferences.DailyRhythm;}};parent.Items.Add(rhythms);
  dailyNow=MenuEntry("现在活动一下","\uE768",delegate{behavior.Touch(clock.ElapsedMilliseconds);dailyRoutine.RequestNow();dailySnooze=clock.ElapsedMilliseconds+150;RefreshDailyMenu();});parent.Items.Add(dailyNow);
  parent.SubmenuOpened+=delegate{RefreshDailyMenu();};menu.Items.Add(parent);
 }
 void RefreshDailyMenu(){
  if(dailyStatus==null)return;dailyToggle.IsChecked=preferences.DailyRoutine;
  string text=!preferences.DailyRoutine?"已关闭，安静陪着你":dailyRoutine.ActiveRow>=0?"小日常 · "+smooth.ActionName(dailyRoutine.ActiveRow):AssistantBusy?"先陪你处理当前任务":windowPerch.Attached?"坐在窗边陪着你":behavior.Sleeping?"正在打盹，醒来后再活动":dailyRoutine.Requested?"等当前互动结束再活动":dailyRoutine.Night?"夜间放轻节奏，偶尔活动一下":"空闲时偶尔读书、伸懒腰、坐一坐";
  dailyStatus.Text=text;dailyNow.IsEnabled=preferences.DailyRoutine&&dailyRoutine.ActiveRow<0&&!AssistantBusy&&!windowPerch.Attached&&!flight.Active&&!pendingWingFold&&perchActionAfterLanding<0;
#if FOCUS_COMPANION
  if(FocusSuppressesDaily){dailyStatus.Text="正在专注陪伴，小日常稍后再安排";dailyNow.IsEnabled=false;}
#endif
 }
 void CancelDailyRoutine(long now,bool resetPose,int manualRow){
  bool owned=DailyOwnsPose;dailyRoutine.Defer(DateTime.Now.Hour,manualRow);dailyPlayback=-1;dailyFinishing=false;dailySnooze=now+1200;
  if(owned){if(readingLibrary.InExchange)readingLibrary.Finish(false);automaticBookChange=false;readingLibrary.TaskEnded();if(resetPose)behavior.Play(0,false,now);}
 }
 bool PreemptDailyForAssistant(long now){
  if(!DailyOwnsPose||!AssistantBusy||behavior.Pressed||behavior.PendingClick>0||behavior.Paused||behavior.MenuOpen||behavior.Hidden||flight.Active||pendingWingFold)return false;
  CancelDailyRoutine(now,false,-1);return true;
 }
 void UpdateDailyRoutine(long now){
  if(dailyRoutine.ActiveRow>=0&&!DailyOwnsPose){
   if(behavior.Animation==0&&behavior.NaturalIdleTransition)dailyRoutine.Finish(DateTime.Now.Hour);else dailyRoutine.Cancel(DateTime.Now.Hour);
   dailyPlayback=-1;dailyFinishing=false;dailySnooze=now+800;
  }
  bool blocked=behavior.Paused||behavior.MenuOpen||behavior.Hidden||behavior.Pressed||behavior.PendingClick>0||behavior.MediaWaiting||transitioning||flight.Active||pendingWingFold||windowPerch.Attached||perchActionAfterLanding>=0||AssistantBusy;
#if FOCUS_COMPANION
  blocked=blocked||FocusSuppressesDaily;
#endif
  bool eligible=!blocked&&now>=dailySnooze&&(!behavior.SingleAction&&behavior.Animation==0||dailyRoutine.Requested&&behavior.Sleeping);
  dailyRoutine.Update(now,DateTime.Now.Hour,eligible,blocked);
  if(DailyOwnsPose&&dailyRoutine.ActiveRow==19){
   if(dailyRoutine.ReadingRemainingMilliseconds<=0)dailyFinishing=true;
   double time=behavior.VisualTime(now);if(dailyFinishing&&!blocked&&(time<70||time>=behavior.TotalDuration(19)-70)){
    dailyRoutine.Finish(DateTime.Now.Hour);dailyPlayback=-1;dailyFinishing=false;readingLibrary.TaskEnded();behavior.Play(0,false,now);dailySnooze=now+800;return;
   }
  }
  if(!dailyRoutine.Due||!eligible)return;
  int row=behavior.Sleeping?13:dailyRoutine.Pick(DateTime.Now.Hour,smooth.MediaDurations.ContainsKey(26));
  StartDailyActivity(row,now);
 }
 void StartDailyActivity(int row,long now){
  dailyRoutine.Begin(row,DateTime.Now.Hour);if(row==19)readingLibrary.TaskEnded();behavior.Play(row,row!=19,now);dailyPlayback=behavior.PlaybackId;dailyFinishing=false;smooth.Warm(row);
  image.ToolTip="小日常 · "+smooth.ActionName(row)+"\n随时可以摸头、拎起或选择其他动作";
 }
}
}
