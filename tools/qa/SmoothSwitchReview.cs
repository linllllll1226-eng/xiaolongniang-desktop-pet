using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

// A separate test host: loads only the supplied candidate assembly and never
// invokes its Main, saved preferences, tray, or installed desktop-pet process.
public static class SmoothSwitchReview {
    const long MiB=1024*1024;
    static readonly BindingFlags Flags=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    static readonly Dictionary<string,object> Report=new Dictionary<string,object>();
    static readonly List<string> Failures=new List<string>();
    // Weak keys preserve frame identity without preventing candidate eviction.
    static readonly ConditionalWeakTable<BitmapSource,Mark> Known=new ConditionalWeakTable<BitmapSource,Mark>();
    static readonly Dictionary<int,Meta> Metadata=new Dictionary<int,Meta>();
    static Assembly assembly; static Type smoothType,windowType;
    static string output; static long cacheBudget=256*MiB,maxCachedBytes,maxPrivateBytes;
    static int firstFrameLimit=250,stallLimit=120;
    sealed class Meta {public int Row,Count,Duration; public int[] Map; public bool Loop; public int Stored(int i){return Map==null?i:Map[i];}}
    sealed class Mark {public int Row,Sample;public Mark(int row,int sample){Row=row;Sample=sample;}}
    sealed class Stage {
        public string Kind;public int Row,Hold;public bool Direct,Once;
        public Stage(string kind,int row,int hold){Kind=kind;Row=row;Hold=hold;Once=true;}
    }
    sealed class PixelProbe {
        byte[] pixels,previousPixels;ulong hash;long lastPoll=-1000;bool ready;
        public bool Fresh,Changed;public int Painted;public double MaxCopyMs;public ulong Hash{get{return hash;}}
        public void Poll(BitmapSource source,long now,bool force){
            Fresh=false;Changed=false;if(source==null||!force&&now-lastPoll<12)return;lastPoll=now;
            int stride=(source.PixelWidth*source.Format.BitsPerPixel+7)/8,bytes=stride*source.PixelHeight;
            if(source.Format.BitsPerPixel!=32)throw new InvalidOperationException("Expected a 32-bit visible frame");
            if(pixels==null||pixels.Length!=bytes){pixels=new byte[bytes];previousPixels=new byte[bytes];}
            var watch=Stopwatch.StartNew();source.CopyPixels(pixels,stride,0);MaxCopyMs=Math.Max(MaxCopyMs,watch.Elapsed.TotalMilliseconds);
            ulong next=1469598103934665603UL;int painted=0;
            // The copy reads the actual surface, including a WriteableBitmap
            // updated in place. Sparse hashing keeps observer CPU work bounded.
            for(int i=0;i<pixels.Length;i+=64){for(int channel=0;channel<4;channel++){next^=pixels[i+channel];next*=1099511628211UL;}if(pixels[i+3]>12)painted++;}
            // Sparse hash can miss the deliberately tiny wing motion and the
            // first subpixel steps of an eased bridge. Compare every copied
            // pixel for progress; retain the hash only for freeze readback.
            if(ready)for(int i=0;i<pixels.Length;i++)if(pixels[i]!=previousPixels[i]){Changed=true;break;}
            Buffer.BlockCopy(pixels,0,previousPixels,0,bytes);
            Painted=painted;hash=next;ready=true;Fresh=true;
        }
    }
    static object Field(object obj,string name){var field=obj.GetType().GetField(name,Flags);return field==null?null:field.GetValue(obj);}
    static object Prop(object obj,string name){var p=obj.GetType().GetProperty(name,Flags);return p==null?null:p.GetValue(obj,null);}
    static object Call(object obj,string name,params object[] args){try{return obj.GetType().GetMethod(name,Flags).Invoke(obj,args);}catch(TargetInvocationException e){throw e.InnerException??e;}}
    static int IntProp(object obj,string name){object value=Prop(obj,name);return value==null?-1:Convert.ToInt32(value);}
    static long LongProp(object obj,string name){object value=Prop(obj,name);return value==null?-1:Convert.ToInt64(value);}
    static long LongField(object obj,string name){object value=Field(obj,name);return value==null?-1:Convert.ToInt64(value);}
    static void Check(bool condition,string issue){if(!condition&&!Failures.Contains(issue))Failures.Add(issue);}
    static void Save(){Report["failures"]=Failures;Report["ok"]=Failures.Count==0;File.WriteAllText(Path.Combine(output,"smooth-switch-review.json"),new JavaScriptSerializer{MaxJsonLength=8000000}.Serialize(Report));}
    static string Hash(string path){using(var sha=SHA256.Create())using(var stream=File.OpenRead(path))return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-","").ToLowerInvariant();}
    static void LoadMetadata(){
        using(var stream=assembly.GetManifestResourceStream("WhiteDragon.MotionSpec"))using(var reader=new StreamReader(stream)){
            var document=new JavaScriptSerializer{MaxJsonLength=4000000}.Deserialize<Dictionary<string,object>>(reader.ReadToEnd());
            var rows=(Dictionary<string,object>)document["rows"];
            foreach(var pair in rows){var row=(Dictionary<string,object>)pair.Value;var meta=new Meta{Row=int.Parse(pair.Key),Count=Convert.ToInt32(row["count"]),Duration=Convert.ToInt32(row["duration"]),Loop=Convert.ToBoolean(row["loop"])};object map;
                if(row.TryGetValue("map",out map)&&map!=null){var values=(ArrayList)map;meta.Map=new int[values.Count];for(int i=0;i<values.Count;i++)meta.Map[i]=Convert.ToInt32(values[i]);}Metadata.Add(meta.Row,meta);
            }
        }
    }
    static Mark Resolve(object smooth,BitmapSource source){
        if(source==null)return null;Mark mark;if(Known.TryGetValue(source,out mark))return mark;
        object gate=Field(smooth,"gate");if(gate==null)throw new InvalidOperationException("Candidate lacks cache reflection gate");
        lock(gate){var cache=Field(smooth,"cache") as IDictionary;if(cache==null)throw new InvalidOperationException("Candidate lacks cache dictionary");
            foreach(DictionaryEntry item in cache){int row=Convert.ToInt32(item.Key);var meta=Metadata[row];object entry=item.Value;
                var frames=Field(entry,"Frames") as BitmapSource[];
                if(frames!=null)for(int sample=0;sample<meta.Count;sample++)if(Object.ReferenceEquals(frames[meta.Stored(sample)],source)){mark=new Mark(row,sample);Known.Add(source,mark);return mark;}
                var stream=Field(entry,"Stream") as IDictionary;
                if(stream!=null)foreach(DictionaryEntry frame in stream){if(!Object.ReferenceEquals(Field(frame.Value,"Image"),source))continue;int stored=Convert.ToInt32(frame.Key),sample=0;while(sample<meta.Count&&meta.Stored(sample)!=stored)sample++;mark=new Mark(row,sample);Known.Add(source,mark);return mark;}
            }
        }
        return null;
    }
    static int CachedFramesForRow(object smooth,int row){lock(Field(smooth,"gate")){var cache=(IDictionary)Field(smooth,"cache");if(!cache.Contains(row))return 0;var stream=Field(cache[row],"Stream") as IDictionary;return stream==null?0:stream.Count;}}
    static void MemoryCheck(object smooth){
        long bytes=LongProp(smooth,"CachedPixelBytes");maxCachedBytes=Math.Max(maxCachedBytes,bytes);
        Check(bytes>=0,"Candidate exposes CachedPixelBytes");Check(bytes<=cacheBudget,"Pixel cache exceeds configured "+cacheBudget+" byte budget");
        maxPrivateBytes=Math.Max(maxPrivateBytes,Process.GetCurrentProcess().PrivateMemorySize64);
    }
    static void DecoderReview(){
        var probes=new List<Dictionary<string,object>>();object smooth=Activator.CreateInstance(smoothType);
        try{
            int[] rows={1,2,3,4,5,6,7,8,11,12,13,14,15,16,17,18,19,20,21,0};var total=Stopwatch.StartNew();double maxGet=0;
            for(int pass=0;pass<12;pass++)foreach(int row in rows){var elapsed=Stopwatch.StartNew();Call(smooth,"Get",row,0.0);maxGet=Math.Max(maxGet,elapsed.Elapsed.TotalMilliseconds);Call(smooth,"Warm",rows[(row+pass)%rows.Length]);MemoryCheck(smooth);if(pass%3==0)Thread.Sleep(1);}
            bool final=(bool)Call(smooth,"EnsureSample",21,2500.0,10000);var result=Call(smooth,"Get",21,2500.0) as BitmapSource;
            Check(final&&result!=null&&result.IsFrozen,"Decoder latest action ready after rapid eviction churn");Check(maxGet<=50,"Decoder Get blocks longer than 50 ms");Check(Prop(smooth,"LastError")==null,"Decoder error during eviction churn");
            Report["decoder_churn"]=new Dictionary<string,object>{{"requests",240},{"elapsed_ms",total.Elapsed.TotalMilliseconds},{"max_get_ms",maxGet},{"final_ready",final},{"cached_rows",IntProp(smooth,"CachedRows")},{"cached_frames",IntProp(smooth,"CachedStreamFrames")},{"last_error",Prop(smooth,"LastError")}};
        }finally{Call(smooth,"Dispose");CheckWorkerStopped(smooth,"decoder_churn");}
        // Prefix caching is legal: EnsureSample(1000) may also decode sample 0.
        // Assert the displayed frame is never from a later sample, rather than
        // requiring a null result from a future-only fixture as the old QA did.
        foreach(int row in new int[]{3,0,18}){smooth=Activator.CreateInstance(smoothType);try{
            bool ready=(bool)Call(smooth,"EnsureSample",row,1000.0,10000);var source=Call(smooth,"Get",row,0.0) as BitmapSource;var mark=Resolve(smooth,source);
            Check(ready,"Future guard fixture decodes row "+row);Check(source==null||mark!=null&&mark.Row==row&&mark.Sample==0,"Future sample displayed at time zero row "+row);
            probes.Add(new Dictionary<string,object>{{"row",row},{"fixture_ready",ready},{"source_available",source!=null},{"sample",mark==null?-1:mark.Sample}});
        }finally{Call(smooth,"Dispose");CheckWorkerStopped(smooth,"future_guard_"+row);}}
        Report["future_sample_guards"]=probes;Report["decoder_tests_may_ensure_sample"]=true;
    }
    static void CheckWorkerStopped(object smooth,string name){var worker=Field(smooth,"worker") as Thread;bool stopped=worker==null||worker.Join(1500);Check(stopped,"Decoder worker remains alive after Dispose: "+name);Report[name+"_worker_stopped"]=stopped;}
    static void BehaviorReview(bool required){
        var behaviorType=assembly.GetType("WhiteDragon.PetBehavior",true);bool available=behaviorType.GetMethod("SetMediaWaiting",Flags)!=null;
        Report["combined_freeze_api_available"]=available;if(!available){Check(!required,"Candidate lacks SetMediaWaiting required for combined freeze checks");return;}
        var cases=new List<Dictionary<string,object>>();Report["combined_freeze_cases"]=cases;
        foreach(string kind in new string[]{"pause","menu","hidden"}){
            object behavior=Activator.CreateInstance(behaviorType,new object[]{500});Call(behavior,"Play",3,true,0L);
            Call(behavior,"SetMediaWaiting",true,100L);
            if(kind=="pause")Call(behavior,"SetPaused",true,200L);else if(kind=="menu")Call(behavior,"SetMenu",true,200L);else Call(behavior,"SuspendHidden",200L);
            Call(behavior,"SetMediaWaiting",false,300L);double frozen=Convert.ToDouble(Call(behavior,"VisualTime",350L));
            if(kind=="pause")Call(behavior,"SetPaused",false,400L);else if(kind=="menu")Call(behavior,"SetMenu",false,400L);else Call(behavior,"ResumeAfterHidden",400L);
            double resumed=Convert.ToDouble(Call(behavior,"VisualTime",450L));
            Check(Math.Abs(frozen-100)<.001,"Combined media wait + "+kind+" changes frozen visual time");Check(Math.Abs(resumed-150)<.001,"Combined media wait + "+kind+" double-counts freeze duration");
            cases.Add(new Dictionary<string,object>{{"case","wait_then_"+kind},{"frozen_visual_ms",frozen},{"resumed_visual_ms",resumed},{"expected_frozen_ms",100},{"expected_resumed_ms",150}});
        }
        {
            object behavior=Activator.CreateInstance(behaviorType,new object[]{500});Call(behavior,"Play",3,true,0L);Call(behavior,"SetMenu",true,100L);Call(behavior,"Play",8,true,200L);Call(behavior,"SetMediaWaiting",true,200L);Call(behavior,"SetMediaWaiting",false,300L);Call(behavior,"SetMenu",false,400L);
            double start=Convert.ToDouble(Call(behavior,"VisualTime",400L)),resumed=Convert.ToDouble(Call(behavior,"VisualTime",450L));Check(Math.Abs(start)<.001&&Math.Abs(resumed-50)<.001,"Selecting a waiting action inside menu skips its beginning");cases.Add(new Dictionary<string,object>{{"case","menu_select_then_ready"},{"resumed_start_ms",start},{"resumed_plus_50_ms",resumed}});
        }
        {
            object behavior=Activator.CreateInstance(behaviorType,new object[]{500});Call(behavior,"Play",3,true,0L);long first=LongProp(behavior,"PlaybackId");Call(behavior,"Play",3,true,200L);long second=LongProp(behavior,"PlaybackId");double start=Convert.ToDouble(Call(behavior,"VisualTime",200L));Check(first>=0&&second>first,"Same-row replay does not issue a new PlaybackId");Check(Math.Abs(start)<.001,"Same-row replay does not restart visual timeline");cases.Add(new Dictionary<string,object>{{"case","same_row_new_generation"},{"before",first},{"after",second},{"replay_visual_start",start}});
        }
    }
    static void NaturalIdleFlagReview(){
        var type=assembly.GetType("WhiteDragon.PetBehavior",true);object behavior=Activator.CreateInstance(type,new object[]{500});
        Call(behavior,"Play",3,true,0L);long duration=Convert.ToInt64(Call(behavior,"TotalDuration",3));Call(behavior,"Tick",duration+1,null,false,false);
        Check(IntProp(behavior,"Animation")==0&&(bool)Prop(behavior,"NaturalIdleTransition"),"Natural action end lacks moving-idle transition flag");
        Call(behavior,"Select",0,duration+20);Check(!(bool)Prop(behavior,"NaturalIdleTransition"),"Manual idle selection incorrectly skips its visual transition");
        Report["natural_and_interrupted_idle_paths_distinguished"]=true;
    }
    static List<Stage> Stages(bool quick){
        var stages=new List<Stage>();stages.Add(new Stage("startup_idle",0,1000));
        foreach(int row in new int[]{1,2,3,4,5,6,7,8,11,12,13,14,18,19,20,21,22,23,24,25})stages.Add(new Stage("first_action_switch",row,Math.Min(750,Metadata[row].Duration-80)));
        stages.Add(new Stage("first_action_switch",16,260){Direct=true});stages.Add(new Stage("first_action_switch",17,300){Direct=true});stages.Add(new Stage("first_action_switch",15,500){Direct=true});
        foreach(int row in quick?new int[]{4,3,21}:new int[]{1,2,3,4,5,6,7,8,11,13,14,18,19,20,21,22,23,24})stages.Add(new Stage((row==18||row==19)&&assembly.GetName().Version.Minor>=9?"sustained_active_state":"natural_end_to_idle",row,Metadata[row].Duration+850));
        if(assembly.GetName().Version.Minor>=9)stages.Add(new Stage("book_exchange_to_reading",25,Metadata[25].Duration+850));
        stages.Add(new Stage("flight_open_flap_fold_idle",15,Metadata[16].Duration+Metadata[15].Duration+Metadata[17].Duration+800));
        stages.Add(new Stage("idle_loop_boundary",0,Metadata[0].Duration+400){Direct=true,Once=false});
        if(!quick)stages.Add(new Stage("action_loop_boundary",18,Metadata[18].Duration+400){Direct=true,Once=false});
        if(!quick)foreach(int row in new int[]{22,23,24})stages.Add(new Stage("new_action_loop_boundary",row,Metadata[row].Duration+400){Direct=true,Once=false});
        int[] burst={3,8,11,21,2,19,4,20,1,18,22,23,24,25};for(int i=0;i<(quick?20:60);i++)stages.Add(new Stage("rapid_interrupt",burst[i%burst.Length],45));
        stages.Add(new Stage("after_interrupt_settle",8,1000));
        stages.Add(new Stage("pause_freeze",3,1500));stages.Add(new Stage("menu_freeze",8,1500));
        stages.Add(new Stage("after_freeze_idle",0,850));return stages;
    }
    static void NativeReview(bool quick){
        var app=new Application{ShutdownMode=ShutdownMode.OnMainWindowClose};var window=(Window)Activator.CreateInstance(windowType,new object[]{true});
        window.Opacity=0;window.Topmost=false;window.ShowActivated=false;app.MainWindow=window;
        object behavior=Field(window,"behavior"),preferences=Field(window,"preferences"),smooth=Field(window,"smooth");var image=(Image)Field(window,"image");var outgoing=Field(window,"transitionImage") as Image;var clock=(Stopwatch)Field(window,"clock");
        foreach(var pair in Metadata)Check(Convert.ToInt32(Call(behavior,"TotalDuration",pair.Key))==pair.Value.Duration,"Behavior/media duration differs in row "+pair.Key);
        foreach(string preference in new string[]{"Follow","AutoNap","LinkCodex"})preferences.GetType().GetField(preference).SetValue(preferences,false);behavior.GetType().GetField("AutoNap").SetValue(behavior,false);
        var stages=Stages(quick);var results=new List<Dictionary<string,object>>();var events=new List<Dictionary<string,object>>();Report["native_stages"]=results;Report["native_row_events"]=events;
        // Observe above Render priority: an Input-priority timer can be starved
        // by a continuously updated WriteableBitmap even while the candidate's
        // submission trace keeps advancing. The pixel/time gates stay intact.
        var timer=new DispatcherTimer(DispatcherPriority.Normal){Interval=TimeSpan.FromMilliseconds(4)};
        int stageIndex=-1,samples=0,changes=0,pixelChanges=0,nullSamples=0,decodedChanges=0,initialFrames=0,firstSample=-1,actualRow=-1,freezePhase=0,maxSampleJump=0,lastPresentedSample=-1,blankPixels=0,warpChanges=0,lastWarpWeight=-1;
        long began=0,lastChange=0,firstDecoded=-1,firstOpaque=-1,lastTick=0,maxObserver=0,maxStall=0,maxTickStage=0,logicalEntered=0,firstRowDecoded=-1;
        long lastPresentedPlayback=-1;ulong freezeHash=0;var probe=new PixelProbe();
        BitmapSource previous=null,freezeSource=null,freezeOutgoing=null;double freezeVisual=0,freezeOpacity=0,freezeOutgoingOpacity=0;var seenRows=new List<int>();
        Action observeRow=delegate{
            int row=IntProp(behavior,"Row");if(row==actualRow)return;
            if(actualRow>=0&&firstRowDecoded<0&&stages[stageIndex].Kind!="rapid_interrupt")Check(false,"No real decoded frame before logical row ended: "+stages[stageIndex].Kind+" row "+actualRow);
            actualRow=row;logicalEntered=clock.ElapsedMilliseconds;firstRowDecoded=-1;if(!seenRows.Contains(row))seenRows.Add(row);
            events.Add(new Dictionary<string,object>{{"stage",stageIndex},{"event","logical_row_enter"},{"row",row},{"at_ms",logicalEntered}});
        };
        Action sample=delegate{
            long now=clock.ElapsedMilliseconds;var stage=stages[stageIndex];observeRow();samples++;
            var source=image.Source as BitmapSource;var mark=Resolve(smooth,source);if(source==null)nullSamples++;
            object presentedSampleField=Field(window,"presentedSampleIndex");long presentedId=LongField(window,"presentedPlaybackId"),behaviorId=LongProp(behavior,"PlaybackId");
            // A compositor's WriteableBitmap has no cache identity. Its public
            // presentation trace supplies row/sample, while the independent
            // pixel probe proves that the visible surface actually changes.
            if(mark==null&&presentedSampleField!=null&&presentedId>=0&&LongField(window,"shownRow")>=0)mark=new Mark((int)LongField(window,"shownRow"),Convert.ToInt32(presentedSampleField));
            bool currentPlayback=behaviorId<0||presentedId==behaviorId;
            probe.Poll(source,now,false);if(probe.Fresh){if(probe.Painted<50)blankPixels++;if(probe.Changed){pixelChanges++;if(firstDecoded>=0&&now-began>200&&freezePhase!=1)maxStall=Math.Max(maxStall,now-lastChange);lastChange=now;}}
            object bridge=Field(window,"frameTransition");int bridgeWeight=IntProp(bridge,"Weight");
            if(currentPlayback&&mark!=null&&mark.Row==actualRow&&probe.Fresh&&probe.Changed&&(bool)Field(window,"transitionHoldsTimeline")&&(bool)Prop(bridge,"UsesWarp")&&bridgeWeight!=lastWarpWeight){warpChanges++;lastWarpWeight=bridgeWeight;}
            double visible=image.Opacity+(outgoing!=null&&outgoing.Source!=null?outgoing.Opacity:0);Check(source!=null||outgoing!=null&&outgoing.Source!=null,"Both transition images empty stage "+stageIndex);Check(visible>=.95,"Transition loses visible opacity stage "+stageIndex);
            if(mark!=null&&mark.Row==actualRow&&currentPlayback&&firstRowDecoded<0){firstRowDecoded=Math.Max(0,now-logicalEntered);object presentedSample=Field(window,"presentedSampleIndex");int exactSample=presentedSample==null?mark.Sample:Convert.ToInt32(presentedSample);events.Add(new Dictionary<string,object>{{"stage",stageIndex},{"event","decoded_row_first"},{"row",actualRow},{"sample",exactSample},{"stored_first_sample",mark.Sample},{"delay_ms",firstRowDecoded},{"presented_playback_id",Field(window,"presentedPlaybackId")},{"presented_visual_time",Field(window,"presentedVisualTime")}});
                if(stage.Kind!="rapid_interrupt"){Check(firstRowDecoded<=firstFrameLimit,"First decoded row > "+firstFrameLimit+" ms: "+stage.Kind+" row "+actualRow);Check(exactSample<=2,"New row begins after sample 2: "+stage.Kind+" row "+actualRow+" sample "+exactSample);}
            }
            int target=stage.Kind=="flight_open_flap_fold_idle"?16:stage.Row;
            if(mark!=null&&mark.Row==target&&currentPlayback&&firstDecoded<0){firstDecoded=now-began;firstSample=mark.Sample;lastChange=now;}
            if(mark!=null&&mark.Row==target&&currentPlayback&&image.Opacity>=.95&&firstOpaque<0)firstOpaque=now-began;
            if(source!=previous){changes++;previous=source;}
            if(mark!=null&&currentPlayback){int currentSample=presentedSampleField==null?mark.Sample:Convert.ToInt32(presentedSampleField);long currentId=presentedId<0?actualRow:presentedId;
                if(currentId!=lastPresentedPlayback||currentSample!=lastPresentedSample){decodedChanges++;if(currentId==lastPresentedPlayback&&currentSample>=lastPresentedSample)maxSampleJump=Math.Max(maxSampleJump,currentSample-lastPresentedSample);lastPresentedPlayback=currentId;lastPresentedSample=currentSample;}
            }
            MemoryCheck(smooth);
        };
        Action start=delegate{
            stageIndex++;var stage=stages[stageIndex];began=clock.ElapsedMilliseconds;samples=changes=pixelChanges=nullSamples=decodedChanges=maxSampleJump=blankPixels=warpChanges=0;lastWarpWeight=-1;maxStall=maxTickStage=0;firstDecoded=firstOpaque=-1;firstSample=-1;freezePhase=0;actualRow=-1;firstRowDecoded=-1;lastPresentedPlayback=-1;lastPresentedSample=-1;seenRows.Clear();
            initialFrames=CachedFramesForRow(smooth,stage.Kind=="flight_open_flap_fold_idle"?16:stage.Row);previous=image.Source as BitmapSource;lastChange=began;
            if(stage.Direct){Call(behavior,"Play",stage.Row,stage.Once,began);Call(window,"RenderFrame");}else Call(window,"Choose",stage.Row);
            sample();
        };
        window.Loaded+=delegate{start();timer.Start();};
        timer.Tick+=delegate{
            try{
                long now=clock.ElapsedMilliseconds;if(lastTick>0){maxObserver=Math.Max(maxObserver,now-lastTick);maxTickStage=Math.Max(maxTickStage,now-lastTick);}lastTick=now;var stage=stages[stageIndex];long elapsed=now-began;
                bool freeze=stage.Kind=="pause_freeze"||stage.Kind=="menu_freeze";
                if(freeze&&freezePhase==0&&elapsed>=60){Call(behavior,stage.Kind=="pause_freeze"?"SetPaused":"SetMenu",true,now);Call(window,"RenderFrame");freezeSource=image.Source as BitmapSource;freezeOutgoing=outgoing==null?null:outgoing.Source as BitmapSource;freezeVisual=Convert.ToDouble(Call(behavior,"VisualTime",now));freezeOpacity=image.Opacity;freezeOutgoingOpacity=outgoing==null?0:outgoing.Opacity;probe.Poll(freezeSource,now,true);freezeHash=probe.Hash;freezePhase=1;}
                if(freeze&&freezePhase==1){Check(Object.ReferenceEquals(freezeSource,image.Source),stage.Kind+" image changes while frozen");Check(Object.ReferenceEquals(freezeOutgoing,outgoing==null?null:outgoing.Source),stage.Kind+" outgoing image changes while frozen");Check(Math.Abs(Convert.ToDouble(Call(behavior,"VisualTime",now))-freezeVisual)<.001,stage.Kind+" visual timeline advances while frozen");Check(Math.Abs(image.Opacity-freezeOpacity)<.001,stage.Kind+" opacity advances while frozen");Check(Math.Abs((outgoing==null?0:outgoing.Opacity)-freezeOutgoingOpacity)<.001,stage.Kind+" outgoing opacity advances while frozen");probe.Poll(image.Source as BitmapSource,now,false);if(probe.Fresh)Check(probe.Hash==freezeHash,stage.Kind+" surface pixels change while frozen");
                    if(elapsed>=600){Call(behavior,stage.Kind=="pause_freeze"?"SetPaused":"SetMenu",false,now);freezePhase=2;lastChange=now;}
                }
                sample();if(elapsed<stage.Hold)return;
                maxStall=Math.Max(maxStall,now-lastChange);
                var result=new Dictionary<string,object>{{"kind",stage.Kind},{"selected_row",stage.Row},{"actual_row_at_end",actualRow},{"initial_cached_frames",initialFrames},{"elapsed_ms",elapsed},{"samples",samples},{"source_changes",changes},{"pixel_changes",pixelChanges},{"decoded_presentations",decodedChanges},{"max_presented_sample_jump",maxSampleJump},{"first_decoded_ms",firstDecoded},{"first_decoded_sample",firstSample},{"first_95pct_opacity_ms",firstOpaque},{"max_pixel_stall_ms",maxStall},{"max_observer_tick_ms",maxTickStage},{"null_source_samples",nullSamples},{"blank_pixel_probes",blankPixels},{"seen_rows",seenRows.ToArray()}};results.Add(result);
                result["distinct_warped_surface_presentations"]=warpChanges;
                Check(nullSamples==0,"Null image in "+stage.Kind+" row "+stage.Row);
                Check(blankPixels==0,"Empty visible pixel frame in "+stage.Kind+" row "+stage.Row);
                if(stage.Kind!="rapid_interrupt"){
                    Check(firstDecoded>=0,"Selected row never receives decoded frame "+stage.Kind+" row "+stage.Row);Check(firstDecoded<=firstFrameLimit,"Selected row decode exceeds "+firstFrameLimit+" ms "+stage.Kind+" row "+stage.Row);
                    if(!freeze)Check(maxStall<=stallLimit,"Source stalls > "+stallLimit+" ms "+stage.Kind+" row "+stage.Row);
                    // The incoming clip intentionally stays at its decoded
                    // first pose while the visible bridge moves. Count three
                    // independently observed warped surfaces as progress; all
                    // first-frame, stall, opacity and future-sample gates stay.
                    Check(decodedChanges>=3||firstDecoded>=0&&warpChanges>=3,"Fewer than 3 decoded or moving bridge presentations "+stage.Kind+" row "+stage.Row);Check(pixelChanges>=3,"Fewer than 3 visible pixel changes "+stage.Kind+" row "+stage.Row);
                }
                if(stage.Kind=="natural_end_to_idle")Check(actualRow==0,"Natural action did not return to idle row "+stage.Row);
                if(stage.Kind=="sustained_active_state")Check(actualRow==stage.Row,"Active state returned to idle");
                if(stage.Kind=="book_exchange_to_reading")Check(actualRow==19&&seenRows.Contains(25)&&!seenRows.Contains(0),"Book exchange must return to reading without idle");
                if(stage.Kind=="flight_open_flap_fold_idle")foreach(int row in new int[]{16,15,17,0})Check(seenRows.Contains(row),"Flight chain missed row "+row);
                Report["completed_stages"]=stageIndex+1;Report["stage_count"]=stages.Count;Report["native_max_observer_tick_ms"]=maxObserver;Report["pixel_probe_max_copy_ms"]=probe.MaxCopyMs;Report["max_cached_pixel_bytes"]=maxCachedBytes;Report["max_private_bytes"]=maxPrivateBytes;Save();
                if(stageIndex+1<stages.Count){start();return;}
                timer.Stop();Check(Prop(smooth,"LastError")==null,"Native decoder LastError");Report["native_last_error"]=Prop(smooth,"LastError");Report["native_max_frame_decode_ms"]=Prop(smooth,"MaxFrameDecodeMilliseconds");Report["renderer_submission_trace"]=Field(window,"renderSubmissionTrace");Report["renderer_max_ms"]=Field(window,"maxRenderMilliseconds");Report["logic_max_ms"]=Field(window,"maxTickMilliseconds");Report["allocated_pixel_pool_bytes"]=Prop(smooth,"AllocatedPixelPoolBytes");window.Close();
            }catch(Exception error){timer.Stop();Failures.Add(error.ToString());Save();window.Close();}
        };
        app.Run(window);CheckWorkerStopped(smooth,"native_close");Check(!(bool)Prop((DispatcherTimer)Field(window,"timer"),"IsEnabled"),"Window timer remains enabled after Close");Report["native_no_ensure_ready_or_sample"]=true;Report["native_test_constructor"]=true;
    }
    [STAThread]public static int Main(string[] args){
        if(args.Length<2)return 2;output=Path.GetFullPath(args[0]);Directory.CreateDirectory(output);
        try{
            string target=Path.GetFullPath(args[1]);string mode=args.Length>2?args[2]:"native";if(args.Length>3)cacheBudget=long.Parse(args[3])*MiB;if(args.Length>4)firstFrameLimit=int.Parse(args[4]);if(args.Length>5)stallLimit=int.Parse(args[5]);
            assembly=Assembly.LoadFrom(target);smoothType=assembly.GetType("WhiteDragon.SmoothMotion",true);windowType=assembly.GetType("WhiteDragon.PetWindow",true);LoadMetadata();
            Report["created_utc"]=DateTime.UtcNow.ToString("o");Report["candidate_path"]=target;Report["candidate_sha256"]=Hash(target);Report["candidate_version"]=assembly.GetName().Version.ToString();Report["mode"]=mode;
            Report["limits"]=new Dictionary<string,object>{{"first_frame_ms",firstFrameLimit},{"max_source_stall_ms",stallLimit},{"cache_pixel_bytes",cacheBudget},{"new_row_max_first_sample",2}};
            Report["scope"]="Isolated candidate loaded by reflection; installed Main never invoked. Invisible WPF PetWindow(test=true), Follow/AutoNap/LinkCodex off, no real settings writes. Timed native switches never call EnsureReady or EnsureSample. Checks decoded frame identity and source cadence, not actual monitor presentation, perceptual pose continuity, or 60/120 Hz acceptance.";
            Report["intentional_clip_hold_checked_by_distinct_warp_pixels"]=true;Report["visible_progress_compares_every_pixel"]=true;Report["observer_priority"]="Normal (above Render)";NaturalIdleFlagReview();if(mode=="decoder-only")DecoderReview();else if(mode=="behavior-only")BehaviorReview(true);else{BehaviorReview(mode!="quick");NativeReview(mode=="quick");}Report["max_cached_pixel_bytes"]=maxCachedBytes;Report["max_private_bytes"]=maxPrivateBytes;Save();return Failures.Count==0?0:1;
        }catch(Exception error){Failures.Add(error.ToString());Save();return 1;}
    }
}
