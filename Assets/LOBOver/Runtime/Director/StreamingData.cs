using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

namespace LOBOver.Runtime
{
    public static class StreamingData
    {
        public static string Url(string relative)
        {
            var path = Application.streamingAssetsPath.TrimEnd('/') + "/" + relative;
            return path.Contains("://") ? path : new Uri(path).AbsoluteUri;
        }

        public static IEnumerator ReadText(string relative, Action<string> loaded, Action<string> failed)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            using (var request = UnityWebRequest.Get(Url(relative)))
            {
                yield return request.SendWebRequest();
                if (request.result == UnityWebRequest.Result.Success) loaded(request.downloadHandler.text);
                else failed(relative + ": " + request.error);
            }
#else
            string value;
            try { value = File.ReadAllText(Path.Combine(Application.streamingAssetsPath, relative)); }
            catch (Exception error) { failed(relative + ": " + error.Message); yield break; }
            loaded(value);
            yield break;
#endif
        }
    }
}
