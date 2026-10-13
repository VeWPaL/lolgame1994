using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using Depths.Unity.Art;

namespace Depths.Unity.Hud
{
    /// <summary>
    /// The pages laid over the game in the ledger's materials: the pause note, the run's account (the
    /// JS summary sheet), the floor heading and the Warden's notice. Each is built once and shown or
    /// hidden; text is written when a page opens, never per frame. The heading and the notice sit in
    /// the band above the room, so a descent never covers the fight.
    /// </summary>
    public sealed class HudOverlays : VisualElement
    {
        public const int SheetW = 500, SheetH = 560, PauseW = 420, PauseH = 300, RowY = 92, RowStep = 30, RecordsY = RowY + RowStep * 9 + 14;
        readonly VisualElement _dim, _pause, _summary, _banner, _warning;
        readonly Label _pauseLine, _summaryHead, _newFloor, _newRooms;
        readonly Label[] _records = new Label[2];
        readonly VisualElement _pauseSeed;
        Texture2D _pauseSeedTex;
        uint _pauseSeedOf; bool _pauseSeeded;
        readonly Label[] _values;
        readonly List<Texture2D> _owned = new List<Texture2D>();
        Texture2D _bannerTex;
        int _bannerFloor = -1;
        float _bannerAlpha = -1, _warnAlpha = -1;

        public static readonly string[] SummaryRows = { "Floor reached", "Time on this floor", "Time", "Rooms explored", "Enemies defeated", "Damage taken", "Accuracy", "Weapon", "Seed" };

        public bool PauseShown => _pause.style.display == DisplayStyle.Flex;
        public bool SummaryShown => _summary.style.display == DisplayStyle.Flex;
        public bool BannerShown => _banner.style.display == DisplayStyle.Flex;
        public bool WarningShown => _warning.style.display == DisplayStyle.Flex;
        public string SummaryValue(int row) => _values[row].text;

