using System.Collections.Generic;
using static IGSB.BaseCodeLibrary;
using static IGSB.WatchFile;

namespace IGSB
{
    public interface ICodeLibrary
    {
        void Initialise(Schema schema);

        bool Push(string name, string field, string value);

        public List<ValueInstrument> Values { get; set; }

        void Reset();
    }
}
