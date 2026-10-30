using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
namespace WhiteDragon {
public sealed class FocusBadge {
 public readonly Border Layer;
 readonly TextBlock title,caption;readonly Border progress,track;readonly bool showProgress;long last=-1;double badgeWidth=184,badgeHeight=54;
 public FocusBadge():this(true){}
 public FocusBadge(bool showProgress){
  this.showProgress=showProgress;
  title=PetMenuTheme.Text("",12.5,"#61466F");title.FontWeight=FontWeights.Medium;title.Margin=new Thickness(0,0,0,2);caption=PetMenuTheme.Text("",11,"#846E91");
  title.FontFamily=caption.FontFamily=new FontFamily("Microsoft YaHei UI");
  progress=new Border{Height=2,HorizontalAlignment=HorizontalAlignment.Left,Background=PetMenuTheme.Color("#AF8DC5"),CornerRadius=new CornerRadius(1)};
  track=new Border{Height=2,Background=PetMenuTheme.Color("#EEE5F3"),CornerRadius=new CornerRadius(1),Child=progress,Margin=new Thickness(0,4,0,0)};
  if(!showProgress)track.Visibility=Visibility.Collapsed;
  var stack=new StackPanel{VerticalAlignment=VerticalAlignment.Center};stack.Children.Add(title);stack.Children.Add(caption);stack.Children.Add(track);
  Layer=new Border{Width=badgeWidth,Height=badgeHeight,Padding=new Thickness(11,5,11,5),CornerRadius=new CornerRadius(14),Background=PetMenuTheme.Color("#FFFCFF"),BorderBrush=PetMenuTheme.Color("#E6D9EE"),BorderThickness=new Thickness(1),Child=stack,IsHitTestVisible=false,Opacity=0,Visibility=Visibility.Collapsed};
  TextOptions.SetTextFormattingMode(Layer,TextFormattingMode.Display);TextOptions.SetTextRenderingMode(Layer,TextRenderingMode.Grayscale);
 }
 public void Place(double width,double padding,Rect available){
  bool compact=width<220;
  badgeWidth=compact?160:184;badgeHeight=(compact?44:50)+(showProgress?4:0);Layer.Width=badgeWidth;Layer.Height=badgeHeight;
  title.FontSize=compact?11:12.5;caption.FontSize=compact?10:11;track.Margin=new Thickness(0,compact?2:4,0,0);Layer.Padding=new Thickness(compact?9:11,5,compact?9:11,5);
  double totalWidth=width+2*padding,totalHeight=width*360/640+2*padding;
  double x=(totalWidth-badgeWidth)/2,y=Math.Max(2,padding+20*width/640-badgeHeight-6);
  if(y<available.Top)y=padding+340*width/640+6;
  x=Math.Max(0,Math.Min(totalWidth-badgeWidth,Math.Max(available.Left,Math.Min(available.Right-badgeWidth,x))));
  y=Math.Max(0,Math.Min(totalHeight-badgeHeight,Math.Max(available.Top,Math.Min(available.Bottom-badgeHeight,y))));
  Canvas.SetLeft(Layer,x);Canvas.SetTop(Layer,y);
 }
 public void Update(long now,bool visible,bool hardHidden,string heading,string detail,double fraction){
  double delta=last<0?16:Math.Max(0,Math.Min(50,now-last));last=now;
  if(visible){if(title.Text!=heading)title.Text=heading;if(caption.Text!=detail)caption.Text=detail;progress.Width=Math.Max(0,badgeWidth-Layer.Padding.Left-Layer.Padding.Right-Layer.BorderThickness.Left-Layer.BorderThickness.Right)*Math.Max(0,Math.Min(1,fraction));}
  Layer.Opacity=hardHidden?0:visible?Math.Min(1,Layer.Opacity+delta/160):Math.Max(0,Layer.Opacity-delta/180);
  Layer.Visibility=Layer.Opacity>0?Visibility.Visible:Visibility.Collapsed;
 }
}
}
