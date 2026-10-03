$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
Set-Location $root
$unity='C:/Program Files/Unity/Hub/Editor/6000.5.0f1/Editor/Data'
$base='Library/Bee/artifacts/1300b0aE.dag'
New-Item -ItemType Directory -Force '.utmp/review-checks' | Out-Null
foreach($kind in @('Runtime','Editor')) {
    $template=if($kind -eq 'Runtime') {"$base/Assembly-CSharp.rsp"} else {"$base/Assembly-CSharp-Editor.rsp"}
    $lines=Get-Content $template | Where-Object {$_ -notmatch '^"Assets/.*\.cs"$' -and $_ -notmatch '^[-/]additionalfile:'}
    $lines=$lines -replace '^[-/]out:.*$',"-out:`".utmp/review-checks/Review$kind.dll`""
    $lines=$lines -replace '^[-/]refout:.*$',"-refout:`".utmp/review-checks/Review$kind.ref.dll`""
    if($kind -eq 'Editor') {$lines=$lines -replace '-r:"[^"]*Assembly-CSharp.ref.dll"','-r:".utmp/review-checks/ReviewRuntime.dll"'}
    $sources=if($kind -eq 'Runtime') {Get-ChildItem Assets/Scripts -File -Filter '*.cs'} else {Get-ChildItem Assets/Editor,Assets/Scripts/Editor -File -Filter '*.cs'}
    $lines+= $sources | ForEach-Object {'"'+$_.FullName.Replace('\','/')+'"'}
    $rsp=".utmp/review-checks/$kind.rsp"
    [IO.File]::WriteAllLines((Join-Path $root $rsp),$lines)
    & "$unity/NetCoreRuntime/dotnet.exe" "$unity/DotNetSdk/sdk/8.0.318/Roslyn/bincore/csc.dll" "@$rsp"
    if($LASTEXITCODE -ne 0){throw "$kind compilation failed"}
}
