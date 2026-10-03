$ErrorActionPreference='Stop'
Add-Type -TypeDefinition (Get-Content (Join-Path $PSScriptRoot '../Assets/Scripts/MeetingEncounter.cs') -Raw)
$encounter=[GeoSniper.MeetingEncounter]::new()
$encounter.Fire()
if($encounter.ShotFired) {throw 'Fire before identification must not unlock combat'}
$encounter.Tick(20,$true,$true)
if($encounter.Elapsed -ne 0) {throw 'Pause advanced encounter'}
$encounter.Tick(8,$true,$false)
if($encounter.Identified -or $encounter.Observation -ne 0) {throw 'Intro incorrectly counted as observation'}
$encounter.Tick(2,$true,$false)
if($encounter.Identified) {throw 'Identification finished too early'}
$encounter.Tick(1,$false,$false)
if([Math]::Abs($encounter.Observation-1.65) -gt .001) {throw 'Observation recovery/decay incorrect'}
$encounter.Tick(1.4,$true,$false)
if(-not $encounter.Identified) {throw 'Sustained observation did not identify contact'}
$encounter.Fire()
if(-not $encounter.ShotFired) {throw 'Confirmed target did not unlock combat'}
$encounter.Tick(10,$false,$false)
if(-not $encounter.Identified) {throw 'Confirmed identity was lost'}
'PASS: pre-identification fire, map pause, intro pacing, observation duration, forgiving decay, identification, combat release, persistent identity.'
