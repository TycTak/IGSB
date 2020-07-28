using Akka.Util.Internal;
using Google.Protobuf.WellKnownTypes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Security.Cryptography.X509Certificates;
using static IGSB.BaseCodeLibrary;
using static IGSB.WatchFile;
using static IGSB.WatchFile.SchemaInstrument;

//https://www.investopedia.com/terms/t/technical-analysis-of-stocks-and-trends.asp

namespace IGSB
{
    class BaseFormulaLibrary
    {
        private class LocalFormula
        {
            public class ItemInfo
            {
                public ItemInfo(string fieldName, int position)
                {
                    FieldName = fieldName;
                    Position = position;
                }

                public string FieldName { get; set; }
                public int Position { get; set; }
            }
            public string Key { get; set; }
            public enmUnit Unit { get; set; }
            public Dictionary<string, List<string>> Transform { get; set; }
            public string Value { get; set; }
            public ValueInstrument Target { get; set; }
            public List<LocalFormula.ItemInfo> Items { get; set; }
            public enmValueType ValueType { get; set; }
            public int Range { get; set; }
            public int Min { get; set; }
            public int Max { get; set; }
            public enmType Type { get; set; }
            public enmDataType DataType { get; set; }

            public Dictionary<string, string> Settings { get; set; }
        }

        private Dictionary<string, LocalFormula> CachedFormula { get; set; } = new Dictionary<string, LocalFormula>();

        private enum enmValueType
        {
            single,
            scale,
            range
        }

        private int GetRange(ValueInstrument target, List<ValueInstrument> values, enmUnit unit, int position, int max)
        {
            var retval = -1;

            if (unit == enmUnit.seconds)
            {
                var fromMilliseconds = DateTimeOffset.Now.ToUnixTimeMilliseconds() - (Math.Abs(position) * 1000);
                var toMilliseconds = DateTimeOffset.Now.ToUnixTimeMilliseconds() - (Math.Abs(position) * 1000);
                retval = values.FindLastIndex(x => x.Time < fromMilliseconds);
            }
            else if (unit == enmUnit.ticks)
            {
                retval = (values.Count - max + position - 1);
            }

            return retval;
        }

        private List<int> GetSelected(List<ValueInstrument> values, LocalFormula localFormula)
        {
            var retval = new List<int>();

            if (localFormula.Type == enmType.formula && values.Count >= localFormula.Range && values.Count > 0 && localFormula.Items.Count > 0)
            {
                var targetPosition = (values.Count - localFormula.Max + localFormula.Items[0].Position - 1);
                retval.Add(targetPosition);

                for (var i = 1; i < localFormula.Items.Count; i++)
                {
                    var row = localFormula.Items[i];

                    if (localFormula.Unit == enmUnit.ticks)
                        retval.Add(values.Count - localFormula.Max + row.Position - 1);
                    else if (localFormula.ValueType == enmValueType.single || localFormula.ValueType == enmValueType.scale)
                    {
                        var milliseconds = DateTimeOffset.Now.ToUnixTimeMilliseconds() - (Math.Abs(row.Position) * 1000);
                        retval.Add(values.FindLastIndex(x => x.Time < milliseconds));
                    }
                    else if (localFormula.ValueType == enmValueType.range)
                    {
                        var fromMilliseconds = DateTimeOffset.Now.ToUnixTimeMilliseconds() - (Math.Abs(row.Position) * 1000);
                        var toMilliseconds = DateTimeOffset.Now.ToUnixTimeMilliseconds() - (Math.Abs(localFormula.Items[i + 1].Position) * 1000);

                        foreach (var valueInstrument in values.FindAll(x => x.Time <= fromMilliseconds && x.Time >= toMilliseconds)) {
                            retval.Add(values.IndexOf(valueInstrument));
                        }

                        break;
                    }
                }
            } else if (localFormula.Type == enmType.capture)
            {
                retval.Add(values.Count - 1);
            }
                
            return retval;
        }

