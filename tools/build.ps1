param(
    [string]$AssetDirectory = 'work\release-assets-v109',
    [string]$OutputDirectory = 'outputs\小龙娘_v10.9.2_源码构建版',
    [string]$AcceptanceDirectory = 'work\style-v1092\native',
    [switch]$SkipNativeTest,
    [switch]$WindowPerch,
    [switch]$DailyRoutine,
    [switch]$FocusCompanion,
    [switch]$SmallTalk,
    [switch]$ClickBounce,
    [switch]$Unified,
    [string]$ChatSessionPath
)
$ErrorActionPreference='Stop'
if($Unified -or -not($WindowPerch -or $DailyRoutine -or $FocusCompanion -or $SmallTalk -or $ClickBounce)){$ClickBounce=$true}
if($ClickBounce){
    $Unified=$true
    if(-not $PSBoundParameters.ContainsKey('OutputDirectory')){$OutputDirectory='outputs\小龙娘_v10.15.2_空闲联动修正版';$PSBoundParameters['OutputDirectory']=$OutputDirectory}
    if(-not $PSBoundParameters.ContainsKey('AcceptanceDirectory')){$AcceptanceDirectory='work\idle-link-fix-20261030\core';$PSBoundParameters['AcceptanceDirectory']=$AcceptanceDirectory}
    if(-not $PSBoundParameters.ContainsKey('AssetDirectory')){$AssetDirectory='work\release-assets-v1015';$PSBoundParameters['AssetDirectory']=$AssetDirectory}
}
if($ClickBounce){$SmallTalk=$true;if(-not $PSBoundParameters.ContainsKey('OutputDirectory')){$OutputDirectory='outputs\小龙娘_v10.14_连点Q弹版';$PSBoundParameters['OutputDirectory']=$OutputDirectory};if(-not $PSBoundParameters.ContainsKey('AcceptanceDirectory')){$AcceptanceDirectory='work\click-bounce-v1014\regression';$PSBoundParameters['AcceptanceDirectory']=$AcceptanceDirectory};if(-not $PSBoundParameters.ContainsKey('AssetDirectory')){$AssetDirectory='work\release-assets-v1014';$PSBoundParameters['AssetDirectory']=$AssetDirectory}}
if($SmallTalk){$FocusCompanion=$true;if(-not $PSBoundParameters.ContainsKey('OutputDirectory')){$OutputDirectory='outputs\小龙娘_v10.13_轻语气泡版';$PSBoundParameters['OutputDirectory']=$OutputDirectory};if(-not $PSBoundParameters.ContainsKey('AcceptanceDirectory')){$AcceptanceDirectory='work\small-talk-v1013\regression';$PSBoundParameters['AcceptanceDirectory']=$AcceptanceDirectory}}
if($FocusCompanion){$DailyRoutine=$true;if(-not $PSBoundParameters.ContainsKey('OutputDirectory')){$OutputDirectory='outputs\小龙娘_v10.12.1_提示衔接修正版';$PSBoundParameters['OutputDirectory']=$OutputDirectory};if(-not $PSBoundParameters.ContainsKey('AcceptanceDirectory')){$AcceptanceDirectory='work\focus-review-v10121\regression';$PSBoundParameters['AcceptanceDirectory']=$AcceptanceDirectory}}
if($DailyRoutine){$WindowPerch=$true;if(-not $PSBoundParameters.ContainsKey('AssetDirectory')){$AssetDirectory='work\release-assets-v1011'};if(-not $PSBoundParameters.ContainsKey('OutputDirectory')){$OutputDirectory='outputs\小龙娘_v10.11_自己安排小日常'};if(-not $PSBoundParameters.ContainsKey('AcceptanceDirectory')){$AcceptanceDirectory='work\routine-v1011\regression'}}
if($WindowPerch -and -not $DailyRoutine){
    if(-not $PSBoundParameters.ContainsKey('OutputDirectory')){$OutputDirectory='outputs\小龙娘_v10.10.2_菜单移入修正版'}
    if(-not $PSBoundParameters.ContainsKey('AcceptanceDirectory')){$AcceptanceDirectory='work\menu-v10102\regression'}
}
$petProject=Split-Path -Parent $PSScriptRoot
$petSources=Join-Path $petProject 'src\runtime'
$petAssets=[IO.Path]::GetFullPath((Join-Path $petProject $AssetDirectory))
if(-not $petAssets.StartsWith($petProject+[IO.Path]::DirectorySeparatorChar)){throw 'Assets must be inside this project'}
$petOut=[IO.Path]::GetFullPath((Join-Path $petProject $OutputDirectory))
if(-not $petOut.StartsWith($petProject+[IO.Path]::DirectorySeparatorChar)){throw 'Output must be inside this project'}
$petFramework='C:\Windows\Microsoft.NET\Framework64\v4.0.30319'
New-Item -ItemType Directory -Force -Path $petOut | Out-Null
$petIcon=Join-Path $petAssets 'WhiteDragon.ico'
if(-not(Test-Path -LiteralPath $petIcon)){$petIcon=Join-Path $petProject 'assets\WhiteDragon.ico'}
if(-not(Test-Path -LiteralPath $petIcon)){throw 'Missing icon. Import release assets with tools/Import-ReleaseAssets.ps1 first.'}
$petArgs=@('/nologo','/target:winexe','/platform:x64','/optimize+',
    "/out:$petOut\启动小龙娘.exe", "/win32icon:$petIcon",
    "/reference:$petFramework\WPF\PresentationFramework.dll", "/reference:$petFramework\WPF\PresentationCore.dll",
    "/reference:$petFramework\WPF\WindowsBase.dll", "/reference:$petFramework\System.Xaml.dll",
    "/reference:$petFramework\System.Windows.Forms.dll", "/reference:$petFramework\System.Drawing.dll", "/reference:$petFramework\System.Web.Extensions.dll", "/reference:$petFramework\WPF\UIAutomationClient.dll", "/reference:$petFramework\WPF\UIAutomationTypes.dll")
