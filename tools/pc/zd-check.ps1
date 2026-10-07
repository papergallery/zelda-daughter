$env:UMCP_PROJECT = "ZeldaDaughter"
& C:\Users\paper\.local\bin\uv.exe run --no-project python C:\dev\zelda-tools\umcp.py read mcpforunity://project/info 2>&1 | Out-String
& C:\Users\paper\.local\bin\uv.exe run --no-project python C:\dev\zelda-tools\umcp.py call read_console '{\"action\":\"get\",\"types\":[\"warning\",\"error\"],\"count\":40,\"format\":\"plain\"}' 2>&1 | Out-String
