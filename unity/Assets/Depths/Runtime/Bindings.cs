using System;
using UnityEngine.InputSystem;

namespace Depths.Unity
{
    /// <summary>
    /// The player's key binds: a layout preset plus per-slot overrides, saved as the Input System's
    /// own override JSON so a rebind survives a restart and a new build reads it unchanged.
    /// </summary>
    public sealed class Bindings
    {
        public const string OverridesKey = "depths.bindings";
        public const string PresetKey = "depths.preset";

        readonly InputActionAsset _asset;
        readonly ISettingsStore _store;
        public KeyPreset Preset { get; private set; }

        public Bindings(InputActionAsset asset, ISettingsStore store)
        {
            _asset = asset;
            _store = store;
            Load();
        }

        public InputActionMap Gameplay => _asset.FindActionMap("Gameplay", true);

        /// <summary>
        /// The binding a slot names: "Move/up" is the "up" part of the FIRST composite (WASD, not the
        /// arrows, which stay fixed); "Blink" is the first keyboard binding of the action.
        /// </summary>
        public static int FindIndex(InputAction action, string slot)
        {
            int slash = slot.IndexOf('/');
            string part = slash < 0 ? null : slot.Substring(slash + 1);
            var b = action.bindings;
            if (part == null)
            {
                for (int i = 0; i < b.Count; i++)
                    if (!b[i].isComposite && !b[i].isPartOfComposite && b[i].path.StartsWith("<Keyboard>")) return i;
                return -1;
            }
            for (int i = 0; i < b.Count; i++)
            {
                if (!b[i].isComposite) continue;
                for (int j = i + 1; j < b.Count && b[j].isPartOfComposite; j++)
                    if (b[j].name == part) return j;
                return -1;   // only the first composite is rebindable
            }
            return -1;
        }

        public InputAction ActionFor(string slot)
        {
            int slash = slot.IndexOf('/');
            return Gameplay.FindAction(slash < 0 ? slot : slot.Substring(0, slash), true);
        }

        public string EffectivePath(string slot)
        {
            var a = ActionFor(slot);
            return a.bindings[FindIndex(a, slot)].effectivePath;
        }

        public string Display(string slot)
        {
            var a = ActionFor(slot);
            return a.GetBindingDisplayString(FindIndex(a, slot));
        }

        public void ApplyPreset(KeyPreset preset)
        {
            _asset.RemoveAllBindingOverrides();
            foreach (var kv in KeyPresets.PathsFor(preset))
            {
                var a = ActionFor(kv.Key);
                a.ApplyBindingOverride(FindIndex(a, kv.Key), kv.Value);
            }
            Preset = preset;
            Save();
        }

        public void Override(string slot, string path)
        {
            var a = ActionFor(slot);
            a.ApplyBindingOverride(FindIndex(a, slot), path);
            Save();
        }

        /// <summary>Waits for the next key and binds it to the slot. Escape cancels; the mouse is ignored.</summary>
        public InputActionRebindingExtensions.RebindingOperation StartRebind(string slot, Action done)
        {
            var a = ActionFor(slot);
            bool wasEnabled = a.enabled;
            a.Disable();
            return a.PerformInteractiveRebinding(FindIndex(a, slot))
                .WithControlsExcluding("<Mouse>")
                .WithCancelingThrough("<Keyboard>/escape")
                .OnMatchWaitForAnother(0.1f)
                .OnComplete(op => { op.Dispose(); if (wasEnabled) a.Enable(); Save(); done(); })
                .OnCancel(op => { op.Dispose(); if (wasEnabled) a.Enable(); done(); })
                .Start();
        }

        public void Save()
        {
            _store.SetString(OverridesKey, _asset.SaveBindingOverridesAsJson());
            _store.SetString(PresetKey, Preset.ToString());
            _store.Save();
        }

        public void Load()
        {
            Preset = Enum.TryParse(_store.GetString(PresetKey, "Auto"), out KeyPreset p) ? p : KeyPreset.Auto;
            _asset.RemoveAllBindingOverrides();
            string json = _store.GetString(OverridesKey, "");
            if (!string.IsNullOrEmpty(json)) _asset.LoadBindingOverridesFromJson(json);
        }
    }
}
