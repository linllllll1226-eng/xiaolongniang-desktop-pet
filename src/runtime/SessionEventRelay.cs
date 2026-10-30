using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Threading;
using System.Web.Script.Serialization;

namespace WhiteDragonEvents {
// This observer is independent of command hooks. It never executes log content,
// follows transcript_path fields, or copies prompts, code or messages to reports.
public sealed class PhaseMachine {
 public string Status,Turn;
 public bool ActiveSeen;
 public int CompletionCount;
 public void Recover(DateTime lastEvent,DateTime now){
  if(!ActiveSeen||now-lastEvent>=TimeSpan.FromHours(1)){Status="idle";ActiveSeen=false;}
  CompletionCount=0;
 }
 public bool Expire(DateTime lastEvent,DateTime now){
  if(Status=="ready"&&now-lastEvent>=TimeSpan.FromSeconds(12)||ActiveSeen&&now-lastEvent>=TimeSpan.FromHours(1)){Status="idle";ActiveSeen=false;return true;}return false;
 }
 public static Dictionary<string,object> D(object value){return value as Dictionary<string,object>;}
 public static string S(Dictionary<string,object> d,string key){object v;return d!=null&&d.TryGetValue(key,out v)&&v is string?(string)v:null;}
 public static object V(Dictionary<string,object> d,string key){object v;return d!=null&&d.TryGetValue(key,out v)?v:null;}
 public static string ToolPhase(string name,string input){
  name=(name??"").ToLowerInvariant();
  if(name.EndsWith("request_user_input")||name.EndsWith("request_user_input_async")||name.EndsWith("askuserquestion"))return "needs_input";
  if(name=="wait"||name=="functions.wait")return null;
  if(name=="bash"||name=="exec_command"||name.EndsWith(".exec_command")||name=="write_stdin"||name.EndsWith(".write_stdin"))return "executing";
  if(name=="exec"||name=="functions.exec")return ContainsCommandCall(input??"")?"executing":"working";
  return "working";
 }
 // Ignore JS strings, templates and comments when classifying a code-mode call.
 // Never evaluate JavaScript or inspect a shell command's contents.
 static bool ContainsCommandCall(string code){
  var clean=new StringBuilder(code.Length);int mode=0;bool escape=false;
  for(int i=0;i<code.Length;i++){
   char c=code[i],next=i+1<code.Length?code[i+1]:'\0';
   if(mode==4){if(c=='\n'){mode=0;clean.Append('\n');}continue;}
   if(mode==5){if(c=='*'&&next=='/'){mode=0;i++;}continue;}
   if(mode!=0){if(escape){escape=false;continue;}if(c=='\\'){escape=true;continue;}if(c==(mode==1?'\'':mode==2?'"':mode==3?'`':'/'))mode=0;continue;}
   if(c=='/'&&next=='/'){mode=4;i++;continue;}if(c=='/'&&next=='*'){mode=5;i++;continue;}
   if(c=='/'){int previous=clean.Length-1;while(previous>=0&&Char.IsWhiteSpace(clean[previous]))previous--;if(previous<0||"=(:,[!&|?;{".IndexOf(clean[previous])>=0){mode=6;clean.Append(' ');continue;}}
   if(c=='\''||c=='"'||c=='`'){mode=c=='\''?1:c=='"'?2:3;clean.Append(' ');continue;}clean.Append(c);
  }
  return System.Text.RegularExpressions.Regex.IsMatch(clean.ToString(),@"\btools\s*\.\s*(?:exec_command|write_stdin)\s*\(");
 }
 public bool Accept(Dictionary<string,object> record){
  var p=D(V(record,"payload"));string type=S(record,"type"),kind=S(p,"type"),next=null;
  if(type=="event_msg"){
   string turn=S(p,"turn_id")??S(p,"root_turn_id");
   if(kind=="task_started"){Turn=turn;ActiveSeen=true;next="thinking";}
   else if(kind=="task_complete"){
    if(!ActiveSeen||Turn!=null&&turn!=null&&Turn!=turn)return false;
    if(Status=="ready"||Status=="idle")return false;
    next="ready";ActiveSeen=false;CompletionCount++;
   }else if(kind=="turn_aborted"||kind=="task_aborted"){
    if(Turn!=null&&turn!=null&&Turn!=turn)return false;
    next="idle";ActiveSeen=false;
   }
  }else if(type=="response_item"&&(kind=="function_call"||kind=="custom_tool_call")){
   if(!ActiveSeen)return false;
   string name=S(p,"name");if(String.IsNullOrEmpty(name))return false;
   next=ToolPhase(name,S(p,"input"));ActiveSeen=true;
  }
  if(next==null||next==Status)return false;Status=next;return true;
 }
}

public sealed class SafeTail:IDisposable {
 readonly FileStream file;readonly List<byte> partial=new List<byte>();readonly byte[] buffer=new byte[65536];bool dropping;
 public int OversizedLines;
 public SafeTail(string path,bool end):this(path,end,0){}
 public SafeTail(string path,bool end,long historyBytes){file=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete);if(end)file.Seek(0,SeekOrigin.End);else if(historyBytes>0&&file.Length>historyBytes){file.Seek(-historyBytes,SeekOrigin.End);dropping=true;}}
 public bool AtEnd{get{return file.Position>=file.Length;}}
 public IEnumerable<string> Poll(){
  var lines=new List<string>();if(file.Length<file.Position){file.Seek(0,SeekOrigin.End);partial.Clear();dropping=false;return lines;}
  int total=0,n;while(total<2097152&&(n=file.Read(buffer,0,buffer.Length))>0){total+=n;for(int i=0;i<n;i++){
   byte b=buffer[i];if(b==10){if(!dropping&&partial.Count>0)lines.Add(Encoding.UTF8.GetString(partial.ToArray()));partial.Clear();dropping=false;}
   else if(!dropping){if(partial.Count>=1048576){partial.Clear();dropping=true;OversizedLines++;}else partial.Add(b);}
  }}return lines;
 }
 public void Dispose(){file.Dispose();}
}