        public HudOverlays()
        {
            pickingMode = PickingMode.Ignore;
            style.position = Position.Absolute;
            style.left = 0; style.top = 0; style.right = 0; style.bottom = 0;

            _dim = new VisualElement { pickingMode = PickingMode.Ignore };
            _dim.style.position = Position.Absolute;
            _dim.style.left = 0; _dim.style.top = 0; _dim.style.right = 0; _dim.style.bottom = 0;
            _dim.style.backgroundColor = new Color(0.02f, 0.012f, 0.008f, 0.72f);
            Add(_dim);

            // the pause: a short note on the same paper, its title struck
            var pause = LedgerArt.Sheet(PauseW, PauseH);
            LedgerType.Struck(pause, "PAUSED", PauseW / 2, 24, 4, LedgerArt.Ink, LedgerArt.Lift);
            pause.Fill(PauseW / 2 - 120, 70, 240, 1, LedgerArt.InkFaint); pause.Fill(PauseW / 2 - 120, 73, 240, 2, LedgerArt.InkFaint);
            _pause = Sheet(pause, 640 - PauseW / 2, 360 - PauseH / 2 - 10);
            Line(_pause, "Esc", "put the ledger down and play on", 92);
            Line(_pause, "R", "begin a new run", 120);
            Line(_pause, "M", "back to the menu", 148);
            _pauseLine = LedgerFonts.Ink("", 14, (Color)LedgerArt.InkSoft, LedgerFonts.Italic, TextAnchor.MiddleCenter);
            Put(_pauseLine, 20, 180, PauseW - 40, 22);
            _pause.Add(_pauseLine);
            // the run's seed tag, the same luggage tag as the ledger's, so the seed can be read off and shared
            _pauseSeed = new VisualElement { pickingMode = PickingMode.Ignore };
            Put(_pauseSeed, PauseW / 2 - 104, 212, 208, 56);
            _pause.Add(_pauseSeed);

            // the run's account: the JS summary rows, written as a ledger page
            var sheet = LedgerArt.Sheet(SheetW, SheetH);
            for (int i = 0; i < SummaryRows.Length; i++) sheet.Fill(36, RowY + i * RowStep + 26, SheetW - 72, 1, LedgerArt.Rule);
            // the JS's RECORDS block: a ruled heading, then the bests across runs
            LedgerArt.Label(sheet, "RECORDS", 40, RecordsY, LedgerArt.InkSoft);
            sheet.Fill(40 + LedgerType.Width(7, 2) + 10, RecordsY + 6, SheetW - 76 - LedgerType.Width(7, 2) - 14, 2, LedgerArt.InkFaint);
            for (int i = 0; i < 2; i++) sheet.Fill(36, RecordsY + 22 + i * RowStep + 26, SheetW - 72, 1, LedgerArt.Rule);
            sheet.Fill(30, 18, 1, SheetH - 36, LedgerArt.Margin); sheet.Fill(33, 18, 1, SheetH - 36, LedgerArt.Margin);
            _summary = Sheet(sheet, 640 - SheetW / 2, 360 - SheetH / 2 - 6);
            _summaryHead = LedgerFonts.Ink("", 30, (Color)LedgerArt.RedInk, LedgerFonts.Bold, TextAnchor.MiddleCenter);
            Put(_summaryHead, 20, 26, SheetW - 40, 40);
            _summary.Add(_summaryHead);
            var sub = LedgerFonts.Ink("the account of the run", 14, (Color)LedgerArt.InkSoft, LedgerFonts.Italic, TextAnchor.MiddleCenter);
            Put(sub, 20, 64, SheetW - 40, 20);
            _summary.Add(sub);
            _values = new Label[SummaryRows.Length];
            for (int i = 0; i < SummaryRows.Length; i++)
            {
                var k = LedgerFonts.Ink(SummaryRows[i], 16, (Color)LedgerArt.InkSoft, LedgerFonts.Regular);
                Put(k, 40, RowY + i * RowStep, 240, 26);
                _summary.Add(k);
                var v = LedgerFonts.Ink("", 16, (Color)LedgerArt.Ink, LedgerFonts.Bold, TextAnchor.MiddleRight);
                Put(v, 240, RowY + i * RowStep, SheetW - 240 - 40, 26);
                _summary.Add(v);
                _values[i] = v;
            }
            string[] recs = { "Deepest floor", "Best rooms explored" };
            for (int i = 0; i < 2; i++)
            {
                var k = LedgerFonts.Ink(recs[i], 16, (Color)LedgerArt.InkSoft, LedgerFonts.Regular);
                Put(k, 40, RecordsY + 22 + i * RowStep, 240, 26);
                _summary.Add(k);
                _records[i] = LedgerFonts.Ink("", 16, (Color)LedgerArt.Ink, LedgerFonts.Bold, TextAnchor.MiddleRight);
                Put(_records[i], 240, RecordsY + 22 + i * RowStep, SheetW - 240 - 40, 26);
                _summary.Add(_records[i]);
            }
            _newFloor = NewTag(_summary, RowY + 0 * RowStep);
            _newRooms = NewTag(_summary, RowY + 3 * RowStep);
            Line(_summary, "R", "a new run", SheetH - 62, 120);
            Line(_summary, "M", "the menu", SheetH - 62, 290);

            _banner = new VisualElement { pickingMode = PickingMode.Ignore };
            var br = HudLayout.Banner;
            Put(_banner, (int)br.x, (int)br.y, (int)br.width, (int)br.height);
            Add(_banner);

            var notice = LedgerArt.Notice((int)HudLayout.Warning.width, (int)HudLayout.Warning.height);
            // the notice sits in the HUD's band, so it is written in the HUD's hand
            LedgerType.Struck(notice, "SOMETHING HEAVY IS DOWN THERE", notice.W / 2 - 2, 20, 2, LedgerArt.RedInk, LedgerArt.Lift);
            LedgerType.Struck(notice, "THE GOLD KEY OPENS ITS DOOR", notice.W / 2 - 2, 46, 1, LedgerArt.InkSoft, LedgerArt.Lift);
            _warning = Sheet(notice, (int)HudLayout.Warning.x, (int)HudLayout.Warning.y);

            Hide(_dim); Hide(_pause); Hide(_summary); Hide(_banner); Hide(_warning);
        }

        VisualElement Sheet(Img img, int x, int y)
        {
            var tex = Atlas.ToTexture(img, "sheet");
            _owned.Add(tex);
            var e = new VisualElement { pickingMode = PickingMode.Ignore };
            Put(e, x, y, img.W, img.H);
            e.style.backgroundImage = new StyleBackground(tex);
            Add(e);
            return e;
        }

