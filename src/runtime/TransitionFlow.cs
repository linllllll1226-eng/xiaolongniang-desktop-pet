using System;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Collections.Generic;
using System.Web.Script.Serialization;
namespace WhiteDragon {
public sealed class FlowRow {public int row,count;public int[] keys;}
public sealed class FlowSpec {public Dictionary<string,FlowRow> rows;}
public sealed class FlowField {
 public const int GW=81,GH=46,Step=8;public readonly float[] Data=new float[GW*GH*4];
 public void At(double x,double y,int channel,out double vx,out double vy){
  double gx=Math.Max(0,Math.Min(GW-1,x/Step)),gy=Math.Max(0,Math.Min(GH-1,y/Step));int ix=(int)gx,iy=(int)gy,jx=Math.Min(GW-1,ix+1),jy=Math.Min(GH-1,iy+1);double u=gx-ix,v=gy-iy;
  int a=(iy*GW+ix)*4+channel,b=(iy*GW+jx)*4+channel,c=(jy*GW+ix)*4+channel,d=(jy*GW+jx)*4+channel;
  vx=(Data[a]*(1-u)+Data[b]*u)*(1-v)+(Data[c]*(1-u)+Data[d]*u)*v;vy=(Data[a+1]*(1-u)+Data[b+1]*u)*(1-v)+(Data[c+1]*(1-u)+Data[d+1]*u)*v;
 }
 public static FlowField Lerp(FlowField a,FlowField b,double u){var f=new FlowField();for(int i=0;i<f.Data.Length;i++)f.Data[i]=(float)(a.Data[i]+(b.Data[i]-a.Data[i])*u);return f;}
}
// Only compact displacement grids are read when a transition starts. No
// external process, video generation or optical-flow DLL is required at run time.
public sealed class TransitionFlowArchive {
 readonly FlowSpec spec;readonly Dictionary<string,FlowField> cache=new Dictionary<string,FlowField>();readonly Queue<string> order=new Queue<string>();
 public bool Enabled{get{return spec!=null;}}public string LastError{get;private set;}public int DecodeCount{get;private set;}
 public TransitionFlowArchive(){using(var s=Assembly.GetExecutingAssembly().GetManifestResourceStream("WhiteDragon.FlowSpec")){if(s==null)return;using(var r=new StreamReader(s))spec=new JavaScriptSerializer().Deserialize<FlowSpec>(r.ReadToEnd());}}
 FlowField Load(int row,int key){
  string id=row+":"+key;FlowField f;if(cache.TryGetValue(id,out f))return f;
  using(var s=Assembly.GetExecutingAssembly().GetManifestResourceStream("WhiteDragon.Flow"+row))using(var r=new BinaryReader(s)){
   if(r.ReadUInt32()!=0x31464457||r.ReadInt32()!=81||r.ReadInt32()!=46||r.ReadInt32()!=8)throw new InvalidDataException("Invalid transition flow grid");int count=r.ReadInt32();if(key<0||key>=count)throw new InvalidDataException("Invalid transition key");
   s.Position=20+key*12;r.ReadInt32();int offset=r.ReadInt32(),length=r.ReadInt32();s.Position=offset;byte[] compressed=r.ReadBytes(length);f=new FlowField();
   // Inflate the grid in one block. Reading each 16-bit component through
   // GZipStream performs almost 15,000 tiny reads on the UI thread per key.
   var raw=new byte[f.Data.Length*2];using(var input=new MemoryStream(compressed))using(var gzip=new GZipStream(input,CompressionMode.Decompress)){
    int used=0,n;while(used<raw.Length&&(n=gzip.Read(raw,used,raw.Length-used))>0)used+=n;
    if(used!=raw.Length||gzip.ReadByte()!=-1)throw new InvalidDataException("Invalid transition flow length");
   }
   for(int i=0;i<f.Data.Length;i++)f.Data[i]=(short)(raw[i*2]|raw[i*2+1]<<8)/16f;
  }
  cache[id]=f;order.Enqueue(id);while(order.Count>12)cache.Remove(order.Dequeue());DecodeCount++;return f;
 }
 public FlowField Get(int row,int sample){
  if(spec==null)return null;
  try{FlowRow meta;if(!spec.rows.TryGetValue(row.ToString(),out meta))return null;sample=Math.Max(0,Math.Min(meta.count-1,sample));int i=Array.BinarySearch(meta.keys,sample);if(i>=0)return Load(row,i);int right=~i,left=Math.Max(0,right-1);right=Math.Min(meta.keys.Length-1,right);if(left==right)return Load(row,left);return FlowField.Lerp(Load(row,left),Load(row,right),(double)(sample-meta.keys[left])/(meta.keys[right]-meta.keys[left]));}
  catch(Exception e){LastError=e.Message;return null;}
 }
}
}
