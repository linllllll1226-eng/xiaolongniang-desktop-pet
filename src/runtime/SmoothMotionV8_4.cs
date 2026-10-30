using System;
using System.IO;
using System.IO.Compression;
using System.Threading;
using System.Diagnostics;
using System.Reflection;
using System.Collections.Generic;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace WhiteDragon {
public sealed class SmoothRowInfo {public int row,count,duration,unique,stored_count;public int[] map;public bool loop,idleTail,readingTail;public string storage,name;}
public sealed class SmoothMotionSpec {public Dictionary<string,SmoothRowInfo> rows;}
public sealed class SmoothMotion:IDisposable {
#if STYLE_V10
    const int FrameWidth=640,FrameHeight=360;
#else
    const int FrameWidth=384,FrameHeight=416;
#endif
    // The larger 2D canvas needs enough decoded lead to absorb an occasional
    // slow resource read. The same 32-frame row bound still caps pixel memory.
#if STYLE_V10
    const int LookAhead=24;
#else
    const int LookAhead=12;
#endif
    const int RowLimit=2,FrameLimit=32,FrontSamples=3,FrontLimit=64,FrameBytes=FrameWidth*FrameHeight*4;
    sealed class CachedFrame {public BitmapSource Image;public byte[] Pixels;public long Used;}
    sealed class Row {
        public int Number,Requested,LastSample=-1,SampleStep=1;public BitmapSource[] Frames;public long Used,Bytes;
#if WINDOW_PERCH
        public int Direction=1;
#endif
        public bool Loading,Failed;public int[] Offsets,Lengths;
        public readonly Dictionary<int,CachedFrame> Stream=new Dictionary<int,CachedFrame>();
        public BitmapSource Last;
    }
    readonly object gate=new object();
    readonly Dictionary<int,Row> cache=new Dictionary<int,Row>();
    readonly Stack<byte[]> pixelPool=new Stack<byte[]>();
    int allocatedPixelBuffers;
    // Opening poses share frozen images with the streaming cache and survive row eviction.
    readonly Dictionary<int,Dictionary<int,CachedFrame>> fronts=new Dictionary<int,Dictionary<int,CachedFrame>>();
    struct FrameTimings {public double Open,Inflate,Create,Freeze;}
    readonly SmoothMotionSpec spec;
    readonly AutoResetEvent wake=new AutoResetEvent(false);
    readonly Thread worker;
    // One worker owns this scratch buffer. BitmapSource.Create takes a copy.
    readonly byte[] framePixels=new byte[FrameBytes];
    sealed class FrameStream:Stream {
        readonly Stream source;long remaining;
        public FrameStream(Stream source,int length){this.source=source;remaining=length;}
        public override int Read(byte[] buffer,int offset,int count){int n=source.Read(buffer,offset,(int)Math.Min(count,remaining));remaining-=n;return n;}
        public override int ReadByte(){if(remaining<=0)return -1;int n=source.ReadByte();if(n>=0)remaining--;return n;}
        public override bool CanRead{get{return true;}}public override bool CanSeek{get{return false;}}public override bool CanWrite{get{return false;}}
        public override long Length{get{throw new NotSupportedException();}}public override long Position{get{throw new NotSupportedException();}set{throw new NotSupportedException();}}
        public override void Flush(){}public override long Seek(long offset,SeekOrigin origin){throw new NotSupportedException();}public override void SetLength(long length){throw new NotSupportedException();}public override void Write(byte[] buffer,int offset,int count){throw new NotSupportedException();}
    }
    long serial;bool stopped,wakeClosed;int activeRow=-1,decodeCount,discardedDecodes,streamRequests,streamMisses;
    double maxDecodeMs,maxFrameDecodeMs,maxOpenMs,maxInflateMs,maxCreateMs,maxFreezeMs,maxPublishWaitMs;
    public int SampleIndex{get;private set;}
    public int PresentedSampleIndex{get;private set;}
    public string LastError{get;private set;}
    public bool WorkerStopped{get{return !worker.IsAlive;}}
    public bool DisposeTimedOut{get;private set;}
    public int CachedRows{get{lock(gate)return cache.Count;}}
    public int CachedFrontFrames{get{lock(gate)return FrontCount();}}
    // Bitmap pixels only: WPF/assembly allocations and delayed native reclamation are extra.
    public long StreamingPixelBudget{get{return (RowLimit*FrameLimit+FrontLimit+RowLimit)*(long)FrameBytes;}}
    public long AllocatedPixelPoolBytes{get{lock(gate)return allocatedPixelBuffers*(long)FrameBytes;}}
    public long CachedPixelBytes{get{lock(gate){long n=0;var seen=new HashSet<CachedFrame>();var images=new HashSet<BitmapSource>();foreach(var r in cache.Values){n+=r.Bytes;foreach(var f in r.Stream.Values)if(seen.Add(f)){n+=FrameBytes;if(f.Image!=null&&images.Add(f.Image))n+=FrameBytes;}if(r.Last!=null&&images.Add(r.Last))n+=FrameBytes;}foreach(var row in fronts.Values)foreach(var f in row.Values)if(seen.Add(f)){n+=FrameBytes;if(f.Image!=null&&images.Add(f.Image))n+=FrameBytes;}return n;}}}
    public int CachedStreamFrames{get{lock(gate){int n=0;foreach(var r in cache.Values)n+=r.Stream.Count;return n;}}}
    public int DecodeCount{get{lock(gate)return decodeCount;}}
    public int DiscardedDecodes{get{lock(gate)return discardedDecodes;}}
    public int StreamRequests{get{lock(gate)return streamRequests;}}
    public int StreamMisses{get{lock(gate)return streamMisses;}}
    public double MaxDecodeMilliseconds{get{lock(gate)return maxDecodeMs;}}
    public double MaxFrameDecodeMilliseconds{get{lock(gate)return maxFrameDecodeMs;}}
    public double MaxResourceOpenMilliseconds{get{lock(gate)return maxOpenMs;}}
    public double MaxInflateMilliseconds{get{lock(gate)return maxInflateMs;}}
    public double MaxBitmapCreateMilliseconds{get{lock(gate)return maxCreateMs;}}
    public double MaxBitmapFreezeMilliseconds{get{lock(gate)return maxFreezeMs;}}
    public double MaxPublishWaitMilliseconds{get{lock(gate)return maxPublishWaitMs;}}
    public Dictionary<int,int> MediaDurations{get{var result=new Dictionary<int,int>();foreach(var item in spec.rows)result.Add(int.Parse(item.Key),item.Value.duration);return result;}}
    public string ActionName(int row){return spec.rows[row.ToString()].name;}
    public bool HasMovingIdleTail(int row){SmoothRowInfo meta;return spec.rows.TryGetValue(row.ToString(),out meta)&&meta.idleTail;}
    public bool HasMovingReadingTail(int row){SmoothRowInfo meta;return spec.rows.TryGetValue(row.ToString(),out meta)&&meta.readingTail;}
    public SmoothMotion(){
        PresentedSampleIndex=-1;
        using(var s=Assembly.GetExecutingAssembly().GetManifestResourceStream("WhiteDragon.MotionSpec"))using(var r=new StreamReader(s))spec=new JavaScriptSerializer().Deserialize<SmoothMotionSpec>(r.ReadToEnd());
        worker=new Thread(DecodeLoop){IsBackground=true,Name="WhiteDragon bounded frame decoder",Priority=ThreadPriority.Normal};worker.Start();
    }
    bool Streaming(int row){return spec.rows[row.ToString()].storage=="gzip-pbgra";}
    void Signal(){if(!wakeClosed)wake.Set();}
    Row Request(int row,bool displayed){
        if(stopped||!spec.rows.ContainsKey(row.ToString()))return null;
        if(displayed)activeRow=row;
        Row entry;
        if(cache.TryGetValue(row,out entry)){if(displayed)entry.Used=++serial;return entry;}
        if(cache.Count>=RowLimit){int victim=-1;long oldest=long.MaxValue;foreach(var kv in cache)if(kv.Key!=activeRow&&kv.Value.Used<oldest){victim=kv.Key;oldest=kv.Value.Used;}if(victim<0){foreach(var kv in cache)if(kv.Value.Used<oldest){victim=kv.Key;oldest=kv.Value.Used;}}Row retired=cache[victim];cache.Remove(victim);foreach(var frame in retired.Stream.Values)Recycle(frame);}
        entry=new Row{Number=row,Used=++serial};cache[row]=entry;Signal();return entry;
    }
    public void Warm(int row){lock(gate)Request(row,false);}
    public void WarmFrom(int row,double time){WarmForward(row,time,1);}
    // A 2.6x recovery only displays about half of the 60Hz source samples.
    // Decode that same bounded window at a stable even-sample phase, instead
    // of spending the worker budget on frames that cannot be presented.
    public void WarmRecovery(int row,double time){WarmForward(row,time,2);}
    void WarmForward(int row,double time,int step){lock(gate){Row entry=Request(row,false);if(entry==null)return;
        if(entry.SampleStep!=step){entry.SampleStep=step;Signal();}
#if WINDOW_PERCH
        if(entry.Direction!=1){entry.Direction=1;Signal();}
#endif
        int sample=Index(spec.rows[row.ToString()],time);if(entry.Requested!=sample){entry.Requested=sample;Signal();}}}
#if WINDOW_PERCH
    public void WarmDirected(int row,double time,int direction){lock(gate){Row entry=Request(row,false);if(entry==null)return;bool resetStep=entry.SampleStep!=1;entry.SampleStep=1;int sample=Index(spec.rows[row.ToString()],time);direction=direction<0?-1:1;if(resetStep||entry.Requested!=sample||entry.Direction!=direction){entry.Requested=sample;entry.Direction=direction;Signal();}}}
    int DirectedAhead(SmoothRowInfo meta,Row row,int amount){int sample=row.Requested+row.Direction*amount*row.SampleStep;return meta.loop?(sample%meta.count+meta.count)%meta.count:Math.Max(0,Math.Min(meta.count-1,sample));}
#endif
    // The final non-looping pose owns a full duration/count interval before the behavior changes rows.
    int Index(SmoothRowInfo meta,double time){double unit=Double.IsNaN(time)?0:Math.Max(0,Math.Min(1,time/Math.Max(1,meta.duration)));int sample=Math.Min(meta.count-1,(int)Math.Floor(unit*meta.count+.00001));Row row;if(sample<meta.count-1&&cache.TryGetValue(meta.row,out row)&&row.SampleStep>1)sample=sample/row.SampleStep*row.SampleStep;return sample;}
    int Stored(SmoothRowInfo meta,int sample){return meta.map==null?sample:meta.map[sample];}
    int Ahead(SmoothRowInfo meta,int sample,int amount){return meta.loop?(sample+amount)%meta.count:Math.Min(meta.count-1,sample+amount);}
    int RequestedAhead(SmoothRowInfo meta,Row row,int amount){
#if WINDOW_PERCH
        return DirectedAhead(meta,row,amount);
#else
        return Ahead(meta,row.Requested,amount*row.SampleStep);
#endif
    }
    public int GetSampleIndex(int row,double time){lock(gate){SmoothRowInfo meta;return spec.rows.TryGetValue(row.ToString(),out meta)?Index(meta,time):-1;}}
    bool Find(Row row,int stored,out CachedFrame frame){if(row.Stream.TryGetValue(stored,out frame))return true;Dictionary<int,CachedFrame> head;return fronts.TryGetValue(row.Number,out head)&&head.TryGetValue(stored,out frame);}
    bool Ready(Row entry,SmoothRowInfo meta,int sample,int lookahead){if(entry.Failed)return false;if(!Streaming(entry.Number))return entry.Frames!=null;CachedFrame frame;for(int k=0;k<=lookahead;k++)if(!Find(entry,Stored(meta,Ahead(meta,sample,k*entry.SampleStep)),out frame))return false;return true;}
    // Nonblocking; lookahead=2 checks this and the next two samples. It does not move the decoder cursor.
    public bool IsReady(int row,double time,int lookahead){lock(gate){Row entry;SmoothRowInfo meta;if(stopped||!cache.TryGetValue(row,out entry)||!spec.rows.TryGetValue(row.ToString(),out meta))return false;return Ready(entry,meta,Index(meta,time),Math.Max(0,Math.Min(LookAhead,lookahead)));}}
    public bool EnsureReady(int row,int timeout){return EnsureSample(row,0,timeout);}
    public bool EnsureSample(int row,double time,int timeout){
        var clock=Stopwatch.StartNew();lock(gate){Row entry=Request(row,true);if(entry==null)return false;var meta=spec.rows[row.ToString()];int sample=Index(meta,time);entry.Requested=sample;Signal();
            while(!stopped&&!entry.Failed&&!Ready(entry,meta,sample,0)){
                Row current;if(!cache.TryGetValue(row,out current)||!Object.ReferenceEquals(current,entry))return false;
                int remaining=timeout-(int)clock.ElapsedMilliseconds;if(remaining<=0)return false;Monitor.Wait(gate,remaining);
            }
            return !stopped&&!entry.Failed&&Ready(entry,meta,sample,0);
        }
    }
    public bool HasFailed(int row){lock(gate){Row entry;return cache.TryGetValue(row,out entry)&&entry.Failed;}}
    BitmapSource Present(Row row,int sample,CachedFrame frame){
        // Legacy snapshots are created only for seek/export APIs. The live
        // renderer copies raw pixels into one UI-owned WriteableBitmap, so a
        // new WIC bitmap no longer allocates native memory on every video frame.
        if(frame.Image==null){frame.Image=BitmapSource.Create(FrameWidth,FrameHeight,96,96,PixelFormats.Pbgra32,null,frame.Pixels,FrameWidth*4);frame.Image.Freeze();}
        frame.Used=++serial;row.Last=frame.Image;row.LastSample=sample;PresentedSampleIndex=sample;return frame.Image;
    }
    public bool TryCopyExact(int row,double time,byte[] destination){
        if(destination==null||destination.Length!=FrameBytes)throw new ArgumentException("Wrong frame buffer");
        lock(gate){PresentedSampleIndex=-1;SmoothRowInfo meta;if(!spec.rows.TryGetValue(row.ToString(),out meta))return false;Row entry=Request(row,true);if(entry==null)return false;
            SampleIndex=Index(meta,time);if(entry.Requested!=SampleIndex){entry.Requested=SampleIndex;Signal();}streamRequests++;
            if(!Streaming(row)){if(entry.Frames==null){streamMisses++;return false;}entry.Frames[Stored(meta,SampleIndex)].CopyPixels(destination,FrameWidth*4,0);entry.LastSample=PresentedSampleIndex=SampleIndex;return true;}
            CachedFrame frame;if(!Find(entry,Stored(meta,SampleIndex),out frame)){streamMisses++;return false;}
            Buffer.BlockCopy(frame.Pixels,0,destination,0,FrameBytes);frame.Used=++serial;entry.LastSample=PresentedSampleIndex=SampleIndex;return true;
        }
    }
    byte[] RentPixels(){lock(gate){if(pixelPool.Count>0)return pixelPool.Pop();allocatedPixelBuffers++;return new byte[FrameBytes];}}
    void Recycle(CachedFrame frame){
        if(frame.Pixels==null)return;
        foreach(var head in fronts.Values)foreach(var item in head.Values)if(Object.ReferenceEquals(frame,item))return;
        foreach(var row in cache.Values)foreach(var item in row.Stream.Values)if(Object.ReferenceEquals(frame,item))return;
        pixelPool.Push(frame.Pixels);frame.Pixels=null;
    }
    public bool TryGetExact(int row,double time,out BitmapSource image){lock(gate){image=null;PresentedSampleIndex=-1;SmoothRowInfo meta;if(!spec.rows.TryGetValue(row.ToString(),out meta))return false;Row data=Request(row,true);SampleIndex=Index(meta,time);if(data==null)return false;if(data.Requested!=SampleIndex){data.Requested=SampleIndex;Signal();}if(!Streaming(row)){if(data.Frames==null)return false;image=data.Frames[Stored(meta,SampleIndex)];data.Last=image;data.LastSample=PresentedSampleIndex=SampleIndex;return true;}streamRequests++;CachedFrame frame;if(Find(data,Stored(meta,SampleIndex),out frame)){image=Present(data,SampleIndex,frame);return true;}streamMisses++;return false;}}
    public BitmapSource Get(int row,double time){
        lock(gate){PresentedSampleIndex=-1;SmoothRowInfo meta;if(!spec.rows.TryGetValue(row.ToString(),out meta))return null;Row data=Request(row,true);
            SampleIndex=Index(meta,time);if(data==null)return null;
            if(!Streaming(row)){if(data.Frames==null)return null;data.Last=data.Frames[Stored(meta,SampleIndex)];data.LastSample=PresentedSampleIndex=SampleIndex;return data.Last;}
            if(data.Requested!=SampleIndex){data.Requested=SampleIndex;Signal();}
            streamRequests++;CachedFrame frame;
            if(Find(data,Stored(meta,SampleIndex),out frame))return Present(data,SampleIndex,frame);
            streamMisses++;
            // A late decoder can miss every exact sample as the timeline advances.
            // Display the closest available past sample so decoded progress is visible.
            // Future look-ahead frames remain hidden until their time is reached.
            for(int sample=SampleIndex-1;sample>=0;sample--){
                if(Find(data,Stored(meta,sample),out frame))return Present(data,sample,frame);
            }
            if(data.Last!=null)PresentedSampleIndex=data.LastSample;
            return data.Last; // Keep a decoded pose if no past sample remains cached.
        }
    }
    int Pending(Row r){
        if(r.Failed||r.Loading)return -1;
        if(!Streaming(r.Number))return r.Frames==null?0:-1;
        var meta=spec.rows[r.Number.ToString()];CachedFrame frame;
        for(int k=0;k<=LookAhead;k++){int i=Stored(meta,RequestedAhead(meta,r,k));if(!Find(r,i,out frame))return i;}
        return -1;
    }
    int FrontCount(){int count=0;foreach(var head in fronts.Values)count+=head.Count;return count;}
    void RememberFront(int row,int index,CachedFrame frame){
        var meta=spec.rows[row.ToString()];bool opening=false;for(int sample=0;sample<Math.Min(FrontSamples,meta.count);sample++)if(Stored(meta,sample)==index){opening=true;break;}if(!opening)return;
        Dictionary<int,CachedFrame> head;if(!fronts.TryGetValue(row,out head)){head=new Dictionary<int,CachedFrame>();fronts[row]=head;}if(head.ContainsKey(index))return;
        if(FrontCount()>=FrontLimit){int victimRow=-1,victimIndex=-1;long oldest=long.MaxValue;foreach(var pair in fronts)foreach(var f in pair.Value)if(f.Value.Used<oldest){oldest=f.Value.Used;victimRow=pair.Key;victimIndex=f.Key;}if(victimRow>=0){var removed=fronts[victimRow][victimIndex];fronts[victimRow].Remove(victimIndex);Recycle(removed);}}
        head[index]=frame;
    }
    byte[] DecodeFrame(Row entry,int index,out FrameTimings timings){
        timings=new FrameTimings();byte[] pixels=RentPixels();var phase=Stopwatch.StartNew();
        using(var s=Assembly.GetExecutingAssembly().GetManifestResourceStream("WhiteDragon.Motion"+entry.Number)){
            timings.Open=phase.Elapsed.TotalMilliseconds;
            if(s==null)throw new InvalidDataException("Missing streaming action");
            using(var reader=new BinaryReader(s)){
                if(entry.Offsets==null){
                    if(new string(reader.ReadChars(4))!="WDP1"||reader.ReadInt32()!=FrameWidth||reader.ReadInt32()!=FrameHeight)throw new InvalidDataException("Invalid frame pack header");
                    int count=reader.ReadInt32();if(count<1||count>2000)throw new InvalidDataException("Invalid frame count");
                    entry.Offsets=new int[count];entry.Lengths=new int[count];
                    for(int i=0;i<count;i++){entry.Offsets[i]=reader.ReadInt32();entry.Lengths[i]=reader.ReadInt32();if(entry.Offsets[i]<16+8*count||entry.Lengths[i]<1||(long)entry.Offsets[i]+entry.Lengths[i]>s.Length)throw new InvalidDataException("Invalid frame range");}
                }
                s.Position=entry.Offsets[index];phase.Restart();
                using(var slice=new FrameStream(s,entry.Lengths[index]))using(var gzip=new GZipStream(slice,CompressionMode.Decompress)){
                    int used=0,n;while(used<pixels.Length&&(n=gzip.Read(pixels,used,pixels.Length-used))>0)used+=n;
                    if(used!=pixels.Length||gzip.ReadByte()!=-1)throw new InvalidDataException("Invalid decoded frame length");
                }
                timings.Inflate=phase.Elapsed.TotalMilliseconds;
            }
        }
        BookStackConsistency.Apply(entry.Number,index,pixels);
        MotionColorGrade.Apply(entry.Number,index,pixels);
        return pixels;
    }
    BitmapSource[] DecodeAtlas(Row entry,out long bytes){
        var meta=spec.rows[entry.Number.ToString()];
        using(var s=Assembly.GetExecutingAssembly().GetManifestResourceStream("WhiteDragon.Motion"+entry.Number)){
            if(s==null)throw new InvalidDataException("Missing action resource");
            var png=BitmapFrame.Create(s,BitmapCreateOptions.PreservePixelFormat,BitmapCacheOption.OnLoad);
            var converted=new FormatConvertedBitmap(png,PixelFormats.Bgra32,null,0);int stride=converted.PixelWidth*4;
            var pixels=new byte[stride*converted.PixelHeight];converted.CopyPixels(pixels,stride,0);
            var bgra=BitmapSource.Create(converted.PixelWidth,converted.PixelHeight,96,96,PixelFormats.Bgra32,null,pixels,stride);bgra.Freeze();bytes=(long)stride*bgra.PixelHeight;
            int stored=meta.stored_count>0?meta.stored_count:meta.count;var frames=new BitmapSource[stored];
            for(int i=0;i<stored;i++){var crop=new CroppedBitmap(bgra,new Int32Rect(i%8*FrameWidth,i/8*FrameHeight,FrameWidth,FrameHeight));crop.Freeze();frames[i]=crop;}return frames;
        }
    }
    void DecodeLoop(){
        while(true){
            Row entry=null;int index=-1;
            lock(gate){if(stopped)return;foreach(var r in cache.Values){int candidate=Pending(r);if(candidate>=0&&(entry==null||(r.Number==activeRow&&entry.Number!=activeRow)||(r.Number!=activeRow&&entry.Number!=activeRow&&r.Used>entry.Used))){entry=r;index=candidate;}}if(entry!=null)entry.Loading=true;}
            // A fully prefetched window sleeps, including when its frames live in fronts.
            if(entry==null){wake.WaitOne();continue;}
            var timer=Stopwatch.StartNew();
            try {
                bool streaming=Streaming(entry.Number);byte[] pixels=null;BitmapSource[] frames=null;long bytes=0;var timings=new FrameTimings();
                if(streaming)pixels=DecodeFrame(entry,index,out timings);else frames=DecodeAtlas(entry,out bytes);
                var publish=Stopwatch.StartNew();
                lock(gate){maxPublishWaitMs=Math.Max(maxPublishWaitMs,publish.Elapsed.TotalMilliseconds);if(stopped)return;Row current;
                    if(streaming){var decoded=new CachedFrame{Pixels=pixels,Used=++serial};RememberFront(entry.Number,index,decoded);
                        if(cache.TryGetValue(entry.Number,out current)&&Object.ReferenceEquals(current,entry)){
                            if(entry.Stream.Count>=FrameLimit){
                                // Played frames get recent access stamps. Plain LRU therefore
                                // evicts the older *future* frames and repeatedly decodes the
                                // same look-ahead window. Preserve this window and retire a
                                // past frame, including across a loop/map boundary.
                                var meta=spec.rows[entry.Number.ToString()];int victim=-1;long oldest=long.MaxValue;
                                foreach(var kv in entry.Stream){bool needed=false;for(int k=0;k<=LookAhead;k++)if(Stored(meta,RequestedAhead(meta,entry,k))==kv.Key){needed=true;break;}if(!needed&&kv.Value.Used<oldest){oldest=kv.Value.Used;victim=kv.Key;}}
                                if(victim<0)throw new InvalidDataException("Frame cache cannot retain its look-ahead window");
                                var removed=entry.Stream[victim];entry.Stream.Remove(victim);Recycle(removed);
                            }
                            entry.Stream[index]=decoded;
                        }else {discardedDecodes++;Recycle(decoded);}
                    }else if(cache.TryGetValue(entry.Number,out current)&&Object.ReferenceEquals(current,entry)){entry.Frames=frames;entry.Bytes=bytes;}else discardedDecodes++;
                    entry.Loading=false;decodeCount++;maxDecodeMs=Math.Max(maxDecodeMs,timer.Elapsed.TotalMilliseconds);
                    if(streaming){maxFrameDecodeMs=Math.Max(maxFrameDecodeMs,timer.Elapsed.TotalMilliseconds);maxOpenMs=Math.Max(maxOpenMs,timings.Open);maxInflateMs=Math.Max(maxInflateMs,timings.Inflate);maxCreateMs=Math.Max(maxCreateMs,timings.Create);maxFreezeMs=Math.Max(maxFreezeMs,timings.Freeze);}
                    Monitor.PulseAll(gate);
                }
            }catch(Exception error){lock(gate){entry.Failed=true;entry.Loading=false;LastError=error.GetType().Name+": "+error.Message;Monitor.PulseAll(gate);}}
        }
    }
    public void Dispose(){
        lock(gate){if(!stopped){stopped=true;cache.Clear();fronts.Clear();pixelPool.Clear();Monitor.PulseAll(gate);Signal();}}
        bool joined=Thread.CurrentThread!=worker&&worker.Join(1500);DisposeTimedOut=!joined;
        if(joined)lock(gate){if(!wakeClosed){wake.Close();wakeClosed=true;}}
    }
}
}
