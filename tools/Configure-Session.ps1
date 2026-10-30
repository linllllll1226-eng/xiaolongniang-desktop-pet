param(
 [Parameter(Mandatory=$true)][string]$ProjectPath,
 [string]$SessionPath,
 [switch]$FixedSession,
 [string]$RuntimeDirectory
)
$ErrorActionPreference='Stop'
if(-not $RuntimeDirectory){$RuntimeDirectory=Split-Path -Parent $PSScriptRoot}
$petRuntime=[IO.Path]::GetFullPath($RuntimeDirectory)
$petProject=[IO.Path]::GetFullPath($ProjectPath)
if(-not(Test-Path -LiteralPath $petProject -PathType Container)){throw 'Project folder is missing'}
if($FixedSession -and -not $SessionPath){throw 'FixedSession requires SessionPath'}
$petSession=if($SessionPath){[IO.Path]::GetFullPath($SessionPath)}else{''}
if($petSession -and (-not(Test-Path -LiteralPath $petSession -PathType Leaf) -or [IO.Path]::GetExtension($petSession) -ne '.jsonl')){throw 'Select a local Codex session JSONL file'}
$petLink=Join-Path $petRuntime '会话联动'
if(-not(Test-Path -LiteralPath (Join-Path $petLink '启动当前会话联动.exe'))){throw 'Select the complete extracted runtime folder'}
$petConfig=Join-Path $petLink '会话联动配置.json'
if(Test-Path -LiteralPath $petConfig){Copy-Item -LiteralPath $petConfig -Destination ($petConfig+'.bak-'+[DateTime]::UtcNow.ToString('yyyyMMddTHHmmssfffZ'))}
@{project_path=$petProject;session_path=$petSession;follow_project=(-not $FixedSession);scope='Only main chats in the selected project; FixedSession pins one chat'}|ConvertTo-Json|Set-Content -LiteralPath $petConfig -Encoding UTF8
Write-Output 'Configured. Restart 会话联动/启动当前会话联动.exe to apply the selected scope.'
