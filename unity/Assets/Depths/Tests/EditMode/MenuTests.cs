using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Depths.Unity.Tests
{
    sealed class MemoryStore : ISettingsStore
    {
        public readonly Dictionary<string, string> S = new Dictionary<string, string>();
        public readonly Dictionary<string, float> F = new Dictionary<string, float>();
        public int Saves;
        public string GetString(string k, string fb) => S.TryGetValue(k, out var v) ? v : fb;
        public void SetString(string k, string v) => S[k] = v;
        public float GetFloat(string k, float fb) => F.TryGetValue(k, out var v) ? v : fb;
        public void SetFloat(string k, float v) => F[k] = v;
        public void Save() => Saves++;
    }

    public class VolumeTests
    {
        [Test]
        public void DecibelsFollowTheSliderAndSilenceIsTheFloor()
        {
            Assert.That(VolumeSettings.ToDecibels(1f), Is.EqualTo(0f).Within(1e-4));
            Assert.That(VolumeSettings.ToDecibels(0.5f), Is.EqualTo(-6.0206f).Within(1e-3));
            Assert.That(VolumeSettings.ToDecibels(0f), Is.EqualTo(VolumeSettings.SilentDb));
        }

        [Test]
        public void AVolumeIsClampedSavedAndAnnounced()
        {
            var store = new MemoryStore();
            var v = new VolumeSettings(store);
            Assert.That(v.Get(Channel.Music), Is.EqualTo(VolumeSettings.Default), "an unset channel is not at the default");
            Channel? seen = null;
            v.Changed += (c, _) => seen = c;
            v.Set(Channel.Music, 1.7f);
            Assert.That(v.Get(Channel.Music), Is.EqualTo(1f), "a slider value above 1 was not clamped");
            Assert.That(seen, Is.EqualTo(Channel.Music), "listeners were not told");
            Assert.That(store.Saves, Is.GreaterThan(0), "the volume was not saved");
            Assert.That(new VolumeSettings(store).Get(Channel.Music), Is.EqualTo(1f), "a new session did not read it back");
        }

        [Test]
        public void EveryChannelHasItsOwnKeyAndMixerParameter()
        {
            var keys = new[] { Channel.Master, Channel.Music, Channel.Sfx }.Select(VolumeSettings.Key).ToList();
            Assert.That(keys.Distinct().Count(), Is.EqualTo(3));
            Assert.That(VolumeSettings.Param(Channel.Sfx), Is.EqualTo("SfxVolume"));
        }
    }

    public class KeyPresetTests
    {
        [Test]
        public void EveryPresetBindsEverySlot()
        {
            foreach (KeyPreset p in System.Enum.GetValues(typeof(KeyPreset)))
            {
                var m = KeyPresets.PathsFor(p);
                foreach (var slot in KeyPresets.Slots)
                    Assert.That(m.ContainsKey(slot), $"{p} leaves {slot} unbound");
            }
        }

        [Test]
        public void AzertyBindsByLetterAndAutoByPosition()
        {
            var az = KeyPresets.PathsFor(KeyPreset.Azerty);
            Assert.That(az["Move/up"], Is.EqualTo("<Keyboard>/#(Z)"));
            Assert.That(az["Move/left"], Is.EqualTo("<Keyboard>/#(Q)"));
            Assert.That(az["Active"], Is.EqualTo("<Keyboard>/#(A)"));
            var auto = KeyPresets.PathsFor(KeyPreset.Auto);
            Assert.That(auto["Move/up"], Is.EqualTo("<Keyboard>/w"), "Auto must bind the key POSITION");
            Assert.That(KeyPresets.Label(KeyPreset.Auto), Does.Contain("position"));
        }
    }

    public class BindingsTests
    {
        InputActionAsset _asset;

        [SetUp]
        public void Load()
        {
            var shipped = AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/Depths/Input/DepthsControls.inputactions");
            Assert.That(shipped, Is.Not.Null, "the controls asset is missing");
            _asset = Object.Instantiate(shipped);   // never mutate the shipped asset
        }

        [TearDown]
        public void Drop() { Object.DestroyImmediate(_asset); }

        [Test]
        public void EverySlotFindsAKeyboardBinding()
        {
            var b = new Bindings(_asset, new MemoryStore());
            foreach (var slot in KeyPresets.Slots)
            {
                var a = b.ActionFor(slot);
                int i = Bindings.FindIndex(a, slot);
                Assert.That(i, Is.GreaterThanOrEqualTo(0), $"{slot} has no binding");
                Assert.That(a.bindings[i].path, Does.StartWith("<Keyboard>"), $"{slot} points at a non-keyboard binding");
            }
            Assert.That(b.EffectivePath("Move/up"), Is.EqualTo("<Keyboard>/w"), "Move/up is not the WASD composite's up");
        }

        [Test]
        public void APresetAndARebindSurviveARestart()
        {
            var store = new MemoryStore();
            var b = new Bindings(_asset, store);
            b.ApplyPreset(KeyPreset.Azerty);
            Assert.That(b.EffectivePath("Move/up"), Is.EqualTo("<Keyboard>/#(Z)"));
            b.Override("Blink", "<Keyboard>/f");

            _asset.RemoveAllBindingOverrides();   // a restart: the asset comes back clean
            var again = new Bindings(_asset, store);
            Assert.That(again.Preset, Is.EqualTo(KeyPreset.Azerty), "the preset was not remembered");
            Assert.That(again.EffectivePath("Move/up"), Is.EqualTo("<Keyboard>/#(Z)"), "the preset's binds were lost");
            Assert.That(again.EffectivePath("Blink"), Is.EqualTo("<Keyboard>/f"), "a rebind was lost");
        }

        [Test]
        public void ResetToAutoClearsEveryOverride()
        {
            var b = new Bindings(_asset, new MemoryStore());
            b.Override("Active", "<Keyboard>/e");
            b.ApplyPreset(KeyPreset.Auto);
            Assert.That(b.EffectivePath("Active"), Is.EqualTo("<Keyboard>/q"));
        }
    }

    /// <summary>Depths.Core compiled by Unity's compiler must build the same floors as under dotnet.</summary>
    public class CoreInUnityTests
    {
        [Test]
        public void TheGeneratorMatchesTheDotnetParityTable()
        {
            // the 8675309 row of csharp/Depths.Tests/GeneratorParityTests.cs, itself read from the JS
            var rng = new Rng(8675309u);
            var d = Dungeon.FromSeed(rng);
            Assert.That(d.AllRooms.Count, Is.EqualTo(16));
            Assert.That(rng.RunCalls, Is.EqualTo(36));
            Assert.That(d.Signature(), Is.EqualTo(
                "0,2:n:E:K|1,2:n:EW:|1,5:b:E:|2,2:n:SW:|2,3:n:ENS:|2,4:n:NS:|2,5:n:NW:SS|2,6:s::|3,3:s:EW:|4,2:n:ES:|4,3:n:NSW:|4,4:n:NS:|4,5:n:NS:|4,6:i:N:|5,2:n:EW:|6,2:n:W:G"));
            Assert.That(Rng.Encode(4294967295u), Is.EqualTo("1Z141Z3"));
            Assert.That(Rng.Decode("ZZZZZZZ"), Is.Null);
        }
    }
}
