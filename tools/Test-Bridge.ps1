param([string]$Executable='outputs\小龙娘_v10.8_新动作与思考版\白龙娘状态桥.exe',[string]$OutputDirectory='work\audit-v108\bridge')
$ErrorActionPreference='Stop';$petProject=Split-Path -Parent $PSScriptRoot
$petExe=if([IO.Path]::IsPathRooted($Executable)){[IO.Path]::GetFullPath($Executable)}else{[IO.Path]::GetFullPath((Join-Path $petProject $Executable))}
$petFolder=[IO.Path]::GetFullPath((Join-Path $petProject $OutputDirectory));if(-not $petFolder.StartsWith($petProject+[IO.Path]::DirectorySeparatorChar)){throw 'Fixtures must stay inside project'}
New-Item -ItemType Directory -Force -Path $petFolder|Out-Null
$petCases=@(@('UserPromptSubmit','','thinking'),@('PreToolUse','Bash','working'),@('PostToolUse','Bash','thinking'),@('PreToolUse','functions.request_user_input','needs_input'),@('PostToolUse','functions.request_user_input','thinking'),@('PreToolUse','functions.request_user_input_async','needs_input'),@('PostToolUse','functions.request_user_input_async','needs_input'),@('PermissionRequest','Bash','needs_input'),@('Stop','','ready'),@('Interrupt','','idle'),@('SessionEnd','','idle'))
$petResults=@();$petIndex=0
foreach($petCase in $petCases){
 $petSession='audit-'+$petIndex;$petPayload=@{hook_event_name=$petCase[0];session_id=$petSession;tool_name=$petCase[1];transcript_path='must-not-open';tool_input=@{question='synthetic-do-not-persist'}}|ConvertTo-Json -Compress
 $petInfo=[Diagnostics.ProcessStartInfo]::new();$petInfo.FileName=$petExe;$petInfo.Arguments='--hook --state-dir "'+$petFolder+'"';$petInfo.UseShellExecute=$false;$petInfo.CreateNoWindow=$true;$petInfo.RedirectStandardInput=$true;$petInfo.RedirectStandardOutput=$true;$petInfo.RedirectStandardError=$true
 $petProcess=[Diagnostics.Process]::Start($petInfo);$petProcess.StandardInput.WriteLine($petPayload);$petProcess.StandardInput.Close()
 if(-not $petProcess.WaitForExit(4000)){$petProcess.Kill();throw 'Fixture bridge timed out'}
 $petReply=$petProcess.StandardOutput.ReadToEnd().Trim();if($petProcess.ExitCode -ne 0 -or $petReply -ne '{}'){throw 'Bridge fixture failed'}
 $petHasher=[Security.Cryptography.SHA256]::Create();try{$petHash=[BitConverter]::ToString($petHasher.ComputeHash([Text.Encoding]::UTF8.GetBytes($petSession))).Replace('-','').ToLowerInvariant().Substring(0,24)}finally{$petHasher.Dispose()}
 $petText=[IO.File]::ReadAllText((Join-Path $petFolder ('session-'+$petHash+'.json')));$petState=$petText|ConvertFrom-Json
 if($petState.status -ne $petCase[2] -or $petState.source -ne 'codex' -or $petText.Contains('synthetic-do-not-persist')){throw 'Bridge mapping or payload persistence failed'}
 $petResults+=@{event=$petCase[0];tool=$petCase[1];status=$petState.status};$petIndex++
}
$petReport=@{ok=$true;count=$petResults.Count;bridge_sha256=(Get-FileHash -LiteralPath $petExe).Hash.ToLowerInvariant();cases=$petResults;scope='Bounded synthetic payloads through actual executable; no real lifecycle callback claimed';no_payload_text_persisted=$true}
$petReport|ConvertTo-Json -Depth 5|Set-Content -LiteralPath (Join-Path $petFolder 'bridge-review.json') -Encoding UTF8
$petReport|Select-Object ok,count,bridge_sha256,scope|ConvertTo-Json
