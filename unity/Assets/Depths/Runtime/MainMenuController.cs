using System;
using System.Linq;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Depths.Unity
{
    /// <summary>Start Game / Options / Quit, and the Options pages: key binds and volume.</summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class MainMenuController : MonoBehaviour
    {
        public InputActionAsset controls;
        public AudioMixer mixer;
        public string gameScene = "Game";

        static readonly (string slot, string label)[] Rows =
        {
            ("Move/up", "Move up"), ("Move/left", "Move left"), ("Move/down", "Move down"), ("Move/right", "Move right"),
            ("Blink", "Blink"), ("Active", "Use active item"), ("Pause", "Pause"), ("Sheet", "Character sheet"), ("Controls", "Controls help"),
        };

        VisualElement _main, _options, _controlsPage, _audioPage;
        Button _tabControls, _tabAudio;
        ScrollView _bindList;
        DropdownField _preset;
        Label _presetHint;
        Bindings _bindings;
        VolumeSettings _volume;
        InputActionRebindingExtensions.RebindingOperation _rebind;

        void OnEnable()
        {
            var store = new PlayerPrefsStore();
            _bindings = new Bindings(controls, store);
            _volume = new VolumeSettings(store);
            _volume.Changed += (c, v) => ApplyVolume();
            ApplyVolume();

            var root = GetComponent<UIDocument>().rootVisualElement;
            _main = root.Q("main");
            _options = root.Q("optionsPanel");
            _controlsPage = root.Q("controlsPage");
            _audioPage = root.Q("audioPage");
            _tabControls = root.Q<Button>("tabControls");
            _tabAudio = root.Q<Button>("tabAudio");
            _bindList = root.Q<ScrollView>("bindList");
            _preset = root.Q<DropdownField>("preset");
            _presetHint = root.Q<Label>("presetHint");

            root.Q<Button>("start").clicked += () => SceneManager.LoadScene(gameScene);
            root.Q<Button>("options").clicked += () => ShowView("controls");
            root.Q<Button>("quit").clicked += Quit;
            root.Q<Button>("back").clicked += () => ShowView("menu");
            _tabControls.clicked += () => ShowView("controls");
            _tabAudio.clicked += () => ShowView("audio");
            root.Q<Button>("resetBinds").clicked += () => { _bindings.ApplyPreset(_bindings.Preset); RefreshBinds(); };

            var presets = Enum.GetValues(typeof(KeyPreset)).Cast<KeyPreset>().ToList();
            _preset.choices = presets.Select(KeyPresets.Label).ToList();
            _preset.index = presets.IndexOf(_bindings.Preset);
            _preset.RegisterValueChangedCallback(_ =>
            {
                _bindings.ApplyPreset(presets[_preset.index]);
                RefreshBinds();
            });

            foreach (Channel c in Enum.GetValues(typeof(Channel)))
            {
                var s = root.Q<Slider>("vol-" + c);
                s.SetValueWithoutNotify(_volume.Get(c));
                var ch = c;
                s.RegisterValueChangedCallback(e => _volume.Set(ch, e.newValue));
            }
            RefreshBinds();
            ShowView("menu");
        }

        void OnDisable() { _rebind?.Cancel(); }

        // Without a mixer (it could not be generated) the master slider still works, on the listener.
        void ApplyVolume()
        {
            if (mixer != null) _volume.ApplyAll(mixer);
            else AudioListener.volume = _volume.Get(Channel.Master);
        }

        /// <summary>"menu", "controls" or "audio". Public so the screenshot mode can open a page.</summary>
        public void ShowView(string view)
        {
            bool menu = view == "menu";
            _main.EnableInClassList("hidden", !menu);
            _options.EnableInClassList("hidden", menu);
            _controlsPage.EnableInClassList("hidden", view != "controls");
            _audioPage.EnableInClassList("hidden", view != "audio");
            _tabControls.EnableInClassList("selected", view == "controls");
            _tabAudio.EnableInClassList("selected", view == "audio");
            (menu ? _main.Q<Button>("start") : _options.Q<Button>("back")).Focus();
        }

        void RefreshBinds()
        {
            _presetHint.text = _bindings.Preset == KeyPreset.Auto || _bindings.Preset == KeyPreset.Dvorak
                ? "Keys follow their position on the keyboard, so movement sits under the left hand on any layout."
                : "Keys follow the letter printed on them. Choose Auto if your keyboard is not this layout.";
            _bindList.Clear();
            foreach (var (slot, label) in Rows)
            {
                var row = new VisualElement();
                row.AddToClassList("bind-row");
                var name = new Label(label);
                name.AddToClassList("bind-name");
                var key = new Button { text = _bindings.Display(slot) };
                key.AddToClassList("bind-key");
                key.clicked += () =>
                {
                    if (_rebind != null) return;
                    key.text = "press a key...";
                    key.AddToClassList("listening");
                    _rebind = _bindings.StartRebind(slot, () => { _rebind = null; RefreshBinds(); });
                };
                row.Add(name);
                row.Add(key);
                _bindList.Add(row);
            }
        }

        static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
