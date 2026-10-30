param(
 [Parameter(Mandatory=$true)][string]$ProjectPath,
 [Parameter(Mandatory=$true)][string]$SessionPath,
 [string]$RuntimeDirectory
)
$ErrorActionPreference='Stop'
if(-not $RuntimeDirectory){$RuntimeDirectory=Split-Path -Parent $PSScriptRoot}
$petRuntime=[IO.Path]::GetFullPath($RuntimeDirectory)
$petProject=[IO.Path]::GetFullPath($ProjectPath)
$petSession=[IO.Path]::GetFullPath($SessionPath)
if(-not(Test-Path -LiteralPath $petProject -PathType Container)){throw 'Project folder is missing'}
if(-not(Test-Path -LiteralPath $petSession -PathType Leaf) -or [IO.Path]::GetExtension($petSession) -ne '.jsonl'){throw 'Select a local Codex session JSONL file'}
$petLink=Join-Path $petRuntime '会话联动'
if(-not(Test-Path -LiteralPath (Join-Path $petLink '启动当前会话联动.exe'))){throw 'Select the complete extracted runtime folder'}
$petConfig=Join-Path $petLink '会话联动配置.json'
if(Test-Path -LiteralPath $petConfig){Copy-Item -LiteralPath $petConfig -Destination ($petConfig+'.bak-'+[DateTime]::UtcNow.ToString('yyyyMMddTHHmmssfffZ'))}
@{project_path=$petProject;session_path=$petSession;scope='Only the explicitly selected local chat'}|ConvertTo-Json|Set-Content -LiteralPath $petConfig -Encoding UTF8
Write-Output 'Configured. Start 会话联动/启动当前会话联动.exe to use this selected chat.'
