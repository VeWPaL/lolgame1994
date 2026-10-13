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

        // a room holds a handful of Lungers: a fixed array and a linear search, allocation-free by construction
        // (the Dictionary version allocated every frame under Unity's Mono, measured with AllocMeter)
        struct Lunger { public Enemy E; public string State; public float FromX, FromY, PuffT; public int Seen; }
        readonly Lunger[] _lungers = new Lunger[64];
        int _lungerCount;

        int FindLunger(Enemy e)
        {
            for (int i = 0; i < _lungerCount; i++) if (ReferenceEquals(_lungers[i].E, e)) return i;
            return -1;
        }
        struct Shell { public Projectile P; public float X, Y, R; public bool Hook; public Color32 Color; public int Seen; }
        readonly Shell[] _shells = new Shell[64];
        int _shellCount, _frame;

        RunState _run;
        float _px, _py; int _charges; Dir? _secret; Room _room;

        /// <summary>Where a lunger's committed line starts, once it is lunging (lungeFromX/Y in the JS).</summary>
        public bool LungeFrom(Enemy e, out float x, out float y)
        {
            int i = FindLunger(e);
            if (i >= 0 && _lungers[i].State == "lunge") { x = _lungers[i].FromX; y = _lungers[i].FromY; return true; }
            x = y = 0; return false;
        }

        public void Clear() { _count = 0; _lungerCount = 0; _shellCount = 0; }

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
                int at = FindLunger(e);
                if (at < 0)
                {
                    if (_lungerCount == _lungers.Length) continue;   // past 64 Lungers the extra ones show no puffs
                    at = _lungerCount++;
                    _lungers[at] = new Lunger { E = e, State = e.lungeState };
                }
                ref var l = ref _lungers[at];
                l.Seen = _frame;
                if (e.lungeState == "lunge" && l.State != "lunge") { l.FromX = (float)e.x; l.FromY = (float)e.y; }
                l.State = e.lungeState;
                if (dt <= 0) continue;
                l.PuffT -= dt;
                if (l.PuffT > 0) continue;
                if (e.lungeState == "wind") { l.PuffT = 0.06f; Add(new Fx { X = (float)e.x, Y = (float)e.y, Life = ChargeTrail, Max = ChargeTrail, Charge = true }); }
                else if (e.lungeState == "lunge") { l.PuffT = 2f / 60; Add(new Fx { X = (float)e.x, Y = (float)e.y, Life = LungeTrail, Max = LungeTrail }); }
            }
            for (int i = _lungerCount - 1; i >= 0; i--)   // a Lunger gone from the room is forgotten
                if (_lungers[i].Seen != _frame) _lungers[i] = _lungers[--_lungerCount];

            // an alt shell that has gone off: its ring, in its colour, the way the weapon acts
            for (int i = 0; i < _shellCount; i++) _shells[i].Seen = 0;
            foreach (var s in run.projectiles)
            {
                if (!s.alt && !s.lob) continue;   // the alt shells, and a mortar's lob (it rings where it lands)
                int at = -1;
                for (int i = 0; i < _shellCount; i++) if (ReferenceEquals(_shells[i].P, s)) { at = i; break; }
                if (at < 0 && _shellCount < _shells.Length) at = _shellCount++;
                if (at < 0) continue;
                _shells[at] = new Shell { P = s, X = (float)(s.lob ? s.tx : s.x), Y = (float)(s.lob ? s.ty : s.y), Hook = s.mode == "hook",
                    R = s.lob ? (float)s.blastR : 0, Color = Hex.Of(string.IsNullOrEmpty(s.color) ? Sprites.BlastColor : s.color), Seen = 1 };
            }
            for (int i = _shellCount - 1; i >= 0; i--)
            {
                if (_shells[i].Seen == 1) continue;
                var sh = _shells[i];
                if (ReferenceEquals(run.CurrentRoom, _room) && run.state == "playing")   // a room change clears shells; that is not a burst
                    Add(new Fx { X = sh.X, Y = sh.Y, Life = BurstLife, Max = BurstLife, R = sh.R > 0 ? sh.R : sh.Hook ? (float)Balance.HookAoe : (float)Balance.AltAoe, Color = sh.Color, Dir = (sbyte)(sh.Hook ? -1 : 1), Ring = true });
                _shells[i] = _shells[--_shellCount];
            }

            // a fake wall blown open: a small ring where it stood
            var room = run.CurrentRoom;
            if (ReferenceEquals(room, _room) && _secret.HasValue && room != null && !room.Secret.HasValue)
            {
                var (dx, dy) = Rooms.DoorPoint(_secret.Value);
                Add(new Fx { X = (float)dx, Y = (float)dy, Life = SecretBurst, Max = SecretBurst, R = 46, Color = Hex.Of("#ff9640"), Dir = 1, Ring = true });
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
