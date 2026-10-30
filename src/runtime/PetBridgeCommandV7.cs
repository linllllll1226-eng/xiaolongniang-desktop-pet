using System;
using System.IO;
using System.Collections.Generic;
using System.Web.Script.Serialization;
namespace WhiteDragon {
public static class PetBridgeCommand {
    public static string HookStatus(string evt,string tool){
        tool=(tool??"").ToLowerInvariant();bool question=tool.EndsWith("request_user_input")||tool.EndsWith("request_user_input_async")||tool.EndsWith("askuserquestion");
        bool command=tool=="bash"||tool=="exec_command"||tool.EndsWith(".exec_command");
        switch(evt){case "UserPromptSubmit":case "PreCompact":case "PostCompact":return "thinking";case "PreToolUse":return question?"needs_input":command?"executing":"working";case "PostToolUse":return question?(tool.EndsWith("_async")?"needs_input":"thinking"):null;case "PermissionRequest":return "needs_input";case "Stop":return "ready";case "Interrupt":case "SessionEnd":return "idle";default:return null;}
    }
    public static int Main(string[] args){
        bool hook=Array.IndexOf(args,"--hook")>=0;
        try{
            string folder=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"联动状态"),status="idle",session="manual",source="manual";
            for(int i=0;i<args.Length;i++){if(args[i]=="--state-dir"&&i+1<args.Length)folder=Path.GetFullPath(args[++i]);else if(args[i]=="--status"&&i+1<args.Length)status=args[++i];else if(args[i]=="--session"&&i+1<args.Length)session=args[++i];}
            if(hook){
                // Read a bounded payload. Never follow transcript_path or persist prompt/tool content.
                var chars=new char[1048577];int used=0,n;while(used<chars.Length&&(n=Console.In.Read(chars,used,chars.Length-used))>0)used+=n;
                if(used>=chars.Length)throw new InvalidDataException("Hook payload exceeds 1 MiB");
                var input=new JavaScriptSerializer{MaxJsonLength=1048576}.Deserialize<Dictionary<string,object>>(new string(chars,0,used));
                object value;string evt=input.TryGetValue("hook_event_name",out value)?Convert.ToString(value):"";
                session=input.TryGetValue("session_id",out value)?Convert.ToString(value):"unknown";source="codex";
                string tool=input.TryGetValue("tool_name",out value)?Convert.ToString(value):"";status=HookStatus(evt,tool);if(status==null){Console.WriteLine("{}");return 0;}
            }
            PetBridge.Write(folder,session,status,source,DateTime.UtcNow);
            Console.WriteLine(hook?"{}":"Pet state: "+status);return 0;
        }catch(Exception ex){if(hook){Console.WriteLine("{}");return 0;}Console.Error.WriteLine("Pet bridge: "+ex.GetType().Name);return 1;}
    }
}
}
