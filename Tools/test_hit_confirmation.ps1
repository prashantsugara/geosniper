$ErrorActionPreference='Stop'
Add-Type -TypeDefinition (Get-Content (Join-Path $PSScriptRoot '../Assets/Scripts/HitConfirmation.cs') -Raw)
$hit=[GeoSniper.HitConfirmation]::new()
if($hit.Remaining(1) -ne 0){throw 'Marker before impact'}
foreach($damage in @(0,-1,[float]::NaN,[float]::PositiveInfinity)) {
 if($hit.Confirm(1,$damage,$false)){throw 'Invalid damage confirmed'}
}
if(!$hit.Confirm(1,25,$false) -or $hit.Headshot -or $hit.Remaining(1.1) -le 0){throw 'Body hit missing'}
if($hit.Remaining(1.3) -ne 0){throw 'Body marker did not expire'}
if(!$hit.Confirm(2,50,$true) -or !$hit.Headshot -or $hit.Remaining(2.4) -le 0){throw 'Head marker missing'}
if($hit.Remaining(2.5) -ne 0){throw 'Head marker did not expire'}
$hit.Confirm(3,10,$false) | Out-Null
if($hit.Headshot){throw 'Old headshot leaked into new body hit'}
'PASS: no pre-hit marker, invalid damage rejection, body/head timing, repeated hit replacement.'
