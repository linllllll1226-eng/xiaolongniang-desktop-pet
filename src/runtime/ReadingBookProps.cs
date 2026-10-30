using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
namespace WhiteDragon {
// Frozen vector scenery; the contact stack remains inside the source clip.
public sealed class ReadingBookProps {
 public readonly Image Layer=new Image{IsHitTestVisible=false,Opacity=0,Visibility=Visibility.Collapsed,Stretch=Stretch.Fill};
 public readonly Image FrontLayer=new Image{IsHitTestVisible=false,Opacity=0,Visibility=Visibility.Collapsed,Stretch=Stretch.Fill};
 long last=-1;
 static Brush Ink(string color){var b=new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));b.Freeze();return b;}
 static Pen Line(string color,double width){var p=new Pen(Ink(color),width);p.Freeze();return p;}
 static Geometry Shape(params Point[] points){var g=new StreamGeometry();using(var c=g.Open()){c.BeginFigure(points[0],true,true);for(int i=1;i<points.Length;i++)c.LineTo(points[i],true,false);}g.Freeze();return g;}
 static void Book(DrawingContext d,double x,double y,double width,double height,double tilt,string cover){
  d.PushTransform(new RotateTransform(tilt,x+width/2,y+height/2));
  d.DrawRoundedRectangle(Ink(cover),Line("#78648D",.9),new Rect(x,y,width,height),1.1,1.1);
  d.DrawGeometry(Ink("#E0D4EC"),Line("#9D88AF",.55),Shape(new Point(x+.8,y+.6),new Point(x+3.5,y-1.1),new Point(x+width-1,y-1.1),new Point(x+width-.4,y+.6)));
  d.DrawRoundedRectangle(Ink("#F7F0E4"),Line("#B8A4BE",.55),new Rect(x+4.2,y+1.7,width-5.6,height-3),.55,.55);
  d.DrawLine(Line("#D6C8D2",.45),new Point(x+6,y+height*.53),new Point(x+width-2,y+height*.53));
  d.DrawLine(Line("#EEE3F6",.7),new Point(x+1.5,y+height-1),new Point(x+width-1,y+height-1));
  d.DrawLine(Line("#A18AAF",.65),new Point(x+3,y+1.2),new Point(x+3,y+height-1.2));d.Pop();
 }
 public ReadingBookProps(){
  var back=new DrawingGroup();var front=new DrawingGroup();
  using(var d=back.Open()){
   d.DrawRectangle(Brushes.Transparent,null,new Rect(0,0,640,360));
   d.DrawEllipse(Ink("#187C688C"),null,new Point(400,330),25,2.1);
   Book(d,376,322,47,7.4,-.7,"#BBA7D1");Book(d,378,314.6,44,7.5,.9,"#B2B0CD");Book(d,375.6,307.2,47,7.4,-.5,"#D2BCDE");Book(d,378,299.9,43,7.3,1,"#BEB0D8");Book(d,377.1,292.8,44,7.2,-1.1,"#D7C8E7");
  }
  using(var d=front.Open()){
   d.DrawRectangle(Brushes.Transparent,null,new Rect(0,0,640,360));
   d.DrawEllipse(Ink("#197C688C"),null,new Point(381,334),25,1.8);
   Book(d,357.5,326,47,7.1,.6,"#C1AED8");Book(d,360,318.9,43,7.2,-1.4,"#D4C5E4");
  }
  back.Freeze();front.Freeze();var a=new DrawingImage(back);a.Freeze();Layer.Source=a;var b=new DrawingImage(front);b.Freeze();FrontLayer.Source=b;
 }
 public void Place(double width,double padding){foreach(var layer in new[]{Layer,FrontLayer}){layer.Width=width;layer.Height=width*360/640;Canvas.SetLeft(layer,padding);Canvas.SetTop(layer,padding);}}
 public void Update(long now,bool active,bool frozen){double elapsed=last<0?0:Math.Max(0,Math.Min(60,now-last));last=now;if(frozen)return;Layer.Opacity=active?Math.Min(1,Layer.Opacity+elapsed/160):Math.Max(0,Layer.Opacity-elapsed/160);Layer.Visibility=Layer.Opacity>0?Visibility.Visible:Visibility.Collapsed;FrontLayer.Opacity=Layer.Opacity;FrontLayer.Visibility=Layer.Visibility;}
}
}
