using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using Depths.Unity.Art;

namespace Depths.Unity
{
    /// <summary>
    /// The lab (F2), after src/35-devlab.js: one room, a specimen of every body on two rows (frozen until
    /// F3), a dropper (F4 arms a kind, F7 drops a dozen, F8 one at the cursor), the keys and a heal, and
    /// damage numbers over whatever is hit. The only place the middle enemies spawn until they join the
    /// pool. The run never ends in here: a lab death refills you.
    /// </summary>
    public sealed class LabMode
    {
        public static readonly (BodyKind kind, string name, string blurb)[] Specimens =
        {
            (BodyKind.Lunger, "Lunger", "the baseline body"), (BodyKind.Brunch, "Brunch", "cover, in numbers"),
            (BodyKind.Shooter, "Shooter", "stands off and shoots"), (BodyKind.Gunner, "Gunner", "solves your line"),
            (BodyKind.Boss, "Warden", "the whole fight"),
            (BodyKind.Splitter, "Splitter", "bursts at range: keep moving"), (BodyKind.Mortar, "Mortar", "lands where you stood"),
            (BodyKind.Burrower, "Burrower", "comes up under your spot"), (BodyKind.Sprayer, "Sprayer", "long wind-up, short cone"),
            (BodyKind.Egg, "Turret egg", "never moves; rings the room"),
        };

        public bool On { get; private set; }
        public bool Frozen { get; private set; } = true;
        public int Armed { get; private set; } = 5;   // the dropper starts on the first new body

        readonly List<(Enemy e, double x, double y)> _row = new List<(Enemy, double, double)>();
        readonly Dictionary<Enemy, double> _lastHp = new Dictionary<Enemy, double>();
        readonly List<Enemy> _gone = new List<Enemy>();
        struct Number { public float X, Y, Vy, T; public double V; }
        readonly List<Number> _numbers = new List<Number>();
        int _pack = 1000000;
        VisualElement _layer, _legend;
        Label _armed;
        readonly List<Label> _numberLabels = new List<Label>();

        public void Enter(RunState run)
        {
            On = true; Frozen = true;
            run.enemies.Clear(); run.pickups.Clear(); run.projectiles.Clear(); run.hookFields.Clear();
            run.state = "playing"; run.readyT = 0; run.fadeT = 0; run.roomFade = 0; run.trans = null; run.bossWarnT = 0; run.bossWarned = true;
            if (run.CurrentRoom != null) run.CurrentRoom.Fought = true;
            var p = run.player;
            p.x = p.lagX = Balance.MidX; p.y = p.lagY = Balance.RoomBottom - 70;
            _row.Clear(); _lastHp.Clear(); _numbers.Clear();
            for (int i = 0; i < Specimens.Length; i++)
            {
                double x = Balance.RoomLeft + 70 + (i % 5) * 140, y = Balance.RoomTop + (i < 5 ? 70 : 180);
                var e = Spawn.Body(run, Specimens[i].kind, x, y);
                if (e.kind == BodyKind.Brunch) { e.packId = ++_pack; e.packSlot = 0; }
                run.enemies.Add(e);
                _row.Add((e, x, y));
            }
            Hold(run);
            if (_layer != null) _layer.style.display = DisplayStyle.Flex;
        }

        public void Leave()
        {
            On = false; _row.Clear(); _lastHp.Clear(); _numbers.Clear();
            if (_layer != null) _layer.style.display = DisplayStyle.None;
        }

        // frozen: the row is pinned where it stands and does not notice anything; it still takes damage
        void Hold(RunState run)
        {
            foreach (var (e, x, y) in _row)
            {
                if (e.hp <= 0) continue;
                if (Frozen)   // re-frozen mid-dive, a Burrower comes back up onto its plinth
                {
                    e.x = x; e.y = y; e.kvx = e.kvy = 0; e.noticeTimer = int.MaxValue / 2; e.castT = 0; e.castReady = false;
                    e.buried = false; e.move = "idle"; e.moveT = 0;
                }
            }
        }

        public void SetFrozen(bool frozen, RunState run)
        {
            Frozen = frozen;
            if (!frozen) Release(run);
        }

