import subprocess

cmd = r'Get-CimInstance Win32_Process -Filter "Name = \'Unity.exe\'" | Select-Object ProcessId, CommandLine | Format-List'
res = subprocess.run(["powershell", "-NoProfile", "-Command", cmd], capture_output=True, text=True)
print(res.stdout)
