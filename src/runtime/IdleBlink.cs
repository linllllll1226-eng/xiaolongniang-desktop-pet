using System;
using System.IO;
using System.Reflection;
using System.Collections.Generic;
using System.Web.Script.Serialization;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace WhiteDragon {
public sealed class BlinkSpec {
    public int x,y,width,height,count,minIntervalMs,maxIntervalMs,minDurationMs,maxDurationMs;
}
// Advances only by decoded, unfrozen idle time. The five-second body loop
// does not reset this clock or force a blink at its playback boundary.
public sealed class BlinkSchedule {
    readonly Random random;readonly int count,minInterval,maxInterval,minDuration,maxDuration;
    bool wasIdle,blinking;double remaining,phase,duration,elapsedIdle,scheduledInterval;
    public int Frame{get;private set;}
    public int Started{get;private set;}public int Completed{get;private set;}
    public double NextInMilliseconds{get{return remaining;}}
    public readonly List<double[]> History=new List<double[]>();
    public BlinkSchedule(int seed,int frames,int minimum,int maximum,int shortest,int longest){
        random=seed<0?new Random():new Random(seed);count=frames;minInterval=minimum;maxInterval=maximum;minDuration=shortest;maxDuration=longest;Frame=-1;
    }
    double Interval(){return minInterval+random.NextDouble()*(maxInterval-minInterval);}
    public void Advance(double delta,bool idle,bool restart){
        if(!idle){wasIdle=false;blinking=false;Frame=-1;return;}
        if(!wasIdle||restart){wasIdle=true;blinking=false;Frame=-1;remaining=scheduledInterval=Interval();phase=0;}
        delta=Math.Max(0,delta);elapsedIdle+=delta;
        if(!blinking){remaining-=delta;if(remaining>0){Frame=-1;return;}
            phase=-remaining;blinking=true;duration=minDuration+random.NextDouble()*(maxDuration-minDuration);Started++;
            if(History.Count<2000)History.Add(new double[]{elapsedIdle-phase,scheduledInterval,duration});
        }else phase+=delta;
        if(phase>=duration){blinking=false;Frame=-1;Completed++;remaining=scheduledInterval=Interval();return;}
        Frame=Math.Min(count-1,(int)(phase/duration*count));
    }
    public void Force(){if(wasIdle&&!blinking)remaining=0;}
}
public sealed class IdleBlink {
    readonly BlinkSpec spec;readonly byte[][] frames;
    public readonly BlinkSchedule Schedule;
    public IdleBlink(){
        var assembly=Assembly.GetExecutingAssembly();
        using(var s=assembly.GetManifestResourceStream("WhiteDragon.BlinkSpec"))using(var r=new StreamReader(s))spec=new JavaScriptSerializer().Deserialize<BlinkSpec>(r.ReadToEnd());
        frames=new byte[spec.count][];
        using(var s=assembly.GetManifestResourceStream("WhiteDragon.BlinkAtlas")){
            var source=BitmapFrame.Create(s,BitmapCreateOptions.PreservePixelFormat,BitmapCacheOption.OnLoad);
            var bitmap=new FormatConvertedBitmap(source,PixelFormats.Pbgra32,null,0);
            if(bitmap.PixelWidth!=spec.width*spec.count||bitmap.PixelHeight!=spec.height)throw new InvalidDataException("Invalid eye atlas");
            for(int n=0;n<spec.count;n++){frames[n]=new byte[spec.width*spec.height*4];bitmap.CopyPixels(new System.Windows.Int32Rect(n*spec.width,0,spec.width,spec.height),frames[n],spec.width*4,0);}
        }
        Schedule=new BlinkSchedule(-1,spec.count,spec.minIntervalMs,spec.maxIntervalMs,spec.minDurationMs,spec.maxDurationMs);
    }
    public void Apply(byte[] pixels,int frame){
        if(frame<0)return;byte[] patch=frames[frame];
        for(int y=0;y<spec.height;y++)for(int x=0;x<spec.width;x++){
            int s=(y*spec.width+x)*4,d=((y+spec.y)*640+x+spec.x)*4,a=patch[s+3];if(a==0)continue;
            for(int c=0;c<4;c++)pixels[d+c]=(byte)Math.Min(255,patch[s+c]+(pixels[d+c]*(255-a)+127)/255);
        }
    }
}
}