        private LocalFormula GetUnit(SchemaInstrument formula)
        {
            var retval = new LocalFormula();

            if (CachedFormula.ContainsKey(formula.CacheKey))
                retval = CachedFormula[formula.CacheKey];
            else
            {
                retval.Key = formula.Key;

                retval.Items = new List<LocalFormula.ItemInfo>();
                retval.Value = formula.Value;
                retval.Unit = formula.Unit;
                retval.Type = formula.Type;
                retval.DataType = formula.DataType;

                int min = int.MaxValue;
                int max = int.MinValue;

                if (formula.Transform != null)
                {
                    var transforms = formula.Transform.Split(';');

                    for (var i = 0; i < transforms.Length; i++)
                    {
                        retval.Transform = new Dictionary<string, List<string>>();
                        var items = transforms[i].Split(',');
                        var args = new string[items.Length - 1];
                        Array.Copy(items, 1, args, 0, args.Length);

                        retval.Transform.Add(items[0], args.ToList<string>());
                    }
                }

                var fields = retval.Value.Split(';');

                if (formula.Type == enmType.formula)
                {
                    for (var i = 0; i < fields.Length; i++)
                    {
                        var item = fields[i].Split(',');
                        if (item.Length == 2)
                        {
                            var fieldName = item[0];

                            var selectedRows = item[1];
                            retval.ValueType = (selectedRows.Contains('>') ? enmValueType.range : (selectedRows.Contains('~') ? enmValueType.scale : enmValueType.single));

                            if (retval.ValueType == enmValueType.single)
                            {
                                var y = int.Parse(selectedRows);
                                min = Math.Min(min, y);
                                max = Math.Max(max, y);
                                retval.Items.Add(new LocalFormula.ItemInfo(fieldName, y));
                            }
                            else if (retval.ValueType == enmValueType.range && retval.Unit == enmUnit.ticks)
                            {
                                var range = selectedRows.Split(">");
                                var a = int.Parse(range[0]);
                                var b = int.Parse(range[1]);
                                if (a > b) (a, b) = (b, a);

                                for (var x = a; x <= b; x++)
                                {
                                    min = Math.Min(min, x);
                                    max = Math.Max(max, x);
                                    retval.Items.Add(new LocalFormula.ItemInfo(fieldName, x));
                                }
                            }
                            else if (retval.ValueType == enmValueType.range && retval.Unit == enmUnit.seconds)
                            {
                                var range = selectedRows.Split(">");
                                var a = int.Parse(range[0]);
                                var b = int.Parse(range[1]);
                                retval.Items.Add(new LocalFormula.ItemInfo(fieldName, a));
                                retval.Items.Add(new LocalFormula.ItemInfo(fieldName, b));
                            }
                            else
                            {
                                var range = selectedRows.Split("~");
                                foreach (var x in range)
                                {
                                    var y = int.Parse(x);
                                    min = Math.Min(min, y);
                                    max = Math.Max(max, y);
                                    retval.Items.Add(new LocalFormula.ItemInfo(fieldName, y));
                                }
                            }
                        }
                        else throw new Exception("Formula item in value element must have <field>,<position> combination");
                    }
                
                    retval.Range = max - min + 1;
                    retval.Min = min;
                    retval.Max = max;
                }

                CachedFormula.Add(formula.CacheKey, retval);
            }

            return retval;
        }

        private bool CheckValid(List<ValueInstrument> values, List<int> selectedIndex, LocalFormula localFormula)
        {
            var retval = true;

            for (var i = 0; i < selectedIndex.Count; i++)
            {
                if (string.IsNullOrEmpty(values[selectedIndex[i]].Values[localFormula.Items[i].FieldName]) && localFormula.Items[i].FieldName != localFormula.Key)
                {
                    retval = false;
                    break;
                }
            }

            return retval;
        }

