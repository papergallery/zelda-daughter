param([string]$file)
$env:UMCP_PROJECT = "ZeldaDaughter"
$env:UMCP_TIMEOUT = "900"
& C:\Users\paper\.local\bin\uv.exe run --no-project python C:\dev\zelda-tools\umcp.py code "C:\dev\zelda-tools\$file" 2>&1 | Out-String
