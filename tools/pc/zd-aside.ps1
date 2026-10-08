param([string]$mode = "out")
# D-24: временно отложить неотслеживаемый код других задач (копии с сервера), чтобы проверить чистый master; "in" — вернуть.
$ProgressPreference = "SilentlyContinue"
$root = "C:\dev\zelda"; $aside = "C:\dev\zelda-aside\d24"
Set-Location $root
if ($mode -eq "out") {
  $files = git status --short --untracked-files=all | Where-Object { $_ -match '^\?\? ' } | ForEach-Object { $_.Substring(3) } | Where-Object { $_ -match '\.(cs|cs\.meta)$' }
  foreach ($f in $files) { $dst = Join-Path $aside $f; New-Item -ItemType Directory -Force -Path (Split-Path $dst) | Out-Null; Move-Item -Force (Join-Path $root $f) $dst; "aside $f" }
} else {
  Get-ChildItem -Recurse -File $aside | ForEach-Object { $rel = $_.FullName.Substring($aside.Length + 1); $dst = Join-Path $root $rel; New-Item -ItemType Directory -Force -Path (Split-Path $dst) | Out-Null; Move-Item -Force $_.FullName $dst; "back $rel" }
}
