using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace WhiteDragon {
// Blend premultiplied pixels once, rather than stacking two translucent pets.
// Buffers are reused, including when a new action interrupts a transition.
public sealed class FrameTransition {
#if STYLE_V10
    const int Width=640,Height=360,Stride=Width*4;
#else
    const int Width=384,Height=416,Stride=Width*4;
#endif
    readonly byte[] from=new byte[Stride*Height];
    readonly byte[] targetPixels=new byte[Stride*Height];
    readonly byte[] pixels=new byte[Stride*Height];
    readonly WriteableBitmap bitmap=new WriteableBitmap(Width,Height,96,96,PixelFormats.Pbgra32,null);
    int duration;
    FlowField sourceFlow,targetFlow,between;
    readonly float[] mapA=new float[81*46*2],mapB=new float[81*46*2];
    bool warped;int left,top,right,bottom;
    readonly float[] sourceDistance=new float[Width*Height],targetDistance=new float[Width*Height],distanceScratch=new float[Width*Height];
    double protectedHeadBottom=225;
    public bool RecordDiagnostics;
    public long BodySoftPixelsBefore{get;private set;}public long BodySoftPixelsAfter{get;private set;}
    public bool UsesWarp{get{return warped;}}
    public int Weight{get;private set;}
    public double MaxBlendMilliseconds{get;private set;}
    public void Begin(BitmapSource source,int milliseconds) {
        source.CopyPixels(from,Stride,0);duration=milliseconds;Weight=-1;warped=false;
    }
    public bool BeginAligned(BitmapSource source,byte[] target,FlowField a,FlowField b,int milliseconds){
        source.CopyPixels(from,Stride,0);Buffer.BlockCopy(target,0,targetPixels,0,targetPixels.Length);duration=milliseconds;Weight=-1;sourceFlow=a;targetFlow=b;warped=a!=null&&b!=null;
        long difference=0;int painted=0;left=Width;top=Height;right=bottom=0;
        for(int y=0;y<Height;y++)for(int x=0;x<Width;x++){int p=(y*Width+x)*4;if(from[p+3]>0||target[p+3]>0){left=Math.Min(left,x);right=Math.Max(right,x);top=Math.Min(top,y);bottom=Math.Max(bottom,y);}if(from[p+3]>12||target[p+3]>12){painted++;for(int c=0;c<4;c++)difference+=Math.Abs(from[p+c]-target[p+c]);}}
        if(painted>0&&difference/(double)(painted*4)<2.0){warped=false;Weight=256;return false;}
        left=Math.Max(0,left-16);right=Math.Min(Width,right+17);top=Math.Max(0,top-16);bottom=Math.Min(Height,bottom+17);
        if(warped){
            DistanceField(from,sourceDistance);DistanceField(targetPixels,targetDistance);
            double headX,sourceChin,targetX,targetChin;a.At(320,210,2,out headX,out sourceChin);b.At(320,210,2,out targetX,out targetChin);protectedHeadBottom=Math.Max(215,Math.Min(290,Math.Max(210+sourceChin,210+targetChin)+12));
            between=new FlowField();RebuildBetween();}return true;
    }
    void RebuildBetween(){var a=sourceFlow;var b=targetFlow;for(int gy=0;gy<46;gy++)for(int gx=0;gx<81;gx++){
            double x=gx*8,y=gy*8,ax,ay,bx,by;a.At(x,y,0,out ax,out ay);b.At(x+ax,y+ay,2,out bx,out by);int p=(gy*81+gx)*4;between.Data[p]=(float)(ax+bx);between.Data[p+1]=(float)(ay+by);
            b.At(x,y,0,out bx,out by);a.At(x+bx,y+by,2,out ax,out ay);between.Data[p+2]=(float)(ax+bx);between.Data[p+3]=(float)(ay+by);
        }
    }
    // The incoming clip keeps moving during a bridge. Update both its pixels
    // and its correspondence, rather than restarting its first pose afterward.
    public void FollowIncoming(byte[] target,FlowField flow){
        if(!warped)return;Buffer.BlockCopy(target,0,targetPixels,0,targetPixels.Length);
        for(int y=0;y<Height;y++)for(int x=0;x<Width;x++)if(targetPixels[(y*Width+x)*4+3]>0){left=Math.Min(left,Math.Max(0,x-16));right=Math.Max(right,Math.Min(Width,x+17));top=Math.Min(top,Math.Max(0,y-16));bottom=Math.Max(bottom,Math.Min(Height,y+17));}
        DistanceField(targetPixels,targetDistance);if(flow!=null){targetFlow=flow;RebuildBetween();}
    }
    void DistanceField(byte[] image,float[] output){
        // The padded union contains all nontransparent pixels. Distance
        // propagation only needs this region; clear distant background once.
        Array.Clear(output,0,output.Length);for(int i=0;i<distanceScratch.Length;i++)distanceScratch[i]=64;
        for(int y=top;y<bottom;y++)for(int x=left;x<right;x++){int i=y*Width+x;bool inside=image[i*4+3]>=128;output[i]=inside?64:0;distanceScratch[i]=inside?0:64;}
        Chamfer(output);Chamfer(distanceScratch);
        for(int i=0;i<output.Length;i++)output[i]=Math.Max(-64,Math.Min(64,output[i]-distanceScratch[i]))+image[i*4+3]/255f-.5f;
    }
    void Chamfer(float[] d){
        for(int y=top;y<bottom;y++)for(int x=left;x<right;x++){int p=y*Width+x;float v=d[p];if(x>0)v=Math.Min(v,d[p-1]+1);if(y>0){v=Math.Min(v,d[p-Width]+1);if(x>0)v=Math.Min(v,d[p-Width-1]+1.414214f);if(x+1<Width)v=Math.Min(v,d[p-Width+1]+1.414214f);}d[p]=v;}
        for(int y=bottom-1;y>=top;y--)for(int x=right-1;x>=left;x--){int p=y*Width+x;float v=d[p];if(x+1<Width)v=Math.Min(v,d[p+1]+1);if(y+1<Height){v=Math.Min(v,d[p+Width]+1);if(x>0)v=Math.Min(v,d[p+Width-1]+1.414214f);if(x+1<Width)v=Math.Min(v,d[p+Width+1]+1.414214f);}d[p]=v;}
    }
    static double Ease(double u){u=Math.Max(0,Math.Min(1,u));return u*u*(3-2*u);}
    public FlowField SnapshotFlow(){
        if(!warped||sourceFlow==null||targetFlow==null)return targetFlow;
        double u=Math.Max(0,Weight)/256.0;var result=new FlowField();
        for(int gy=0;gy<46;gy++)for(int gx=0;gx<81;gx++){
            double x=gx*8,y=gy*8,px=x,py=y,rx=x,ry=y,dx,dy;
            for(int n=0;n<3;n++){between.At(px,py,0,out dx,out dy);px=x-u*dx;py=y-u*dy;between.At(rx,ry,2,out dx,out dy);rx=x-(1-u)*dx;ry=y-(1-u)*dy;}
            double ax,ay,bx,by;sourceFlow.At(px,py,0,out ax,out ay);targetFlow.At(rx,ry,0,out bx,out by);int p=(gy*81+gx)*4;
            result.Data[p]=(float)((px+ax-x)*(1-u)+(rx+bx-x)*u);result.Data[p+1]=(float)((py+ay-y)*(1-u)+(ry+by-y)*u);
            sourceFlow.At(x,y,2,out ax,out ay);targetFlow.At(x,y,2,out bx,out by);double sx=x+ax,sy=y+ay,tx=x+bx,ty=y+by;
            between.At(sx,sy,0,out dx,out dy);sx+=u*dx;sy+=u*dy;between.At(tx,ty,2,out dx,out dy);tx+=(1-u)*dx;ty+=(1-u)*dy;
            result.Data[p+2]=(float)(sx*(1-u)+tx*u-x);result.Data[p+3]=(float)(sy*(1-u)+ty*u-y);
        }return result;
    }
    public BitmapSource Render(BitmapSource target,double elapsed) {
        if(elapsed>=duration){Weight=256;return target;}
        target.CopyPixels(targetPixels,Stride,0);
        return PresentPixels(targetPixels,elapsed,true);
    }
    public BitmapSource PresentPixels(byte[] target,double elapsed,bool blend) {
        var watch=Stopwatch.StartNew();
        if(!blend||elapsed>=duration){bitmap.WritePixels(new Int32Rect(0,0,Width,Height),target,Stride,0);Weight=256;return bitmap;}
        if(elapsed<=0){bitmap.WritePixels(new Int32Rect(0,0,Width,Height),from,Stride,0);Weight=0;return bitmap;}
        double u=Math.Max(0,Math.Min(1,elapsed/duration));
        int weight=(int)Math.Round(256*u*u*(3-2*u)),inverse=256-weight;
        if(warped)Warp(weight/256.0,weight,inverse);
        else for(int i=0;i<pixels.Length;i++)pixels[i]=(byte)((from[i]*inverse+target[i]*weight+128)>>8);
        bitmap.WritePixels(new Int32Rect(0,0,Width,Height),pixels,Stride,0);
        Weight=weight;MaxBlendMilliseconds=Math.Max(MaxBlendMilliseconds,watch.Elapsed.TotalMilliseconds);
        return bitmap;
    }
    void Warp(double u,int weight,int inverse){
        Array.Clear(pixels,0,pixels.Length);
        // Geometry moves throughout the bridge. Retain one appearance until
        // the silhouettes are close, then exchange the fine texture. This
        // avoids two sets of eyes, hands and outlines for the whole duration.
        int texture=(int)Math.Round(Ease((u-.55)/.45)*256),oldTexture=256-texture;
        double contourStrength=Ease(Math.Min(u/.12,(1-u)/.12));
        for(int gy=0;gy<46;gy++)for(int gx=0;gx<81;gx++){
            double x=gx*8,y=gy*8,ax=x,ay=y,bx=x,by=y,dx,dy;
            for(int n=0;n<3;n++){between.At(ax,ay,0,out dx,out dy);ax=x-u*dx;ay=y-u*dy;between.At(bx,by,2,out dx,out dy);bx=x-(1-u)*dx;by=y-(1-u)*dy;}
            int p=(gy*81+gx)*2;mapA[p]=(float)ax;mapA[p+1]=(float)ay;mapB[p]=(float)bx;mapB[p+1]=(float)by;
        }
        for(int y=top;y<bottom;y++){
            int gy=y/8;double fy=(y%8)/8.0;
            for(int x0=(left/8)*8;x0<right;x0+=8){
                int gx=x0/8,p=(gy*81+gx)*2,q=p+81*2;double ax=mapA[p]*(1-fy)+mapA[q]*fy,ay=mapA[p+1]*(1-fy)+mapA[q+1]*fy,bx=mapB[p]*(1-fy)+mapB[q]*fy,by=mapB[p+1]*(1-fy)+mapB[q+1]*fy;
                double dax=(mapA[p+2]*(1-fy)+mapA[q+2]*fy-ax)/8,day=(mapA[p+3]*(1-fy)+mapA[q+3]*fy-ay)/8,dbx=(mapB[p+2]*(1-fy)+mapB[q+2]*fy-bx)/8,dby=(mapB[p+3]*(1-fy)+mapB[q+3]*fy-by)/8;
                for(int x=x0;x<Math.Min(right,x0+8);x++,ax+=dax,ay+=day,bx+=dbx,by+=dby){if(x<left)continue;int at=(y*Width+x)*4;int ia=(int)Math.Floor(ax),ja=(int)Math.Floor(ay),ib=(int)Math.Floor(bx),jb=(int)Math.Floor(by);
                    int fa=(int)((ax-ia)*256),ga=(int)((ay-ja)*256),fb=(int)((bx-ib)*256),gb=(int)((by-jb)*256);
                    bool va=ia>=0&&ia<Width-1&&ja>=0&&ja<Height-1,vb=ib>=0&&ib<Width-1&&jb>=0&&jb<Height-1;int pa=(ja*Width+ia)*4,pb=(jb*Width+ib)*4;
                    int a00=(256-fa)*(256-ga),a10=fa*(256-ga),a01=(256-fa)*ga,a11=fa*ga,b00=(256-fb)*(256-gb),b10=fb*(256-gb),b01=(256-fb)*gb,b11=fb*gb;
                    int aa=va?(from[pa+3]*a00+from[pa+7]*a10+from[pa+Stride+3]*a01+from[pa+Stride+7]*a11+32768)>>16:0,bb=vb?(targetPixels[pb+3]*b00+targetPixels[pb+7]*b10+targetPixels[pb+Stride+3]*b01+targetPixels[pb+Stride+7]*b11+32768)>>16:0;
                    int mixAlpha=(aa*oldTexture+bb*texture+128)>>8,newAlpha=mixAlpha;
                    double body=Ease((y-protectedHeadBottom)/22.0)*contourStrength;
                    if(body>0){
                        int da=ja*Width+ia,db=jb*Width+ib;
                        double sa=va?(sourceDistance[da]*a00+sourceDistance[da+1]*a10+sourceDistance[da+Width]*a01+sourceDistance[da+Width+1]*a11)/65536.0:-64;
                        double sb=vb?(targetDistance[db]*b00+targetDistance[db+1]*b10+targetDistance[db+Width]*b01+targetDistance[db+Width+1]*b11)/65536.0:-64;
                        double coverage=Math.Max(0,Math.Min(1,.5+sa*(1-u)+sb*u))*255;newAlpha=(int)Math.Round(mixAlpha*(1-body)+coverage*body);
                    }
                    for(int c=0;c<3;c++){int av=va?(from[pa+c]*a00+from[pa+4+c]*a10+from[pa+Stride+c]*a01+from[pa+Stride+4+c]*a11+32768)>>16:0,bv=vb?(targetPixels[pb+c]*b00+targetPixels[pb+4+c]*b10+targetPixels[pb+Stride+c]*b01+targetPixels[pb+Stride+4+c]*b11+32768)>>16:0;int mixed=(av*oldTexture+bv*texture+128)>>8;int color=mixAlpha>0?(mixed*newAlpha+mixAlpha/2)/mixAlpha:bb>0?(bv*newAlpha+bb/2)/bb:0;pixels[at+c]=(byte)Math.Max(0,Math.Min(newAlpha,color));}
                    pixels[at+3]=(byte)newAlpha;
                    if(RecordDiagnostics&&body>.9){int oldAlpha=(aa*inverse+bb*weight+128)>>8;if(oldAlpha>32&&oldAlpha<220)BodySoftPixelsBefore++;if(newAlpha>32&&newAlpha<220)BodySoftPixelsAfter++;}
                }
            }
        }
    }
}
}
