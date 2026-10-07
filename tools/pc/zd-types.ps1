foreach ($kind in "release","debug") {
  $dll = "C:\dev\zelda-builds\StandaloneWindows64-$kind\ZeldaDaughter_Data\Managed\ZeldaDaughter.dll"
  $text = [Text.Encoding]::ASCII.GetString([IO.File]::ReadAllBytes($dll))
  $found = @("PerfProbe","DebugTools","HeroController","IsoCamera","ZdLog") | ForEach-Object { "$_=" + $text.Contains($_) }
  "$kind : " + ($found -join " ")
}
