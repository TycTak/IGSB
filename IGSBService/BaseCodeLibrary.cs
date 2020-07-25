﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using static IGSB.IGClient;
using static IGSB.WatchFile;
using static IGSB.WatchFile.SchemaInstrument;

namespace IGSB
{
    public class BaseCodeLibrary : ICodeLibrary
    {
        private Dictionary<string, List<SchemaInstrument>> instrumentKeyed;
        private Dictionary<string, string> settings;
        private List<SchemaInstrument> instruments;
        private List<SchemaInstrument> schemaFormulas;
        private List<SchemaInstrument> columns;

        private BaseFormulaLibrary formulaLib = new BaseFormulaLibrary();

        private string completedDefaultValue;
        static readonly object lockObject = new object();
        private string isNewRecordEventColumnName;

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

        public void Initialise(Dictionary<string, string> settings, List<SchemaInstrument> instruments)
        {
            this.instruments = instruments;
            this.settings = settings;
            this.instrumentKeyed = new Dictionary<string, List<SchemaInstrument>>();
            this.schemaFormulas = instruments.FindAll(x => x.Type == SchemaInstrument.enmType.formula);
            this.columns = instruments.FindAll(x => x.IsColumn);
            completedDefaultValue = instruments.Find(x => x.Key == "completed").Value;

            foreach (var instrument in instruments.FindAll(x => x.Type == enmType.capture))
            {
                var key = $"{instrument.Name}_{instrument.Value}";
                if (!instrumentKeyed.ContainsKey(key))
                    instrumentKeyed.Add(key, new List<SchemaInstrument>() { instrument });
                else
                    instrumentKeyed[key].Add(instrument);
            }

            Reset();

            isNewRecordEventColumnName = instruments.Single<SchemaInstrument>(x => x.IsNewRecordEvent).Key;
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

                if (instrument != null && (((instrument.IsColumn || includeAllColumns) && !includePrediction) || (instrument.IsPredict && includePrediction && instrument.IsColumn)))
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

        public bool Push(string name, string field, string value)
        {
            lock (lockObject)
            {
                var key = $"{name}_{field}";
                var schemaInstruments = instrumentKeyed[key];

                if (Values.Count > 0)
                {
                    var currentRecord = Values.Last();

                    var milliseconds = DateTimeOffset.Now.ToUnixTimeMilliseconds();
                    currentRecord.Time = milliseconds;

                    foreach (var instrument in schemaInstruments)
                    {
                        currentRecord.Values[instrument.Key] = value;
                    }

                    foreach (var formula in schemaFormulas)
                    {
                        formulaLib.ExecuteMethod(formula, Values);
                    }

                    var isNewRecord = schemaInstruments.Exists(x => x.Key == isNewRecordEventColumnName);

                    if (isNewRecord)
                    {
                        foreach (var instrument in instruments)
                        {
                            formulaLib.TransformData(instrument, instruments, Values);
                        }

                        var regex = new Regex(Regex.Escape("X"));

                        foreach (var column in columns.Where(x => !x.IsFuture))
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

                        if (IGClient.StreamDisplay != enmContinuousDisplay.None && IGClient.StreamDisplay != enmContinuousDisplay.Subscription)
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
            }

            return true;
        }

        public void Reset()
        {
            Values.Clear();
            Values.Add(NewRecord());
        }
    }
}
