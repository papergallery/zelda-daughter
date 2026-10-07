Set-Location C:\dev\zelda
git fetch -q origin 2>&1 | Out-Null
git reset -q --hard origin/master 2>&1 | Out-Null
"head: " + (git log --oneline -1)
icacls C:\dev\zelda /setowner "*S-1-5-21-4130441686-4109364392-877954578-1001" /T /C /Q 2>&1 | Select -Last 1
