using System;using System.IO;using System.Reflection;using System.Collections.Generic;using System.Security.Cryptography;using System.Web.Script.Serialization;using System.Windows.Media.Imaging;
public static class CompositeEquality {
 static BindingFlags F=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
 static object C(object o,string n,params object[] v){foreach(var m in o.GetType().GetMethods(F))if(m.Name==n&&m.GetParameters().Length==v.Length)return m.Invoke(o,v);throw new Exception(n);}
 static byte[] Pixels(object o){var p=new byte[640*360*4];((BitmapSource)o).CopyPixels(p,2560,0);return p;}
 static bool Same(byte[] a,byte[] b){for(int i=0;i<a.Length;i++)if(a[i]!=b[i])return false;return true;}
 static string Hash(Stream s){using(var h=SHA256.Create())return BitConverter.ToString(h.ComputeHash(s)).Replace("-","").ToLowerInvariant();}
 static string Hash(string p){using(var f=File.OpenRead(p))return Hash(f);}
 static object[] Setup(Assembly a){return new[]{Activator.CreateInstance(a.GetType("WhiteDragon.SmoothMotion")),Activator.CreateInstance(a.GetType("WhiteDragon.TransitionFlowArchive"))};}
 [STAThread] public static int Main(string[] args){var r=new Dictionary<string,object>{{"baseline_sha256",Hash(args[0])},{"candidate_sha256",Hash(args[1])}};object[] x=null,y=null;bool ok=false;try{
 var a=Assembly.LoadFile(Path.GetFullPath(args[0]));var b=Assembly.LoadFile(Path.GetFullPath(args[1]));var resources=new Dictionary<string,string>();foreach(string n in a.GetManifestResourceNames()){if(n=="WhiteDragon.MotionSpec"||n=="WhiteDragon.FlowSpec"||n=="WhiteDragon.FallbackSpec"||n=="WhiteDragon.Posters")continue;using(var f=a.GetManifestResourceStream(n))using(var g=b.GetManifestResourceStream(n)){string h=Hash(f);if(g==null||h!=Hash(g))throw new Exception("Changed embedded resource: "+n);resources[n]=h;}}if(a.GetManifestResourceNames().Length!=59||resources.Count!=55||b.GetManifestResourceNames().Length!=65)throw new Exception("Merged resource count incorrect");r["unchanged_resources"]=resources;r["merged_descriptor_scope"]="Four descriptors merge book rows and tablet phases; all 61 payload hashes are checked independently by UnifiedFeatureReview";
 x=Setup(a);y=Setup(b);int flowProbes=0,pixelProbes=0;
 foreach(int row in new[]{0,1,2,3,4,5,6,7,8,11,12,13,14,15,16,17,18,19,20,21,22,23,24,25,26,27})foreach(int sample in new[]{0,1,3,4,23,88,102,211,296,299}){
 var f=C(x[1],"Get",row,sample);var g=C(y[1],"Get",row,sample);var fa=(float[])f.GetType().GetField("Data").GetValue(f);var ga=(float[])g.GetType().GetField("Data").GetValue(g);for(int i=0;i<fa.Length;i++)if(fa[i]!=ga[i])throw new Exception("Flow value changed: "+row+":"+sample+":"+i);flowProbes++;}
 int[][] pairs={new[]{19,18},new[]{18,19},new[]{19,0},new[]{22,0},new[]{24,19},new[]{27,0}};
 foreach(var pair in pairs){int from=pair[0],to=pair[1];object bx=Activator.CreateInstance(a.GetType("WhiteDragon.FrameTransition")),by=Activator.CreateInstance(b.GetType("WhiteDragon.FrameTransition"));bx.GetType().GetField("RecordDiagnostics").SetValue(bx,true);by.GetType().GetField("RecordDiagnostics").SetValue(by,true);
 foreach(var s in new[]{x[0],y[0]}){if(!(bool)C(s,"EnsureSample",from,1700.0,10000)||!(bool)C(s,"EnsureSample",to,0.0,10000))throw new Exception("Decode not ready");}
 C(bx,"BeginAligned",C(x[0],"Get",from,1700.0),Pixels(C(x[0],"Get",to,0.0)),C(x[1],"Get",from,102),C(x[1],"Get",to,0),240);
 C(by,"BeginAligned",C(y[0],"Get",from,1700.0),Pixels(C(y[0],"Get",to,0.0)),C(y[1],"Get",from,102),C(y[1],"Get",to,0),240);
 for(int i=0;i<=16;i++){double t=i*15.0;C(x[0],"EnsureSample",to,t,10000);C(y[0],"EnsureSample",to,t,10000);byte[] px=Pixels(C(x[0],"Get",to,t)),py=Pixels(C(y[0],"Get",to,t));if(i>0){C(bx,"FollowIncoming",px,C(x[1],"Get",to,(int)(t*.06)));C(by,"FollowIncoming",py,C(y[1],"Get",to,(int)(t*.06)));}if(!Same(Pixels(C(bx,"PresentPixels",px,t,true)),Pixels(C(by,"PresentPixels",py,t,true))))throw new Exception("Composite pixel changed: "+from+" to "+to+" at "+i);pixelProbes++;}
 foreach(string n in new[]{"BodySoftPixelsBefore","BodySoftPixelsAfter"})if(!bx.GetType().GetProperty(n).GetValue(bx,null).Equals(by.GetType().GetProperty(n).GetValue(by,null)))throw new Exception("Diagnostic count changed: "+n);
 }
 r["flow_grid_probes"]=flowProbes;r["full_surface_comparisons"]=pixelProbes;r["diagnostic_counters_identical"]=true;ok=true;
 }catch(Exception e){r["error"]=e.ToString();}finally{if(x!=null)C(x[0],"Dispose");if(y!=null)C(y[0],"Dispose");r["ok"]=ok;r["scope"]="55 unchanged book payload hashes, old/new flow grids and six advancing-target full Pbgra32 composites including diagnostic counters. Proves these source-preserving optimisations do not change sampled pixels; separate native scheduling tests assess timing.";File.WriteAllText(args[2],new JavaScriptSerializer{MaxJsonLength=8000000}.Serialize(r));}return ok?0:1;}
}
