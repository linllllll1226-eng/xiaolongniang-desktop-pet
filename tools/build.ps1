param(
    [string]$AssetDirectory = 'work\release-assets-v109',
    [string]$OutputDirectory = 'outputs\小龙娘_v10.9_源码构建版',
    [string]$AcceptanceDirectory = 'work\style-v109\native',
    [switch]$SkipNativeTest
)
$ErrorActionPreference='Stop'
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
foreach($petSource in @('PetBehaviorV8_4.cs','AssistantStatesV8.cs','AssistantChecksV8.cs','ThrowFlightV6.cs','ThrowFlightChecksV6.cs','FlightAssetV7.cs','GrabMotionV5.cs','BehaviorChecksV8_4.cs','PetBridgeV7.cs','SmoothMotionV8_4.cs','TransitionFlow.cs','FrameTransition.cs','IdleBlink.cs','ClientLink.cs','ThinkingIndicator.cs','ReadingLibrary.cs','PetMenuTheme.cs','PetWindowMenu.cs','WhiteDragonDesktopV9.cs')){
    $petArgs+=Join-Path $petSources $petSource
}
& "$petFramework\csc.exe" @petArgs
if($LASTEXITCODE -ne 0){throw 'Desktop compilation failed'}
& "$petFramework\csc.exe" /nologo /target:exe /platform:x64 /optimize+ "/out:$petOut\白龙娘状态桥.exe" "/reference:$petFramework\System.Web.Extensions.dll" "$petSources\PetBridgeV7.cs" "$petSources\PetBridgeCommandV7.cs"
if($LASTEXITCODE -ne 0){throw 'Bridge compilation failed'}
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
