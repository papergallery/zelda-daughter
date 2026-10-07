#!/usr/bin/env bash
# Память и диск: сервер и ПК автора (через мост TensorLay). Пороги — когда тяжёлое не запускать (автор 2026-10-07: «следи за памятью и хранилкой»).
#   сервер: диск < 3 ГБ или память < 1 ГБ — не собирать, чистить scratchpad; ПК: C: < 20 ГБ или память < 4 ГБ — не запускать Unity.
set -u
df -BG --output=avail / | tail -1 | tr -dc 0-9 | xargs -I{} echo "server disk free: {} GB"
awk '/MemAvailable/ {printf "server mem available: %.1f GB\n", $2/1048576} /SwapFree/ {printf "server swap free: %.1f GB\n", $2/1048576}' /proc/meminfo
du -sh /tmp/claude-1002 2>/dev/null | awk '{print "scratchpad+tasks: " $1}'
B=/var/www/html/Other/tensorlay/tools/bridge/tlbridge.py
python3 "$B" exec '$ProgressPreference="SilentlyContinue"; $c=Get-PSDrive C; "pc C: free: {0:N1} GB" -f ($c.Free/1GB); $os=Get-CimInstance Win32_OperatingSystem; "pc mem free: {0:N1} GB of {1:N1}" -f ($os.FreePhysicalMemory/1MB), ($os.TotalVisibleMemorySize/1MB); $u=Get-Process Unity -ErrorAction SilentlyContinue; "pc unity processes: {0}, {1:N1} GB" -f $u.Count, (($u | Measure-Object WorkingSet64 -Sum).Sum/1GB)' --timeout 60 2>/dev/null | grep "^pc " || echo "pc: мост TensorLay не отвечает"