        // a key cap and what it does, written on the line
        static void Line(VisualElement page, string key, string what, int y, int x = 64)
        {
            var cap = LedgerFonts.Ink(key, 13, new Color(0.94f, 0.88f, 0.75f), LedgerFonts.Bold, TextAnchor.MiddleCenter);
            Put(cap, x, y, 44, 22);
            cap.style.backgroundColor = new Color(0.36f, 0.23f, 0.12f);
            cap.style.borderBottomWidth = 3; cap.style.borderBottomColor = new Color(0.14f, 0.08f, 0.04f);
            cap.style.borderTopLeftRadius = cap.style.borderTopRightRadius = cap.style.borderBottomLeftRadius = cap.style.borderBottomRightRadius = 3;
            page.Add(cap);
            var l = LedgerFonts.Ink(what, 16, (Color)LedgerArt.Ink, LedgerFonts.Italic);
            Put(l, x + 56, y - 1, 280, 24);
            page.Add(l);
        }

        static void Put(VisualElement e, int x, int y, int w, int h)
        {
            e.style.position = Position.Absolute;
            e.style.left = x; e.style.top = y; e.style.width = w; e.style.height = h;
        }

        static void Hide(VisualElement e) { if (e.style.display != DisplayStyle.None) e.style.display = DisplayStyle.None; }
        static void Show(VisualElement e) { if (e.style.display != DisplayStyle.Flex) e.style.display = DisplayStyle.Flex; }
        static void Opacity(VisualElement e, float a) { if (Math.Abs(e.style.opacity.value - a) > 1e-4f || e.style.opacity.keyword != StyleKeyword.Undefined) e.style.opacity = a; }

        static string Clock(double seconds)
        {
            int s = (int)seconds;
            return (s / 60) + ":" + (s % 60).ToString("00") + "." + ((int)((seconds - s) * 10)).ToString();
        }

        /// <summary>Opens or closes the pause note; its line is written as it opens.</summary>
        public void SetPause(bool on, RunState run)
        {
            if (on == PauseShown) return;
            if (on)
            {
                _pauseLine.text = "floor " + run.floor + "  ·  " + run.kills + " slain  ·  " + Clock(run.ticks / (double)Balance.TickHz);
                if (!_pauseSeeded || _pauseSeedOf != run.rootSeed)
                {
                    _pauseSeeded = true; _pauseSeedOf = run.rootSeed;
                    Atlas.Free(_pauseSeedTex);
                    _pauseSeedTex = Atlas.ToTexture(Materials.SeedTag(208, 56, Rng.Encode(run.rootSeed)), "pause-seed");
                    _pauseSeed.style.backgroundImage = new StyleBackground(_pauseSeedTex);
                }
                Show(_pause);
            }
            else Hide(_pause);
            Dim();
        }

        /// <summary>Opens the account when the run ends, filled once from the run.</summary>
        // the red NEW beside a value that beat its record
        static Label NewTag(VisualElement page, int y)
        {
            var l = LedgerFonts.Ink("NEW", 11, (Color)LedgerArt.RedInk, LedgerFonts.Bold, TextAnchor.MiddleCenter);
            Put(l, 286, y + 6, 36, 15);
            l.style.borderTopWidth = l.style.borderBottomWidth = l.style.borderLeftWidth = l.style.borderRightWidth = 1;
            l.style.borderTopColor = l.style.borderBottomColor = l.style.borderLeftColor = l.style.borderRightColor = (Color)LedgerArt.RedInk;
            l.style.display = DisplayStyle.None;
            page.Add(l);
            return l;
        }

        public bool NewFloorShown => _newFloor.style.display == DisplayStyle.Flex;
        public bool NewRoomsShown => _newRooms.style.display == DisplayStyle.Flex;
        public string RecordValue(int row) => _records[row].text;

        public void SetSummary(bool on, RunState run) => SetSummary(on, run, default);

        public void SetSummary(bool on, RunState run, RunRecords.Result records)
        {
            if (on == SummaryShown) return;
            if (on)
            {
                Fill(run, records);
                Show(_summary);
            }
            else Hide(_summary);
            Dim();
        }

        void Dim() { if (PauseShown || SummaryShown) Show(_dim); else Hide(_dim); }

        /// <summary>The JS summary rows from the run (written so the EditMode tests can read them back).</summary>
        public void Fill(RunState run) => Fill(run, default);

