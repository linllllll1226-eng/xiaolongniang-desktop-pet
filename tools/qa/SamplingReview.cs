using System;using System.IO;using System.Reflection;using System.Collections.Generic;using System.Security.Cryptography;using System.Web.Script.Serialization;
public static class SamplingReview {
 static BindingFlags F=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;static List<string> checks=new List<string>();
 static object C(object o,string n,params object[] v){foreach(var m in o.GetType().GetMethods(F))if(m.Name==n&&m.GetParameters().Length==v.Length)return m.Invoke(o,v);throw new Exception(n);}
 static object P(object o,string n){return o.GetType().GetProperty(n,F).GetValue(o,null);}static void Check(bool v,string n){if(!v)throw new Exception(n);if(!checks.Contains(n))checks.Add(n);}
 static string Hash(string p){using(var h=SHA256.Create())using(var s=File.OpenRead(p))return BitConverter.ToString(h.ComputeHash(s)).Replace("-","").ToLowerInvariant();}
 [STAThread]public static int Main(string[] args){var r=new Dictionary<string,object>{{"candidate_sha256",Hash(args[0])},{"checks",checks}};object s=null,old=null;bool ok=false;try{
 var a=Assembly.LoadFile(Path.GetFullPath(args[0]));var b=Assembly.LoadFile(Path.GetFullPath(args[1]));s=Activator.CreateInstance(a.GetType("WhiteDragon.SmoothMotion"));old=Activator.CreateInstance(b.GetType("WhiteDragon.SmoothMotion"));int probes=0;
 foreach(int row in new[]{22,24}){
  C(s,"WarmRecovery",row,3000.0);for(int i=180;i<300;i++){int expected=i==299?299:i/2*2;Check((int)C(s,"GetSampleIndex",row,i*5000.0/300)==expected,"recovery phase uses even samples and retains final sample");}
  foreach(int i in new[]{180,191,211,239,277,298,299}){double t=i*5000.0/300;int expected=i==299?299:i/2*2;Check((bool)C(s,"EnsureSample",row,t,10000)&&(bool)C(old,"EnsureSample",row,expected*5000.0/300,10000),"selected recovery source samples decode");var x=new byte[640*360*4];var y=new byte[x.Length];Check((bool)C(s,"TryCopyExact",row,t,x)&&(bool)C(old,"TryCopyExact",row,expected*5000.0/300,y),"recovery copies actual selected source samples");for(int k=0;k<x.Length;k++)if(x[k]!=y[k])throw new Exception("Recovery source pixel differs");Check(true,"recovery preserves all pixels of selected original samples");probes++;}
  C(s,"WarmFrom",row,3183.333333);Check((int)C(s,"GetSampleIndex",row,191*5000.0/300)==191,"ordinary playback restores odd samples immediately");
  C(s,"WarmRecovery",row,3000.0);C(s,"WarmDirected",row,191*5000.0/300,-1);Check((int)C(s,"GetSampleIndex",row,191*5000.0/300)==191,"directed window-perch playback restores full sampling");Check((bool)C(s,"EnsureSample",row,191*5000.0/300,10000),"directed sampling change wakes decoder");
  C(s,"WarmFrom",row,4999.0);Check((int)C(s,"GetSampleIndex",row,4999.0)==299,"normal final sample stays exact");Check((int)P(s,"CachedRows")<=2,"row cache remains bounded");
 }
 Check(P(s,"LastError")==null,"recovery decoder has no errors");r["selected_frame_probes"]=probes;r["pixel_budget_bytes"]=P(s,"StreamingPixelBudget");ok=true;
 }catch(Exception e){r["error"]=e.ToString();}finally{if(s!=null){C(s,"Dispose");r["worker_stopped"]=P(s,"WorkerStopped");}if(old!=null)C(old,"Dispose");r["ok"]=ok;r["scope"]="Accelerated recovery sampling, exact selected original pixels, full last sample, normal/directed sampling restoration and bounded cache. Does not claim that all 60Hz source frames are displayed during 2.6x playback.";File.WriteAllText(args[2],new JavaScriptSerializer().Serialize(r));}return ok?0:1;}
}