        public void TransformData(SchemaInstrument formula, List<SchemaInstrument> instruments, List<ValueInstrument> values)
        {
            //if (!string.IsNullOrEmpty(formula.Transform))
            //{
                var localFormula = GetUnit(formula);
                var selected = GetSelected(values, localFormula);

                if (selected.Count > 0)
                {
                    var value = values[selected[0]].Values[formula.Key];

                    foreach (var transform in localFormula.Transform)
                    {
                        switch (transform.Key.ToLower())
                        {
                            case "normalise":
                                value = String.Format("{0:0.000000}", Math.Round((Convert.ToDouble(value) - Convert.ToDouble(transform.Value[0])) / (Convert.ToDouble(transform.Value[1]) - Convert.ToDouble(transform.Value[0])), 6));
                                break;
                            case "categorise":
                                var categories = new Dictionary<string, string>();
                                for (var i = 1; i < transform.Value.Count; i++)
                                {
                                    var splt = transform.Value[i].Split('=');
                                    categories.Add(splt[0], splt[1]);
                                }

                                if (categories.ContainsKey(value))
                                {
                                    value = categories[value];
                                }

                                break;
                            default:
                                break;
                        }

                        var transformKey = $"{formula.Key}+";
                        values[selected[0]].Values[transformKey] = value;
                    }
                }
            //}
        }

        //https://www.babypips.com/learn/forex/how-to-use-moving-average-envelopes
        public enum enmProcess {
            volatility,
            signal, // done
            getvalue, // done
            count, // done
            changedvalue, // done
            percentage, // done
            changedinterval, // done
            average, // done
            ema, // done
            rsi, // done
            macd, // done
            roc, // done
            bollinger,
            stochasticoscillator,
            standarddeviation,
            averagedirectionalindex
        }

        public void ExecuteMethod(SchemaInstrument formula, List<ValueInstrument> values)
        {
            var localFormula = GetUnit(formula);
            var selectedIndex = GetSelected(values, localFormula);
            var isCapture = (selectedIndex.Count >= 1);
            var isValid = CheckValid(values, selectedIndex, localFormula);

            if (isCapture && isValid)
            {
                var target = values[selectedIndex[0]];

                string newValue = "0";

                switch (System.Enum.Parse(typeof(enmProcess), formula.Name.ToLower()))
                {
                    case enmProcess.volatility:
                        if (selectedIndex.Count == 2)
                        {
                            newValue = (GetInt(1, localFormula, selectedIndex, values) + GetInt(2, localFormula, selectedIndex, values)).ToString();
                        }
                        break;
                    case enmProcess.signal:
                        if (selectedIndex.Count == 3)
                        {
                            var change = (GetDouble(1, localFormula, selectedIndex, values) - GetDouble(2, localFormula, selectedIndex, values));
                            var signalRange = (formula.Settings.ContainsKey("range") ? double.Parse(formula.Settings["range"]) : 5);

                            newValue = (change > signalRange ? $"1": (change < -signalRange ? $"-1" : "0"));
                        }
                        break;
                    case enmProcess.getvalue:
                        if (selectedIndex.Count == 2)
                        {
                            newValue = GetString(1, localFormula, selectedIndex, values);
                        }
                        break;
                    case enmProcess.count:
                        if (selectedIndex.Count >= 1)
                        {
                            var count = 0;
                            for (var i = 1; i <= (selectedIndex.Count - 1); ++i)
                            {
                                count++;
                            }

                            newValue = count.ToString();
                        }
                        break;
                    case enmProcess.changedvalue:
                        if (selectedIndex.Count == 3)
                        {
                            newValue = (GetDouble(1, localFormula, selectedIndex, values) - GetDouble(2, localFormula, selectedIndex, values)).ToString();
                        }
                        break;
                    case enmProcess.percentage:
                        if (selectedIndex.Count == 3)
                        {
                            var v1 = GetDouble(1, localFormula, selectedIndex, values);
                            var v2 = GetDouble(2, localFormula, selectedIndex, values);
                            var difference = (v1 - v2);
                            var percentage = difference / ((v1 + v2) / 2) * 100;
                            newValue = String.Format("{0:0.000}", percentage);
                        }
                        break;
                    case enmProcess.roc:
                        if (selectedIndex.Count == 3)
                        {
                            var v1 = GetDouble(1, localFormula, selectedIndex, values);
                            var v2 = GetDouble(2, localFormula, selectedIndex, values);
                            var roc = ((v1 / v2) - 1) * 100;
                            newValue = String.Format("{0:0.000}", roc);
                        }
                        break;
                    case enmProcess.changedinterval:
                        if (selectedIndex.Count == 3)
                        {
                            newValue = (GetValueInstrument(1, selectedIndex, values).Time - GetValueInstrument(2, selectedIndex, values).Time).ToString();
                        }
                        break;
                    case enmProcess.average:
                        if (selectedIndex.Count >= 3)
                        {
                            var sum = 0d;
                            for (var i = 1; i < selectedIndex.Count; i++)
                            {
                                sum += GetDouble(i, localFormula, selectedIndex, values);
                            }

                            var average = (sum / (selectedIndex.Count - 1));

                            newValue = String.Format("{0:0.00}", average);
                        }
                        break;
                    case enmProcess.ema:
                        if (selectedIndex.Count >= 3)
                        {
                            newValue = String.Format("{0:0.00}", Formula_EMA(formula, localFormula, selectedIndex, values));
                        }
                        break;
                    case enmProcess.macd:
                        if (selectedIndex.Count == 3)
                        {
                            var v1 = GetDouble(1, localFormula, selectedIndex, values);
                            var v2 = GetDouble(2, localFormula, selectedIndex, values);

                            newValue = (Convert.ToDouble(v1) - Convert.ToDouble(v2)).ToString();
                        }
                        break;
                    case enmProcess.rsi:
                        if (selectedIndex.Count >= 3)
                        {
                            var gain = 0d;
                            var loss = 0d;
                            var times = string.Empty;

                            for (var i = 2; i < selectedIndex.Count; i++)
                            {
                                var v1 = GetDouble(i, localFormula, selectedIndex, values);
                                var v2 = GetDouble(i - 1, localFormula, selectedIndex, values);

                                var diff = v2 - v1;

                                if (diff <= 0)
                                    loss += Math.Abs(diff);
                                else
                                    gain += diff;

                                times += "X";
                            }

                            var rsi = (100 - (100 / (1 + (gain / (selectedIndex.Count - 2)) / (loss / (selectedIndex.Count - 2)))));

                            newValue = String.Format("{0:0.00}", rsi);
                        }
                        break;
                    default:
                        break;
                }

                newValue = (newValue == "0" ? null : newValue);
                
                target.Values[localFormula.Items[0].FieldName] = newValue;
            }
        }