        public void Fill(RunState run, RunRecords.Result records)
        {
            _summaryHead.text = run.state == "won" ? "THE DEPTHS ARE QUIET" : "THE DEPTHS TAKE YOU";
            int rooms = 0, visited = 0;
            if (run.dungeon != null)
                for (int i = 0; i < run.dungeon.AllRooms.Count; i++) { rooms++; if (run.dungeon.AllRooms[i].Visited) visited++; }
            double hearts = run.dmgTaken / 2;
            _values[0].text = run.floor.ToString();
            _values[1].text = Clock(run.floorTicks / (double)Balance.TickHz);
            _values[2].text = Clock(run.ticks / (double)Balance.TickHz);
            _values[3].text = visited + " / " + rooms;
            _values[4].text = run.kills.ToString();
            _values[5].text = hearts.ToString("0.#") + (Math.Abs(hearts - 1) < 1e-9 ? " heart" : " hearts");
            _values[6].text = run.shots > 0 ? Mathf.RoundToInt(100f * run.hits / run.shots) + "%  (" + run.hits + "/" + run.shots + ")" : "no shots";
            _values[7].text = Weapons.All[Mathf.Clamp(run.player.weaponIdx, 0, Weapons.All.Length - 1)].Name;
            _values[8].text = Rng.Encode(run.rootSeed);
            _records[0].text = records.DeepestFloor.ToString();
            _records[1].text = records.BestRooms.ToString();
            _newFloor.style.display = records.NewFloor ? DisplayStyle.Flex : DisplayStyle.None;
            _newRooms.style.display = records.NewRooms ? DisplayStyle.Flex : DisplayStyle.None;
        }

        /// <summary>The floor heading over the room: FLOOR N struck in the area's accent, the area named beneath.</summary>
        public void SetBanner(int floor, float alpha)
        {
            if (alpha <= 0.001f) { Hide(_banner); _bannerAlpha = 0; return; }
            if (floor != _bannerFloor)
            {
                _bannerFloor = floor;
                Atlas.Free(_bannerTex);
                _bannerTex = Atlas.ToTexture(BannerArt(floor), "banner");
                _banner.style.backgroundImage = new StyleBackground(_bannerTex);
            }
            Show(_banner);
            float a = Mathf.Round(alpha * 64) / 64;
            if (a != _bannerAlpha) { _bannerAlpha = a; Opacity(_banner, a); }
        }

        public static Img BannerArt(int floor)
        {
            var r = HudLayout.Banner;
            int w = (int)r.width, h = (int)r.height;
            var g = Materials.WoodPlate(w, h);
            Materials.Inset(g, 8, 8, w - 16, 52, Hex.C("#140c06"));
            var area = AreaRules.AreaForFloor(floor);
            var pal = AreaPalette.For(area);
            LedgerType.Struck(g, "FLOOR " + floor, w / 2, 17, 5, Hex.C(pal.Accent), Hex.C("#000000"));
            // the area's name on a paper slip pinned under the heading
            int sw = w - 120, sx = (w - sw) / 2;
            g.Fill(sx + 2, 66, sw, 26, Hex.Rgba(0, 0, 0, 0.35f));
            g.Draw(LedgerArt.AgedPaper(sw, 24, 0xBA77u, 0.08f), sx, 64);
            string name = LedgerHud.AreaNames[(int)area].ToUpperInvariant().Replace("'", "");
            LedgerType.Struck(g, name, w / 2, 69, 2, LedgerArt.Ink, LedgerArt.Lift);
            return g;
        }

        /// <summary>The Warden's notice, faded in and out over its time (the JS drawBossWarning envelope).</summary>
        public void SetWarning(float alpha)
        {
            if (alpha <= 0.001f) { Hide(_warning); _warnAlpha = 0; return; }
            Show(_warning);
            float a = Mathf.Round(alpha * 64) / 64;
            if (a != _warnAlpha) { _warnAlpha = a; Opacity(_warning, a); }
        }

        public static float WarningAlpha(RunState run)
        {
            if (run.bossWarnT <= 0) return 0;
            float t = 1 - run.bossWarnT / (float)Balance.BossWarnTime;
            return Mathf.Clamp01(Mathf.Min(1, t * 7) * Mathf.Min(1, (1 - t) * 5));
        }

        public void Release()
        {
            foreach (var t in _owned) Atlas.Free(t);
            _owned.Clear();
            Atlas.Free(_bannerTex);
            _bannerTex = null;
            Atlas.Free(_pauseSeedTex);
            _pauseSeedTex = null;
        }
    }
}
