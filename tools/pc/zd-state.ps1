$env:UMCP_PROJECT = "ZeldaDaughter"
& C:\Users\paper\.local\bin\uv.exe run --no-project python C:\dev\zelda-tools\umcp.py code C:\dev\zelda-tools\state.cs 2>&1 | Out-String
