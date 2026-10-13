using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using Depths.Unity.Art;
using Depths.Unity.Audio;
using Depths.Unity.Hud;

namespace Depths.Unity
{
    /// <summary>
    /// Start Game / Options / Quit, and the Options pages: key binds, volume, gameplay. The menu is an
    /// open ledger on a desk (the HUD's wood and paper): the contents on the right page, the options'
    /// sections on the left and the chosen section on the right. Its textures are baked here at start.
    /// </summary>
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

        public const int KeyCapW = 132, KeyCapH = 30;

        VisualElement _main, _options, _controlsPage, _audioPage, _gameplayPage;
        Button _tabControls, _tabAudio, _tabGameplay;
        Label _pageTitle;
        ScrollView _bindList;
        DropdownField _preset;
        Label _presetHint;
        Toggle _curve;
        Bindings _bindings;
        VolumeSettings _volume;
        GameplaySettings _gameplay;
        InputActionRebindingExtensions.RebindingOperation _rebind;
        readonly List<Texture2D> _owned = new List<Texture2D>();
        Texture2D _cap, _capLit, _boxOn, _boxOff;

        void OnEnable()
        {
            var store = new PlayerPrefsStore();
            _bindings = new Bindings(controls, store);
            _volume = new VolumeSettings(store);
            _gameplay = new GameplaySettings(store);
            _volume.Changed += (c, v) => ApplyVolume();
            ApplyVolume();

            var root = GetComponent<UIDocument>().rootVisualElement;
            _main = root.Q("main");
            _options = root.Q("optionsPanel");
            _controlsPage = root.Q("controlsPage");
            _audioPage = root.Q("audioPage");
            _gameplayPage = root.Q("gameplayPage");
            _tabControls = root.Q<Button>("tabControls");
            _tabAudio = root.Q<Button>("tabAudio");
            _tabGameplay = root.Q<Button>("tabGameplay");
            _pageTitle = root.Q<Label>("pageTitle");
            _bindList = root.Q<ScrollView>("bindList");
            _preset = root.Q<DropdownField>("preset");
            _presetHint = root.Q<Label>("presetHint");
            Skin(root);

            root.Q<Button>("start").clicked += () => { SoundEngine.Play("door"); SceneManager.LoadScene(gameScene); };
            root.Q<Button>("options").clicked += () => { SoundEngine.Play("pickup"); ShowView("controls"); };
            root.Q<Button>("quit").clicked += Quit;
            root.Q<Button>("back").clicked += () => { SoundEngine.Play("pickup", 0, 0.7f); ShowView("menu"); };
            _tabControls.clicked += () => { SoundEngine.Play("pickup", 0, 0.6f); ShowView("controls"); };
            _tabAudio.clicked += () => { SoundEngine.Play("pickup", 0, 0.6f); ShowView("audio"); };
            _tabGameplay.clicked += () => { SoundEngine.Play("pickup", 0, 0.6f); ShowView("gameplay"); };
            // Esc (the UI's cancel) closes the options, as the back entry does
            root.RegisterCallback<NavigationCancelEvent>(_ => { if (_rebind == null && _main.ClassListContains("hidden")) ShowView("menu"); });
            _curve = root.Q<Toggle>("curve");
            _curve.SetValueWithoutNotify(_gameplay.Curve);
            _curve.RegisterValueChangedCallback(e => { _gameplay.Curve = e.newValue; Tick(); SoundEngine.Play("blink", 0, 0.6f); });
            Tick();
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
                s.RegisterValueChangedCallback(e => { _volume.Set(ch, e.newValue); Preview(ch); });
            }
            RefreshBinds();
            ShowView("menu");
        }

        void OnDisable()
        {
            _rebind?.Cancel();
            foreach (var t in _owned) Atlas.Free(t);
            _owned.Clear();
        }

        Texture2D Tex(Img img, string name)
        {
            var t = Atlas.ToTexture(img, name);
            _owned.Add(t);
            return t;
        }

        // The desk, the book, the struck title and the portrait; the ledger's hand on every label.
        void Skin(VisualElement root)
        {
            root.Q("root").style.backgroundImage = new StyleBackground(Tex(LedgerArt.Desk(1280, 720), "desk"));
            root.Q("book").style.backgroundImage = new StyleBackground(Tex(LedgerArt.Book(1100, 620), "book"));
            SetImage(root.Q("titleStamp"), LedgerArt.Stamp("DEPTHS", 9, LedgerArt.Ink));
            SetImage(root.Q("portrait"), LedgerArt.Portrait(7));
            LedgerFonts.Use(root, LedgerFonts.Regular);
            root.Query(className: "italic").ForEach(e => LedgerFonts.Use(e, LedgerFonts.Italic));
            _cap = Tex(LedgerArt.KeyCap(KeyCapW, KeyCapH, false), "keycap");
            _capLit = Tex(LedgerArt.KeyCap(KeyCapW, KeyCapH, true), "keycap-lit");
            _boxOn = Tex(LedgerArt.TickBox(26, true), "tick-on");
            _boxOff = Tex(LedgerArt.TickBox(26, false), "tick-off");
            var knob = Tex(LedgerArt.Knob(16, 26), "knob");
            root.Query(className: "unity-base-slider__dragger").ForEach(d => d.style.backgroundImage = new StyleBackground(knob));
            // the slider rides in a groove cut in a plank; the dropdown is a framed paper slip with a brass arrow
            var groove = Tex(LedgerArt.Groove(40, 14), "groove");
            root.Query(className: "unity-base-slider__tracker").ForEach(t => Sliced(t, groove, 5));
            var field = Tex(LedgerArt.FieldFrame(48, 30), "field");
            root.Query(className: "unity-base-popup-field__input").ForEach(f => Sliced(f, field, 6));
            var arrow = Tex(LedgerArt.BrassArrow(13, 9), "arrow");
            root.Query(className: "unity-base-popup-field__arrow").ForEach(a => { a.style.backgroundImage = new StyleBackground(arrow); a.style.width = 13; a.style.height = 9; });
        }

        static void Sliced(VisualElement e, Texture2D t, int edge)
        {
            e.style.backgroundImage = new StyleBackground(t);
            e.style.unitySliceLeft = edge; e.style.unitySliceRight = edge; e.style.unitySliceTop = edge; e.style.unitySliceBottom = edge;
        }

        void SetImage(VisualElement e, Img img)
        {
            e.style.backgroundImage = new StyleBackground(Tex(img, e.name));
            e.style.width = img.W; e.style.height = img.H;
        }

        Label _curveState;

        // the tick box, and On or Off written beside it so the state never rests on the tick alone
        void Tick()
        {
            var mark = _curve.Q(className: "unity-toggle__checkmark");
            if (mark != null) mark.style.backgroundImage = new StyleBackground(_curve.value ? _boxOn : _boxOff);
            if (_curveState == null)
            {
                _curveState = new Label { name = "curveState" };
                _curveState.AddToClassList("toggle-state");
                LedgerFonts.Use(_curveState, LedgerFonts.Italic);
                (_curve.Q(className: "unity-toggle__input") ?? _curve).Add(_curveState);
            }
            _curveState.text = _curve.value ? "On" : "Off";
            _curveState.EnableInClassList("on", _curve.value);
        }

        // Dragging a slider plays the level you are setting, at most a few times a second.
        float _lastPreview;
        void Preview(Channel c)
        {
            if (c == Channel.Music || Time.unscaledTime - _lastPreview < 0.18f) return;
            _lastPreview = Time.unscaledTime;
            SoundEngine.Play("hit");
        }

        // Without a mixer (it could not be generated) the master slider still works, on the listener.
        void ApplyVolume()
        {
            if (mixer != null) _volume.ApplyAll(mixer);
            else AudioListener.volume = _volume.Get(Channel.Master);
        }

        /// <summary>"menu", "controls", "audio" or "gameplay". Public so the screenshot mode can open a page.</summary>
        public void ShowView(string view)
        {
            bool menu = view == "menu";
            _main.EnableInClassList("hidden", !menu);
            _options.EnableInClassList("hidden", menu);
            _controlsPage.EnableInClassList("hidden", view != "controls");
            _audioPage.EnableInClassList("hidden", view != "audio");
            _gameplayPage.EnableInClassList("hidden", view != "gameplay");
            _tabControls.EnableInClassList("selected", view == "controls");
            _tabAudio.EnableInClassList("selected", view == "audio");
            _tabGameplay.EnableInClassList("selected", view == "gameplay");
            _pageTitle.text = view == "audio" ? "Audio" : view == "gameplay" ? "Gameplay" : "Controls";
            if (menu) _main.Q<Button>("start").Focus();
            else (view == "audio" ? _tabAudio : view == "gameplay" ? _tabGameplay : _tabControls).Focus();
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
                key.style.backgroundImage = new StyleBackground(_cap);
                key.clicked += () =>
                {
                    if (_rebind != null) return;
                    key.text = "press a key...";
                    key.AddToClassList("listening");
                    key.style.backgroundImage = new StyleBackground(_capLit);
                    _rebind = _bindings.StartRebind(slot, () => { _rebind = null; SoundEngine.Play("blink"); RefreshBinds(); });
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
