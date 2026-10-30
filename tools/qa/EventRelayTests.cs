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
  string file=Path.Combine(args[0],"partial-fixture.jsonl");File.WriteAllText(file,"historical\n",new UTF8Encoding(false));
  using(var tail=new SafeTail(file,true)){
   File.AppendAllText(file,"半",new UTF8Encoding(false));Check(new List<string>(tail.Poll()).Count==0,"Partial appended line is held");
   File.AppendAllText(file,"条\n",new UTF8Encoding(false));var lines=new List<string>(tail.Poll());Check(lines.Count==1&&lines[0]=="半条","UTF8 appended record completes without replaying history");
   File.AppendAllText(file,new string('x',1048600)+"\nvalid\n",new UTF8Encoding(false));var large=new List<string>(tail.Poll());Check(tail.OversizedLines==1&&large.Count==1&&large[0]=="valid","Oversized input is skipped and following records recover");
  }
  File.Delete(file);File.WriteAllText(Path.Combine(args[0],"logic-review.json"),json.Serialize(new{ok=true,checks=cases,scope="Synthetic parser and bounded tail tests; not proof of live Codex events"}));Console.WriteLine("PASS "+cases.Count+" event relay cases");return 0;
 }catch(Exception e){Console.WriteLine(e.GetType().Name+": "+e.Message);return 1;}}
}
