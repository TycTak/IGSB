using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Timers;
using static IGSB.IGClient;
using static IGSB.SchemaInstrument;
using static IGSB.WatchFile;

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
        private List<SchemaInstrument> schemaCaptures;
        private List<SchemaInstrument> columns;
        private Timer timer;
        private enmUnit unit;
        private double unitValue;

        private BaseFormulaLibrary formulaLib = new BaseFormulaLibrary();

        private string CompletedDefaultValue;
        static readonly object lockObject = new object();
        private string isNewRecordEventColumnName;
        private string isNewRecordEventKeyName;

        private enum enmActionType
        {
            Push,
            TimedEvent
        }

        static public event Message M;
        static public event Response R;
        static public event Beep B;

        public List<RecordInstrument> Record { get; set; } = new List<RecordInstrument>();
        public static string Key { get; private set; }

        public void Initialise(Schema schema)
        {
            if (timer != null && timer.Enabled) timer.Stop();

            this.instruments = schema.SchemaInstruments;
            this.schemaName = schema.SchemaName;
            this.settings = schema.Settings;
            this.unit = schema.Unit;
            this.unitValue = schema.UnitValue;
            this.schemaFormulas = schema.SchemaInstruments.FindAll(x => x.Type == SchemaInstrument.enmType.formula);
            this.schemaCaptures = schema.SchemaInstruments.FindAll(x => x.Type == SchemaInstrument.enmType.capture);
            this.columns = schema.SchemaInstruments.FindAll(x => x.Key != "completed" && !x.IsFuture);
            this.CompletedDefaultValue = schema.SchemaInstruments.Find(x => x.Key == "completed").Value;
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

            if (this.unit == enmUnit.seconds && this.unitValue > 0) SetTimer(this.unitValue);
        }

        private void SetTimer(double seconds)
        {
            var milliseconds = (1000 * seconds);

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
                    //TODO do all instruments???
                    var schemaInstruments = instrumentKeyed[this.isNewRecordEventKeyName];
                    CreateNewRecord(enmActionType.TimedEvent, 0, schemaInstruments, null);

                    //Console.WriteLine("The Elapsed event was raised at {0:HH:mm:ss.fff}", e.SignalTime);
                }
            }
        }

        private RecordInstrument NewRecord()
        {
            var valueInstrument = new RecordInstrument() { Values = new Dictionary<string, string>() };

            foreach (var instrument in this.instruments)
            {
                valueInstrument.Values.Add(instrument.Key, null);
                if (instrument.Key == "completed") valueInstrument.Values["completed"] = CompletedDefaultValue;
            }

            return valueInstrument;
        }

        static private bool IsNumeric(string value)
        {
            return double.TryParse(value, out _);
        }

        static public string GetDatasetRecord(RecordInstrument record, List<SchemaInstrument> instruments, bool includeAllColumns, bool includePrediction)
        {
            var message = string.Empty;
            var predictValues = new Dictionary<string, string>();

            message += $"{record.Time},{record.TimeDiff}";

            foreach (var value in record.Values)
            {
                var instrument = instruments.Find(x => x.Key.Equals(value.Key));

                if (instrument != null && (((instrument.IsColumn || includeAllColumns) && !includePrediction) || (!instrument.IsSignal && includePrediction && instrument.IsColumn)))
                {
                    message += (string.IsNullOrEmpty(message) ? "" : ",");

                    if (value.Value == null && !instrument.IsFuture)
                        message += string.Empty;
                    if (value.Value == null && instrument.IsFuture)
                        message += "-1";
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
                var tempRecord = new RecordInstrument() { Key = record.Key, Time = record.Time, Values = predictValues };

                var prediction = IGClient.ML.Predict(tempRecord);

                if (prediction != null) message += (string.IsNullOrEmpty(message) ? "" : ", ") + $"p={string.Format("{0:0.000}", prediction.Label)}";
            }

            return message;
        }

        private double TempTimeValue = 0d;

        public bool CheckKeyExists(string name, string field)
        {
            var key = $"{name}_{field}";
            return instrumentKeyed.ContainsKey(key);
        }

        public bool Push(long timeStamp, string name, string field, string value)
        {
            lock (lockObject)
            {
                var key = $"{name}_{field}";
                var schemaInstruments = instrumentKeyed[key];

                if (Record.Count > 0)
                {
                    CreateNewRecord(enmActionType.Push, timeStamp, schemaInstruments, value);
                }
            }

            return true;
        }

        private void CreateNewRecord(enmActionType actionType, long timeStamp, List<SchemaInstrument> schemaInstruments, string value)
        {
            var currentRecord = Record.Last();
            var milliseconds = (timeStamp <= 0 ? DateTimeOffset.Now.ToUnixTimeMilliseconds() : timeStamp);

            var previousRecord = Record.ElementAt(Record.Count == 1 ? 0 : Record.Count - 2);
            currentRecord.TimeDiff = (previousRecord.Time == 0 ? 0 : milliseconds - previousRecord.Time);
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
                formulaLib.ExecuteMethod(formula, Record);
            }

            var isNewRecord = false;
            var isCorrect = false;

            if (this.unit == enmUnit.ticks)
            {
                isNewRecord = schemaInstruments.Exists(x => x.Key == isNewRecordEventColumnName);

                TempTimeValue += 1;
                if (TempTimeValue >= this.unitValue)
                {
                    isCorrect = true;
                    TempTimeValue = 0;
                }
            }
            else if (this.unit == enmUnit.seconds)
            {
                isNewRecord = actionType == enmActionType.TimedEvent;
                isCorrect = true;

                var temp = DateTimeOffset.Now.ToUnixTimeSeconds();
                if ((temp - TempTimeValue) > this.unitValue || timeStamp != 0)
                {
                    isCorrect = true;
                    TempTimeValue = DateTimeOffset.Now.ToUnixTimeSeconds();
                }
            }

            if (isNewRecord && isCorrect)
            {
                foreach (var instrument in transformInstruments)
                {
                    formulaLib.TransformData(instrument, instruments, Record);
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
                        //Console.Write($"\r{currentRecord.Values["offer"]}");
                    }
                }

                if (Record.Count > 0)
                {
                    var tempValues = new Dictionary<string, string>();
                    foreach(var instrument in schemaCaptures)
                    {
                        tempValues.Add(instrument.Key, currentRecord.Values[instrument.Key]);
                    }

                    currentRecord = NewRecord();

                    foreach(var tempValue in tempValues)
                    {
                        currentRecord.Values[tempValue.Key] = tempValue.Value;
                    }

                    Record.Add(currentRecord);
                }
            }
        }

        public void Reset()
        {
            Record.Clear();
            Record.Add(NewRecord());
        }
    }
}
