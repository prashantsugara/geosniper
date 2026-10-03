$ErrorActionPreference='Stop'
Add-Type -TypeDefinition (Get-Content (Join-Path $PSScriptRoot '../Assets/Scripts/EnemyAwarenessPolicy.cs') -Raw)
$standing=[GeoSniper.EnemyAwarenessPolicy]::Detection(0,$true,.5,$false,1,.1)
$crouched=[GeoSniper.EnemyAwarenessPolicy]::Detection(0,$true,.5,$true,1,.1)
if($crouched -ge $standing -or $crouched -le 0){throw 'Crouching detection balance'}
if([GeoSniper.EnemyAwarenessPolicy]::Detection(0,$true,.1,$false,1,.1) -le $standing){throw 'Near targets not detected faster'}
if([GeoSniper.EnemyAwarenessPolicy]::Detection(.5,$false,0,$false,1,.1) -ge .5){throw 'Lost visibility does not decay'}
foreach($fps in @(30,60,120)) {
 $value=0.0
 for($i=0;$i -lt $fps;$i++){$value=[GeoSniper.EnemyAwarenessPolicy]::Detection($value,$true,.5,$false,1,(1.0/$fps))}
 if([Math]::Abs($value-.64) -gt .0001){throw "Detection depends on FPS: $fps $value"}
}
if([GeoSniper.EnemyAwarenessPolicy]::Detection(0,$true,0,$false,1,5) -gt .12){throw 'Resume spike instant detection'}
if([GeoSniper.EnemyAwarenessPolicy]::Detection(0,$false,0,$false,1,.1) -ne 0){throw 'Negative awareness'}
if([GeoSniper.EnemyAwarenessPolicy]::Detection(1,$true,0,$false,1,.1) -ne 1){throw 'Awareness overflow'}
'PASS: crouch/distance balance, sight-loss decay, 30/60/120 FPS consistency, resume spike and bounds.'
