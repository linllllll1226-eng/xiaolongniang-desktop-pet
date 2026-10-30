using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Microsoft.Win32;
namespace WhiteDragon {
public sealed partial class PetWindow {
 readonly FocusSession focusSession=new FocusSession();readonly FocusBadge focusBadge=new FocusBadge();
 readonly DispatcherTimer focusTimer=new DispatcherTimer(DispatcherPriority.Background){Interval=TimeSpan.FromSeconds(1)};
 int focusPlayback=-1;bool focusStopping;long focusSnooze,focusNoticeUntil;bool focusPowerSubscribed;
 long focusNoticeRemaining,focusNoticeLast=-1;bool focusNoticeWasVisible;
 TextBlock focusStatus;MenuItem focusStartMenu,focusRest,focusPause,focusEnd,focusReadToggle,focusBadgeToggle;
 bool FocusOwnsPose{get{return focusPlayback>=0&&behavior.PlaybackId==focusPlayback;}}
 bool FocusSuppressesDaily{get{return focusSession.Active||focusSession.NoticePending||focusNoticeRemaining>0;}}
 bool FocusBadgeHardHidden{get{return !IsVisible||behavior.Hidden||behavior.MenuOpen||behavior.Pressed||behavior.PendingClick>0||flight.Active||pendingWingFold||behavior.Animation>=14&&behavior.Animation<=17||focusSession.Suspended;}}
 void InitializeFocus(){
  if(preferences.FocusMinutes!=25&&preferences.FocusMinutes!=45&&preferences.FocusMinutes!=60)preferences.FocusMinutes=25;
  canvas.Children.Add(focusBadge.Layer);focusTimer.Tick+=delegate{AdvanceFocusClock(clock.ElapsedMilliseconds);UpdateFocusBadge(clock.ElapsedMilliseconds);if(behavior.MenuOpen)RefreshFocusMenu();if(!focusSession.Active)focusTimer.Stop();};
  if(!testing){SystemEvents.PowerModeChanged+=FocusPowerChanged;focusPowerSubscribed=true;}
 }
 void DisposeFocus(){focusTimer.Stop();if(focusPowerSubscribed){SystemEvents.PowerModeChanged-=FocusPowerChanged;focusPowerSubscribed=false;}}
 void FocusPowerChanged(object sender,PowerModeChangedEventArgs e){
  if(e.Mode!=PowerModes.Suspend&&e.Mode!=PowerModes.Resume)return;bool suspended=e.Mode==PowerModes.Suspend;
  if(!closed)Dispatcher.BeginInvoke(new Action(delegate{if(!closed){long now=clock.ElapsedMilliseconds;AdvanceFocusClock(now);focusSession.SetSuspended(suspended,now);AdvanceFocusClock(now);UpdateFocusBadge(now);}}));
 }
 void BuildFocusMenu(){
  var root=MenuEntry("专注陪伴","\uE916",null);focusStatus=PetMenuTheme.Text("选一段时间，我安静陪着你",11,"#9A85A9");root.Items.Add(Card(new Border{Padding=new Thickness(12,7,12,7),Child=focusStatus}));
  focusStartMenu=MenuEntry("开始一轮专注","\uE768",null);
  foreach(int m in new int[]{25,45,60}){int minutes=m;var item=MenuEntry(minutes+" 分钟","\uE73E",delegate{if(focusSession.Active)return;preferences.FocusMinutes=minutes;StartFocusPeriod(minutes*60,false);Save();});item.Tag=m;focusStartMenu.Items.Add(item);}
  focusStartMenu.SubmenuOpened+=delegate{foreach(MenuItem item in focusStartMenu.Items){item.IsCheckable=true;item.IsChecked=(int)item.Tag==preferences.FocusMinutes;}};root.Items.Add(focusStartMenu);
  focusRest=MenuEntry("先休息 5 分钟","\uE708",delegate{if(!focusSession.Active)StartFocusPeriod(300,true);});root.Items.Add(focusRest);
  focusPause=MenuEntry("暂停计时","\uE769",delegate{bool resume=focusSession.Paused;long now=clock.ElapsedMilliseconds;focusSession.SetPaused(!resume,now);if(resume){focusSnooze=now+200;WakeForFocus(now);}RefreshFocusMenu();});root.Items.Add(focusPause);
  focusEnd=MenuEntry("结束本轮","\uE711",delegate{long now=clock.ElapsedMilliseconds;focusSession.End(now);focusTimer.Stop();ResetFocusNotice(now);RefreshFocusMenu();});root.Items.Add(focusEnd);
  root.Items.Add(new Separator());
  focusReadToggle=MenuToggle("专注时陪我读书","\uE8F1",preferences.FocusReading,delegate(bool value){preferences.FocusReading=value;if(value){long now=clock.ElapsedMilliseconds;focusSnooze=now+200;WakeForFocus(now);}Save();});root.Items.Add(focusReadToggle);
  focusBadgeToggle=MenuToggle("显示倒计时","\uE787",preferences.FocusShowTimer,delegate(bool value){preferences.FocusShowTimer=value;Save();});root.Items.Add(focusBadgeToggle);
  var explanation=PetMenuTheme.Text("暂停动画或隐藏时照常计时\n电脑休眠时暂停，退出后结束本轮",10,"#AB98B5");root.Items.Add(Card(new Border{Padding=new Thickness(12,7,12,7),Child=explanation}));root.SubmenuOpened+=delegate{RefreshFocusMenu();};menu.Items.Add(root);
 }
 void RefreshFocusMenu(){
  if(focusStatus==null)return;
  focusStatus.Text=focusSession.Active?(focusSession.IsBreak?"休息":"专注")+" · "+focusSession.TimeText+(focusSession.Paused?" · 已暂停":focusSession.Suspended?" · 随电脑暂停":""):focusSession.Finished?(focusSession.IsBreak?"休息结束，可以慢慢回来":"这一轮完成啦，可以休息5分钟"):"选一段时间，我安静陪着你";
  focusStartMenu.IsEnabled=focusRest.IsEnabled=!focusSession.Active;focusPause.IsEnabled=focusSession.Active;focusPause.Header=focusSession.Paused?"继续计时":"暂停计时";focusEnd.IsEnabled=focusSession.Active||focusSession.Finished;
  focusReadToggle.IsChecked=preferences.FocusReading;focusBadgeToggle.IsChecked=preferences.FocusShowTimer;
 }
 void StartFocusPeriod(int seconds,bool rest){
  long now=clock.ElapsedMilliseconds;if(focusSession.Active)return;
  CancelDailyRoutine(now,true,-1);focusSession.Start(seconds,rest,now);ResetFocusNotice(now);focusStopping=false;focusSnooze=now+200;focusTimer.Start();behavior.Touch(now);WakeForFocus(now);smooth.Warm(19);RefreshFocusMenu();
 }
 void WakeForFocus(long now){if(focusSession.Running&&!focusSession.IsBreak&&preferences.FocusReading&&behavior.Sleeping)behavior.Wake(now);}
 void AdvanceFocusClock(long now){
  focusSession.Update(now);
  bool visible=!FocusBadgeHardHidden;
  long elapsed=focusNoticeLast<0?0:Math.Max(0,now-focusNoticeLast);focusNoticeLast=Math.Max(focusNoticeLast,now);if(elapsed>10000)elapsed=0;
  if(focusNoticeRemaining>0&&visible&&focusNoticeWasVisible)focusNoticeRemaining=Math.Max(0,focusNoticeRemaining-elapsed);
  if(focusSession.NoticePending&&visible){focusSession.ConsumeNotice();focusNoticeRemaining=8000;}
  focusNoticeWasVisible=visible;focusNoticeUntil=focusNoticeRemaining>0?now+focusNoticeRemaining:0;
 }
 void ResetFocusNotice(long now){focusNoticeRemaining=focusNoticeUntil=0;focusNoticeLast=now;focusNoticeWasVisible=false;}
 void CancelFocusOwnership(long now,bool resetPose){
  bool owned=FocusOwnsPose;focusPlayback=-1;focusStopping=false;focusSnooze=now+8000;
  if(owned){if(readingLibrary.InExchange)readingLibrary.Finish(false);automaticBookChange=false;readingLibrary.TaskEnded();if(resetPose)behavior.Play(0,false,now);}
 }
 bool PreemptFocusForAssistant(long now){
  if(!FocusOwnsPose||!AssistantBusy||behavior.Pressed||behavior.PendingClick>0||behavior.Paused||behavior.MenuOpen||behavior.Hidden||flight.Active||pendingWingFold)return false;
  CancelFocusOwnership(now,false);return true;
 }
 void UpdateFocusCompanion(long now){
  AdvanceFocusClock(now);
  if(focusPlayback>=0&&!FocusOwnsPose){
   if(automaticBookChange&&behavior.Animation==25||behavior.Animation==19&&behavior.NaturalReadingTransition)focusPlayback=behavior.PlaybackId;
   else{focusPlayback=-1;focusSnooze=now+8000;}
  }
  bool wantsReading=focusSession.Running&&!focusSession.IsBreak&&preferences.FocusReading;
  bool blocked=AssistantBusy||behavior.Hidden||behavior.Paused||behavior.MenuOpen||behavior.Pressed||behavior.PendingClick>0||behavior.MediaWaiting||transitioning||flight.Active||pendingWingFold||windowPerch.Attached||perchActionAfterLanding>=0;
  if(FocusOwnsPose){
   focusStopping=!wantsReading;
   if(focusStopping&&!blocked&&behavior.Animation==19){double t=behavior.VisualTime(now);if(t<70||t>=behavior.TotalDuration(19)-70){CancelFocusOwnership(now,false);behavior.Play(0,false,now);}}
   return;
  }
  if(!wantsReading||blocked||now<focusSnooze||behavior.SingleAction||behavior.Animation!=0)return;
  readingLibrary.TaskEnded();behavior.Play(19,false,now);focusPlayback=behavior.PlaybackId;smooth.Warm(19);
 }
 void UpdateFocusBadge(long now){
  bool notice=focusNoticeRemaining>0;
  if(!notice&&(!focusSession.Active||!preferences.FocusShowTimer)&&focusBadge.Layer.Opacity<=0)return;
  Rect area=CurrentWorkArea();focusBadge.Place(bodyWidth,RigPadding,new Rect(area.Left-Left+4,area.Top-Top+4,Math.Max(1,area.Width-8),Math.Max(1,area.Height-8)));
  bool visible=notice||focusSession.Active&&preferences.FocusShowTimer;
  string title=notice?(focusSession.IsBreak?"休息结束啦":"这一轮辛苦啦"):(focusSession.IsBreak?"休息":"专注")+" · "+focusSession.TimeText;
  string caption=notice?(focusSession.IsBreak?"准备好了，就开始下一轮":"喝口水，歇一小会儿"):focusSession.Suspended?"电脑休息，计时也暂停":focusSession.Paused?"计时暂停啦，我等你":focusSession.IsBreak?"让眼睛和肩膀也歇歇":"慢慢来，我陪着你";
  focusBadge.Update(now,visible,FocusBadgeHardHidden,title,caption,notice?1:focusSession.Progress);
 }
}
}
