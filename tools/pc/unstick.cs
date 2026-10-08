// Снять зависшее задание тестов: выйти из play mode, очистить задание раннера (рефлексией), перезагрузить скрипты.
UnityEditor.EditorApplication.isPlaying = false;
var asm = System.AppDomain.CurrentDomain.GetAssemblies();
string res = "";
foreach (var a in asm)
{
    foreach (var t in a.GetTypes())
    {
        if (t.Name == "TestJobManager")
        {
            var m = t.GetMethod("ClearStuckJob", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
            if (m != null) { m.Invoke(null, null); res += "cleared " + t.FullName; }
        }
    }
}
UnityEditor.EditorUtility.RequestScriptReload();
return "stopped playing=" + UnityEditor.EditorApplication.isPlaying + " " + res;
