param([Parameter(Mandatory=$true)][string]$Executable,[string]$OutputDirectory='work\release-assets-v109')
$ErrorActionPreference='Stop';$petProject=Split-Path -Parent $PSScriptRoot
$petInput=[IO.Path]::GetFullPath($Executable);if(-not(Test-Path -LiteralPath $petInput -PathType Leaf)){throw 'Release executable is missing'}
$petOutput=[IO.Path]::GetFullPath((Join-Path $petProject $OutputDirectory));if(-not $petOutput.StartsWith($petProject+[IO.Path]::DirectorySeparatorChar)){throw 'Imported assets must stay inside project'}
$petCompiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319';$petToolFolder=Join-Path $petProject 'work\asset-import-tool';New-Item -ItemType Directory -Force -Path $petToolFolder|Out-Null;$petTool=Join-Path $petToolFolder 'ReleaseAssets.exe'
& "$petCompiler\csc.exe" /nologo /target:exe /optimize+ "/out:$petTool" "/reference:$petCompiler\System.Core.dll" "/reference:$petCompiler\System.Web.Extensions.dll" (Join-Path $PSScriptRoot 'qa\ReleaseAssets.cs');if($LASTEXITCODE -ne 0){throw 'Resource reader compilation failed'}
& $petTool extract $petInput $petOutput;if($LASTEXITCODE -ne 0){throw 'Release resource import failed'}
$petIcon=Join-Path $petOutput 'WhiteDragon.ico'
if(-not(Test-Path -LiteralPath $petIcon)){
 $petOriginalIcon=Join-Path $petProject 'assets\WhiteDragon.ico'
 if(Test-Path -LiteralPath $petOriginalIcon){Copy-Item -LiteralPath $petOriginalIcon -Destination $petIcon}else{Add-Type -AssemblyName System.Drawing;$petImage=[Drawing.Icon]::ExtractAssociatedIcon($petInput);$petStream=[IO.File]::Create($petIcon);try{$petImage.Save($petStream)}finally{$petStream.Dispose();$petImage.Dispose()}}
}
$petCompanionRoot=Join-Path $petOutput 'preview-files';$petReleaseFolder=Split-Path -Parent $petInput
$petCompanions=@('打开预览.html','联动说明.html','抛掷体验.html','修正前后对比.html','素材目录.js','随机眨眼.js','抛掷运动.js','角色主图.png','眨眼眼睑图集.png','眨眼参数.json','菜单预览.png','联动菜单预览.png')
foreach($petRelative in $petCompanions){$petFrom=Join-Path $petReleaseFolder $petRelative;if(Test-Path -LiteralPath $petFrom){New-Item -ItemType Directory -Force -Path $petCompanionRoot|Out-Null;Copy-Item -LiteralPath $petFrom -Destination (Join-Path $petCompanionRoot $petRelative)}}
foreach($petMediaFolder in @('动画','衔接对比素材')){
 $petFromFolder=Join-Path $petReleaseFolder $petMediaFolder
 if(Test-Path -LiteralPath $petFromFolder){foreach($petMedia in Get-ChildItem -LiteralPath $petFromFolder -File){if($petMedia.Name -match '^(WD10-\d{2}\.webm|[新旧]-\d+\.mp4)$'){$petDestination=Join-Path $petCompanionRoot $petMediaFolder;New-Item -ItemType Directory -Force -Path $petDestination|Out-Null;Copy-Item -LiteralPath $petMedia.FullName -Destination $petDestination}}}
}
if(-not(Test-Path -LiteralPath (Join-Path $petCompanionRoot '联动说明.html'))){Write-Warning 'No companion help page found. Import from the complete extracted release folder to include menu help and web previews.'}
Write-Output ('Ready to build from '+$OutputDirectory)
