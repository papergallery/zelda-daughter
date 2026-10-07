$env:UMCP_PROJECT = "ZeldaDaughter"
& C:\Users\paper\.local\bin\uv.exe run --no-project python C:\dev\zelda-tools\umcp.py code C:\dev\zelda-tools\apply.cs 2>&1 | Out-String
Start-Sleep 5
& C:\Users\paper\.local\bin\uv.exe run --no-project python C:\dev\zelda-tools\umcp.py call read_console '{\"action\":\"get\",\"types\":[\"log\",\"error\"],\"count\":30,\"filter_text\":\"ZD:Setup\",\"format\":\"plain\"}' 2>&1 | Out-String
"git status:"; git -C C:\dev\zelda status --short 2>&1 | Out-String
