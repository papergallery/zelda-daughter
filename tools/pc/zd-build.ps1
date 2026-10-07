$env:UMCP_PROJECT = "ZeldaDaughter"
& C:\Users\paper\.local\bin\uv.exe run --no-project python C:\dev\zelda-tools\umcp.py code C:\dev\zelda-tools\build.cs 2>&1 | Out-String
& C:\Users\paper\.local\bin\uv.exe run --no-project python C:\dev\zelda-tools\umcp.py call read_console '{\"action\":\"get\",\"types\":[\"log\",\"warning\",\"error\"],\"count\":40,\"format\":\"plain\"}' 2>&1 | Out-String
