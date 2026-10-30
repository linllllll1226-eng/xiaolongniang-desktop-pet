using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
namespace WhiteDragon {
public sealed partial class PetWindow {
#if STYLE_V10
 TextBlock menuMood,codexConnection,chatgptConnection;
 MenuItem codexLinkItem,chatgptLinkItem;
 MenuItem MenuEntry(string label,string glyph,Action callback){var item=new MenuItem{Header=label,Icon=PetMenuTheme.Icon(glyph)};if(callback!=null)item.Click+=delegate{callback();};return item;}
 MenuItem MenuToggle(string label,string glyph,bool initial,Action<bool> callback){var item=MenuEntry(label,glyph,null);item.IsCheckable=true;item.IsChecked=initial;item.Click+=delegate{callback(item.IsChecked);};return item;}
 MenuItem Card(UIElement content){return new MenuItem{Header=content,Style=(Style)menu.Resources["Card"]};}
 void BuildStyledMenu(){
  menu=new ContextMenu();PetMenuTheme.Apply(menu);
  menu.Opened+=delegate{behavior.SetMenu(true,clock.ElapsedMilliseconds);RefreshMenu();};
  menu.Closed+=delegate{behavior.SetMenu(false,clock.ElapsedMilliseconds);};
  var header=new Grid{Margin=new Thickness(11,10,11,12)};header.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(48)});header.ColumnDefinitions.Add(new ColumnDefinition());
  var avatar=new Border{Width=40,Height=44,Background=PetMenuTheme.Color("#F0E8F8"),CornerRadius=new CornerRadius(13),Clip=new RectangleGeometry(new Rect(0,0,40,44),13,13)};avatar.Child=new Image{Source=new CroppedBitmap(frames[0,0],new Int32Rect(230,75,180,180)),Stretch=Stretch.UniformToFill};header.Children.Add(avatar);
  var intro=new StackPanel{VerticalAlignment=VerticalAlignment.Center};Grid.SetColumn(intro,1);var name=PetMenuTheme.Text("小龙娘",16,"#493453");name.FontWeight=FontWeights.SemiBold;intro.Children.Add(name);menuMood=PetMenuTheme.Text("今天也陪着你",11,"#A08BAE");intro.Children.Add(menuMood);header.Children.Add(intro);menu.Items.Add(Card(header));
  var quick=new Grid{Margin=new Thickness(7,0,7,9)};for(int i=0;i<3;i++)quick.ColumnDefinitions.Add(new ColumnDefinition());
  int[] quickRows={11,3,0};string[] quickNames={"摸摸头","挥挥手","回待机"};for(int i=0;i<3;i++){int row=quickRows[i];var button=new Button{Content=quickNames[i],Margin=new Thickness(i==0?0:3,0,i==2?0:3,0),Tag=row};button.Click+=delegate(object sender,RoutedEventArgs e){e.Handled=true;menu.IsOpen=false;Choose(row);};Grid.SetColumn(button,i);quick.Children.Add(button);}menu.Items.Add(Card(quick));
  var actions=MenuEntry("更多动作","\uE712",null);
  string[] groups={"互动与表情","日常陪伴","移动与小龙翼"};int[][] rows={new int[]{3,4,5,8,11,13,24},new int[]{0,6,7,12,18,19,20,21,22,23,25},new int[]{1,2,14,15}};
#if DAILY_ROUTINE
  rows[1]=new int[]{0,6,7,12,18,19,20,21,22,23,25,26};
#endif
#if CLICK_BOUNCE
  rows[0]=new int[]{3,4,5,8,11,13,24,27};
  rows[1]=new int[]{0,6,7,12,18,19,20,21,22,23,25,26,28};
#endif
  for(int g=0;g<groups.Length;g++){var group=new MenuItem{Header=groups[g]};foreach(int r in rows[g]){int row=r;group.Items.Add(MenuEntry(smooth.ActionName(row),"\uE768",delegate{Choose(row);}));}actions.Items.Add(group);}menu.Items.Add(actions);
  pauseItem=MenuToggle("暂停动画","\uE769",behavior.Paused,delegate(bool v){behavior.SetPaused(v,clock.ElapsedMilliseconds);RenderFrame();});menu.Items.Add(pauseItem);
#if WINDOW_PERCH
  BuildPerchMenu();
#endif
#if DAILY_ROUTINE
  BuildDailyMenu();
#endif
#if FOCUS_COMPANION
  BuildFocusMenu();
#endif
#if SMALL_TALK
  BuildSmallTalkMenu();
#endif
  menu.Items.Add(new Separator());
  var links=MenuEntry("ChatGPT 与 Codex 联动","\uE71B",null);
  codexConnection=PetMenuTheme.Text("Codex · 等待任务事件",11,"#9A85A9");links.Items.Add(Card(new Border{Child=codexConnection,Padding=new Thickness(12,8,12,4)}));
  codexLinkItem=MenuToggle("Codex 任务状态","\uE756",preferences.LinkCodex,delegate(bool v){preferences.LinkCodex=v;bridgeNextRead=0;bridgeSnooze=0;Save();RefreshMenu();});links.Items.Add(codexLinkItem);
  chatgptConnection=PetMenuTheme.Text("ChatGPT · 等待客户端状态",11,"#9A85A9");links.Items.Add(Card(new Border{Child=chatgptConnection,Padding=new Thickness(12,8,12,4)}));
  chatgptLinkItem=MenuToggle("ChatGPT 客户端状态","\uE8F2",preferences.LinkChatGPT,delegate(bool v){preferences.LinkChatGPT=v;clientLink.Enabled=v;bridgeNextRead=0;bridgeSnooze=0;Save();RefreshMenu();});links.Items.Add(chatgptLinkItem);
  links.Items.Add(new Separator());links.Items.Add(MenuEntry("打开 ChatGPT / Codex","\uE8A7",delegate{ClientLink.OpenApp();}));links.Items.Add(MenuEntry("联动说明与状态检查","\uE946",delegate{Process.Start(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"联动说明.html"));}));links.SubmenuOpened+=delegate{RefreshMenu();};menu.Items.Add(links);
  var display=MenuEntry("显示与习惯","\uE713",null);
  topItem=MenuToggle("始终在其他窗口上方","\uE718",preferences.Topmost,delegate(bool v){preferences.Topmost=v;Topmost=v;Save();});display.Items.Add(topItem);
  napItem=MenuToggle("安静三分钟后打盹","\uE708",preferences.AutoNap,delegate(bool v){preferences.AutoNap=v;behavior.AutoNap=v;behavior.Touch(clock.ElapsedMilliseconds);Save();});display.Items.Add(napItem);
  var sizes=MenuEntry("角色大小","\uE740",null);foreach(int n in new int[]{280,350,420,480}){int size=n;var entry=MenuEntry(n==350?"标准 · 与 DeepSeek 同高":n==280?"小一点":n==420?"稍大一点":"大一点","\uE91B",delegate{ResizePet(size);});entry.Tag=size;sizes.Items.Add(entry);}sizes.SubmenuOpened+=delegate{foreach(MenuItem s in sizes.Items){s.IsCheckable=true;s.IsChecked=Math.Abs(bodyWidth-(int)s.Tag)<.5;}};display.Items.Add(sizes);
  display.Items.Add(MenuEntry("回到屏幕右下角","\uE80F",ResetPosition));menu.Items.Add(display);
  menu.Items.Add(new Separator());menu.Items.Add(MenuEntry("暂时隐藏 · 托盘找回","\uE921",delegate{SetHidden(true);}));var exit=MenuEntry("退出小龙娘","\uE7E8",Close);exit.Foreground=PetMenuTheme.Color("#A17386");menu.Items.Add(exit);
  var footer=PetMenuTheme.Text(
#if CLICK_BOUNCE
  "WHITE DRAGON    ·    v10.15",
#elif SMALL_TALK
  "WHITE DRAGON    ·    v10.13",
#elif FOCUS_COMPANION
  "WHITE DRAGON    ·    v10.12.1",
#elif DAILY_ROUTINE
  "WHITE DRAGON    ·    v10.11",
#elif WINDOW_PERCH
  "WHITE DRAGON    ·    v10.10.2",
#else
  "WHITE DRAGON    ·    v10.9.3",
#endif
  9,"#BAA7C9");footer.Margin=new Thickness(12,8,0,5);menu.Items.Add(Card(footer));image.ContextMenu=menu;
 }
 void RefreshMenu(){
  if(menuMood==null)return;
#if SMALL_TALK
  RefreshSmallTalkMenu();
#endif
#if FOCUS_COMPANION
  RefreshFocusMenu();
#endif
#if DAILY_ROUTINE
  RefreshDailyMenu();
#endif
#if WINDOW_PERCH
  RefreshPerchMenu();
#endif
  menuMood.Text=behavior.Paused?"休息一下，等你回来":bridgeSource!=null?SourceName(bridgeSource)+" · "+StateName(bridgeStatus):"今天也陪着你";
  pauseItem.IsChecked=behavior.Paused;topItem.IsChecked=preferences.Topmost;napItem.IsChecked=preferences.AutoNap;
  codexLinkItem.IsChecked=preferences.LinkCodex;chatgptLinkItem.IsChecked=preferences.LinkChatGPT;
  codexConnection.Text="Codex · "+(!preferences.LinkCodex?"未开启":lastCodexState!=null?StateName(lastCodexState.status):"等待新任务事件");
  chatgptConnection.Text="ChatGPT · "+(!preferences.LinkChatGPT?"未开启":clientLink.Description);
 }
 static string SourceName(string source){return source=="codex-events"?"Codex 会话事件":source=="codex"?"Codex 回调":source=="chatgpt-client"?"客户端联动":"助手";}
 static string StateName(string state){switch(state){case "working":return "正在工作";case "executing":return "正在执行命令";case "thinking":return "正在思考";case "review":return "正在检查";case "needs_input":return "等你回答";case "ready":return "回复已结束";case "error":return "遇到问题";default:return "安静陪伴";}}
#endif
}
}
