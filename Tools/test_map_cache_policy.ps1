$ErrorActionPreference='Stop'
Add-Type -TypeDefinition (Get-Content (Join-Path $PSScriptRoot '../Assets/Scripts/MapCachePolicy.cs') -Raw)
foreach($distance in @(0,10,40)) {
    if(-not [GeoSniper.MapCachePolicy]::CanReuse($distance)) {throw "Nearby cache rejected: $distance"}
}
foreach($distance in @(40.01,399,1000,1000000,-1,[double]::NaN,[double]::PositiveInfinity)) {
    if([GeoSniper.MapCachePolicy]::CanReuse($distance)) {throw "Wrong-location cache accepted: $distance"}
}
'PASS: exact/nearby cache accepted; distant and invalid coordinates rejected.'
