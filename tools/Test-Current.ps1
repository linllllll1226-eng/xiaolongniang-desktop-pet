param(
 [string]$Target='outputs\小龙娘_v10.15.2_空闲联动修正版\启动小龙娘.exe',
 [string]$OutputDirectory='work\idle-link-fix-20261030\core',
 [ValidateSet('All','Switch','Menu','Indicator','Transition','Blink','Flight','Library','Moving')][string]$Suite='All',
 [string]$Baseline='outputs\小龙娘_v10.14.6_播放收尾版\启动小龙娘.exe'
)
$ErrorActionPreference='Stop';$petProject=Split-Path -Parent $PSScriptRoot
$petTarget=if([IO.Path]::IsPathRooted($Target)){[IO.Path]::GetFullPath($Target)}else{[IO.Path]::GetFullPath((Join-Path $petProject $Target))}
if(-not(Test-Path -LiteralPath $petTarget -PathType Leaf)){throw 'Candidate executable is missing'}
$petOutput=[IO.Path]::GetFullPath((Join-Path $petProject $OutputDirectory))
if(-not $petOutput.StartsWith($petProject+[IO.Path]::DirectorySeparatorChar)){throw 'Test output must stay inside the project'}
New-Item -ItemType Directory -Force -Path $petOutput|Out-Null
$petFramework=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$petNames=@{Switch='SmoothSwitchReview';Menu='MenuLinkReview';Indicator='IndicatorReview';Transition='TransitionReview';Blink='BlinkReview';Flight='FlightReview';Library='ReadingReview';Moving='MovingTransitionReview'}
$petReports=@{Switch='smooth-switch-review.json';Menu='menu-link-review.json';Indicator='indicator-review.json';Transition='transition-review.json';Blink='blink-review.json';Flight='flight-review.json';Library='reading-review.json';Moving='moving-transition-review.json'}
$petResults=@();$petSuites=if($Suite -eq 'All'){@('Moving','Library','Switch','Menu','Indicator','Transition','Blink','Flight')}else{@($Suite)}
foreach($petSuite in $petSuites){
 $petFolder=Join-Path $petOutput $petSuite;New-Item -ItemType Directory -Force -Path $petFolder|Out-Null
 $petHost=Join-Path $petFolder ($petNames[$petSuite]+'.exe')
 $petCompile=@('/nologo','/target:winexe','/platform:x64','/optimize+',"/out:$petHost","/reference:$petFramework\WPF\PresentationFramework.dll","/reference:$petFramework\WPF\PresentationCore.dll","/reference:$petFramework\WPF\WindowsBase.dll","/reference:$petFramework\System.Xaml.dll","/reference:$petFramework\System.Web.Extensions.dll",(Join-Path $PSScriptRoot ('qa\'+$petNames[$petSuite]+'.cs')))
 & "$petFramework\csc.exe" @petCompile;if($LASTEXITCODE -ne 0){throw "QA host compilation failed: $petSuite"}
 $petArgs=if($petSuite -eq 'Switch'){@(('"'+$petFolder+'"'),('"'+$petTarget+'"'),'native','256','250','120')}else{@(('"'+$petTarget+'"'),('"'+$petFolder+'"'))}
 if($petSuite -eq 'Menu'){$petBaseline=Join-Path $petProject $Baseline;if(Test-Path -LiteralPath $petBaseline){$petArgs+=('"'+$petBaseline+'"')}}
 $petTest=Start-Process -FilePath $petHost -ArgumentList $petArgs -WorkingDirectory $petProject -WindowStyle Hidden -PassThru
 $petTimeout=if($petSuite -eq 'Switch'){180000}elseif($petSuite -eq 'Blink'){55000}else{45000}
 if(-not $petTest.WaitForExit($petTimeout)){Stop-Process -Id $petTest.Id -Force;throw "QA timeout: $petSuite"}
 $petResult=Get-Content -LiteralPath (Join-Path $petFolder $petReports[$petSuite]) -Raw -Encoding UTF8 | ConvertFrom-Json
 if($petTest.ExitCode -ne 0 -or -not $petResult.ok){throw ($petSuite+' failed: '+($petResult|ConvertTo-Json -Depth 4 -Compress))}
 if($petResult.candidate_sha256 -ne (Get-FileHash -LiteralPath $petTarget).Hash.ToLowerInvariant()){throw 'QA candidate hash changed'}
 $petResults+=@{suite=$petSuite;ok=$true;report=$petReports[$petSuite]};Write-Output ("PASS $petSuite")
}
$petSummary=@{ok=$true;candidate_sha256=(Get-FileHash -LiteralPath $petTarget).Hash.ToLowerInvariant();suites=$petResults;scope='Isolated hidden WPF windows; no saved preferences, real pointer gestures, paid generation or installed app changes';utc=[DateTime]::UtcNow.ToString('o')}
$petSummary|ConvertTo-Json -Depth 6|Set-Content -LiteralPath (Join-Path $petOutput 'summary.json') -Encoding UTF8
$petSummary|ConvertTo-Json -Depth 6
