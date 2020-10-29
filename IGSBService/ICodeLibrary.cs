using System.Collections.Generic;
using static IGSB.BaseCodeLibrary;
using static IGSB.WatchFile;

namespace IGSB
{
    public interface ICodeLibrary
    {
        void Initialise(Schema schema);

        public bool CheckKeyExists(string name, string field);

        bool Push(long timeStamp, string name, string field, string value);

        public List<RecordInstrument> Record { get; set; }

        void Reset();
    }
}
