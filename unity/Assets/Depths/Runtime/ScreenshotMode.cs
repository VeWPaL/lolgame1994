using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Depths.Unity
{
    /// <summary>
    /// A built player started with <c>-depthsShot path.png [-depthsView menu|controls|audio|game]</c>
    /// opens that view, captures one frame and quits. It is how the menu is checked by eye from a script.
    /// </summary>
    public sealed class ScreenshotMode : MonoBehaviour
    {
        string _path, _view;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            var args = Environment.GetCommandLineArgs();
            string path = Arg(args, "-depthsShot");
            if (path == null) return;
            var go = new GameObject("ScreenshotMode");
            DontDestroyOnLoad(go);
            var s = go.AddComponent<ScreenshotMode>();
            s._path = path;
            s._view = Arg(args, "-depthsView") ?? "menu";
        }

        static string Arg(string[] a, string name)
        {
            int i = Array.IndexOf(a, name);
            return i >= 0 && i + 1 < a.Length ? a[i + 1] : null;
        }

        IEnumerator Start()
        {
            for (int i = 0; i < 10; i++) yield return null;
            if (_view == "game") SceneManager.LoadScene("Game");
            else
            {
                var m = FindAnyObjectByType<MainMenuController>();
                if (m != null) m.ShowView(_view);
            }
            for (int i = 0; i < 20; i++) yield return null;
            ScreenCapture.CaptureScreenshot(_path);
            for (int i = 0; i < 10; i++) yield return null;
            Application.Quit();
        }
    }
}