        void Release(RunState run)
        {
            foreach (var (e, _, _) in _row) { e.noticeTimer = 0; e.alerted = true; e.aggroTimer = Balance.AggroTime; }
        }

        /// <summary>The lab's keys. True if one was used (the caller then ignores it).</summary>
        public void Keys(Keyboard kb, RunState run, Vector2 aim)
        {
            if (kb == null) return;
            if (kb.f3Key.wasPressedThisFrame) { Frozen = !Frozen; if (!Frozen) Release(run); }
            if (kb.f4Key.wasPressedThisFrame)
                Armed = (Armed + (kb.shiftKey.isPressed ? Specimens.Length - 1 : 1)) % Specimens.Length;
            if (kb.f7Key.wasPressedThisFrame) Drove(run);
            if (kb.f8Key.wasPressedThisFrame) Drop(run, aim.x, aim.y);
            if (kb.gKey.wasPressedThisFrame) run.player.hasGold = true;
            if (kb.yKey.wasPressedThisFrame) run.player.hasSilver = true;
            if (kb.hKey.wasPressedThisFrame) Heal(run);
        }

        static void Heal(RunState run)
        {
            var p = run.player;
            p.hp = p.maxHp; p.regenHeart = p.regenHeartMax; p.cooldown = 0; p.altCooldown = 0;
            run.blinkCharges = 2; p.blinkRegen = 0;
        }

        Enemy Drop(RunState run, double x, double y)
        {
            x = Math.Max(Balance.RoomLeft + 30, Math.Min(Balance.RoomRight - 30, x));
            y = Math.Max(Balance.RoomTop + 30, Math.Min(Balance.RoomBottom - 30, y));
            var e = Spawn.Body(run, Specimens[Armed].kind, x, y);
            e.noticeTimer = 0; e.alerted = true; e.aggroTimer = Balance.AggroTime;
            run.enemies.Add(e);
            return e;
        }

        // a dozen of the armed body on a ring across the room from the player; Brunch as one pack
        void Drove(RunState run)
        {
            var p = run.player;
            double cx = Balance.MidX - (p.x - Balance.MidX) * 0.6, cy = Balance.MidY - (p.y - Balance.MidY) * 0.6;
            int pack = ++_pack, n = Specimens[Armed].kind == BodyKind.Boss ? 1 : 12;
            for (int i = 0; i < n; i++)
            {
                double a = i * Math.PI * 2 / n;
                var e = Drop(run, cx + Math.Cos(a) * 70, cy + Math.Sin(a) * 50);
                if (e.kind == BodyKind.Brunch) { e.packId = pack; e.packSlot = i; }
            }
        }

        /// <summary>After the frame's ticks: the row held, the run kept alive, damage turned into numbers.</summary>
        public void AfterTicks(RunState run, float dt)
        {
            if (!On) return;
            Hold(run);
            if (run.state != "playing" || run.player.hp <= 0) { Heal(run); run.state = "playing"; }
            foreach (var e in run.enemies)
            {
                if (!_lastHp.TryGetValue(e, out double was)) { _lastHp[e] = e.hp; continue; }
                if (Math.Abs(e.hp - was) > 1e-9)
                {
                    _numbers.Add(new Number { X = (float)e.x, Y = (float)(e.y - e.r - 6), Vy = -33, V = e.hp - was });
                    _lastHp[e] = e.hp;
                }
            }
            _gone.Clear();
            foreach (var kv in _lastHp) if (!run.enemies.Contains(kv.Key)) _gone.Add(kv.Key);
            foreach (var e in _gone)   // a kill shows what the last hit took
            {
                if (_lastHp[e] > 0) _numbers.Add(new Number { X = (float)e.x, Y = (float)(e.y - e.r - 6), Vy = -33, V = -_lastHp[e] });
                _lastHp.Remove(e);
            }
            for (int i = _numbers.Count - 1; i >= 0; i--)
            {
                var n = _numbers[i];
                n.T += dt; n.Y += n.Vy * dt; n.Vy *= Mathf.Pow(0.94f, dt * 60);
                if (n.T >= 1.1f) _numbers.RemoveAt(i); else _numbers[i] = n;
            }
        }

        // ---- the picture: damage numbers over the room, the legend along the bottom ----