$petMediaSpec=Get-Content -LiteralPath (Join-Path $petAssets 'motion-spec.json') -Raw -Encoding UTF8 | ConvertFrom-Json
if($petMediaSpec.revision -like '10.*'){$petArgs+='/define:STYLE_V10'}
if($ClickBounce){$petArgs+='/define:CLICK_BOUNCE';$petArgs+=Join-Path $petSources 'ClickBounce.cs'}
if($Unified){$petArgs+='/define:TABLET_PHASES';$petArgs+=Join-Path $petSources 'TabletPhases.cs';$petArgs+=Join-Path $petSources 'TabletPhaseIntegration.cs'}
if($SmallTalk){$petArgs+='/define:SMALL_TALK';$petArgs+=Join-Path $petSources 'SmallTalk.cs';$petArgs+=Join-Path $petSources 'SmallTalkIntegration.cs'}
if($FocusCompanion){$petArgs+='/define:FOCUS_COMPANION';$petArgs+=Join-Path $petSources 'FocusSession.cs';$petArgs+=Join-Path $petSources 'FocusBadge.cs';$petArgs+=Join-Path $petSources 'FocusCompanionIntegration.cs'}
if($DailyRoutine){$petArgs+='/define:DAILY_ROUTINE';$petArgs+=Join-Path $petSources 'DailyRoutine.cs';$petArgs+=Join-Path $petSources 'DailyRoutineIntegration.cs'}
if($WindowPerch){
    if($petMediaSpec.revision -notlike '10.*'){throw 'Window perching requires v10 media'}
    $petArgs+='/define:WINDOW_PERCH'
    $petArgs+=Join-Path $petSources 'WindowPerch.cs'
    $petArgs+=Join-Path $petSources 'WindowPerchIntegration.cs'
}
if(Test-Path -LiteralPath (Join-Path $petAssets 'blink-atlas.png')){
    $petArgs+='/resource:'+(Join-Path $petAssets 'blink-atlas.png')+',WhiteDragon.BlinkAtlas'
    $petArgs+='/resource:'+(Join-Path $petAssets 'blink-spec.json')+',WhiteDragon.BlinkSpec'
}
if(Test-Path -LiteralPath (Join-Path $petAssets 'flow-spec.json')){
    $petArgs+='/resource:'+(Join-Path $petAssets 'flow-spec.json')+',WhiteDragon.FlowSpec'
    foreach($petFlowRow in @($petMediaSpec.rows.PSObject.Properties.Name | ForEach-Object {[int]$_} | Sort-Object)){$petArgs+='/resource:'+(Join-Path $petAssets ('flow-{0:00}.wdf' -f $petFlowRow))+',WhiteDragon.Flow'+$petFlowRow}
}
foreach($petResource in @(@('gaze.png','Gaze'),@('posters.png','Posters'),@('fallback-spec.json','FallbackSpec'),@('motion-spec.json','MotionSpec'))){
    $petArgs+='/resource:'+(Join-Path $petAssets $petResource[0])+',WhiteDragon.'+$petResource[1]
}
foreach($petRow in @($petMediaSpec.rows.PSObject.Properties.Name | ForEach-Object {[int]$_} | Sort-Object)){
    $petFile=('motion-{0:00}.wdp' -f $petRow)
    $petArgs+='/resource:'+(Join-Path $petAssets $petFile)+',WhiteDragon.Motion'+$petRow
}
foreach($petSource in @('PetBehaviorV8_4.cs','AssistantStatesV8.cs','AssistantChecksV8.cs','ThrowFlightV6.cs','ThrowFlightChecksV6.cs','FlightAssetV7.cs','GrabMotionV5.cs','BehaviorChecksV8_4.cs','PetBridgeV7.cs','SmoothMotionV8_4.cs','TransitionFlow.cs','FrameTransition.cs','IdleBlink.cs','ClientLink.cs','ThinkingIndicator.cs','ReadingBookProps.cs','ReadingLibrary.cs','PetMenuTheme.cs','PetWindowMenu.cs','WhiteDragonDesktopV9.cs')){
    $petArgs+=Join-Path $petSources $petSource
}
$petArgs+=Join-Path $petSources 'MotionColorGrade.cs'
$petArgs+=Join-Path $petSources 'BookStackConsistency.cs'
& "$petFramework\csc.exe" @petArgs
if($LASTEXITCODE -ne 0){throw 'Desktop compilation failed'}
& "$petFramework\csc.exe" /nologo /target:exe /platform:x64 /optimize+ "/out:$petOut\白龙娘状态桥.exe" "/reference:$petFramework\System.Web.Extensions.dll" "$petSources\PetBridgeV7.cs" "$petSources\PetBridgeCommandV7.cs"
if($LASTEXITCODE -ne 0){throw 'Bridge compilation failed'}
if($Unified){
    $petLink=Join-Path $petOut '会话联动';New-Item -ItemType Directory -Force -Path $petLink|Out-Null
    $petGenerated=Join-Path $petProject 'work\unified-v1015-20261029\generated';New-Item -ItemType Directory -Force -Path $petGenerated|Out-Null
    $petHash=(Get-FileHash -LiteralPath (Join-Path $petOut '启动小龙娘.exe')).Hash.ToLowerInvariant()
    $petRelayText=[IO.File]::ReadAllText((Join-Path $petSources 'SessionEventRelay.cs'),[Text.Encoding]::UTF8).Replace('__WHITE_DRAGON_MAIN_SHA256__',$petHash)
    $petRelaySource=Join-Path $petGenerated 'SessionEventRelay.bound.cs';[IO.File]::WriteAllText($petRelaySource,$petRelayText,[Text.UTF8Encoding]::new($true))
    & "$petFramework\csc.exe" /nologo /target:winexe /platform:x64 /optimize+ "/out:$petLink\启动当前会话联动.exe" "/win32icon:$petIcon" "/reference:$petFramework\System.Web.Extensions.dll" "/reference:$petFramework\System.Windows.Forms.dll" $petRelaySource
    if($LASTEXITCODE -ne 0){throw 'Current-chat adapter compilation failed'}
    if(-not $ChatSessionPath -and $env:CODEX_THREAD_ID){$petSessionRoot=Join-Path ([Environment]::GetFolderPath('UserProfile')) '.codex\sessions';$petSession=Get-ChildItem -LiteralPath $petSessionRoot -Recurse -File -Filter ('*-'+$env:CODEX_THREAD_ID+'.jsonl')|Select-Object -First 1;if($petSession){$ChatSessionPath=$petSession.FullName}}
    @{project_path=$petProject;session_path=$ChatSessionPath;follow_project=$true;scope='Follow the latest main chat in this project; no chat text, commands or tool output retained'}|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $petLink '会话联动配置.json') -Encoding UTF8
}
$petPreviewFiles=Join-Path $petAssets 'preview-files'
if(Test-Path -LiteralPath $petPreviewFiles){Get-ChildItem -LiteralPath $petPreviewFiles | Copy-Item -Destination $petOut -Recurse -Force}
if(-not $SkipNativeTest){
  if($petMediaSpec.revision -like '10.*'){
    & (Join-Path $PSScriptRoot 'Test-Current.ps1') -Target (Join-Path $petOut '启动小龙娘.exe') -OutputDirectory $AcceptanceDirectory
    if($LASTEXITCODE -ne 0){throw 'Current native checks failed'}
  }else{
    $petQa=[IO.Path]::GetFullPath((Join-Path $petProject $AcceptanceDirectory))
    if(-not $petQa.StartsWith($petProject+[IO.Path]::DirectorySeparatorChar)){throw 'Acceptance output must be inside this project'}
    $petProcess=Start-Process -FilePath (Join-Path $petOut '启动小龙娘.exe') -ArgumentList @('--self-test',('"'+$petQa+'"')) -WindowStyle Hidden -PassThru
    [IO.File]::WriteAllText((Join-Path $petOut '自测进程.json'),(@{pid=$petProcess.Id;folder=$petQa;scope='Hidden test mode; does not change installed pet or settings'}|ConvertTo-Json),[Text.UTF8Encoding]::new($false))
    Write-Output ('Native self-test started: '+$petProcess.Id)
  }
}
Write-Output ('Built '+$petOut)
