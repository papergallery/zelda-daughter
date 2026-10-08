# Unity 6000.3.24f1: Android Build Support + child modules (OpenJDK, SDK, NDK) via Unity Hub, no window.
# Run detached: tlbridge.py spawn "C:\dev\zelda-tools\zd-android-module.ps1"; progress — hub-android.log.
# Start-Process -Wait: Hub is a GUI (Electron) exe, a plain call returns at once.
$hub = "C:\Program Files\Unity Hub\Unity Hub.exe"
$log = "C:\dev\zelda-tools\hub-android.log"
$err = "C:\dev\zelda-tools\hub-android.err"
$p = Start-Process -FilePath $hub -ArgumentList @("--","--headless","install-modules","--version","6000.3.24f1","-m","android","--childModules") `
     -RedirectStandardOutput $log -RedirectStandardError $err -PassThru -Wait -WindowStyle Hidden
"exit=$($p.ExitCode)" | Out-File C:\dev\zelda-tools\hub-android.done
