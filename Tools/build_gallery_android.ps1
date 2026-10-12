param([switch]$ReuseReviewCache,[switch]$ExcludeUnusedAIPackages,[switch]$ValidateLoadingFix)
$ErrorActionPreference='Stop'
$workspace=[IO.Path]::GetFullPath((Split-Path $PSScriptRoot -Parent))
$buildProject=Join-Path $workspace '.utmp/android-gallery-build'
$reviewProject=Join-Path $workspace '.utmp/review-unity'
$unity='C:/Program Files/Unity/Hub/Editor/6000.5.0f1/Editor/Unity.exe'
$running=Get-CimInstance Win32_Process -Filter "Name='Unity.exe'"
if($running | Where-Object {$_.CommandLine -and $_.CommandLine.Replace('\','/').Contains($buildProject.Replace('\','/'))}) {throw 'Android build project is already running.'}
New-Item $buildProject -ItemType Directory -Force | Out-Null
foreach($folder in @('Assets','ProjectSettings','Packages','GooglePackages')) {
    & robocopy (Join-Path $workspace $folder) (Join-Path $buildProject $folder) /E /NFL /NDL /NJH /NJS /NP | Out-Null
    if($LASTEXITCODE -gt 7){throw "Failed copying $folder"}
}
if($ExcludeUnusedAIPackages) {
    # Gameplay has no references to these editor/ML tools. Their unused compute
    # resources inflate the isolated mobile build and its temporary disk usage.
    $galleryManifestPath=Join-Path $buildProject 'Packages/manifest.json'
    $galleryManifest=Get-Content -LiteralPath $galleryManifestPath -Raw | ConvertFrom-Json -AsHashtable
    $galleryManifest.dependencies.Remove('com.unity.ai.assistant')
    $galleryManifest.dependencies.Remove('com.unity.ai.inference')
    $galleryManifest | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $galleryManifestPath
}
if($ReuseReviewCache) {
    $cacheSource=[IO.Path]::GetFullPath((Join-Path $reviewProject 'Library'))
    $cacheDestination=[IO.Path]::GetFullPath((Join-Path $buildProject 'Library'))
    $allowedPrefix=$workspace+[IO.Path]::DirectorySeparatorChar
    if(!$cacheSource.StartsWith($allowedPrefix,[StringComparison]::OrdinalIgnoreCase) -or !$cacheDestination.StartsWith($allowedPrefix,[StringComparison]::OrdinalIgnoreCase)) {throw 'Cache paths leave workspace.'}
    if($running | Where-Object {$_.CommandLine -and $_.CommandLine.Replace('\','/').Contains($reviewProject.Replace('\','/'))}) {throw 'Wait for isolated gameplay review to close before reusing its cache.'}
    if((Test-Path -LiteralPath $cacheSource) -and !(Test-Path -LiteralPath $cacheDestination)) {
        Move-Item -LiteralPath $cacheSource -Destination $cacheDestination
    }
}
$buildMethod=if($ValidateLoadingFix){'AndroidLoadingFixChecks.ValidateAndBuild'}else{'AndroidBuildValidation.Build'}
$galleryUnityArgs=@('-batchmode','-quit','-job-worker-count','2','-buildTarget','Android','-projectPath',('"'+$buildProject+'"'),'-executeMethod',$buildMethod,'-logFile',('"'+(Join-Path $buildProject 'gallery-android.log')+'"'))
$process=Start-Process $unity -ArgumentList $galleryUnityArgs -WindowStyle Hidden -PassThru
"Android isolated build PID: $($process.Id)"
"Result: $buildProject/Logs/AndroidBuildResult.txt"
