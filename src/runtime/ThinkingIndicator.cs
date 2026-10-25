using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
namespace WhiteDragon {
public sealed class ThinkingIndicator {
 public readonly Canvas Layer=new Canvas{Width=24,Height=24,IsHitTestVisible=false,Opacity=0,Visibility=Visibility.Collapsed};
 readonly RotateTransform rotation=new RotateTransform(0,12,12);long last=-1;
 public double Angle{get{return rotation.Angle;}}
 public ThinkingIndicator(){
  Layer.Children.Add(new Ellipse{Width=22,Height=22,Stroke=new SolidColorBrush(Color.FromRgb(225,214,235)),StrokeThickness=2,Margin=new Thickness(1)});
  Layer.Children.Add(new System.Windows.Shapes.Path{Data=Geometry.Parse("M12,1 A11,11 0 1 1 1,12"),Stroke=new SolidColorBrush(Color.FromRgb(157,125,181)),StrokeThickness=3,StrokeStartLineCap=PenLineCap.Round,StrokeEndLineCap=PenLineCap.Round,RenderTransform=rotation});
 }
 public void Place(double width,double padding){double scale=width/640;Layer.RenderTransform=new ScaleTransform(scale,scale);Canvas.SetLeft(Layer,padding+308*scale);Canvas.SetTop(Layer,padding+20*scale);}
 public void Update(long now,bool thinking,bool frozen){double elapsed=last<0?0:Math.Max(0,Math.Min(60,now-last));last=now;if(frozen)return;double opacity=Layer.Opacity;opacity=thinking?Math.Min(1,opacity+elapsed/160):Math.Max(0,opacity-elapsed/160);Layer.Opacity=opacity;Layer.Visibility=opacity>0?Visibility.Visible:Visibility.Collapsed;if(opacity>0)rotation.Angle=(rotation.Angle+elapsed*.32)%360;}
}
}
