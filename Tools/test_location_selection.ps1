$ErrorActionPreference='Stop'
$policy=Get-Content (Join-Path $PSScriptRoot '../Assets/Scripts/LocationSelectionPolicy.cs') -Raw
# Compile a strongly typed caller so the test also works on Windows PowerShell.
$checks=@'
using GeoSniper;
public static class LocationSelectionChecks {
    public static void Run() {
        if (LocationSelectionPolicy.Select(true,"searched","cached") != null)
            throw new System.Exception("Live GPS reused old coordinates");
        if (LocationSelectionPolicy.Select(false,"searched","cached") != "searched")
            throw new System.Exception("Explicit selection lost priority");
        if (LocationSelectionPolicy.Select<string>(false,null,"cached") != "cached")
            throw new System.Exception("Manual mode lost selected place");
        if (LocationSelectionPolicy.AllowsSavedFallback(true))
            throw new System.Exception("Failed GPS may fall back to wrong place");
        if (!LocationSelectionPolicy.RequiresLiveFix("gps") || LocationSelectionPolicy.RequiresLiveFix("selected"))
            throw new System.Exception("Source preference is not respected");
    }
}
'@
Add-Type -TypeDefinition ($checks + [Environment]::NewLine + $policy)
[LocationSelectionChecks]::Run()
'PASS: live overrides cache, explicit selection wins, manual location retained, GPS failure cannot use cache, source preference.'
