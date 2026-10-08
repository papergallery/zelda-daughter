# Строки обычного журнала консоли редактора ([ZD:*] и прочее): zd-log.ps1 [-count 200]
param([int]$count = 200)
$env:UMCP_PROJECT = "ZeldaDaughter"
& C:\Users\paper\.local\bin\uv.exe run --no-project python C:\dev\zelda-tools\umcp.py call read_console ('{\"action\":\"get\",\"types\":[\"log\"],\"count\":' + $count + ',\"format\":\"plain\"}') 2>&1 | Out-String
