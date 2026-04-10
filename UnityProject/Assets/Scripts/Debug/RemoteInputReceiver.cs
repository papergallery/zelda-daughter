using UnityEngine;
using System.IO;

namespace ZeldaDaughter.Debugging
{
    /// <summary>
    /// Reads touch commands from a file and injects them into GestureDispatcher.
    /// This bypasses Android input system issues on emulators where adb input doesn't work.
    ///
    /// Usage from host:
    ///   adb shell "echo 'swipe 540 1500 540 700 500' > /data/local/tmp/unity_input.txt"
    ///   adb shell "echo 'tap 540 1000' > /data/local/tmp/unity_input.txt"
    ///
    /// Commands:
    ///   swipe X1 Y1 X2 Y2 DURATION_MS  — simulates a swipe gesture
    ///   tap X Y                         — simulates a tap at position
    ///   longpress X Y DURATION_MS       — simulates a long press
    /// </summary>
    public class RemoteInputReceiver : MonoBehaviour
    {
        private static string CMD_FILE => Application.persistentDataPath + "/unity_input.txt";
        private float _checkInterval = 0.2f;
        private float _timer;

        // Swipe simulation state
        private bool _swiping;
        private Vector2 _swipeStart;
        private Vector2 _swipeEnd;
        private float _swipeDuration;
        private float _swipeElapsed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoCreate()
        {
            var go = new GameObject("[RemoteInputReceiver]");
            DontDestroyOnLoad(go);
            go.AddComponent<RemoteInputReceiver>();
            Debug.Log("[ZD:RemoteInput] Receiver active. Send commands to " + CMD_FILE);
        }

        private void Update()
        {
            // Check for new commands
            _timer += Time.unscaledDeltaTime;
            if (_timer >= _checkInterval)
            {
                _timer = 0;
                CheckForCommands();
            }

            // Process ongoing swipe
            if (_swiping)
            {
                ProcessSwipe();
            }
        }

        private void CheckForCommands()
        {
            if (!File.Exists(CMD_FILE)) return;

            try
            {
                string cmd = File.ReadAllText(CMD_FILE).Trim();
                File.Delete(CMD_FILE);

                if (string.IsNullOrEmpty(cmd)) return;

                Debug.Log($"[ZD:RemoteInput] Command: {cmd}");
                ParseAndExecute(cmd);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[ZD:RemoteInput] Error reading cmd: {e.Message}");
            }
        }

        private void ParseAndExecute(string cmd)
        {
            var parts = cmd.Split(' ');
            if (parts.Length == 0) return;

            switch (parts[0].ToLower())
            {
                case "swipe" when parts.Length >= 5:
                    float x1 = float.Parse(parts[1]);
                    float y1 = float.Parse(parts[2]);
                    float x2 = float.Parse(parts[3]);
                    float y2 = float.Parse(parts[4]);
                    float duration = parts.Length >= 6 ? float.Parse(parts[5]) / 1000f : 0.5f;
                    StartSwipe(new Vector2(x1, y1), new Vector2(x2, y2), duration);
                    break;

                case "tap" when parts.Length >= 3:
                    float tx = float.Parse(parts[1]);
                    float ty = float.Parse(parts[2]);
                    ExecuteTap(new Vector2(tx, ty));
                    break;

                case "hold" when parts.Length >= 3:
                    float hx = float.Parse(parts[1]);
                    float hy = float.Parse(parts[2]);
                    float holdDuration = parts.Length >= 4 ? float.Parse(parts[3]) / 1000f : 1f;
                    StartSwipe(new Vector2(hx, hy), new Vector2(hx, hy), holdDuration);
                    break;

                case "longpress_player":
                    ExecuteLongPressOnPlayer(parts.Length >= 2 ? float.Parse(parts[1]) / 1000f : 0.7f);
                    break;

                case "scan":
                    ScanTargets();
                    break;

                case "tap_enemy":
                    TapNearestEnemy();
                    break;

                default:
                    Debug.LogWarning($"[ZD:RemoteInput] Unknown command: {parts[0]}");
                    break;
            }
        }

