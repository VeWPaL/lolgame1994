using System.Collections.Generic;
using UnityEngine;

namespace Depths.Unity.Art
{
    /// <summary>One puff or ring the view is showing: the JS's dashFX and burstFX entries.</summary>
    public struct Fx
    {
        public float X, Y, Life, Max, R;
        public Color32 Color;
        public sbyte Dir;          // a ring: +1 grows from the point, -1 collapses onto it
        public bool Ring, Charge, Mine;
        public float Mom;          // the momentum a blink was spent at, for its tint
    }

    /// <summary>
    /// The JS keeps its blink trail, lunge puffs and burst rings as lists the simulation pushes to.
    /// Depths.Core has no view lists (it is the simulation only), so the view derives the same marks
    /// from what changed between frames: a spent blink charge, a lunger's state, an alt shell that is
    /// gone. Fixed pools: a frame allocates nothing once warm.
    /// </summary>
    public sealed class FxTracker
    {
        public const float DashTrail = 0.23f, LungeTrail = 0.16f, ChargeTrail = 0.34f, BurstLife = 0.2f, SecretBurst = 0.2f;
        const int Max = 256;

        readonly Fx[] _fx = new Fx[Max];
        int _count;
        public int Count => _count;
        public ref Fx this[int i] => ref _fx[i];

        sealed class Lunger { public string State = ""; public float FromX, FromY, PuffT; public int Seen; }
        readonly Dictionary<Enemy, Lunger> _lungers = new Dictionary<Enemy, Lunger>();
        readonly List<Enemy> _gone = new List<Enemy>();
        struct Shell { public Projectile P; public float X, Y; public bool Hook; public Color32 Color; public int Seen; }
        readonly Shell[] _shells = new Shell[64];
        int _shellCount, _frame;

        RunState _run;
        float _px, _py; int _charges; Dir? _secret; Room _room;

        /// <summary>Where a lunger's committed line starts, once it is lunging (lungeFromX/Y in the JS).</summary>
        public bool LungeFrom(Enemy e, out float x, out float y)
        {
            if (_lungers.TryGetValue(e, out var l) && l.State == "lunge") { x = l.FromX; y = l.FromY; return true; }
            x = y = 0; return false;
        }

        public void Clear() { _count = 0; _lungers.Clear(); _shellCount = 0; }

        void Add(Fx f)
        {
            if (_count == Max) { System.Array.Copy(_fx, 1, _fx, 0, Max - 1); _count--; }   // oldest out
            _fx[_count++] = f;
        }

        /// <summary>One frame: age what is shown by <paramref name="dt"/> (0 while paused), then look for new marks.</summary>
        public void Update(RunState run, float dt)
        {
            _frame++;
            if (!ReferenceEquals(run, _run)) { Clear(); _run = run; Snapshot(run); return; }
            if (dt > 0)
                for (int i = _count - 1; i >= 0; i--)
                    if ((_fx[i].Life -= dt) <= 0) { _fx[i] = _fx[--_count]; }

            var p = run.player;
            // a blink: a charge spent this frame lays up to six puffs over the distance covered
            if (run.blinkCharges < _charges && dt >= 0)
            {
                float ex = Mathf.Clamp((float)p.x, Balance.RoomLeft, Balance.RoomRight), ey = Mathf.Clamp((float)p.y, Balance.RoomTop, Balance.RoomBottom);
                float trav = Mathf.Sqrt((ex - _px) * (ex - _px) + (ey - _py) * (ey - _py));
                int puffs = Mathf.Max(1, Mathf.Min(6, Mathf.CeilToInt(trav / 24)));
                for (int i = 0; i < puffs; i++)
                {
                    float t = puffs == 1 ? 1 : i / (float)(puffs - 1);
                    Add(new Fx { X = _px + (ex - _px) * t, Y = _py + (ey - _py) * t, Life = DashTrail, Max = DashTrail, Mine = true, Mom = Mathf.Clamp01((float)p.momentum) });
                }
            }

            // lungers: puffs while winding up, a trail while lunging, and where the line started
            foreach (var e in run.enemies)
            {
                if (e.kind != BodyKind.Lunger) continue;
                if (!_lungers.TryGetValue(e, out var l)) _lungers[e] = l = new Lunger { State = e.lungeState };
                l.Seen = _frame;
                if (e.lungeState == "lunge" && l.State != "lunge") { l.FromX = (float)e.x; l.FromY = (float)e.y; }
                l.State = e.lungeState;
                if (dt <= 0) continue;
                l.PuffT -= dt;
                if (l.PuffT > 0) continue;
                if (e.lungeState == "wind") { l.PuffT = 0.06f; Add(new Fx { X = (float)e.x, Y = (float)e.y, Life = ChargeTrail, Max = ChargeTrail, Charge = true }); }
                else if (e.lungeState == "lunge") { l.PuffT = 2f / 60; Add(new Fx { X = (float)e.x, Y = (float)e.y, Life = LungeTrail, Max = LungeTrail }); }
            }
            _gone.Clear();
            foreach (var kv in _lungers) if (kv.Value.Seen != _frame) _gone.Add(kv.Key);
            foreach (var e in _gone) _lungers.Remove(e);

            // an alt shell that has gone off: its ring, in its colour, the way the weapon acts
            for (int i = 0; i < _shellCount; i++) _shells[i].Seen = 0;
            foreach (var s in run.projectiles)
            {
                if (!s.alt) continue;
                int at = -1;
                for (int i = 0; i < _shellCount; i++) if (ReferenceEquals(_shells[i].P, s)) { at = i; break; }
                if (at < 0 && _shellCount < _shells.Length) at = _shellCount++;
                if (at < 0) continue;
                _shells[at] = new Shell { P = s, X = (float)s.x, Y = (float)s.y, Hook = s.mode == "hook", Color = Hex.C(string.IsNullOrEmpty(s.color) ? Sprites.BlastColor : s.color), Seen = 1 };
            }
            for (int i = _shellCount - 1; i >= 0; i--)
            {
                if (_shells[i].Seen == 1) continue;
                var sh = _shells[i];
                if (ReferenceEquals(run.CurrentRoom, _room) && run.state == "playing")   // a room change clears shells; that is not a burst
                    Add(new Fx { X = sh.X, Y = sh.Y, Life = BurstLife, Max = BurstLife, R = sh.Hook ? (float)Balance.HookAoe : (float)Balance.AltAoe, Color = sh.Color, Dir = (sbyte)(sh.Hook ? -1 : 1), Ring = true });
                _shells[i] = _shells[--_shellCount];
            }

            // a fake wall blown open: a small ring where it stood
            var room = run.CurrentRoom;
            if (ReferenceEquals(room, _room) && _secret.HasValue && room != null && !room.Secret.HasValue)
            {
                var (dx, dy) = Rooms.DoorPoint(_secret.Value);
                Add(new Fx { X = (float)dx, Y = (float)dy, Life = SecretBurst, Max = SecretBurst, R = 46, Color = Hex.C("#ff9640"), Dir = 1, Ring = true });
            }
            Snapshot(run);
        }

        void Snapshot(RunState run)
        {
            _px = (float)run.player.x; _py = (float)run.player.y; _charges = run.blinkCharges;
            _room = run.CurrentRoom; _secret = _room?.Secret;
        }
    }
}
