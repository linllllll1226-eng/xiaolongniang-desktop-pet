using System;using System.IO;using System.Text;using System.Collections.Generic;using System.Web.Script.Serialization;using WhiteDragonEvents;
public static class EventRelayTests {
 static JavaScriptSerializer json=new JavaScriptSerializer();static List<string> cases=new List<string>();
 static void Check(bool ok,string name){if(!ok)throw new Exception(name);cases.Add(name);}
 static Dictionary<string,object> E(string type,string kind,string turn){return new Dictionary<string,object>{{"type",type},{"payload",new Dictionary<string,object>{{"type",kind},{"turn_id",turn}}}};}
 static Dictionary<string,object> Call(string name,string input){var e=E("response_item","custom_tool_call",null);var p=PhaseMachine.D(e["payload"]);p["name"]=name;p["input"]=input;return e;}
 public static int Main(string[] args){try{
  var m=new PhaseMachine();Check(!m.Accept(E("event_msg","task_complete","old")),"Starting on an old completion does not invent a cue");
  Check(m.Accept(E("event_msg","task_started","a"))&&m.Status=="thinking","Native task start begins thinking");
  Check(m.Accept(Call("exec","await tools.exec_command({cmd:'Get-Date'})"))&&m.Status=="executing","Real code-mode command shape selects tablet");
  Check(!m.Accept(Call("exec","await tools.exec_command({cmd:'Get-Date'})")),"Repeated command does not restart the phase");
  Check(!m.Accept(E("response_item","custom_tool_call_output",null))&&m.Status=="executing","Tool return retains the command phase");
  Check(!m.Accept(Call("exec","await tools.write_stdin({session_id:123})"))&&m.Status=="executing","Polling a still-running command keeps the tablet phase");
  Check(!m.Accept(Call("wait",""))&&m.Status=="executing","Waiting on the existing script does not restart or leave its phase");
  Check(m.Accept(Call("exec","await tools.web__run({search_query: []})"))&&m.Status=="working","Research wrapper selects reading");
  Check(PhaseMachine.ToolPhase("exec","const s='tools.exec_command({})'; await tools.web__run({})")=="working","A quoted command name does not classify research as execution");
  Check(PhaseMachine.ToolPhase("exec","/* tools.exec_command({}) */ await tools.web__run({})")=="working","A commented command name does not classify research as execution");
  Check(PhaseMachine.ToolPhase("exec","const s=`tools.exec_command({})`; await tools.web__run({})")=="working","A literal template does not classify research as execution");
  Check(PhaseMachine.ToolPhase("exec","const r=/tools.exec_command\\(/; await tools.web__run({})")=="working","A regular expression is not an executed command");
  Check(m.Accept(Call("request_user_input_async",""))&&m.Status=="needs_input","Native question signals waiting");
  Check(m.Accept(Call("Bash",""))&&m.Status=="executing","Further work leaves the question phase");
  Check(!m.Accept(E("event_msg","task_complete","other"))&&m.Status=="executing","An older different turn cannot end the current phase");
  Check(m.Accept(E("event_msg","task_complete","a"))&&m.Status=="ready"&&m.CompletionCount==1,"Native same-turn completion issues one cue");
  Check(!m.Accept(E("event_msg","task_complete","a"))&&m.CompletionCount==1,"Duplicate native completion does not repeat a cue");
  Check(m.Accept(E("event_msg","task_started","b"))&&m.Status=="thinking","New task preempts the completion cue");
  Check(m.Accept(E("event_msg","turn_aborted","b"))&&m.Status=="idle"&&m.CompletionCount==1,"An interrupt does not report successful completion");
  Check(!m.Accept(E("event_msg","token_count","b")),"Usage and unrelated events do not change pose");
  Check(!m.Accept(Call("exec_command",""))&&m.Status=="idle","Orphan tools after interruption cannot resurrect execution");
  DateTime now=DateTime.UtcNow;
  m.Accept(E("event_msg","task_started","c"));m.Accept(Call("exec_command",""));m.Recover(now,now);
  Check(m.Status=="executing"&&m.ActiveSeen,"Startup recovers an ongoing command without replaying pickup events");
  m.Accept(E("event_msg","task_complete","c"));m.Recover(now,now);
  Check(m.Status=="idle"&&m.CompletionCount==0,"Startup after task completion is idle and does not replay a historical cue");
  m.Accept(E("event_msg","task_started","d"));m.Accept(Call("exec_command",""));
  Check(!m.Expire(now,now.AddMinutes(59))&&m.Status=="executing","Long running commands retain the continuous phase within the liveness bound");
  Check(m.Expire(now,now.AddHours(1))&&m.Status=="idle","An abandoned phase returns to idle at the liveness bound");
  m.Accept(E("event_msg","task_started","e"));m.Accept(E("event_msg","task_complete","e"));
  Check(!m.Expire(now,now.AddSeconds(11))&&m.Expire(now,now.AddSeconds(12))&&m.Status=="idle","One completion cue settles into an authoritative idle lease");
  string root=Path.Combine(args[0],"selection-fixture"),project=Path.Combine(root,"project");
  string date=Path.Combine(root,now.ToLocalTime().ToString("yyyy"),now.ToLocalTime().ToString("MM"),now.ToLocalTime().ToString("dd"));Directory.CreateDirectory(date);
  Action<string,string,string> header=(name,cwd,parent)=>File.WriteAllText(Path.Combine(date,name),json.Serialize(new{type="session_meta",payload=new{id=name,cwd=cwd,source="vscode",parent_thread_id=parent}})+"\n");
  header("old.jsonl",project,null);File.SetLastWriteTimeUtc(Path.Combine(date,"old.jsonl"),now.AddMinutes(-5));
  header("new.jsonl",project,null);File.SetLastWriteTimeUtc(Path.Combine(date,"new.jsonl"),now.AddMinutes(-1));
  header("agent.jsonl",project,"parent");header("other.jsonl",root,null);
  Check(SessionEventRelay.LatestProjectSession(root,project,null)==Path.Combine(date,"new.jsonl"),"Project discovery follows recent main chats and excludes child agents and other projects");
  Check(SessionEventRelay.LatestProjectSession(root,Path.Combine(root,"absent"),"fallback")=="fallback","No project match retains the explicit selected session");
  string file=Path.Combine(args[0],"partial-fixture.jsonl");File.WriteAllText(file,"historical\n",new UTF8Encoding(false));
  using(var tail=new SafeTail(file,true)){
   File.AppendAllText(file,"半",new UTF8Encoding(false));Check(new List<string>(tail.Poll()).Count==0,"Partial appended line is held");
   File.AppendAllText(file,"条\n",new UTF8Encoding(false));var lines=new List<string>(tail.Poll());Check(lines.Count==1&&lines[0]=="半条","UTF8 appended record completes without replaying history");
   File.AppendAllText(file,new string('x',1048600)+"\nvalid\n",new UTF8Encoding(false));var large=new List<string>(tail.Poll());Check(tail.OversizedLines==1&&large.Count==1&&large[0]=="valid","Oversized input is skipped and following records recover");
  }
  File.WriteAllText(file,"discarded partial prefix\nvalid\n",new UTF8Encoding(false));using(var tail=new SafeTail(file,false,10)){var lines=new List<string>(tail.Poll());Check(tail.AtEnd&&lines.Count==1&&lines[0]=="valid","Bounded startup tail drops a cut record and recovers the next full line");}
  File.Delete(file);File.WriteAllText(Path.Combine(args[0],"logic-review.json"),json.Serialize(new{ok=true,checks=cases,scope="Synthetic parser, startup recovery, project discovery and bounded tail tests; not proof of live Codex events"}));Console.WriteLine("PASS "+cases.Count+" event relay cases");return 0;
 }catch(Exception e){Console.WriteLine(e.GetType().Name+": "+e.Message);return 1;}}
}
