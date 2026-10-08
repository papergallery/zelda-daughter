# D-16: selective import of Ian's Fire Pack from the Asset Store cache — no Unity windows, no package scripts' dialogs.
# Takes everything the URP prefabs need (URP prefabs, materials, shader graphs, textures, meshes, sounds, the two small scripts),
# leaves out the Standard-pipeline copy and the demo. GUIDs and .meta are kept, so the references inside the pack stay whole.
# Result: Assets\ThirdParty\IansFirePack\ (not in git). Run on the PC; then zd-refresh.
param([string]$Project = "C:\dev\zelda\ZeldaDaughter")
$ErrorActionPreference = "Stop"
$pkg = Get-ChildItem -Recurse -Filter "Ians Fire Pack.unitypackage" "$env:APPDATA\Unity\Asset Store-5.x" | Select-Object -First 1
if (-not $pkg) { throw "package not in the Asset Store cache" }
$dest = Join-Path $Project "Assets\ThirdParty\IansFirePack"
$tmp = "C:\dev\zelda-tools\tmp-firepack"
if (Test-Path $tmp) { Remove-Item -Recurse -Force $tmp }
New-Item -ItemType Directory $tmp | Out-Null
tar -xf $pkg.FullName -C $tmp
$root = "Assets/Ian's Fire Pack Universal/"
$skip = "^(_Standard Pipeline Specific|DemoSharedAssets|_URP Specific/Demo)"
$n = 0
foreach ($d in Get-ChildItem $tmp -Directory) {
    $pf = Join-Path $d.FullName "pathname"
    if (-not (Test-Path $pf)) { continue }
    $path = (Get-Content $pf -TotalCount 1 -Encoding UTF8).Trim()
    if (-not $path.StartsWith($root)) { continue }
    $rel = $path.Substring($root.Length)
    if ($rel -match $skip) { continue }
    $target = Join-Path $dest ($rel -replace "/", "\")
    $asset = Join-Path $d.FullName "asset"
    if (Test-Path $asset) {
        New-Item -ItemType Directory -Force (Split-Path $target) | Out-Null
        Copy-Item $asset $target -Force
        $n++
    } else {
        New-Item -ItemType Directory -Force $target | Out-Null
    }
    $meta = Join-Path $d.FullName "asset.meta"
    if (Test-Path $meta) { Copy-Item $meta ($target + ".meta") -Force }
}
Remove-Item -Recurse -Force $tmp
"imported files=$n into $dest"
