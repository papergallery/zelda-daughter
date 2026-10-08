param([string]$exe = "C:\dev\zelda-builds\StandaloneWindows64-debug\ZeldaDaughter.exe")
$log = "C:\dev\zelda-tools\player.log"
Remove-Item $log -ErrorAction SilentlyContinue
$p = Start-Process -FilePath $exe -ArgumentList @("-batchmode","-nographics","-logFile",$log) -PassThru
Start-Sleep 15
if (-not $p.HasExited) { Stop-Process -Id $p.Id }
Select-String -Path $log -Pattern "\[ZD:|Exception|Error" | Select -First 20 | ForEach-Object { $_.Line }
