$ErrorActionPreference='Stop'
$root=Join-Path $PSScriptRoot '../Assets/Scripts'
$stubs=Get-Content (Join-Path $PSScriptRoot '../Tests/MobileGraphicsChecks.cs') -Raw
$policy=Get-Content (Join-Path $root 'MobileQualityPolicy.cs') -Raw
$runtime=(Get-Content (Join-Path $root 'MobileGraphics.cs') -Raw).Replace('using UnityEngine;','')
Add-Type -TypeDefinition ('using UnityEngine;'+[Environment]::NewLine+$policy+$runtime+$stubs)
[GeoSniper.GraphicsChecks]::Run()
'PASS: defaults, preset application, saved reapply, independent FPS, weather refresh, high restoration, corrupt preferences.'
