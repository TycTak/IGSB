using System.Collections.Generic;

namespace IGSB
{

    public class RecordInstrument
    {
        public string Key { get; set; }

        public long Time { get; set; }

        public long TimeDiff { get; set; }

        public Dictionary<string, string> Values { get; set; }
    }
}
