var sb = new System.Text.StringBuilder();
sb.Append("timeScale=" + UnityEngine.Time.timeScale + " time=" + UnityEngine.Time.time + " unscaled=" + UnityEngine.Time.unscaledTime + " dt=" + UnityEngine.Time.deltaTime + " frame=" + UnityEngine.Time.frameCount + " fixedDt=" + UnityEngine.Time.fixedDeltaTime);
sb.Append(" paused=" + UnityEditor.EditorApplication.isPaused + " playing=" + UnityEditor.EditorApplication.isPlaying + " changing=" + UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode + " focused=" + UnityEditorInternal.InternalEditorUtility.isApplicationActive);
sb.Append(" scene=" + UnityEngine.SceneManagement.SceneManager.GetActiveScene().name + " scenes=" + UnityEngine.SceneManagement.SceneManager.sceneCount);
return sb.ToString();
