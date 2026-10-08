# D-19: замер производительности Windows-плеера (сборка perf: release + ZD_DEBUG): окно 1080x2340, героиня идёт на восток 60 с, итог — строки [ZD:Perf] из журнала.
param([string]$exe = "C:\dev\zelda-builds\perf\ZeldaDaughter.exe", [int]$seconds = 60)
$log = "C:\dev\zelda-tools\perf-player.log"
Remove-Item $log -ErrorAction SilentlyContinue
$t0 = Get-Date
$p = Start-Process -FilePath $exe -ArgumentList @("-screen-width","1080","-screen-height","2340","-screen-fullscreen","0","-logFile",$log,"-zd-perf-seconds","$seconds") -PassThru
$deadline = (Get-Date).AddSeconds($seconds + 90)
while (-not $p.HasExited -and (Get-Date) -lt $deadline) { Start-Sleep 2 }
if (-not $p.HasExited) { Stop-Process -Id $p.Id; "killed after timeout" }
"exit=$($p.ExitCode) wall=$([int]((Get-Date) - $t0).TotalSeconds)s"
Select-String -Path $log -Pattern "\[ZD:|Exception|Error|Display|Renderer:|Initialize engine" | Select -First 60 | ForEach-Object { $_.Line }
