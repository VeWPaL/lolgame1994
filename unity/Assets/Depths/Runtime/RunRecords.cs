namespace Depths.Unity
{
    /// <summary>The player's records across runs (the JS's RECORDS block): the deepest floor reached and the most rooms... [h:RunRecords-1]</summary>
    public sealed class RunRecords
    {
        public const string FloorKey = "depths.records.floor", RoomsKey = "depths.records.rooms";
        public const string WinsKey = "depths.records.wins", BestWinKey = "depths.records.bestwin";
        readonly ISettingsStore _store;

        public RunRecords(ISettingsStore store) { _store = store; }

        public int DeepestFloor => (int)_store.GetFloat(FloorKey, 0);
        public int BestRooms => (int)_store.GetFloat(RoomsKey, 0);
        public int Wins => (int)_store.GetFloat(WinsKey, 0);
        /// <summary>The quickest win, in seconds; 0 when the run has never been won.</summary>
        public float BestWin => _store.GetFloat(BestWinKey, 0);

        /// <summary>What a finished run did to the records: the bests after it, and which it beat.</summary>
        public readonly struct Result
        {
            public readonly int DeepestFloor, BestRooms, Wins;
            public readonly float BestWin;
            public readonly bool NewFloor, NewRooms, NewBestWin;
            public Result(int floor, int rooms, bool newFloor, bool newRooms) : this(floor, rooms, newFloor, newRooms, 0, 0, false) { }
            public Result(int floor, int rooms, bool newFloor, bool newRooms, int wins, float bestWin, bool newBestWin)
            {
                DeepestFloor = floor; BestRooms = rooms; NewFloor = newFloor; NewRooms = newRooms;
                Wins = wins; BestWin = bestWin; NewBestWin = newBestWin;
            }
        }

        /// <summary>Folds a finished run in. Only beating a record is new; matching it is not.</summary>
        public Result Record(int floor, int rooms, bool won = false, float seconds = 0)
        {
            int f = DeepestFloor, r = BestRooms, w = Wins;
            float best = BestWin;
            bool nf = floor > f, nr = rooms > r, nb = won && (best <= 0 || seconds < best);
            if (nf) _store.SetFloat(FloorKey, floor);
            if (nr) _store.SetFloat(RoomsKey, rooms);
            if (won) _store.SetFloat(WinsKey, ++w);
            if (nb) _store.SetFloat(BestWinKey, seconds);
            if (nf || nr || won) _store.Save();
            return new Result(nf ? floor : f, nr ? rooms : r, nf, nr, w, nb ? seconds : best, nb);
        }
    }
}
