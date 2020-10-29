using System;
using System.Collections.Generic;
using System.Text;

namespace IGSB
{
    public class SchemaInstrument
    {
        public enum enmType
        {
            formula,
            capture,
            transform
        }

        public enum enmUnit
        {
            ticks,
            seconds,
            none
        }

        public enum enmDataType
        {
            @double6,
            @double,
            @int,
            @string,
            @long
        }

        public bool IsNewRecordEvent { get; set; }

        public bool IsColumn { get; set; }

        public bool IsSignal { get; set; }

        public bool IsFuture { get; set; }

        public string CacheKey { get; set; }

        public string Key { get; set; }

        public string Value { get; set; }

        public string Name { get; set; }

        public string Transform { get; set; }

        public enmType Type { get; set; }

        public enmDataType DataType { get; set; }

        public enmUnit Unit { get; set; }

        public int UnitValue { get; set; }

        public Dictionary<string, string> Settings { get; set; }
    }
}
