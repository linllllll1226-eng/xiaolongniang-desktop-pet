param(
 [string]$Target='outputs\小龙娘_v10.15.2_空闲联动修正版\启动小龙娘.exe',
 [string]$OutputDirectory='work\idle-link-fix-20261030\extended',
 [switch]$SkipCore,
 [string]$BookBaseline='outputs\小龙娘_v10.14.6_播放收尾版\启动小龙娘.exe',
 [string]$TabletBaseline='outputs\小龙娘_平板三段候选_20261029\启动小龙娘.exe',
 [string[]]$Suites=@('Unified','TabletLogic','RelayLogic','BridgeLease','TabletNative','Colour','Scene','BookSwitch','Pose','Click','Sampling','Equality','Visual','ReaderLive','Cadence','WindowPerch','DailyRoutine','Focus','SmallTalk')
)
$ErrorActionPreference='Stop'
$root=Split-Path -Parent $PSScriptRoot
$targetPath=if([IO.Path]::IsPathRooted($Target)){[IO.Path]::GetFullPath($Target)}else{[IO.Path]::GetFullPath((Join-Path $root $Target))}
$out=[IO.Path]::GetFullPath((Join-Path $root $OutputDirectory))
if(-not $out.StartsWith($root+'\',[StringComparison]::OrdinalIgnoreCase)){throw 'Review output must stay inside the project'}
New-Item -ItemType Directory -Force -Path $out|Out-Null
$hash=(Get-FileHash -LiteralPath $targetPath).Hash.ToLowerInvariant()
if(-not $SkipCore){& (Join-Path $PSScriptRoot 'Test-Current.ps1') -Target $Target -OutputDirectory ($OutputDirectory+'\core');if($LASTEXITCODE -ne 0){throw 'Core regression failed'}}
$book=if([IO.Path]::IsPathRooted($BookBaseline)){$BookBaseline}else{Join-Path $root $BookBaseline}
$tablet=if([IO.Path]::IsPathRooted($TabletBaseline)){$TabletBaseline}else{Join-Path $root $TabletBaseline}
$framework=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$names=@{Unified='UnifiedFeatureReview';TabletLogic='TabletPhaseReview';RelayLogic='EventRelayTests';BridgeLease='BridgeLeaseReview';TabletNative='NativeTabletPhaseReview';Colour='ColourReview';Scene='BookSceneReview';BookSwitch='BookSwitchReview';Pose='PoseExitReview';Click='ClickInputAudit';Sampling='SamplingReview';Equality='CompositeEquality';Visual='GestureVisualReview';ReaderLive='ReaderLiveReview';Cadence='CadenceReview';WindowPerch='WindowPerchReview';DailyRoutine='DailyRoutineReview';Focus='FocusCompanionReview';SmallTalk='SmallTalkReview'}
$reports=@{Unified='unified-feature-review.json';TabletLogic='phase-review.json';RelayLogic='logic-review.json';BridgeLease='bridge-lease-review.json';TabletNative='native-tablet-phases.json';Colour='colour-review.json';Scene='book-scene-review.json';BookSwitch='book-switch-review.json';Pose='pose-exit-review.json';Click='click-bounce-review.json';Sampling='sampling-review.json';Equality='equality.json';Visual='gesture-visual.json';ReaderLive='reader-live-review.json';Cadence='cadence-review.json';WindowPerch='window-perch-review.json';DailyRoutine='daily-routine-review.json';Focus='focus-companion-review.json';SmallTalk='small-talk-review.json'}
$results=@()
if(Test-Path -LiteralPath (Join-Path $out 'summary.json')){
 $previous=Get-Content -LiteralPath (Join-Path $out 'summary.json') -Raw -Encoding UTF8|ConvertFrom-Json
 if($previous.candidate_sha256 -eq $hash){$results=@($previous.suites)}
}
foreach($suite in $Suites){
 if(-not $names.ContainsKey($suite)){throw ('Unknown suite: '+$suite)}
 $folder=Join-Path $out $suite;New-Item -ItemType Directory -Force -Path $folder|Out-Null
 $name=$names[$suite];$exe=Join-Path $folder ($name+'.exe');$reportPath=Join-Path $folder $reports[$suite]
 $compile=@('/nologo','/target:winexe','/platform:x64','/optimize+',"/out:$exe","/reference:$framework\WPF\PresentationFramework.dll","/reference:$framework\WPF\PresentationCore.dll","/reference:$framework\WPF\WindowsBase.dll","/reference:$framework\System.Xaml.dll","/reference:$framework\System.Web.Extensions.dll","/reference:$framework\System.Windows.Forms.dll",(Join-Path $PSScriptRoot ('qa\'+$name+'.cs')))
 if($suite -eq 'TabletLogic'){$compile+='/define:STYLE_V10,WINDOW_PERCH,DAILY_ROUTINE,FOCUS_COMPANION,SMALL_TALK,CLICK_BOUNCE,TABLET_PHASES';$compile+=Join-Path $root 'src\runtime\PetBehaviorV8_4.cs';$compile+=Join-Path $root 'src\runtime\TabletPhases.cs'}
 if($suite -eq 'RelayLogic'){$compile+='/main:EventRelayTests';$compile+=Join-Path $root 'work\unified-v1015-20261029\generated\SessionEventRelay.bound.cs'}
 & "$framework\csc.exe" @compile
 if($LASTEXITCODE -ne 0){throw ('Review compilation failed: '+$suite)}
 $argsList=@($targetPath,$folder)
 switch($suite){
  Unified {$argsList+=@($book,$tablet)}
  TabletLogic {$argsList=@($reportPath)}
  RelayLogic {$argsList=@($folder)}
  Sampling {$argsList=@($targetPath,$book,$reportPath)}
  Equality {$argsList=@($book,$targetPath,$reportPath)}
  Click {$argsList+=@($book,'0','validate')}
  Pose {$argsList+='visible'}
  WindowPerch {$argsList+=$book}
  DailyRoutine {$argsList+=$book}
  Focus {$argsList+=$book}
  SmallTalk {$argsList+=$book}
 }
 $quoted=@($argsList|ForEach-Object {'"'+$_+'"'})
 $p=Start-Process -FilePath $exe -ArgumentList $quoted -WorkingDirectory $root -WindowStyle Hidden -PassThru
 if(-not $p.WaitForExit(160000)){Stop-Process -Id $p.Id;throw ('Review timed out: '+$suite)}
 if(-not(Test-Path -LiteralPath $reportPath)){throw ('Review did not write a report: '+$suite+'; exit '+$p.ExitCode)}
 $r=Get-Content -LiteralPath $reportPath -Raw -Encoding UTF8|ConvertFrom-Json
 if($p.ExitCode -ne 0 -or -not $r.ok){$r|Select-Object error,native_error,failures,failed_phase|ConvertTo-Json -Depth 3|Write-Output;throw ('Review failed: '+$suite)}
 if($suite -notin @('TabletLogic','RelayLogic') -and $r.candidate_sha256 -ne $hash){throw ('Candidate evidence hash mismatch: '+$suite)}
 $results=@($results|Where-Object {$_.suite -ne $suite})+@(@{suite=$suite;ok=$true;report=$reportPath});Write-Output ('PASS '+$suite)
 @{ok=$true;candidate_sha256=$hash;suites=$results;scope='Sequential regression; exact binary checks except source-bound pure logic cases';utc=[DateTime]::UtcNow.ToString('o')}|ConvertTo-Json -Depth 6|Set-Content -LiteralPath (Join-Path $out 'summary.json') -Encoding UTF8
}
