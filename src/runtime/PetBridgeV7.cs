using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Web.Script.Serialization;

namespace WhiteDragon {
public sealed class PetStatus {
    public int version=1;
    public string status,source,updated_utc,expires_utc;
}
public static class PetBridge {
    // A single failed atomic-file read must not hand an active native phase
    // back to the less specific window observer. Retain only the previously
    // validated event state, within its original lease; never renew it here.
    public static PetStatus RetainEventLease(PetStatus current,PetStatus previous,DateTime utc){
        if(current!=null)return current;
        if(previous==null||previous.source!="codex-events"||previous.version!=1||Priority(previous.status)<0)return null;
        DateTime at,end;
        if(!DateTime.TryParse(previous.updated_utc,null,System.Globalization.DateTimeStyles.RoundtripKind,out at)||!DateTime.TryParse(previous.expires_utc,null,System.Globalization.DateTimeStyles.RoundtripKind,out end))return null;
        at=at.ToUniversalTime();end=end.ToUniversalTime();
        return end>utc&&at<=utc.AddSeconds(60)&&end<=at.AddHours(1)?previous:null;
    }
    public static int Priority(string status){switch(status){case "needs_input":return 5;case "error":return 4;case "ready":return 3;case "thinking":case "review":case "working":case "executing":return 1;case "idle":return 0;default:return -1;}}
    public static PetStatus Read(string folder,DateTime utc) {
        return Read(folder,utc,null);
    }
    public static PetStatus Best(PetStatus a,PetStatus b){
        if(a==null)return b;if(b==null)return a;
        return Priority(b.status)>Priority(a.status)||Priority(b.status)==Priority(a.status)&&String.CompareOrdinal(b.updated_utc,a.updated_utc)>0?b:a;
    }
    public static PetStatus ResolveSources(PetStatus primary,PetStatus legacy,PetStatus client){
        // Native lifecycle events own the phase while valid. The window
        // observer is a fallback and cannot overwrite an executing hook
        // with its less specific working/thinking/ready observation.
        return primary??legacy??client;
    }
    public static PetStatus Read(string folder,DateTime utc,string source) {
        PetStatus best=null;DateTime bestTime=DateTime.MinValue;
        if(!Directory.Exists(folder))return null;
        // Bounded scan; files contain state enums, never chat text or credentials.
        string[] files;try{files=Directory.GetFiles(folder,"session-*.json");}catch{return null;}
        for(int i=0;i<Math.Min(files.Length,128);i++)try{
            if(new FileInfo(files[i]).Length>4096)continue;
            var state=new JavaScriptSerializer().Deserialize<PetStatus>(File.ReadAllText(files[i],Encoding.UTF8));DateTime at,end;
            if(state==null||state.version!=1||source!=null&&state.source!=source||Priority(state.status)<0||!DateTime.TryParse(state.updated_utc,null,System.Globalization.DateTimeStyles.RoundtripKind,out at)||!DateTime.TryParse(state.expires_utc,null,System.Globalization.DateTimeStyles.RoundtripKind,out end))continue;
            at=at.ToUniversalTime();end=end.ToUniversalTime();
            if(end<=utc||at>utc.AddSeconds(60)||end>at.AddHours(1))continue;
            if(best==null||Priority(state.status)>Priority(best.status)||(Priority(state.status)==Priority(best.status)&&at>bestTime)){best=state;bestTime=at;}
        }catch{}
        return best;
    }
    public static string Write(string folder,string session,string status,string source,DateTime utc) {
        if(Priority(status)<0)throw new ArgumentException("Unknown pet status");
        Directory.CreateDirectory(folder);string hash;
        using(var sha=SHA256.Create())hash=BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(session??"manual"))).Replace("-","").Substring(0,24).ToLowerInvariant();
        int seconds=status=="ready"||status=="error"?12:status=="idle"?8:status=="needs_input"?1800:status=="working"||status=="executing"?3600:300;
        var state=new PetStatus{status=status,source=source,updated_utc=utc.ToString("o"),expires_utc=utc.AddSeconds(seconds).ToString("o")};
        string path=Path.Combine(folder,"session-"+hash+".json"),temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";
        File.WriteAllText(temp,new JavaScriptSerializer().Serialize(state),new UTF8Encoding(false));
        try{for(int i=0;;i++)try{if(File.Exists(path))File.Replace(temp,path,null);else File.Move(temp,path);break;}catch(IOException){if(i>=3)throw;Thread.Sleep(10);}}finally{if(File.Exists(temp))File.Delete(temp);}
        return path;
    }
}
}