        public void Attach(VisualElement root)
        {
            _layer = new VisualElement { pickingMode = PickingMode.Ignore };
            _layer.style.position = Position.Absolute;
            _layer.style.left = 0; _layer.style.top = 0; _layer.style.right = 0; _layer.style.bottom = 0;
            _layer.style.display = On ? DisplayStyle.Flex : DisplayStyle.None;
            root.Add(_layer);

            _legend = new VisualElement { pickingMode = PickingMode.Ignore };
            _legend.style.position = Position.Absolute;
            _legend.style.left = 274; _legend.style.width = 732; _legend.style.top = 682; _legend.style.height = 30;
            _legend.style.flexDirection = FlexDirection.Row; _legend.style.alignItems = Align.Center;
            _legend.style.paddingLeft = 10; _legend.style.paddingRight = 10;
            _legend.style.backgroundImage = Atlas.ToTexture(Materials.WoodPlate(732, 30, false), "lab-legend");
            foreach (var (k, what) in new[] { ("F2", "leave"), ("F3", "freeze"), ("F4", "arm"), ("F7", "drop 12"), ("F8", "drop 1"), ("G Y", "keys"), ("H", "heal") })
            {
                var cap = new Label(k);
                cap.style.backgroundColor = (Color)new Color32(20, 12, 6, 230);
                cap.style.color = (Color)new Color32(232, 220, 192, 255);
                cap.style.borderBottomWidth = 2; cap.style.borderBottomColor = (Color)new Color32(138, 98, 56, 255);
                cap.style.paddingLeft = 4; cap.style.paddingRight = 4; cap.style.fontSize = 11; cap.style.marginRight = 3;
                cap.style.unityFontStyleAndWeight = FontStyle.Bold;
                var lbl = new Label(what);
                lbl.style.color = (Color)new Color32(36, 18, 6, 255); lbl.style.fontSize = 11; lbl.style.marginRight = 7;
                lbl.style.unityFontStyleAndWeight = FontStyle.Bold;
                _legend.Add(cap); _legend.Add(lbl);
            }
            _armed = new Label();
            _armed.style.color = (Color)new Color32(255, 232, 170, 255); _armed.style.fontSize = 12; _armed.style.unityFontStyleAndWeight = FontStyle.Bold;
            _armed.style.marginLeft = StyleKeyword.Auto;
            _armed.style.backgroundColor = (Color)new Color32(20, 12, 6, 210); _armed.style.paddingLeft = 6; _armed.style.paddingRight = 6;
            _legend.Add(_armed);
            _layer.Add(_legend);
        }

        string _armedText;

        /// <summary>Positions the pooled number labels (no allocation once the pool covers the busiest frame).</summary>
        public void Draw()
        {
            if (!On || _layer == null) return;
            string armed = Specimens[Armed].name + (Frozen ? " - row frozen" : " - row live");
            if (armed != _armedText) { _armedText = armed; _armed.text = armed; }
            while (_numberLabels.Count < _numbers.Count)
            {
                var l = new Label { pickingMode = PickingMode.Ignore };
                l.style.position = Position.Absolute; l.style.fontSize = 13; l.style.unityFontStyleAndWeight = FontStyle.Bold;
                l.style.unityTextOutlineColor = (Color)new Color32(0, 0, 0, 255); l.style.unityTextOutlineWidth = 1;
                _layer.Add(l);
                _numberLabels.Add(l);
            }
            for (int i = 0; i < _numberLabels.Count; i++)
            {
                var l = _numberLabels[i];
                if (i >= _numbers.Count) { l.style.display = DisplayStyle.None; continue; }
                var n = _numbers[i];
                var at = WorldView.ToPanel(n.X, n.Y);
                l.style.display = DisplayStyle.Flex;
                l.style.left = at.x - 12; l.style.top = at.y;
                l.style.opacity = Mathf.Clamp01(1.4f - n.T / 1.1f);
                l.style.color = n.V > 0 ? (Color)new Color32(94, 226, 122, 255) : (Color)new Color32(255, 233, 168, 255);
                l.text = (n.V > 0 ? "+" : "") + (Math.Round(n.V * 10) / 10).ToString("0.#");
            }
        }
    }
}
