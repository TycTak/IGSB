using System.Collections.Generic;

namespace IGSBShared
{
    public class RecordInstrument
    {
        public long Time { get; set; }

        public long TimeDiff { get; set; }

        public Dictionary<string, string> Values { get; set; }
    }
}
