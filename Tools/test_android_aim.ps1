$ErrorActionPreference='Stop'
Add-Type -TypeDefinition (Get-Content (Join-Path $PSScriptRoot '../Assets/Scripts/AndroidAimMath.cs') -Raw)
foreach($fps in @(30,60,120)) {
    $total=0.0
    for($i=0;$i -lt $fps;$i++) { $total += [GeoSniper.AndroidAimMath]::GyroDelta(1,(1.0/$fps),1) }
    if([Math]::Abs($total-(180/[Math]::PI)) -gt .001) { throw "Frame-rate dependent gyro at $fps FPS" }
}
foreach($rate in @([float]::NaN,[float]::PositiveInfinity,[float]::NegativeInfinity,0,.004,-.004)) {
    if([GeoSniper.AndroidAimMath]::GyroDelta($rate,.016,1) -ne 0) { throw 'Invalid or resting sensor moves aim' }
}
foreach($dt in @(-1,0,.101,5,[float]::NaN)) {
    if([GeoSniper.AndroidAimMath]::GyroDelta(1,$dt,1) -ne 0) { throw 'Stalled frame causes aim jump' }
}
$positive=[GeoSniper.AndroidAimMath]::GyroDelta(1,.016,1)
$negative=[GeoSniper.AndroidAimMath]::GyroDelta(-1,.016,1)
if([Math]::Abs($positive+$negative) -gt .00001) { throw 'Direction is asymmetric' }
if([Math]::Abs([GeoSniper.AndroidAimMath]::GyroDelta(1,.016,2)-2*$positive) -gt .00001) { throw 'Sensitivity scaling failed' }
if([GeoSniper.AndroidAimMath]::GyroDelta(100,.016,1) -ne [GeoSniper.AndroidAimMath]::GyroDelta(8,.016,1)) { throw 'Sensor spike is not bounded' }
'PASS: 30/60/120 FPS consistency, dead zone, invalid samples, stall rejection, direction, sensitivity and spike bounds.'
