Add-Type @"
using System;
using System.Runtime.InteropServices;
using System.Text;
public static class W {
  public delegate bool EnumProc(IntPtr h, IntPtr l);
  [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc f, IntPtr l);
  [DllImport("user32.dll")] public static extern bool EnumChildWindows(IntPtr p, EnumProc f, IntPtr l);
  [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern int GetWindowText(IntPtr h, StringBuilder s, int n);
  [DllImport("user32.dll")] public static extern IntPtr SendMessage(IntPtr h, int m, IntPtr w, IntPtr l);
  public static string Text(IntPtr h) { var sb = new StringBuilder(256); GetWindowText(h, sb, 256); return sb.ToString(); }
}
"@
$dlg = [IntPtr]::Zero
[W]::EnumWindows({ param($h, $l) if ([W]::Text($h) -like "*modified externally*") { $script:dlg = $h; return $false }; return $true }, [IntPtr]::Zero) | Out-Null
if ($dlg -eq [IntPtr]::Zero) { "dialog not found"; return }
$btn = [IntPtr]::Zero
[W]::EnumChildWindows($dlg, { param($h, $l) if ([W]::Text($h) -eq "Reload" -or [W]::Text($h) -eq "&Reload") { $script:btn = $h; return $false }; return $true }, [IntPtr]::Zero) | Out-Null
if ($btn -eq [IntPtr]::Zero) { "button not found"; return }
[W]::SendMessage($btn, 0x00F5, [IntPtr]::Zero, [IntPtr]::Zero) | Out-Null
"BM_CLICK sent"
