using UnityEngine;

namespace ZeldaDaughter
{
    /// <summary>What the hand feels (D-26): one short, clear pulse per event, stronger on a blow to the hero, nothing on a miss.</summary>
    public enum HapticKind { Hit, Kill, HeroStruck, Knockout }

    /// <summary>
    /// Vibration on Android through the system's own haptic effects (<c>View.performHapticFeedback</c> with HapticFeedbackConstants), not a long
    /// <c>Handheld.Vibrate</c>: Android Haptics design principles — rare, short and crisp, and silence when the device cannot do it. It needs no
    /// permission and follows the phone's «touch feedback» setting. Everywhere else (the editor, Windows) it only counts. The player's switch is
    /// <see cref="Enabled"/> (PlayerPrefs «zd_haptics», default on).
    /// </summary>
    public static class Haptics
    {
        private const string Pref = "zd_haptics";

        /// <summary>Pulses sent since the start (tests and the log read it).</summary>
        public static int Count { get; private set; }
        public static HapticKind Last { get; private set; }

        public static bool Enabled
        {
            get => PlayerPrefs.GetInt(Pref, 1) != 0;
            set { PlayerPrefs.SetInt(Pref, value ? 1 : 0); PlayerPrefs.Save(); }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Count = 0;
#if UNITY_ANDROID && !UNITY_EDITOR
            _failed = false;
#endif
        }

        public static void Pulse(HapticKind kind)
        {
            if (!Enabled) return;
            Count++;
            Last = kind;
#if UNITY_ANDROID && !UNITY_EDITOR
            Send(kind);
#endif
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        // View.HapticFeedbackConstants: LONG_PRESS 0, KEYBOARD_TAP 3, CONTEXT_CLICK 6 (API 23), CONFIRM 16 / REJECT 17 (API 30)
        private static bool _failed;

        private static void Send(HapticKind kind)
        {
            if (_failed) return;
            try
            {
                int sdk;
                using (var v = new AndroidJavaClass("android.os.Build$VERSION")) sdk = v.GetStatic<int>("SDK_INT");
                int constant;
                switch (kind)
                {
                    case HapticKind.Hit: constant = 3; break;
                    case HapticKind.Kill: constant = sdk >= 23 ? 6 : 3; break;
                    case HapticKind.HeroStruck: constant = 0; break;
                    default: constant = sdk >= 30 ? 17 : 0; break;
                }
                using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                {
                    var activity = player.GetStatic<AndroidJavaObject>("currentActivity");
                    activity.Call("runOnUiThread", new AndroidJavaRunnable(() =>
                    {
                        try
                        {
                            using (var window = activity.Call<AndroidJavaObject>("getWindow"))
                            using (var view = window.Call<AndroidJavaObject>("getDecorView"))
                                view.Call<bool>("performHapticFeedback", constant);
                        }
                        catch (System.Exception) { _failed = true; }   // no support: quiet
                    }));
                }
            }
            catch (System.Exception) { _failed = true; }
        }
#endif
    }
}
