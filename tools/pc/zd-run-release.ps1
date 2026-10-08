# D-19 п. 2: Windows-релиз стартует и живёт 60 с с окном; в журнале — [ZD:Data], [ZD:Save], без Exception/Error.
param([string]$exe = "C:\dev\zelda-builds\StandaloneWindows64-release\ZeldaDaughter.exe", [int]$seconds = 60)
$log = "C:\dev\zelda-tools\release-player.log"
Remove-Item $log -ErrorAction SilentlyContinue
$t0 = Get-Date
$p = Start-Process -FilePath $exe -ArgumentList @("-screen-width","540","-screen-height","1170","-screen-fullscreen","0","-logFile",$log) -PassThru
Start-Sleep $seconds
$alive = -not $p.HasExited
if ($alive) { Stop-Process -Id $p.Id }
"alive_after_${seconds}s=$alive"
"--- errors:"
Select-String -Path $log -Pattern "Exception|Error|error" | ForEach-Object { $_.Line }
"--- ZD lines:"
Select-String -Path $log -Pattern "\[ZD:(Data|Save|Session|Combat|Nature)\]" | Select -First 12 | ForEach-Object { $_.Line }
