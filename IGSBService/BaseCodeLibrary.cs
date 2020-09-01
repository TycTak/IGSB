using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Timers;
using static IGSB.IGClient;
using static IGSB.WatchFile;
using static IGSB.WatchFile.SchemaInstrument;

namespace IGSB
{
    public class BaseCodeLibrary : ICodeLibrary
    {
        private Dictionary<string, List<SchemaInstrument>> instrumentKeyed;
        private Dictionary<string, string> settings;
        private string schemaName;
        private List<SchemaInstrument> instruments;
        private List<SchemaInstrument> transformInstruments;
        private List<SchemaInstrument> schemaFormulas;
        private List<SchemaInstrument> columns;
        private Timer timer;
        private enmUnit unit;
        private int unitValue;

        private BaseFormulaLibrary formulaLib = new BaseFormulaLibrary();

        private string completedDefaultValue;
        static readonly object lockObject = new object();
        private string isNewRecordEventColumnName;
        private string isNewRecordEventKeyName;

        static public event Message M;
        static public event Beep B;

        public List<ValueInstrument> Values { get; set; } = new List<ValueInstrument>();
        public static string Key { get; private set; }

        public class ValueInstrument
        {
            public string Key { get; set; }

            public long Time { get; set; }

            public Dictionary<string, string> Values { get; set; }
        }

        public void Initialise(Schema schema)
        {
            if (timer != null && timer.Enabled) timer.Stop();

            this.instruments = schema.SchemaInstruments;
            this.schemaName = schema.SchemaName;
            this.settings = schema.Settings;
            this.unit = schema.Unit;
            this.unitValue = schema.UnitValue;
            this.schemaFormulas = schema.SchemaInstruments.FindAll(x => x.Type == SchemaInstrument.enmType.formula);
            this.columns = schema.SchemaInstruments.FindAll(x => x.Key != "completed" && !x.IsFuture);
            this.completedDefaultValue = schema.SchemaInstruments.Find(x => x.Key == "completed").Value;
            this.transformInstruments = schema.SchemaInstruments.FindAll(x => !string.IsNullOrEmpty(x.Transform));
            var temp = schema.SchemaInstruments.Single<SchemaInstrument>(x => x.IsNewRecordEvent);
            this.isNewRecordEventColumnName = temp.Key;
            this.isNewRecordEventKeyName = $"{temp.Name}_{temp.Value}";
            this.instrumentKeyed = new Dictionary<string, List<SchemaInstrument>>();

            foreach (var instrument in schema.SchemaInstruments.FindAll(x => x.Type == enmType.capture))
            {
                var key = $"{instrument.Name}_{instrument.Value}";
                if (!this.instrumentKeyed.ContainsKey(key))
                    this.instrumentKeyed.Add(key, new List<SchemaInstrument>() { instrument });
                else
                    this.instrumentKeyed[key].Add(instrument);
            }

            Reset();

            if (this.unit == enmUnit.seconds && this.unitValue >= 1) SetTimer(this.unitValue);
        }

        private void SetTimer(int seconds)
        {
            var milliseconds = (seconds * 1000);

            timer = new System.Timers.Timer(milliseconds);

            timer.Elapsed += OnTimedEvent;
            timer.AutoReset = true;
            timer.Enabled = true;
        }

        private void OnTimedEvent(Object source, ElapsedEventArgs e)
        {
            lock (lockObject)
            {
                if (!IGClient.Pause)
                {
                    //var temp = schemaInstruments.FindAll(x => x.Key == isNewRecordEventColumnName);
                    //var key = $"{name}_{field}";
                    var schemaInstruments = instrumentKeyed[this.isNewRecordEventKeyName];
                    CreateNewRecord(schemaInstruments, null);
                }
            }
            //Console.WriteLine("The Elapsed event was raised at {0:HH:mm:ss.fff}", e.SignalTime);
        }

        private ValueInstrument NewRecord()
        {
            var valueInstrument = new ValueInstrument() { Values = new Dictionary<string, string>() };

            foreach (var instrument in this.instruments)
            {
                valueInstrument.Values.Add(instrument.Key, null);
                if (instrument.Key == "completed") valueInstrument.Values["completed"] = completedDefaultValue;
            }

            return valueInstrument;
        }

        static private bool IsNumeric(string value)
        {
            return double.TryParse(value, out _);
        }

