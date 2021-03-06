﻿using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using System.Reflection;
using static IGSB.BaseFormulaLibrary;
using System.Linq;
using static IGSB.IGClient;
using static IGSB.SchemaInstrument;
using Akka.Dispatch.SysMsg;
using static IGSBShared.Delegates;

namespace IGSB
{
    public class WatchFile
    {
        static public event Message M;

        public class Schema
        {
            public ICodeLibrary CodeLibrary { get; set; }

            public Dictionary<string, string> Settings { get; set; }

            public List<SchemaInstrument> SchemaInstruments { get; set; }

            public string SchemaId { get; set; }

            public string SchemaName { get; set; }

            public enmUnit Unit { get; set; }

            public double UnitValue { get; set; }

            public bool IsActive { get; set; }
        }

        public string WatchFileUri { get; set; }

        public Dictionary<string, string> Alias { get; set; }

        private string Substitute(string source)
        {
            var retval = source;

            foreach (var x in Alias)
            {
                retval = retval.Replace($"{{{x.Key}}}", x.Value);
            }

            return retval;
        }

        private bool IsValid(JObject json, string key)
        {
            return (json.ContainsKey(key) && !string.IsNullOrEmpty(json[key].ToString()));
        }

        public WatchFile(string watchFileUri, JObject parseObject)
        {
            try
            {
                WatchFileUri = watchFileUri;
                Schemas = new List<Schema>();
                Alias = new Dictionary<string, string>();
                MergeFieldList = new List<string>();
                MergeCaptureList = new List<string>();
                DistinctFieldList = new List<string>();
                DistinctCaptureList = new List<string>();
                ChartFieldList = new List<string>();
                ChartCaptureList = new List<string>();

                WatchFileId = parseObject["watchfileid"].ToString();
                WatchName = parseObject["watchname"].ToString();
                Currency = parseObject["currency"].ToString().ToUpper();

                var assembly = Assembly.GetExecutingAssembly();

                if (parseObject.ContainsKey("alias"))
                {
                    foreach (JObject alias in parseObject["alias"])
                    {
                        if (!IsValid(alias, "alias") || !IsValid(alias, "name")) throw new Exception("Alias or Name missing from watch file settings");
                        Alias.Add(alias["alias"].ToString(), alias["name"].ToString());
                    }
                }

                foreach (var alias in Alias.ToArray())
                {
                    Alias[alias.Key] = Substitute(alias.Value);
                }

                foreach (JObject schema in parseObject["schemas"])
                {
                    Type codeType;
                    var watchSchema = new Schema();
                    
                    if (!schema.ContainsKey("codelibrary")) codeType = typeof(DefaultCodeLibrary);
                    else codeType = assembly.GetType(schema["codelibrary"].ToString());

                    watchSchema.SchemaId = Substitute(schema["schemaid"].ToString());
                    watchSchema.SchemaName = Substitute(schema["schemaname"].ToString());
                    watchSchema.Unit = (SchemaInstrument.enmUnit)Enum.Parse(typeof(SchemaInstrument.enmUnit), schema["unit"].ToString());
                    watchSchema.UnitValue = double.Parse(schema["unitvalue"].ToString());
                    watchSchema.IsActive = (schema.ContainsKey("isactive") ? bool.Parse(schema["isactive"].ToString()) : false);
                    watchSchema.SchemaInstruments = new List<SchemaInstrument>();

                    watchSchema.CodeLibrary = (ICodeLibrary)Activator.CreateInstance(codeType);

                    M(enmMessageType.Debug, String.Format("WatchList.cstor: CodeLibrary [{0}] loaded for {1}", codeType, watchSchema.SchemaId));

                    watchSchema.Settings = new Dictionary<string, string>();

                    if (schema.ContainsKey("settings"))
                    {
                        foreach (JObject setting in schema["settings"])
                        {
                            if (!IsValid(setting, "key") || !IsValid(setting, "value")) throw new Exception("Key or Value missing from watch file settings");
                            watchSchema.Settings.Add(setting["key"].ToString(), Substitute(setting["value"].ToString()));
                        }
                    }

                    // TODO NEED TO VALIDATE SCHEMA -->
                    // TODO NO RANGES with MINUS FIGURE ALLOWED
                    // TODO Should only be one ISPREDICT = TRUE setting
                    // TODO There must be at least one predict key
                    // TODO Alias must be 3 characters or more
                    // TODO Some methods do not allow minus values, see attributes
                    // TODO Some methods do not allow ranges, see attributes
                    // TODO If a field in the "value" element refers to a field that has not been collected then it is in the wrong order to capture or calculate
                    // TODO Check two instruments are not writing to same field
                    // TODO Key is not allowed with _ at the end, this is used to indicate a transformed field
                    // TODO Predict key must be the first column, now called isnewrecord
                    // TODO All formula items must reference rows <=0 i.e. minus figures or zero
                    // TODO Range values must be 2 items or more
                    foreach (JObject instrument in schema["instruments"])
                    {
                        var isActive = (instrument.ContainsKey("isactive") ? bool.Parse(instrument["isactive"].ToString()) : true);

                        if (isActive)
                        {
                            var schemaInstrument = new SchemaInstrument();
                            schemaInstrument.IsNewRecordEvent = (instrument.ContainsKey("isnewrecord") ? bool.Parse(instrument["isnewrecord"].ToString()) : false);
                            schemaInstrument.IsColumn = (instrument.ContainsKey("iscolumn") ? bool.Parse(instrument["iscolumn"].ToString()) : true);
                            schemaInstrument.IsSignal = (instrument.ContainsKey("issignal") ? bool.Parse(instrument["issignal"].ToString()) : false);
                            schemaInstrument.CacheKey = $"{watchSchema.SchemaId}_{instrument["key"]}";
                            schemaInstrument.Key = instrument["key"].ToString();
                            schemaInstrument.Value = Substitute(instrument["value"].ToString());
                            schemaInstrument.Name = Substitute(instrument["name"].ToString());
                            schemaInstrument.Transform = (instrument.ContainsKey("transform") ? Substitute(instrument["transform"].ToString()) : null);

                            schemaInstrument.Type = (instrument.ContainsKey("type") ? (SchemaInstrument.enmType)Enum.Parse(typeof(SchemaInstrument.enmType), instrument["type"].ToString()) : enmType.formula);
                            schemaInstrument.DataType = (instrument.ContainsKey("datatype") ? (SchemaInstrument.enmDataType)Enum.Parse(typeof(SchemaInstrument.enmDataType), instrument["datatype"].ToString()) : enmDataType.@double);
                            //schemaInstrument.Unit = (schemaInstrument.Type == enmType.capture ? enmUnit.none : unit); // (instrument.ContainsKey("unit") ? (SchemaInstrument.enmUnit)Enum.Parse(typeof(SchemaInstrument.enmUnit), instrument["unit"].ToString()) : (schemaInstrument.Type == enmType.capture ? enmUnit.none : SchemaInstrument.enmUnit.ticks));
                            //schemaInstrument.UnitValue = (schemaInstrument.Type == enmType.capture ? 0 : unitValue);

                            var isFuture = false;

                            if (schemaInstrument.Type == enmType.formula)
                            {
                                var splt = schemaInstrument.Value.Split(";")[0].Split(",");
                                isFuture = (Convert.ToDouble(splt[1]) < 0);
                            }

                            schemaInstrument.IsFuture = isFuture;
                            schemaInstrument.Settings = new Dictionary<string, string>();

                            if (instrument.ContainsKey("settings"))
                            {
                                foreach (JObject setting in instrument["settings"])
                                {
                                    schemaInstrument.Settings.Add(setting["key"].ToString(), Substitute(setting["value"].ToString()));
                                }
                            }

                            if (schemaInstrument.Type == SchemaInstrument.enmType.capture)
                            {
                                if (schemaInstrument.Name.EndsWith("TICK"))
                                {
                                    if (!DistinctCaptureList.Exists(x => x.Equals(schemaInstrument.Name))) DistinctCaptureList.Add(schemaInstrument.Name);
                                    if (!DistinctFieldList.Exists(x => x.Equals(schemaInstrument.Value))) DistinctFieldList.Add(schemaInstrument.Value);
                                }
                                else if (schemaInstrument.Name.StartsWith("CHART"))
                                {
                                    if (!ChartCaptureList.Exists(x => x.Equals(schemaInstrument.Name))) ChartCaptureList.Add(schemaInstrument.Name);
                                    if (!ChartFieldList.Exists(x => x.Equals(schemaInstrument.Value))) ChartFieldList.Add(schemaInstrument.Value);
                                }
                                else
                                {
                                    if (!MergeCaptureList.Exists(x => x.Equals(schemaInstrument.Name))) MergeCaptureList.Add(schemaInstrument.Name);
                                    if (!MergeFieldList.Exists(x => x.Equals(schemaInstrument.Value))) MergeFieldList.Add(schemaInstrument.Value);
                                }
                            }

                            if (!string.IsNullOrEmpty(schemaInstrument.Transform))
                            {
                                var transform = schemaInstrument.Transform.Split(',');

                                var foundDataType = Enum.IsDefined(typeof(enmDataType), transform[transform.Length - 1]);
                                var dataType = (foundDataType ? (enmDataType)Enum.Parse(typeof(enmDataType), transform[transform.Length - 1]) : schemaInstrument.DataType);

                                SchemaInstrument transformInstrument = new SchemaInstrument()
                                {
                                    IsColumn = schemaInstrument.IsColumn,
                                    DataType = dataType,
                                    Key = $"{schemaInstrument.Key}_t",
                                    IsSignal = schemaInstrument.IsSignal,
                                    Settings = schemaInstrument.Settings,
                                    Type = SchemaInstrument.enmType.transform
                                };

                                schemaInstrument.IsSignal = false;
                                schemaInstrument.IsColumn = false;
                                schemaInstrument.Transform = (foundDataType ? string.Join(',', transform, 0, transform.Length - 1) : schemaInstrument.Transform);

                                watchSchema.SchemaInstruments.Add(transformInstrument);
                            }

                            watchSchema.SchemaInstruments.Add(schemaInstrument);
                        }
                    }

                    SchemaInstrument completedInstrument = new SchemaInstrument()
                    {
                        IsColumn = false,
                        DataType = enmDataType.@string,
                        Key = "completed",
                        IsSignal = false,
                        Settings = new Dictionary<string, string>(),
                        Type = SchemaInstrument.enmType.transform,
                        Value = new String('X', watchSchema.SchemaInstruments.Where(x => (x.Type == enmType.transform || x.Type == enmType.capture || x.Type == enmType.formula) && !x.IsFuture).Count())
                    };

                    watchSchema.SchemaInstruments.Add(completedInstrument);
                    watchSchema.CodeLibrary.Initialise(watchSchema);

                    Schemas.Add(watchSchema);
                }

                Loaded = DateTime.Now;
                CheckSum = GetCheckSum(parseObject.ToString());

                MergeFieldList.Sort();
                MergeCaptureList.Sort();
            } catch (Exception ex)
            {
                M(enmMessageType.Error, $"WatchList.cstor ERROR: Please check your watch file format [{ex.Message}]");
                throw ex;
            }
        }

        private string GetCheckSum(string message)
        {
            var sha1 = System.Security.Cryptography.SHA1.Create();
            byte[] buf = System.Text.Encoding.UTF8.GetBytes(message);
            byte[] hash = sha1.ComputeHash(buf, 0, buf.Length);
            return System.BitConverter.ToString(hash).Replace("-", "");
        }

        public List<Schema> Schemas { get; set; }

        public string WatchFileId { get; set; }

        public string WatchName { get; set; }

        public DateTime Loaded { get; set; }

        public String CheckSum { get; set; }

        public List<Schema> Items { get; set; }

        public List<string> MergeFieldList { get; set; }

        public List<string> MergeCaptureList { get; set; }

        public List<string> ChartFieldList { get; set; }

        public List<string> ChartCaptureList { get; set; }

        public List<string> DistinctFieldList { get; set; }

        public List<string> DistinctCaptureList { get; set; }

        public string Currency { get; set; }
    }
}
