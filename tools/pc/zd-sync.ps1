# -ref: что поставить на ПК — origin/master (по умолчанию) или ветка/коммит агента (origin/D-27, <sha>). Ревью процесса 2026-10-08: у каждой задачи своя ветка.
param([string]$ref = "origin/master")
Set-Location C:\dev\zelda
# Leave no scene file open before git rewrites Assets/Scenes — otherwise Unity asks "modified externally — Reload?"
# and the modal silences the bridge (and nags the author). 2026-10-08.
$env:UMCP_PROJECT = "ZeldaDaughter"
& C:\Users\paper\.local\bin\uv.exe run --no-project python C:\dev\zelda-tools\umcp.py code C:\dev\zelda-tools\empty-scene.cs 2>&1 | Select-String "ZD:Sync" | ForEach-Object { $_.Line }
git fetch -q origin 2>&1 | Out-Null
git reset -q --hard $ref 2>&1 | Out-Null
"head: " + (git log --oneline -1)
icacls C:\dev\zelda /setowner "*S-1-5-21-4130441686-4109364392-877954578-1001" /T /C /Q 2>&1 | Select -Last 1
# A reset that rewrites the open scene makes Unity ask "modified externally — Reload?" — a modal that silences the bridge
# (2026-10-08, 20 minutes lost). Wait for it and press Reload: the disk version is the committed one.
Start-Sleep 5
& C:\dev\zelda-tools\zd-reload.ps1
