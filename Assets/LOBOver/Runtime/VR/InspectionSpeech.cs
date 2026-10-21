#if UNITY_EDITOR || !UNITY_ANDROID
// Legacy Site.unity remains available in the Editor; WO-L2 excludes this seat from Quest.
using UnityEngine;

namespace LOBOver.VR
{
    public sealed class InspectionSpeech : MonoBehaviour
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        private AndroidJavaObject speech;
        private bool ready;
        private sealed class Listener : AndroidJavaProxy
        {
            private readonly InspectionSpeech owner;
            public Listener(InspectionSpeech value) : base("android.speech.tts.TextToSpeech$OnInitListener") { owner = value; }
            public void onInit(int status) { owner.ready = status == 0; }
        }
        private Listener listener;
        private void Start()
        {
            try
            {
                using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                {
                    listener = new Listener(this);
                    speech = new AndroidJavaObject("android.speech.tts.TextToSpeech", activity, listener);
                }
            }
            catch (AndroidJavaException) { ready = false; }
        }
#endif
        public void Speak(string text)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (!ready || speech == null) return;
            try { speech.Call<int>("speak", text, 0, null, "inspection"); }
            catch (AndroidJavaException) { ready = false; }
#endif
        }
        private void OnDestroy()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (speech == null) return;
            speech.Call("stop"); speech.Call("shutdown"); speech.Dispose(); speech = null;
#endif
        }
    }
}

#endif