        private void StartSwipe(Vector2 start, Vector2 end, float duration)
        {
            // Convert from Android coords (1080x2340 logical) to Unity screen coords
            // Android Y=0 at top, Unity Y=0 at bottom
            // Scale from logical Android resolution to actual Unity Screen resolution
            float scaleX = Screen.width / 1080f;    // Logical Android width
            float scaleY = Screen.height / 2340f;   // Logical Android height
            _swipeStart = new Vector2(start.x * scaleX, Screen.height - start.y * scaleY);
            _swipeEnd = new Vector2(end.x * scaleX, Screen.height - end.y * scaleY);
            Debug.Log($"[ZD:RemoteInput] Scale: {scaleX:F3}x{scaleY:F3} Screen:{Screen.width}x{Screen.height}");
            _swipeDuration = Mathf.Max(duration, 0.1f);
            _swipeElapsed = 0;
            _swiping = true;

            // Fire pointer down
            var dispatcher = FindObjectOfType<ZeldaDaughter.Input.GestureDispatcher>();
            if (dispatcher != null)
            {
                // Use reflection to call OnPointerDown
                var method = dispatcher.GetType().GetMethod("OnPointerDown",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                method?.Invoke(dispatcher, new object[] { _swipeStart });
                Debug.Log($"[ZD:RemoteInput] SwipeStart ({_swipeStart.x:F0},{_swipeStart.y:F0})");
            }
        }

        private void ProcessSwipe()
        {
            _swipeElapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(_swipeElapsed / _swipeDuration);

            Vector2 currentPos = Vector2.Lerp(_swipeStart, _swipeEnd, t);

            var dispatcher = FindObjectOfType<ZeldaDaughter.Input.GestureDispatcher>();
            if (dispatcher != null)
            {
                var heldMethod = dispatcher.GetType().GetMethod("OnPointerHeld",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                heldMethod?.Invoke(dispatcher, new object[] { currentPos });
            }

            if (t >= 1f)
            {
                _swiping = false;
                if (dispatcher != null)
                {
                    var upMethod = dispatcher.GetType().GetMethod("OnPointerUp",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    upMethod?.Invoke(dispatcher, new object[] { _swipeEnd });
                }
                Debug.Log($"[ZD:RemoteInput] SwipeEnd ({_swipeEnd.x:F0},{_swipeEnd.y:F0})");
            }
        }

        private void ExecuteLongPressOnPlayer(float duration)
        {
            // Find Player screen position via camera
            var player = GameObject.FindGameObjectWithTag("Player");
            var cam = Camera.main;
            if (player == null || cam == null)
            {
                Debug.LogWarning("[ZD:RemoteInput] LongPressPlayer: Player or Camera not found");
                return;
            }

            Vector3 screenPos = cam.WorldToScreenPoint(player.transform.position + Vector3.up * 0.9f);
            Vector2 pos2d = new Vector2(screenPos.x, screenPos.y);

            Debug.Log($"[ZD:RemoteInput] LongPressPlayer at screen ({pos2d.x:F0},{pos2d.y:F0}) for {duration}s");

            // Call OnPointerDown at player position, then hold for duration
            var dispatcher = FindObjectOfType<ZeldaDaughter.Input.GestureDispatcher>();
            if (dispatcher == null) return;

            var downMethod = dispatcher.GetType().GetMethod("OnPointerDown",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            downMethod?.Invoke(dispatcher, new object[] { pos2d });

            // Start a coroutine to hold and then release
            _swipeStart = pos2d;
            _swipeEnd = pos2d;
            _swipeDuration = duration;
            _swipeElapsed = 0;
            _swiping = true;
        }

        private void ExecuteTap(Vector2 pos)
        {
            float scaleX = Screen.width / 1080f;
            float scaleY = Screen.height / 2340f;
            Vector2 unityPos = new Vector2(pos.x * scaleX, Screen.height - pos.y * scaleY);

            var dispatcher = FindObjectOfType<ZeldaDaughter.Input.GestureDispatcher>();
            if (dispatcher != null)
            {
                var downMethod = dispatcher.GetType().GetMethod("OnPointerDown",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                downMethod?.Invoke(dispatcher, new object[] { unityPos });

                // Tap = quick down + up
                var upMethod = dispatcher.GetType().GetMethod("OnPointerUp",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                upMethod?.Invoke(dispatcher, new object[] { unityPos });
                Debug.Log($"[ZD:RemoteInput] Tap ({unityPos.x:F0},{unityPos.y:F0})");
            }
        }

        private void ScanTargets()
        {
            var cam = Camera.main;
            if (cam == null) return;
            try
            {
                foreach (var enemy in GameObject.FindGameObjectsWithTag("Enemy"))
                {
                    Vector3 sp = cam.WorldToScreenPoint(enemy.transform.position);
                    float ax = sp.x * 1080f / Screen.width;
                    float ay = (Screen.height - sp.y) * 2340f / Screen.height;
                    Debug.Log($"[ZD:Scan] {enemy.name} android=({ax:F0},{ay:F0}) world=({enemy.transform.position.x:F1},{enemy.transform.position.y:F1},{enemy.transform.position.z:F1})");
                }
            }
            catch { }
            // Also scan interactables
            var mbs = FindObjectsOfType<MonoBehaviour>();
            foreach (var mb in mbs)
            {
                if (mb is ZeldaDaughter.World.IInteractable)
                {
                    Vector3 sp = cam.WorldToScreenPoint(mb.transform.position);
                    float ax = sp.x * 1080f / Screen.width;
                    float ay = (Screen.height - sp.y) * 2340f / Screen.height;
                    Debug.Log($"[ZD:Scan] {mb.gameObject.name} android=({ax:F0},{ay:F0})");
                }
            }
        }

        private void TapNearestEnemy()
        {
            var cam = Camera.main;
            if (cam == null) return;
            GameObject nearest = null;
            float minDist = float.MaxValue;
            try
            {
                foreach (var enemy in GameObject.FindGameObjectsWithTag("Enemy"))
                {
                    float d = Vector3.Distance(cam.transform.position, enemy.transform.position);
                    if (d < minDist) { minDist = d; nearest = enemy; }
                }
            }
            catch { return; }
            if (nearest == null) return;

            Vector3 sp = cam.WorldToScreenPoint(nearest.transform.position + Vector3.up * 0.9f);
            Vector2 pos = new Vector2(sp.x, sp.y);
            Debug.Log($"[ZD:RemoteInput] TapEnemy {nearest.name} at screen ({pos.x:F0},{pos.y:F0})");

            var dispatcher = FindObjectOfType<ZeldaDaughter.Input.GestureDispatcher>();
            if (dispatcher == null) return;
            var downMethod = dispatcher.GetType().GetMethod("OnPointerDown",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            downMethod?.Invoke(dispatcher, new object[] { pos });
            var upMethod = dispatcher.GetType().GetMethod("OnPointerUp",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            upMethod?.Invoke(dispatcher, new object[] { pos });
        }
    }
}
