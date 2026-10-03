Add-Type @"
  using System;
  using System.Runtime.InteropServices;
  public class Win32 {
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetForegroundWindow(IntPtr hWnd);
  }
"@
$procs = Get-Process -Name Unity -ErrorAction SilentlyContinue | Where-Object { $_.MainWindowHandle -ne 0 }
foreach ($p in $procs) {
    Write-Host "Focusing Unity PID $($p.Id)"
    [Win32]::SetForegroundWindow($p.MainWindowHandle)
}
Start-Sleep -Seconds 2
