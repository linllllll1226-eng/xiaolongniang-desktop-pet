using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Collections.Generic;
using System.Text;
using System.Windows.Automation;
namespace WhiteDragon {
// Reads only the accessibility names of visible Send/Stop controls. Chat
// documents, message text, credentials and transcripts are never requested.
public sealed class ClientObservation {
 public bool Found,Observable,Busy,Thinking,NeedsInput;public int ButtonCount;public IntPtr Window;public string Error;
}
public sealed class ClientLink:IDisposable {
 readonly object gate=new object();readonly AutoResetEvent wake=new AutoResetEvent(false);Thread worker;volatile bool stopped,enabled;
 PetStatus state;string description="等待客户端";bool previousBusy;IntPtr previousWindow;int idleSamples;DateTime readyUntil;
 long callbackCount,lastCallbackTicks;AutomationElement watchedRoot;IntPtr watchedWindow;bool scanFromEvent;
 public long EventCallbacks{get{return Interlocked.Read(ref callbackCount);}}
 public bool WorkerStopped{get{return worker==null||!worker.IsAlive;}}
 public readonly List<string[]> RealStateEvents=new List<string[]>();
 public string LastCallbackUtc{get{long ticks=Interlocked.Read(ref lastCallbackTicks);return ticks>0?new DateTime(ticks,DateTimeKind.Utc).ToString("o"):null;}}
 public bool Enabled{get{return enabled;}set{enabled=value;if(!value)lock(gate){state=null;previousBusy=false;description="未开启";}wake.Set();}}
 public string Description{get{lock(gate)return description;}}
 public PetStatus State(DateTime utc){lock(gate){DateTime end;return enabled&&state!=null&&DateTime.TryParse(state.expires_utc,out end)&&end.ToUniversalTime()>utc?state:null;}}
 public void Start(){if(worker!=null)return;worker=new Thread(Loop){IsBackground=true,Name="WhiteDragon client control observer"};worker.SetApartmentState(ApartmentState.MTA);worker.Start();}
 void Loop(){long seen=0;var clock=Stopwatch.StartNew();long previous=-1000;try{while(!stopped){
   long gap=clock.ElapsedMilliseconds-previous;if(gap<220){wake.WaitOne((int)(220-gap));continue;}
   previous=clock.ElapsedMilliseconds;
   if(enabled)try{long events=Interlocked.Read(ref callbackCount);scanFromEvent=events!=seen;seen=events;var observed=Scan();BindWindow(observed.Window);Observe(observed,DateTime.UtcNow);}catch{lock(gate){description="客户端暂不可读取";}}
   else UnbindWindow();wake.WaitOne(1200);
  }}finally{UnbindWindow();}}
 void Changed(){if(stopped)return;Interlocked.Increment(ref callbackCount);Interlocked.Exchange(ref lastCallbackTicks,DateTime.UtcNow.Ticks);wake.Set();}
 void OnStructure(object sender,StructureChangedEventArgs args){Changed();}
 void OnEnabled(object sender,AutomationPropertyChangedEventArgs args){Changed();}
 void BindWindow(IntPtr handle){
  if(handle==watchedWindow)return;UnbindWindow();if(handle==IntPtr.Zero)return;
  try{watchedRoot=AutomationElement.FromHandle(handle);if(watchedRoot==null)return;Automation.AddStructureChangedEventHandler(watchedRoot,TreeScope.Subtree,OnStructure);Automation.AddAutomationPropertyChangedEventHandler(watchedRoot,TreeScope.Subtree,OnEnabled,AutomationElement.IsEnabledProperty);watchedWindow=handle;}catch{UnbindWindow();}
 }
 void UnbindWindow(){var root=watchedRoot;watchedRoot=null;watchedWindow=IntPtr.Zero;if(root==null)return;try{Automation.RemoveStructureChangedEventHandler(root,OnStructure);}catch{}try{Automation.RemoveAutomationPropertyChangedEventHandler(root,OnEnabled);}catch{}}
 public void Observe(ClientObservation observation,DateTime now){
  lock(gate){
   if(!enabled)return;
   if(!observation.Found){state=null;previousBusy=false;idleSamples=0;description="客户端未打开";return;}
   if(!observation.Observable){description="已打开 · 未取得回复状态";return;}
   if(previousWindow!=observation.Window){previousBusy=false;idleSamples=0;readyUntil=DateTime.MinValue;previousWindow=observation.Window;}
   string status;
   if(observation.NeedsInput){previousBusy=true;idleSamples=0;readyUntil=DateTime.MinValue;status="needs_input";description="已连接 · 等你回答";}
   else if(observation.Busy){previousBusy=true;idleSamples=0;readyUntil=DateTime.MinValue;status=observation.Thinking?"thinking":"working";description=observation.Thinking?"已连接 · 正在思考":"已连接 · 正在工作";}
   else if(previousBusy&&++idleSamples<2){description="已连接 · 确认回复结束";return;}
   else {if(previousBusy){readyUntil=now.AddSeconds(12);previousBusy=false;}status=now<readyUntil?"ready":"idle";description=status=="ready"?"已连接 · 回复已结束":"已连接 · 等待回复";}
   if(worker!=null&&Thread.CurrentThread==worker&&(state==null||state.status!=status)){if(RealStateEvents.Count>=128)RealStateEvents.RemoveAt(0);RealStateEvents.Add(new string[]{now.ToString("o"),status,scanFromEvent?"uia-event":"control-poll"});}
   state=new PetStatus{status=status,source="chatgpt-client",updated_utc=now.ToString("o"),expires_utc=now.AddSeconds(8).ToString("o")};
  }
 }
 static bool IsStop(string name){if(name==null)return false;name=name.Trim().ToLowerInvariant();return name=="stop"||name.StartsWith("stop generating")||name.StartsWith("stop streaming")||name.StartsWith("stop response")||name.StartsWith("stop responding")||name.StartsWith("stop (esc)")||name=="停止"||name.StartsWith("停止生成")||name.StartsWith("停止回复")||name.StartsWith("停止响应")||name.StartsWith("停止回答");}
 static bool IsSend(string name){if(name==null)return false;name=name.Trim().ToLowerInvariant();return name=="send"||name.StartsWith("send message")||name.StartsWith("send prompt")||name.StartsWith("send now")||name=="发送"||name.StartsWith("发送消息")||name.StartsWith("发送提示");}
 public static bool IsThinkingControl(string name){name=(name??"").Trim().ToLowerInvariant();return name=="thinking"||name.StartsWith("thinking...")||name.StartsWith("thinking…")||name=="正在思考"||name=="思考中"||name.StartsWith("正在思考…")||name.StartsWith("思考中…");}
 public static bool IsQuestionSubmit(string name){name=(name??"").Trim().ToLowerInvariant();return name=="submit"||name=="submit answers"||name=="submit answer"||name=="提交"||name=="提交答案"||name=="提交回答"||name=="发送答案"||name=="回答问题";}
 public static bool IsApprovalControl(string name){name=(name??"").Trim().ToLowerInvariant();return name=="allow once"||name=="allow this time"||name=="approve"||name=="approve once"||name=="approve and run"||name=="允许一次"||name=="仅本次允许"||name=="允许并运行"||name=="批准";}
 public static ClientObservation Scan(){
  var result=new ClientObservation();IntPtr foreground=GetForegroundWindow();
  foreach(var process in Process.GetProcessesByName("ChatGPT"))try{
   IntPtr h=process.MainWindowHandle;if(h==IntPtr.Zero||!IsWindowVisible(h))continue;bool minimized=IsIconic(h);
   // All of today's ChatGPT/Codex desktop modes share this process name.
   // Prefer the foreground app window; never inspect unrelated applications.
   if(result.Found&&h!=foreground)continue;
   result.Found=true;result.Window=h;
   // Request the app's standard accessible client once, the same handshake
   // used by Windows assistive tools; no restart or command-line flags.
   var targets=new List<IntPtr>{h};EnumChildWindows(h,delegate(IntPtr child,IntPtr unused){var cls=new StringBuilder(256);GetClassName(child,cls,256);if(cls.ToString()=="Chrome_RenderWidgetHostHWND")targets.Add(child);return true;},IntPtr.Zero);
   foreach(IntPtr target in targets)try{object accessible;Guid iid=new Guid("618736E0-3C3D-11CF-810C-00AA00389B71");AccessibleObjectFromWindow(target,0xFFFFFFFC,ref iid,out accessible);if(accessible!=null&&Marshal.IsComObject(accessible))Marshal.ReleaseComObject(accessible);}catch{}
   var root=AutomationElement.FromHandle(h);if(root==null)continue;
   var buttons=root.FindAll(TreeScope.Descendants,new PropertyCondition(AutomationElement.ControlTypeProperty,ControlType.Button));
   var candidate=new ClientObservation{Found=true,Window=h,ButtonCount=buttons.Count};
   for(int i=0;i<buttons.Count;i++)try{var info=buttons[i].Current;if(info.IsOffscreen&&!minimized)continue;string name=info.Name;if(IsStop(name)&&info.IsEnabled){candidate.Busy=true;candidate.Observable=true;}if(IsSend(name))candidate.Observable=true;if(info.IsEnabled&&(IsQuestionSubmit(name)||IsApprovalControl(name))){candidate.NeedsInput=true;candidate.Observable=true;}if(info.IsEnabled&&IsThinkingControl(name))candidate.Thinking=true;}catch(ElementNotAvailableException){}
   if(candidate.Busy&&!candidate.Thinking){var indicators=root.FindAll(TreeScope.Descendants,new OrCondition(new PropertyCondition(AutomationElement.ControlTypeProperty,ControlType.StatusBar),new PropertyCondition(AutomationElement.ControlTypeProperty,ControlType.ProgressBar)));for(int i=0;i<indicators.Count;i++)try{var info=indicators[i].Current;if((minimized||!info.IsOffscreen)&&IsThinkingControl(info.Name)){candidate.Thinking=true;break;}}catch(ElementNotAvailableException){}}
   result=candidate;if(h==foreground)break;
  }catch(Exception error){result.Error=error.GetType().Name;}finally{process.Dispose();}
  return result;
 }
 public static void OpenApp(){foreach(var p in Process.GetProcessesByName("ChatGPT"))try{if(p.MainWindowHandle!=IntPtr.Zero){ShowWindow(p.MainWindowHandle,9);SetForegroundWindow(p.MainWindowHandle);return;}}catch{}finally{p.Dispose();}Process.Start("explorer.exe",@"shell:AppsFolder\OpenAI.Codex_2p2nqsd0c76g0!App");}
 public void Dispose(){stopped=true;wake.Set();if(worker!=null)worker.Join(600);}
 [DllImport("user32.dll")]static extern IntPtr GetForegroundWindow();[DllImport("user32.dll")]static extern bool IsWindowVisible(IntPtr h);[DllImport("user32.dll")]static extern bool IsIconic(IntPtr h);[DllImport("user32.dll")]static extern bool ShowWindow(IntPtr h,int n);[DllImport("user32.dll")]static extern bool SetForegroundWindow(IntPtr h);
 [DllImport("oleacc.dll")]static extern int AccessibleObjectFromWindow(IntPtr h,uint id,ref Guid iid,[MarshalAs(UnmanagedType.Interface)]out object accessible);
 delegate bool EnumProc(IntPtr h,IntPtr p);[DllImport("user32.dll")]static extern bool EnumChildWindows(IntPtr h,EnumProc callback,IntPtr p);[DllImport("user32.dll",CharSet=CharSet.Unicode)]static extern int GetClassName(IntPtr h,StringBuilder s,int n);
}
}
