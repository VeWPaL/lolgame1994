using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Depths.Unity.EditorTools
{
    /// <summary>
    /// Builds the generated half of the project from scratch: the mixer, the panel settings, both
    /// scenes, build and player settings. Idempotent; run from the menu or in batch mode:
    /// Unity -batchmode -projectPath unity -executeMethod Depths.Unity.EditorTools.Bootstrap.Run -quit
    /// Scenes and assets are generated rather than hand-edited so they can be rebuilt and diffed.
    /// </summary>
    public static class Bootstrap
    {
        const string Root = "Assets/Depths";
        const string MixerPath = Root + "/Audio/DepthsMixer.mixer";
        const string PanelPath = Root + "/UI/DepthsPanel.asset";

        [MenuItem("Depths/Rebuild generated assets")]
        public static void Run()
        {
            var mixer = EnsureMixer();
            var panel = EnsurePanel();
            var controls = AssetDatabase.LoadAssetAtPath<InputActionAsset>(Root + "/Input/DepthsControls.inputactions");
            var menuUxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(Root + "/UI/MainMenu.uxml");
            var styles = AssetDatabase.LoadAssetAtPath<StyleSheet>(Root + "/UI/MainMenu.uss");
            if (controls == null || menuUxml == null || styles == null)
                throw new Exception("Bootstrap: missing input actions, menu UXML or USS - did the import finish?");

            // MainMenu
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            AddCamera();
            var menu = new GameObject("MainMenu");
            var doc = menu.AddComponent<UIDocument>();
            doc.panelSettings = panel;
            doc.visualTreeAsset = menuUxml;
            var ctl = menu.AddComponent<MainMenuController>();
            ctl.controls = controls;
            ctl.mixer = mixer;
            var sound = new GameObject("SoundEngine").AddComponent<Depths.Unity.Audio.SoundEngine>();
            var groups = mixer != null ? mixer.FindMatchingGroups("Sfx") : null;
            sound.sfx = groups != null && groups.Length > 0 ? groups[0] : null;
            var musicGroups = mixer != null ? mixer.FindMatchingGroups("Music") : null;
            sound.music = musicGroups != null && musicGroups.Length > 0 ? musicGroups[0] : null;
            EditorSceneManager.SaveScene(scene, Root + "/Scenes/MainMenu.unity");

            // Game (placeholder until the port plays)
            scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            AddCamera();
            var game = new GameObject("Game");
            var gdoc = game.AddComponent<UIDocument>();
            // reloaded, not reused: NewScene(Single) unloads unused assets, and the reference taken
            // before it serialised as null, so the Game scene had no panel and drew nothing
            gdoc.panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelPath);
            gdoc.visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(Root + "/UI/Game.uxml");
            game.AddComponent<GameView>().controls =
                AssetDatabase.LoadAssetAtPath<InputActionAsset>(Root + "/Input/DepthsControls.inputactions");
            EditorSceneManager.SaveScene(scene, Root + "/Scenes/Game.unity");

            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(Root + "/Scenes/MainMenu.unity", true),
                new EditorBuildSettingsScene(Root + "/Scenes/Game.unity", true),
            };

            PlayerSettings.productName = "Depths";
            PlayerSettings.companyName = "Depths";
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.defaultScreenWidth = 1280;
            PlayerSettings.defaultScreenHeight = 720;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.runInBackground = true;
            SetActiveInputHandling(2);   // both: the Input System for the game, the old manager stays harmless
            AssetDatabase.SaveAssets();
            Debug.Log("[Depths] Bootstrap done. Mixer: " + (mixer != null ? MixerPath : "NOT generated (volume falls back to the listener)"));
        }

        static void AddCamera()
        {
            var cam = new GameObject("Camera").AddComponent<UnityEngine.Camera>();   // Depths.Core has its own Camera
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(11 / 255f, 13 / 255f, 18 / 255f);
            cam.orthographic = true;
        }

        static PanelSettings EnsurePanel()
        {
            var p = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelPath);
            if (p == null)
            {
                p = ScriptableObject.CreateInstance<PanelSettings>();
                AssetDatabase.CreateAsset(p, PanelPath);
            }
            p.themeStyleSheet = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(Root + "/UI/DepthsTheme.tss");
            p.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            p.referenceResolution = new Vector2Int(1280, 720);
            p.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            p.match = 0.5f;
            EditorUtility.SetDirty(p);
            return p;
        }

        /* THE MIXER. Unity has no public API to create a mixer or its groups, so this uses the
           internal AudioMixerController the "Create > Audio Mixer" menu uses. If a Unity update moves
           it, this returns null, the menu falls back to AudioListener.volume for Master, and the log
           says so - the game never depends on the reflection succeeding. */
        static AudioMixer EnsureMixer()
        {
            var existing = AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath);
            if (existing != null) return existing;
            try
            {
                var asm = typeof(UnityEditor.Editor).Assembly;
                var ctlType = asm.GetType("UnityEditor.Audio.AudioMixerController", true);
                var create = ctlType.GetMethod("CreateMixerControllerAtPath", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                var mixer = create.Invoke(null, new object[] { MixerPath });
                var master = ctlType.GetProperty("masterGroup", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).GetValue(mixer);
                var newGroup = ctlType.GetMethod("CreateNewGroup", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                var addChild = ctlType.GetMethod("AddChildToParent", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                var music = newGroup.Invoke(mixer, new object[] { "Music", false });
                addChild.Invoke(mixer, new[] { music, master });
                var sfx = newGroup.Invoke(mixer, new object[] { "Sfx", false });
                addChild.Invoke(mixer, new[] { sfx, master });
                Expose(mixer, ctlType, master, "MasterVolume");
                Expose(mixer, ctlType, music, "MusicVolume");
                Expose(mixer, ctlType, sfx, "SfxVolume");
                EditorUtility.SetDirty((UnityEngine.Object)mixer);
                AssetDatabase.SaveAssets();
                return AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Depths] Could not generate the mixer through Unity's internal API: " + e.GetBaseException().Message);
                return null;
            }
        }

        // Exposes a group's volume as a named parameter, the way right-click > Expose does.
        static void Expose(object mixer, Type ctlType, object group, string name)
        {
            var groupType = group.GetType();
            var guid = groupType.GetMethod("GetGUIDForVolume", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).Invoke(group, null);
            var asm = ctlType.Assembly;
            var pathType = asm.GetType("UnityEditor.Audio.AudioGroupParameterPath", true);
            var path = Activator.CreateInstance(pathType, group, guid);
            ctlType.GetMethod("AddExposedParameter", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).Invoke(mixer, new[] { path });
            var prop = ctlType.GetProperty("exposedParameters", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            var arr = (Array)prop.GetValue(mixer);
            for (int i = 0; i < arr.Length; i++)
            {
                var p = arr.GetValue(i);
                var g = p.GetType().GetField("guid").GetValue(p);
                if (!g.Equals(guid)) continue;
                p.GetType().GetField("name").SetValue(p, name);
                arr.SetValue(p, i);
            }
            prop.SetValue(mixer, arr);
        }

        static void SetActiveInputHandling(int value)
        {
            var settings = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset").FirstOrDefault();
            if (settings == null) return;
            var so = new SerializedObject(settings);
            var p = so.FindProperty("activeInputHandler");
            if (p == null) return;
            p.intValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    public static class BuildTools
    {
        /// <summary>Unity -batchmode -projectPath unity -executeMethod Depths.Unity.EditorTools.BuildTools.BuildWindows -quit</summary>
        public static void BuildWindows()
        {
            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            var outPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Build", "Depths.exe"));
            var report = BuildPipeline.BuildPlayer(scenes, outPath, BuildTarget.StandaloneWindows64, BuildOptions.None);
            Debug.Log("[Depths] Build " + report.summary.result + " -> " + outPath + " (" + report.summary.totalErrors + " errors)");
            if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded) EditorApplication.Exit(1);
        }
    }
}
