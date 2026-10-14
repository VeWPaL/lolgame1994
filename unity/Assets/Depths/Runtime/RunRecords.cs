namespace Depths.Unity
{
    /// <summary>The player's records across runs (the JS's RECORDS block): the deepest floor reached and the most rooms... [h:RunRecords-1]</summary>
    public sealed class RunRecords
    {
        public const string FloorKey = "depths.records.floor", RoomsKey = "depths.records.rooms";
        readonly ISettingsStore _store;

        public RunRecords(ISettingsStore store) { _store = store; }

        public int DeepestFloor => (int)_store.GetFloat(FloorKey, 0);
        public int BestRooms => (int)_store.GetFloat(RoomsKey, 0);

        /// <summary>What a finished run did to the records: the bests after it, and which it beat.</summary>
        public readonly struct Result
        {
            public readonly int DeepestFloor, BestRooms;
            public readonly bool NewFloor, NewRooms;
            public Result(int floor, int rooms, bool newFloor, bool newRooms) { DeepestFloor = floor; BestRooms = rooms; NewFloor = newFloor; NewRooms = newRooms; }
        }

        /// <summary>Folds a finished run in. Only beating a record is new; matching it is not.</summary>
        public Result Record(int floor, int rooms)
        {
            int f = DeepestFloor, r = BestRooms;
            bool nf = floor > f, nr = rooms > r;
            if (nf) _store.SetFloat(FloorKey, floor);
            if (nr) _store.SetFloat(RoomsKey, rooms);
            if (nf || nr) _store.Save();
            return new Result(nf ? floor : f, nr ? rooms : r, nf, nr);
        }
    }
}
