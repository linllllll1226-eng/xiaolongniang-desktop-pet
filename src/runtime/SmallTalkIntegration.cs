using System;
using System.Windows;
using System.Windows.Controls;
namespace WhiteDragon {
public sealed partial class PetWindow {
 readonly SmallTalk smallTalk=new SmallTalk(-1);readonly FocusBadge smallTalkBadge=new FocusBadge(false);
 int smallTalkObserved=-1;long smallTalkManualRequest=-1;MenuItem smallTalkToggle,smallTalkNow;TextBlock smallTalkStatus;
 bool SmallTalkBlocked{get{return !IsVisible||behavior.Hidden||behavior.Paused||behavior.MenuOpen||behavior.Pressed||behavior.PendingClick>0||behavior.Sleeping||flight.Active||pendingWingFold||behavior.Animation>=14&&behavior.Animation<=17||AssistantBusy||FocusSuppressesDaily;}}
 void InitializeSmallTalk(){smallTalk.Configure(!testing&&preferences.SmallTalkEnabled,DateTime.Now.Hour);canvas.Children.Add(smallTalkBadge.Layer);}
 void BuildSmallTalkMenu(){
  var root=MenuEntry("轻语气泡","\uE8F2",null);smallTalkStatus=PetMenuTheme.Text("偶尔说两句，也会安静陪着你",11,"#9A85A9");root.Items.Add(Card(new Border{Child=smallTalkStatus,Padding=new Thickness(12,7,12,7)}));
  smallTalkToggle=MenuToggle("偶尔说两句","\uE73A",preferences.SmallTalkEnabled,delegate(bool value){preferences.SmallTalkEnabled=value;smallTalk.Configure(value,DateTime.Now.Hour);smallTalkManualRequest=-1;Save();RefreshSmallTalkMenu();});root.Items.Add(smallTalkToggle);
  smallTalkNow=MenuEntry("现在说一句","\uE768",delegate{smallTalkManualRequest=clock.ElapsedMilliseconds;});root.Items.Add(smallTalkNow);
  var explanation=PetMenuTheme.Text("本地短句，不读取聊天内容\n专注和任务期间保持安静",10,"#AB98B5");root.Items.Add(Card(new Border{Child=explanation,Padding=new Thickness(12,7,12,7)}));root.SubmenuOpened+=delegate{RefreshSmallTalkMenu();};menu.Items.Add(root);
 }
 void RefreshSmallTalkMenu(){
  if(smallTalkStatus==null)return;smallTalkToggle.IsChecked=preferences.SmallTalkEnabled;
  smallTalkStatus.Text=!preferences.SmallTalkEnabled?"已关闭，安静陪着你":FocusSuppressesDaily?"专注时先安静陪你":AssistantBusy?"先陪你处理当前任务":behavior.Sleeping?"睡醒后再轻声说两句":"偶尔回应摸头、挥手和小日常";
  smallTalkNow.IsEnabled=preferences.SmallTalkEnabled&&!FocusSuppressesDaily&&!AssistantBusy&&!behavior.Sleeping&&!behavior.Paused&&!flight.Active&&!pendingWingFold&&!(behavior.Animation>=14&&behavior.Animation<=17);
 }
 void SilenceSmallTalk(long now){smallTalk.Drop(now);smallTalkManualRequest=-1;}
 void UpdateSmallTalk(long now){
  int playback=behavior.PlaybackId;
  if(SmallTalkBlocked){
   smallTalk.Drop(now);smallTalkObserved=behavior.Pressed?-1:playback;
   if(!behavior.MenuOpen)smallTalkManualRequest=-1;
   if(behavior.MenuOpen)RefreshSmallTalkMenu();return;
  }
  bool ready=!behavior.MediaWaiting&&!transitioning;
  if(smallTalkManualRequest>=0){if(now-smallTalkManualRequest<=2000)smallTalk.Request(0,true,now);smallTalkManualRequest=-1;}
  if(playback!=smallTalkObserved){
   smallTalkObserved=playback;
   if(behavior.Animation==11)smallTalk.Request(1,false,now);else if(behavior.Animation==3)smallTalk.Request(2,false,now);else if(DailyOwnsPose)smallTalk.DailyEvent(now);
  }
  smallTalk.Update(now,DateTime.Now.Hour,behavior.Animation==0&&!behavior.SingleAction,ready);
 }
 void UpdateSmallTalkBadge(long now){
  bool hard=SmallTalkBlocked||!smallTalk.Enabled;
  bool visible=smallTalk.Active&&!hard;
  if(!visible&&smallTalkBadge.Layer.Opacity<=0){if(!ToolTipService.GetIsEnabled(image))ToolTipService.SetIsEnabled(image,true);return;}
  Rect area=CurrentWorkArea();smallTalkBadge.Place(bodyWidth,RigPadding,new Rect(area.Left-Left+4,area.Top-Top+4,Math.Max(1,area.Width-8),Math.Max(1,area.Height-8)));
  smallTalkBadge.Update(now,visible,hard,smallTalk.Title,smallTalk.Detail,0);
  bool tooltip=smallTalkBadge.Layer.Opacity<=0;if(ToolTipService.GetIsEnabled(image)!=tooltip)ToolTipService.SetIsEnabled(image,tooltip);
 }
}
}