        private string GetString(int index, LocalFormula localFormula, List<int> selectedIndex, List<ValueInstrument> values)
        {
            var value = GetValueInstrument(index, selectedIndex, values);
            var item = localFormula.Items[index];

            return value.Values[item.FieldName];
        }

        private ValueInstrument GetValueInstrument(int index, List<int> selectedIndex, List<ValueInstrument> values)
        {
            return values[selectedIndex[index]];
        }

        private double GetDouble(int index, LocalFormula localFormula, List<int> selectedIndex, List<ValueInstrument> values)
        {
            var value = GetValueInstrument(index, selectedIndex, values);
            var item = localFormula.Items[index];

            return Double.Parse(value.Values[item.FieldName]);
        }

        private double GetInt(int index, LocalFormula localFormula, List<int> selectedIndex, List<ValueInstrument> values)
        {
            var value = GetValueInstrument(index, selectedIndex, values);
            var item = localFormula.Items[index];

            return Int32.Parse(value.Values[item.FieldName]);
        }

        private string Formula_EMA(SchemaInstrument formula, LocalFormula localFormula, List<int> selectedIndex, List<ValueInstrument> values)
        {
            var retval = string.Empty;
            var closing = GetDouble(selectedIndex.Count - 1, localFormula, selectedIndex, values);
            var previous = GetValueInstrument(selectedIndex.Count - 2, selectedIndex, values);
            var previousEma = Double.Parse(previous.Values[localFormula.Items[0].FieldName]);

            if (previousEma == 0)
            {
                var sum = 0d;
                for (var i = 1; i < selectedIndex.Count; i++)
                {
                    sum += GetDouble(i, localFormula, selectedIndex, values);
                }

                var avg = (sum / (selectedIndex.Count - 1));
                retval = (avg == 0 ? null : avg.ToString());
            }
            else
            {
                var multiplier = (2d / (selectedIndex.Count + 1));
                var ema = closing * multiplier + previousEma * (1 - multiplier);
                retval = (ema == 0 ? null : ema.ToString());
            }

            return retval;
        }
    }
}