        static public string GetDatasetRecord(ValueInstrument record, List<SchemaInstrument> instruments, bool includeAllColumns, bool includePrediction)
        {
            var message = string.Empty;
            var predictValues = new Dictionary<string, string>();

            foreach (var value in record.Values)
            {
                var instrument = instruments.Find(x => x.Key.Equals(value.Key));

                if (instrument != null && (((instrument.IsColumn || includeAllColumns) && !includePrediction) || (!instrument.IsSignal && includePrediction && instrument.IsColumn)))
                {
                    message += (string.IsNullOrEmpty(message) ? "" : ", ");

                    if (value.Value == null)
                        message += string.Empty;
                    else if (instrument.DataType == enmDataType.@double)
                        message += (IsNumeric(value.Value) ? $"{string.Format("{0:0.00}", double.Parse(value.Value))}" : "0.00");
                    else if (instrument.DataType == enmDataType.@double6)
                        message += (IsNumeric(value.Value) ? $"{string.Format("{0:0.000000}", double.Parse(value.Value))}" : "0.000000");
                    else if (instrument.DataType == enmDataType.@int)
                        message += (IsNumeric(value.Value) ? $"{string.Format("{0:0}", int.Parse(value.Value))}" : "0");
                    else if (instrument.DataType == enmDataType.@long)
                        message += (IsNumeric(value.Value) ? $"{string.Format("{0:0}", long.Parse(value.Value))}" : "0");
                    else if (instrument.DataType == enmDataType.@string)
                        message += $"\"{value.Value}\"";

                    if (IGClient.ML.CurrentMetric != null && includePrediction && IGClient.ML.CurrentMetric.Columns.Where(x => x.EndsWith(value.Key)).Count() > 0)
                    {
                        predictValues.Add(value.Key, value.Value);
                    }
                }
            }

            if (includePrediction && predictValues.Count > 0 && !record.Values["completed"].Contains("X"))
            {
                var tempRecord = new ValueInstrument() { Key = record.Key, Time = record.Time, Values = predictValues };

                var prediction = IGClient.ML.Predict(tempRecord);

                //TODO Beep when a buy or sell signal is found
                //B(enmBeep.OneShort);

                if (prediction != null) message += (string.IsNullOrEmpty(message) ? "" : ", ") + $"p={string.Format("{0:0.000}", prediction.Label)}";
            }

            return message;
        }

        private double tempValue = 0d;

        public bool Push(string name, string field, string value)
        {
            lock (lockObject)
            {
                var key = $"{name}_{field}";
                var schemaInstruments = instrumentKeyed[key];

                if (Values.Count > 0)
                {
                    CreateNewRecord(schemaInstruments, value);
                }
            }

            return true;
        }

        private void CreateNewRecord(List<SchemaInstrument> schemaInstruments, string value)
        {
            var currentRecord = Values.Last();

            var milliseconds = DateTimeOffset.Now.ToUnixTimeMilliseconds();
            currentRecord.Time = milliseconds;

            if (!string.IsNullOrEmpty(value))
            {
                foreach (var instrument in schemaInstruments)
                {
                    currentRecord.Values[instrument.Key] = value;
                }
            }

            foreach (var formula in schemaFormulas)
            {
                formulaLib.ExecuteMethod(formula, Values);
            }

            var isNewRecord = schemaInstruments.Exists(x => x.Key == isNewRecordEventColumnName);
            var isCorrect = false;

            if (this.unit == enmUnit.ticks)
            {
                tempValue += 1;
                if (tempValue == this.unitValue)
                {
                    isCorrect = true;
                    tempValue = 0;
                }
            }
            else if (this.unit == enmUnit.seconds)
            {
                var temp = DateTimeOffset.Now.ToUnixTimeSeconds();
                if ((temp - tempValue) > this.unitValue)
                {
                    isCorrect = true;
                    tempValue = DateTimeOffset.Now.ToUnixTimeSeconds();
                }
            }

            if (isNewRecord && isCorrect)
            {
                foreach (var instrument in transformInstruments)
                {
                    formulaLib.TransformData(instrument, instruments, Values);
                }

                var regex = new Regex(Regex.Escape("X"));

                foreach (var column in columns)
                {
                    var temp = currentRecord.Values[column.Key];

                    if (string.IsNullOrEmpty(temp))
                    {
                        if (column.DataType == SchemaInstrument.enmDataType.@string)
                            currentRecord.Values[column.Key] = "-";
                        else
                            currentRecord.Values[column.Key] = "0";
                    }
                    else
                    {
                        currentRecord.Values["completed"] = regex.Replace(currentRecord.Values["completed"], "O", 1);
                    }
                }

                if (IGClient.StreamDisplay != enmContinuousDisplay.None && IGClient.StreamDisplay != enmContinuousDisplay.Subscription && IGClient.SchemaFilterName == schemaName)
                {
                    var message = BaseCodeLibrary.GetDatasetRecord(currentRecord, instruments, (IGClient.StreamDisplay == enmContinuousDisplay.DatasetAllColumns), (IGClient.StreamDisplay == enmContinuousDisplay.Prediction));

                    if (string.IsNullOrEmpty(Filter) || message.ToLower().Contains(Filter.ToLower()))
                    {
                        M(enmMessageType.Info, message);
                    }
                }

                currentRecord = NewRecord();
                Values.Add(currentRecord);
            }
        }

        public void Reset()
        {
            Values.Clear();
            Values.Add(NewRecord());
        }
    }
}