public static class SessionEventRelay {
 static readonly JavaScriptSerializer Json=new JavaScriptSerializer{MaxJsonLength=1048576};
 static readonly string Owner="white-dragon-session-events-v1";
 static string Arg(string[] args,string name){int i=Array.IndexOf(args,name);return i>=0&&i+1<args.Length?args[i+1]:null;}
 static string Hash(string text){using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(text))).Replace("-","").ToLowerInvariant().Substring(0,24);}
 static string Normal(string path){return Path.GetFullPath(path.Replace(@"\\?\","")).TrimEnd('\\');}
 static void Atomic(string path,object value){string temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";File.WriteAllText(temp,Json.Serialize(value),new UTF8Encoding(false));try{if(File.Exists(path))File.Replace(temp,path,null);else File.Move(temp,path);}finally{if(File.Exists(temp))File.Delete(temp);}}
 static Dictionary<string,object> Header(string path){using(var f=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete)){var bytes=new List<byte>();int b;while(bytes.Count<1048576&&(b=f.ReadByte())>=0){if(b==10){var row=Json.Deserialize<Dictionary<string,object>>(Encoding.UTF8.GetString(bytes.ToArray()));if(PhaseMachine.S(row,"type")!="session_meta")throw new InvalidDataException("Missing session metadata");return PhaseMachine.D(PhaseMachine.V(row,"payload"));}bytes.Add((byte)b);}throw new InvalidDataException("Invalid session header");}}
 static string Q(string path){return "\""+path+"\"";}
 public static string LatestProjectSession(string sessions,string project,string fallback){
  string best=fallback;DateTime newest=DateTime.MinValue;
  // Inspect metadata only; exclude agent children and unrelated projects.
  for(int day=0;day<7;day++){
   DateTime date=DateTime.Now.AddDays(-day);
   string dir=Path.Combine(sessions,date.ToString("yyyy"),date.ToString("MM"),date.ToString("dd"));
   if(!Directory.Exists(dir))continue;
   foreach(string candidate in Directory.GetFiles(dir,"*.jsonl"))try{
    DateTime at=File.GetLastWriteTimeUtc(candidate);if(at<=newest)continue;
    var meta=Header(candidate);string cwd=PhaseMachine.S(meta,"cwd"),source=PhaseMachine.S(meta,"source");
    if(cwd==null||!String.Equals(Normal(cwd),Normal(project),StringComparison.OrdinalIgnoreCase)||PhaseMachine.V(meta,"parent_thread_id")!=null||(source!="vscode"&&source!="cli"))continue;
    newest=at;best=candidate;
   }catch{}
  }
  return best;
 }
 static int Launch(){try{
  string here=AppDomain.CurrentDomain.BaseDirectory,folder=Normal(Path.Combine(here,"..")),petExe=Path.Combine(folder,"启动小龙娘.exe");
  var config=Json.Deserialize<Dictionary<string,object>>(File.ReadAllText(Path.Combine(here,"会话联动配置.json"),Encoding.UTF8));
  string project=Normal(PhaseMachine.S(config,"project_path")),session=PhaseMachine.S(config,"session_path");
  bool follow=PhaseMachine.V(config,"follow_project") is bool&&(bool)PhaseMachine.V(config,"follow_project");
  if(follow)session=LatestProjectSession(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),".codex","sessions"),project,session);
  session=Normal(session);
  string hash;using(var stream=File.OpenRead(petExe))using(var sha=SHA256.Create())hash=BitConverter.ToString(sha.ComputeHash(stream)).Replace("-","").ToLowerInvariant();
  if(hash!="__WHITE_DRAGON_MAIN_SHA256__")throw new InvalidOperationException("主程序版本不匹配，请使用同一运行包内的联动入口。");
  Process pet=null;foreach(var p in Process.GetProcessesByName("启动小龙娘")){
   if(!String.Equals(Normal(p.MainModule.FileName),petExe,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("已有另一份桌宠运行，请先退出另一份。");
   if(pet!=null)throw new InvalidOperationException("已有多份桌宠运行。");pet=p;
  }
  if(pet==null)pet=Process.Start(new ProcessStartInfo(petExe){WorkingDirectory=folder,UseShellExecute=true});
  string report=Path.Combine(folder,"联动验收","会话实时事件.json");
  var info=new ProcessStartInfo(Assembly.GetExecutingAssembly().Location,"--session "+Q(session)+" --project "+Q(project)+" --pet-exe "+Q(petExe)+" --pet-pid "+pet.Id+" --until-pet-exit "+(follow?"--follow-project ":"")+"--report "+Q(report)){UseShellExecute=false,CreateNoWindow=true,WorkingDirectory=here};
  Process.Start(info);return 0;
 }catch(Exception e){System.Windows.Forms.MessageBox.Show("当前会话联动未启动："+e.Message,"小龙娘",System.Windows.Forms.MessageBoxButtons.OK,System.Windows.Forms.MessageBoxIcon.Information);return 1;}}
 public static int Main(string[] args){
  if(args.Length==0)return Launch();
  Mutex instance=null;bool ownsMutex=false;
  string stateFile=null,reportFile=null;var events=new List<object>();var machine=new PhaseMachine();DateTime started=DateTime.UtcNow,lastEvent=started,lastWrite=DateTime.MinValue;string stop="error";int malformed=0,oversized=0;bool continuous=Array.IndexOf(args,"--until-pet-exit")>=0;
  try{
   string path=Normal(Arg(args,"--session")),project=Normal(Arg(args,"--project")),petExe=Normal(Arg(args,"--pet-exe"));
   string sessions=Normal(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),".codex","sessions"))+"\\";
   if(!path.StartsWith(sessions,StringComparison.OrdinalIgnoreCase)||!path.EndsWith(".jsonl",StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Unexpected event path");
   var pet=Process.GetProcessById(int.Parse(Arg(args,"--pet-pid")));if(!String.Equals(Normal(pet.MainModule.FileName),petExe,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Unexpected pet process");
   Dictionary<string,object> meta=Header(path);
   string id=PhaseMachine.S(meta,"id"),cwd=PhaseMachine.S(meta,"cwd"),source=PhaseMachine.S(meta,"source");
   if(String.IsNullOrEmpty(id)||cwd==null||!String.Equals(Normal(cwd),project,StringComparison.OrdinalIgnoreCase)||PhaseMachine.V(meta,"parent_thread_id")!=null||(source!="vscode"&&source!="cli"))throw new InvalidOperationException("Not the selected user project session");
   instance=new Mutex(true,"Local\\WhiteDragon.SessionEvents."+Hash(petExe+":"+pet.Id),out ownsMutex);if(!ownsMutex)return 0;
   string folder=Path.GetDirectoryName(petExe),stateDir=Path.Combine(folder,"联动状态");Directory.CreateDirectory(stateDir);
   stateFile=Path.Combine(stateDir,"session-event-relay.json");
   // Only remove obsolete state files produced by this observer.
   foreach(string old in Directory.GetFiles(stateDir,"session-*.json"))try{if(PhaseMachine.S(Json.Deserialize<Dictionary<string,object>>(File.ReadAllText(old)),"owner")==Owner)File.Delete(old);}catch{}
   reportFile=Normal(Arg(args,"--report"));Directory.CreateDirectory(Path.GetDirectoryName(reportFile));
   int seconds=Arg(args,"--seconds")==null?180:int.Parse(Arg(args,"--seconds"));if(seconds<10||seconds>600)throw new InvalidOperationException("Bounded duration must be 10..600 seconds");
   bool followProject=Array.IndexOf(args,"--follow-project")>=0,bootstrap=true;
   DateTime lastReport=DateTime.MinValue,lastDiscovery=DateTime.MinValue;
   SafeTail tail=new SafeTail(path,false,8388608);try{
    while(continuous||DateTime.UtcNow<started.AddSeconds(seconds)){
     pet.Refresh();if(pet.HasExited){stop="pet_closed";break;}
     DateTime now=DateTime.UtcNow;
     if(followProject&&now-lastDiscovery>TimeSpan.FromSeconds(3)){
      lastDiscovery=now;string selected=LatestProjectSession(sessions,project,path);
      if(!String.Equals(selected,path,StringComparison.OrdinalIgnoreCase)){
       var selectedMeta=Header(selected);tail.Dispose();path=selected;id=PhaseMachine.S(selectedMeta,"id");tail=new SafeTail(path,false,8388608);machine=new PhaseMachine();bootstrap=true;lastWrite=DateTime.MinValue;
      }
     }
     foreach(string line in tail.Poll())try{
      var record=Json.Deserialize<Dictionary<string,object>>(line);string kind=PhaseMachine.S(record,"type");
      if(kind!="response_item"&&kind!="event_msg")continue;
      DateTime at;if(!DateTime.TryParse(PhaseMachine.S(record,"timestamp"),CultureInfo.InvariantCulture,DateTimeStyles.RoundtripKind,out at))continue;at=at.ToUniversalTime();
      if(at>now.AddSeconds(10))continue;
      var p=PhaseMachine.D(PhaseMachine.V(record,"payload"));string eventType=PhaseMachine.S(p,"type");
      bool relevant=kind=="response_item"&&(eventType=="function_call"||eventType=="custom_tool_call")||kind=="event_msg"&&(eventType=="task_started"||eventType=="task_complete"||eventType=="turn_aborted"||eventType=="task_aborted");
      if(!relevant)continue;
      if(kind=="response_item"||eventType=="task_started")lastEvent=at;
      if(machine.Accept(record)){
       lastEvent=at;lastWrite=DateTime.MinValue;lastReport=DateTime.MinValue;
       if(!bootstrap){if(events.Count>=128)events.RemoveAt(0);events.Add(new{observed_utc=now.ToString("o"),event_utc=at.ToString("o"),native_event=eventType,status=machine.Status,completion_count=machine.CompletionCount});}
      }
     }catch{malformed++;}
     if(bootstrap){
      if(!tail.AtEnd)continue;
      // Recover ongoing work, but never replay historical completion cues.
      machine.Recover(lastEvent,now);bootstrap=false;lastWrite=DateTime.MinValue;
     }
     if(machine.Expire(lastEvent,now))lastWrite=DateTime.MinValue;
     oversized=tail.OversizedLines;
     if(machine.Status!=null&&now-lastWrite>TimeSpan.FromSeconds(2)){
      bool once=machine.Status=="ready";
      if(!once||lastWrite==DateTime.MinValue){int lease=once?(machine.Status=="ready"?12:8):10;
       Atomic(stateFile,new{version=1,status=machine.Status,source="codex-events",updated_utc=now.ToString("o"),expires_utc=now.AddSeconds(lease).ToString("o"),owner=Owner,event_origin="local-session-lifecycle"});lastWrite=now;
      }
     }
     if(now-lastReport>TimeSpan.FromSeconds(5)){Atomic(reportFile,new{started_utc=started.ToString("o"),updated_utc=now.ToString("o"),finished=false,source="codex-events",session_hash=Hash(id),native_completion_count=machine.CompletionCount,events=events,malformed_records=malformed,oversized_lines=oversized,scope="Only newly appended selected-session lifecycle and tool type; no chat, command or tool result content retained; independent of command hooks"});lastReport=now;}
     Thread.Sleep(150);
    }
   }finally{tail.Dispose();}
   if(stop=="error")stop="duration_limit";
  }catch(Exception e){stop=e.GetType().Name;}
  finally{
   if(stateFile!=null&&File.Exists(stateFile))try{var old=Json.Deserialize<Dictionary<string,object>>(File.ReadAllText(stateFile));if(PhaseMachine.S(old,"owner")==Owner)File.Delete(stateFile);}catch{}
   if(reportFile!=null)try{Atomic(reportFile,new{started_utc=started.ToString("o"),finished_utc=DateTime.UtcNow.ToString("o"),finished=true,stop_reason=stop,source="codex-events",native_completion_count=machine.CompletionCount,events=events,malformed_records=malformed,oversized_lines=oversized});}catch{}
   if(instance!=null){if(ownsMutex)instance.ReleaseMutex();instance.Dispose();}
  }
  return stop=="duration_limit"||stop=="pet_closed"?0:1;
 }
}
}
