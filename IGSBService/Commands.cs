using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using static IGSB.IGClient;

namespace IGSB
{
    public class Commands
    {
        static public event Message M;
        static public event Response R;
        static public event ConfirmText CT;
        static public event ConfirmChar CC;
        static public event TickTock TT;

        static private bool DataCheck(string value, string validation)
        {
            var retval = false;

            if (validation == "N")
            {
                retval = double.TryParse(value, out _);
            }
            else if (validation == "S")
            {
                retval = true;
            }
            else
            {
                var rgx = new Regex(validation);
                retval = rgx.IsMatch(value);
            }

            return retval;
        }

        static private bool Validate(string check, List<string> cmdArgs, bool displayError = true)
        {
            var retval = true;

            var valuesChecked = string.Empty;
            var splt = (!string.IsNullOrEmpty(check) ? check.Split(';') : new string[0]);

            for (var i = 0; i < splt.Length; i++)
            {
                var validation = string.Empty;
                var isMandatory = (splt[i].Substring(0, 1).Equals("M"));

                if (splt[i].Substring(1, 1) == "~")
                    validation = splt[i].Substring(2, splt[i].Length - 2);
                else
                    validation = splt[i].Substring(1, 1);

                if (isMandatory)
                {
                    if (cmdArgs.Count < (i + 1 + 1))
                    {
                        retval = false;
                        break;
                    }
                    else if (string.IsNullOrEmpty(cmdArgs[i + 1]))
                    {
                        retval = false;
                        break;
                    }
                    else if (!DataCheck(cmdArgs[i + 1], validation))
                    {
                        retval = false;
                        break;
                    }
                }
                else if (!isMandatory)
                {
                    if (cmdArgs.Count < (i + 1 + 1))
                    {
                        if (validation == "N")
                            cmdArgs.Add("0");
                        else
                            cmdArgs.Add("");
                    }
                    else if (!string.IsNullOrEmpty(cmdArgs[i + 1]))
                    {
                        if (!DataCheck(cmdArgs[i + 1], validation))
                        {
                            retval = false;
                            break;
                        }
                    }
                }
            }

            retval = (splt.Length == (cmdArgs.Count - 1) ? retval : false);

            if (!retval && displayError) R("INVALID_PARAMETERS");

            return retval;
        }

        private List<string> GetArgs(string command)
        {
            var args = command.Trim().Split(' ');

            if (args.Length > 0 && args[0].StartsWith("/") && args[0].Length > 2)
            {
                var cmds = "[/ty][/em][/ec][/bc][/se][/cs][/cl][/su][/ca][/cd][/ac][/dt][/sc][/ru][/fe]";

                for (var i = args[0].Length; i >= 2; i--)
                {
                    var check = args[0].Substring(0, i);
                    if (cmds.Contains($"[{check.ToLower()}]") || i == 2)
                    {
                        var len = (args[0].Length - (check.Length - 1));
                        var tmpArgs = new String[len + (args.Length - 1)];
                        Array.Copy(args, 1, tmpArgs, len, args.Length - 1);
                        tmpArgs[0] = args[0].Substring(0, check.Length);

                        for (var x = 1; x < len; x++)
                        {
                            tmpArgs[x] = args[0].Substring(check.Length + x - 1, 1);
                        }

                        args = tmpArgs;
                        break;
                    }
                }
            }
            else if (args.Length == 1 && args[0].StartsWith("$") && args[0].Length > 2)
            {
                args = new string[2];
                args[0] = "$";
                args[1] = command.Substring(1);
            }

            args = args.ToList<string>().FindAll(x => x.Length > 0).ToArray();

            return args.ToList<string>();
        }

        public bool CommandParse(string command)
        {
            var @continue = true;

            var cmdArgs = GetArgs(command);

            if (cmdArgs.Count > 0)
            {
                switch (cmdArgs[0].ToLower())
                {
                    case "":
                        break;
                    case "$-":
                        if (Validate("MS", cmdArgs))
                        {
                            var code = cmdArgs[1];
                            DeleteShortCode(IGClient.SettingsFile, code);
                        }
                        break;
                    case "$+":
                        if (Validate("MS;MS;OS;OS;OS;OS;OS", cmdArgs))
                        {
                            var code = cmdArgs[1];
                            if ("+-".Contains(cmdArgs[1]) && code.Length == 1)
                            {
                                R("RESERVED_SHORTCODE");
                            }
                            else
                            {
                                var cmd = string.Join(" ", cmdArgs.ToArray(), 2, cmdArgs.Count - 2).Trim();
                                SetShortCode(IGClient.SettingsFile, code, cmd);
                            }
                        }
                        break;
                    case "$":
                        if (Validate("", cmdArgs, false))
                        {
                            ListShortCodes(IGClient.SettingsFile);
                        }
                        else
                        {
                            var cmdList = GetShortCode(IGClient.SettingsFile, cmdArgs[1]);

                            if (!string.IsNullOrEmpty(cmdList))
                            {
                                if (cmdList.StartsWith("$"))
                                    R("CANNOT_EMBED_CODES");
                                else
                                {
                                    var cmds = cmdList.Split(";");

                                    foreach (var cmd in cmds)
                                    {
                                        M(enmMessageType.Highlight, $"$> {cmd}");
                                        if (!CommandParse(cmd)) break;
                                    }
                                }
                            }
                            else
                                R("INVALID_PARAMETERS");
                        }
                        break;
                    case "/fe":
                    case "feed":
                        if (Validate("MS;MS;ON", cmdArgs)) FeedDataset(cmdArgs[1], cmdArgs[2], int.Parse((cmdArgs[3].Equals("0") ? "100" : cmdArgs[3])));
                        break;
                    case "/dt":
                    case "date":
                        M(enmMessageType.Info, $"{DateTime.Now.ToString(LongDateFormat)}");
                        break;
                    case "/?":
                    case "help":
                        if (Validate("OS", cmdArgs)) Help(cmdArgs[1]);
                        break;
                    case "/ty":
                    case "type":
                        if (Validate("MS;ON;O~^-a|-r$;O~^-a|-r$", cmdArgs)) TypeOut(cmdArgs[1], 0, Convert.ToInt32(cmdArgs[2]), IsCode("-a", cmdArgs), IsCode("-r", cmdArgs));
                        break;
                    case "/r":
                    case "reload":
                        if (Validate("OS;OS", cmdArgs)) ReloadWatchFile(cmdArgs[1], cmdArgs[2]);
                        break;
                    case "/em":
                    case "empty":
                        if (Validate("MS", cmdArgs)) Empty(cmdArgs[1]);
                        break;
                    case "/ec":
                    case "end":
                        if (Validate("", cmdArgs)) IsCapture(true);
                        break;
                    case "/bc":
                    case "begin":
                        if (Validate("", cmdArgs)) IsCapture(false);
                        break;
                    case "/p":
                    case "passwd":
                        if (Validate("MS;MS", cmdArgs)) ChangePassword(null, null, cmdArgs[1], cmdArgs[2]);
                        break;
                    case "/i":
                    case "info":
                        if (Validate("ON", cmdArgs)) Transactions(int.Parse(cmdArgs[1]));
                        break;
                    case "/su":
                    case "summary":
                        if (Validate("", cmdArgs)) Summary();
                        break;
                    case "/o":
                    case "open":
                        if (Validate("", cmdArgs)) Open();
                        break;
                    case "/g":
                    case "get":
                        if (Validate("MS", cmdArgs)) GetDetails(cmdArgs[1]);
                        break;
                    case "/ca":
                    case "closeall":
                        Close(string.Empty);
                        break;
                    case "/cd":
                    case "close":
                        if (Validate("MS", cmdArgs)) Close(cmdArgs[1]);
                        break;
                    case "/s":
                    case "/b":
                    case "sell":
                    case "buy":
                        if (Validate("MS;MN;MS", cmdArgs, false))
                            Process(cmdArgs[0], cmdArgs[1], double.Parse(cmdArgs[2]), cmdArgs[3]);
                        else if (Validate("MS;MN", cmdArgs, false))
                            Process(cmdArgs[0], cmdArgs[1], double.Parse(cmdArgs[2]));
                        else if (Validate("MS", cmdArgs))
                            Process(cmdArgs[0], cmdArgs[1]);
                        break;
                    case ">":
                        if (!IGClient.Pause)
                        {
                            if (Validate("", cmdArgs))
                            {
                                IGClient.StreamDisplay = enmContinuousDisplay.Subscription;
                                R("CONTINUOUS_SUBSCRIPTION", new List<string> { (string.IsNullOrEmpty(IGClient.Filter) ? "--" : $"[{IGClient.Filter}]") });
                            }
                        }
                        else R("NEED_TO_RESTART");
                        break;
                    case ">>":
                        if (!IGClient.Pause)
                        {
                            if (Validate("MS;O~^-a$", cmdArgs))
                            {
                                var schema = GetSchema(cmdArgs[1]);

                                if (schema != null)
                                {
                                    var includeAllColumns = (cmdArgs[2] == "-a");

                                    IGClient.SchemaFilterName = schema.SchemaName;
                                    IGClient.StreamDisplay = (includeAllColumns ? enmContinuousDisplay.DatasetAllColumns : enmContinuousDisplay.Dataset);
                                    R("CONTINUOUS_DATASET", new List<string> { (string.IsNullOrEmpty(IGClient.Filter) ? "--" : $"[{IGClient.Filter}]") });
                                    M(enmMessageType.Info, GetHeader(schema, includeAllColumns));
                                }
                                else R("NO_SCHEMA");
                            }
                        }
                        else
                            R("NEED_TO_RESTART");
                        break;
                    case ">>>":
                        if (!IGClient.Pause)
                        {
                            if (Validate("MS", cmdArgs))
                            {
                                var schema = GetSchema(cmdArgs[1]);

                                if (schema != null)
                                {
                                    IGClient.StreamDisplay = enmContinuousDisplay.Prediction;
                                    R("CONTINUOUS_PREDICTION", new List<string> { (string.IsNullOrEmpty(IGClient.Filter) ? "--" : $"[{IGClient.Filter}]") });
                                    M(enmMessageType.Info, GetHeader(schema, false, true));
                                }
                                else R("NO_SCHEMA");
                            }
                        }
                        else
                            R("NEED_TO_RESTART");
                        break;
                    case "/f":
                    case "filter":
                        if (Validate("MS", cmdArgs, false))
                        {
                            IGClient.Filter = cmdArgs[1];
                            M(enmMessageType.Info, $"Filter set to [{IGClient.Filter}]");
                        }
                        else if (Validate("", cmdArgs))
                        {
                            IGClient.Filter = default(string);
                            M(enmMessageType.Info, $"Filter cleared");
                        }
                        break;
                    case "/se":
                    case "search":
                        if (Validate("MS", cmdArgs, false))
                        {
                            Search($"{cmdArgs[1]}");
                        }
                        break;
                    case "/ac":
                    case "accounts":
                        if (Validate("", cmdArgs, false))
                        {
                            Accounts();
                        }
                        break;
                    case "/x":
                    case "exit":
                        if (Validate("", cmdArgs)) if (CC("Are you sure (Y/n)? ", 'y')) @continue = false;
                        break;
                    case "/w":
                    case "watch":
                        if (Validate("", cmdArgs)) GetWatchList();
                        break;
                    case "/cs":
                    case "cls":
                        if (Validate("", cmdArgs)) Console.Clear();
                        break;
                    case "/sc":
                    case "schema":
                        if (Validate("", cmdArgs, false))
                        {
                            Schemas();
                        }
                        else
                        {
                            switch (cmdArgs[1].ToLower())
                            {
                                case "+":
                                case "+a":
                                    if (Validate("MS;MS", cmdArgs))
                                    {
                                        if (!GetSchema(cmdArgs[2]).IsActive)
                                            SchemaActivate($"{cmdArgs[2]}", true);
                                        else
                                            R("SCHEMA_ALREADY_ACTIVE");
                                    }
                                    break;
                                case "-":
                                case "-a":
                                    if (Validate("MS;MS", cmdArgs))
                                    {
                                        if (GetSchema(cmdArgs[2]).IsActive)
                                            SchemaActivate($"{cmdArgs[2]}", false);
                                        else
                                            R("SCHEMA_ALREADY_INACTIVE");
                                    }
                                    break;
                                default:
                                    R("UNKNOWN");
                                    break;
                            }
                        }
                        break;
                    case "/l":
                    case "log":
                        if (Validate("", cmdArgs, false))
                        {
                            LogFile();
                        }
                        else
                        {
                            switch (cmdArgs[1].ToLower())
                            {
                                case "i":
                                case "info":
                                    if (Validate("MS;OS", cmdArgs)) InfoLogFile($"{cmdArgs[2]}");
                                    break;
                                case "t":
                                case "type":
                                    if (Validate("MS;MN;MS", cmdArgs)) TypeLogFile(Convert.ToInt32(cmdArgs[2]), $"{cmdArgs[3]}");
                                    break;
                                case "d":
                                case "delete":
                                    if (Validate("MS;MS", cmdArgs)) DeleteLogFile($"{cmdArgs[2]}");
                                    break;
                                case "r":
                                case "rename":
                                    if (Validate("MS;MS;MS", cmdArgs)) RenameLogFile($"{cmdArgs[2]}", $"{cmdArgs[3]}");
                                    break;
                                case "c":
                                case "copy":
                                    if (Validate("MS;MS;MS", cmdArgs)) CopyLogFile($"{cmdArgs[2]}", $"{cmdArgs[3]}");
                                    break;
                                default:
                                    R("UNKNOWN");
                                    break;
                            }
                        }
                        break;
                    case "/a":
                    case "alias":
                        if (Validate("", cmdArgs, false))
                        {
                            ListAliases();
                        }
                        else
                        {
                            switch (cmdArgs[1].ToLower())
                            {
                                case "s":
                                case "set":
                                    if (Validate("MS;MS;MS;O~^-r$", cmdArgs)) SetAlias(IGClient.WatchFileName, $"{cmdArgs[2]}", $"{cmdArgs[3]}");
                                    break;
                                case "d":
                                case "delete":
                                    if (Validate("MS;MS", cmdArgs)) DeleteAlias(IGClient.WatchFileName, $"{cmdArgs[2]}");
                                    break;
                                default:
                                    R("UNKNOWN");
                                    break;
                            }
                        }
                        break;
                    case "/e":
                    case "eval":
                        if (Validate("MS;ON", cmdArgs)) IGClient.ML.EvaluateModel($"{cmdArgs[1]}", Convert.ToInt32(cmdArgs[2]));
                        break;
                    case "/t":
                    case "train":
                        if (Validate("MS;MS;OS", cmdArgs))
                            IGClient.ML.TrainModel($"{cmdArgs[1]}", $"{cmdArgs[2]}", cmdArgs[3]);
                        else
                        {
                            switch (cmdArgs[1].ToLower())
                            {
                                case "i":
                                case "info":
                                    //if (Validate("MS;MS", cmdArgs)) StatisticsModel($"{cmdArgs[2]}", $"{cmdArgs[3]}");
                                    break;
                                default:
                                    R("UNKNOWN");
                                    break;
                            }
                        }
                        break;
                    case "/ru":
                    case "run":
                        if (Validate("MS;OS", cmdArgs)) IGClient.ML.RunModel($"{cmdArgs[1]}");
                        break;
                    case "/m":
                    case "model":
                        if (Validate("", cmdArgs, false))
                            Model();
                        else
                        {
                            switch (cmdArgs[1].ToLower())
                            {
                                case "r":
                                case "rename":
                                    if (Validate("MS;MS;MS", cmdArgs)) RenameModel($"{cmdArgs[2]}", $"{cmdArgs[3]}");
                                    break;
                                case "i":
                                case "info":
                                    if (Validate("MS;OS", cmdArgs)) InfoModel(cmdArgs[2]);
                                    break;
                                case "c":
                                case "copy":
                                    if (Validate("MS;MS;MS", cmdArgs)) CopyModel($"{cmdArgs[2]}", $"{cmdArgs[3]}");
                                    break;
                                case "d":
                                case "delete":
                                    if (Validate("MS;MS", cmdArgs)) DeleteModel(cmdArgs[2]);
                                    break;
                                case "s":
                                case "save":
                                    if (Validate("MS;MS", cmdArgs)) IGClient.ML.SaveModel(cmdArgs[2]);
                                    break;
                                case "b":
                                case "bin":
                                    IGClient.ML.CloseModel();
                                    break;
                                case "l":
                                case "load":
                                    if (Validate("MS;MS", cmdArgs)) IGClient.ML.LoadModel(cmdArgs[2]);
                                    break;
                                default:
                                    R("UNKNOWN");
                                    break;
                            }
                        }

                        break;
                    case "/d":
                    case "dataset":
                        if (Validate("", cmdArgs, false))
                            DataSet();
                        else
                        {
                            switch (cmdArgs[1].ToLower())
                            {
                                case "h":
                                case "headings":
                                    if (Validate("MS;OS", cmdArgs)) HeadingsDataSet(cmdArgs[2]);
                                    break;
                                case "i":
                                case "info":
                                    if (Validate("MS;OS", cmdArgs)) InfoDataSet(cmdArgs[2]);
                                    break;
                                case "r":
                                case "rename":
                                    if (Validate("MS;MS;MS", cmdArgs)) RenameDataSet($"{cmdArgs[2]}", $"{cmdArgs[3]}");
                                    break;
                                case "c":
                                case "copy":
                                    if (Validate("MS;MS;MS", cmdArgs)) CopyDataSet($"{cmdArgs[2]}", $"{cmdArgs[3]}");
                                    break;
                                case "d":
                                case "delete":
                                    if (Validate("MS;MS", cmdArgs)) DeleteDataSet(cmdArgs[2]);
                                    break;
                                case "s":
                                case "save":
                                    if (Validate("MS;MS;O~^-d|-a|-r$;O~^-d|-a|-r$;O~^-d|-a|-r$", cmdArgs)) SaveDataSet(cmdArgs[2], IsCode("-d", cmdArgs), IsCode("-a", cmdArgs), IsCode("-r", cmdArgs));
                                    break;
                                default:
                                    R("UNKNOWN");
                                    break;
                            }
                        }

                        break;
                    case "/cl":
                    case "client":
                        if (Validate("", cmdArgs)) GetClientDetails();
                        break;
                    default:
                        Help(cmdArgs[0]);
                        R("UNKNOWN");
                        break;
                }
            }

            return @continue;
        }

        private bool IsCode(string code, List<string> values)
        {
            var found = false;
            code = code.ToLower();

            for(var i = 0; i < values.Count(); i++)
            {
                if (values.ElementAt(i).ToLower().Equals(code))
                {
                    found = true;
                    break;
                }
            }

            return found;
        }

        public bool Authenticate(string settingsFile, string sourceKey, string watchFile, string password)
        {
            bool retval = false;

            M(enmMessageType.Info, "Authenticating access");

            password = (string.IsNullOrEmpty(password) ? Password : password);

            if (CheckStrength(password) >= 4)
            {
                settingsFile = (string.IsNullOrEmpty(settingsFile) ? SettingsFile : settingsFile);
                sourceKey = (string.IsNullOrEmpty(sourceKey) ? SourceKey : sourceKey);
                watchFile = (string.IsNullOrEmpty(watchFile) ? WatchFileName : watchFile);

                var source = GetSource(settingsFile, sourceKey);

                if (source == null) M(enmMessageType.Exit, "No settings file or key found");

                var unencryptedToken = source["token"].ToString();

                if (unencryptedToken.Equals(Token) || string.IsNullOrEmpty(unencryptedToken))
                {
                    unencryptedToken = SaveSource(settingsFile, password, source);
                }

                var token = EncryptionHelper.Decrypt(unencryptedToken, password);
                retval = token.Equals(Token);

                if (retval)
                {
                    SourceKey = sourceKey;
                    SettingsFile = settingsFile;
                    WatchFileName = watchFile;
                    Password = password;

                    ShortDateFormat = GetSetting(settingsFile, "shortdateformat");
                    LongDateFormat = GetSetting(settingsFile, "longdateformat");
                    ListDateFormat = GetSetting(settingsFile, "listdateformat");
                    CurrencySymbol = GetSetting(settingsFile, "currencysymbol");

                    M(enmMessageType.Info, "Initialising watch file");

                    if (InitialiseWatchList(watchFile))
                    {
                        var apikey = (source["X-IG-API-KEY"] != null ? source["X-IG-API-KEY"].ToString() : null);
                        var sourceUrl = (source["sourceurl"] != null ? source["sourceurl"].ToString() : null);
                        var identifier = EncryptionHelper.Decrypt((source["identifier"] != null ? source["identifier"].ToString() : null), password);
                        var sourcePassword = EncryptionHelper.Decrypt((source["password"] != null ? source["password"].ToString() : null), password);

                        if (string.IsNullOrEmpty(apikey) || string.IsNullOrEmpty(sourceUrl) || string.IsNullOrEmpty(identifier) || string.IsNullOrEmpty(password))
                        {
                            M(enmMessageType.Error, $"Missing some config arguments in the [{settingsFile}] file: X-IG-API-KEY, sourceUrl, identifier, password");
                        }
                        else
                        {
                            R("PARAMETERS_OK");
                            retval = (GetSession(sourceUrl, identifier, sourcePassword, apikey) && InitialiseLSC() && InitialiseWEB());
                        }
                    }
                    else retval = false;
                }
                else R("PASSWORD_FAILED");
            }
            else R("WEAK_PASSWORD");

            return retval;
        }

        private int CheckStrength(string password)
        {
            int score = 0;

            if (password.Length > 4)
                score++;
            if (password.Length >= 8)
                score++;
            if (password.Length >= 12)
                score++;
            if (Regex.Match(password, @"(.*\d.*)", RegexOptions.ECMAScript).Success)
                score++;
            if (Regex.Match(password, @"(.*[a-z].*)", RegexOptions.ECMAScript).Success)
                score++;
            if (Regex.Match(password, @"(.*[A-Z].*)", RegexOptions.ECMAScript).Success)
                score++;
            if (Regex.Match(password, @".*[!,@,#,$,%,^,&,*,?,_,~,-,£,(,)].*", RegexOptions.ECMAScript).Success)
                score++;

            R("CHECK_PASSWORD", new List<string> { score.ToString() });

            return score;
        }

        public bool ChangePassword(string settingsFile, string sourceKey, string existingPassword, string newPassword)
        {
            bool retval = false;

            if (CheckStrength(existingPassword) >= 4 && CheckStrength(newPassword) >= 4)
            {
                settingsFile = (string.IsNullOrEmpty(settingsFile) ? SettingsFile : settingsFile);
                sourceKey = (string.IsNullOrEmpty(sourceKey) ? SourceKey : sourceKey);

                var setting = GetSource(settingsFile, sourceKey);

                var token = setting["token"].ToString();

                if (token.Equals(Token) || string.IsNullOrEmpty(token))
                {
                    token = SaveSource(settingsFile, newPassword, setting);
                }
                else
                {
                    var checkExistingPassword = EncryptionHelper.Decrypt(setting["token"].ToString(), existingPassword);

                    if (checkExistingPassword.Equals(Token))
                    {
                        setting["token"] = EncryptionHelper.Decrypt(setting["token"].ToString(), existingPassword);
                        setting["identifier"] = EncryptionHelper.Decrypt(setting["identifier"].ToString(), existingPassword);
                        setting["password"] = EncryptionHelper.Decrypt(setting["password"].ToString(), existingPassword);

                        token = SaveSource(settingsFile, newPassword, setting);
                    }
                    else
                    {
                        token = string.Empty;
                    }
                }

                var unencryptedToken = EncryptionHelper.Decrypt(token, newPassword);
                retval = unencryptedToken.Equals(Token);

                if (retval)
                    R("PASSWORD_CHANGED");
                else
                    R("PASSWORD_NOT_CHANGED");
            }
            else R("WEAK_PASSWORD");

            return retval;
        }

        public string GetSetting(string settingsFile, string key)
        {
            JObject settings;

            M(enmMessageType.Info, $"Loading setting [{key}]");

            using (StreamReader file = File.OpenText(settingsFile))
            using (JsonTextReader reader = new JsonTextReader(file))
            {
                settings = (JObject)JToken.ReadFrom(reader);
            }

            return settings[key].ToString();
        }

        public JToken GetSource(string settingsFile, string source)
        {
            JObject settings;

            M(enmMessageType.Info, $"Loading source [{source}]");

            using (StreamReader file = File.OpenText(settingsFile))
            using (JsonTextReader reader = new JsonTextReader(file))
            {
                settings = (JObject)JToken.ReadFrom(reader);
            }

            return settings["sources"].SelectToken("$[?(@.key == '" + source + "')]");
        }

        private string SaveSource(string settingsFile, string password, JToken setting)
        {
            JObject settings;

            R("ENCRYPTING_SETTINGS");

            using (StreamReader file = File.OpenText(settingsFile))
            using (JsonTextReader reader = new JsonTextReader(file))
            {
                settings = (JObject)JToken.ReadFrom(reader);
            }

            var sourceSetting = settings["sources"].SelectToken("$[?(@.key == '" + setting["key"] + "')]");
            sourceSetting["token"] = EncryptionHelper.Encrypt(Token, password);
            sourceSetting["identifier"] = EncryptionHelper.Encrypt(setting["identifier"].ToString(), password);
            sourceSetting["password"] = EncryptionHelper.Encrypt(setting["password"].ToString(), password);

            setting["token"] = sourceSetting["token"];
            setting["identifier"] = sourceSetting["identifier"];
            setting["password"] = sourceSetting["password"];

            M(enmMessageType.Info, "Saving settings");

            using (StreamWriter file = File.CreateText(settingsFile))
            using (JsonTextWriter writer = new JsonTextWriter(file))
            {
                settings.WriteTo(writer);
            }

            return sourceSetting["token"].ToString();
        }

        public string GetAlias(string instrument, bool returnInstrumentIfNull = true)
        {
            string retval = (returnInstrumentIfNull ? instrument : string.Empty);

            foreach (var alias in IGClient.WatchFile.Alias)
            {
                if (alias.Value.ToUpper().Equals(instrument.ToUpper()))
                {
                    retval = alias.Key;
                }
            }

            return retval;
        }

        public string GetInstrument(string alias)
        {
            string retval = alias;

            if (IGClient.WatchFile.Alias.ContainsKey(alias)) retval = IGClient.WatchFile.Alias[alias];

            return retval;
        }

        public void ListAliases()
        {
            foreach (var alias in IGClient.WatchFile.Alias.OrderBy(x => x.Key))
            {
                M(enmMessageType.Info, $"{alias.Key} = {alias.Value}");
            }
        }

        public void GetClientDetails()
        {
            M(enmMessageType.Info, $"Status: {(IGClient.Pause ? "PAUSED" : "ACTIVE")}");
            if (!IGClient.Pause)
            {
                var started = DateTime.Now.Subtract(IGClient.Started);
                M(enmMessageType.Info, $"Started: {Convert.ToDateTime(IGClient.Started.ToString().Substring(0, 19)):s}");
                M(enmMessageType.Info, $"Collecting: {(started.Hours == 1 ? started.Hours + " hour " : "")}{(started.Hours > 1 ? started.Hours + " hours " : "")}{(started.Minutes == 1 ? started.Minutes + " minute " : "")}{(started.Minutes > 1 ? started.Minutes + " minutes " : "")}{(started.Seconds == 1 ? started.Seconds + " second" : "")}{(started.Seconds > 1 ? started.Seconds + " seconds" : "")}");
            }

            M(enmMessageType.Info, $"Lightstream: {IGClient.LSC.connectionDetails.ServerAddress}");
            M(enmMessageType.Info, $"SessionId: {IGClient.LSC.connectionDetails.SessionId}");
            M(enmMessageType.Info, $"Status: {IGClient.LSC.Status}");

            M(enmMessageType.Info, $"Model: {(IGClient.ML.Model != null ? "ACTIVE" : "NOT ACTIVE")}");
            if (IGClient.ML.Model != null) {
                M(enmMessageType.Info, $"Model Name: {IGClient.ML.ModelName}");
                M(enmMessageType.Info, $"Prediction: {IGClient.ML.PredictColumn}");
                M(enmMessageType.Info, $"Metric: {String.Join(",", IGClient.ML.CurrentMetric.Columns)} > L1={IGClient.ML.CurrentMetric.MAE:0.###}, L2={IGClient.ML.CurrentMetric.Ls2:0.###}, Rms={IGClient.ML.CurrentMetric.Rms:0.###}, Loss={IGClient.ML.CurrentMetric.Lss:0.###}, RSq={IGClient.ML.CurrentMetric.RSq:0.###}, Scr={IGClient.ML.CurrentMetric.Score:0.###}");
            }

            if (!string.IsNullOrEmpty(IGClient.Filter)) M(enmMessageType.Info, $"Filter: {IGClient.Filter}");
            M(enmMessageType.Info, $"Listeners: {IGClient.LSC.Listeners.Count}");
            M(enmMessageType.Info, $"Subscribers: {IGClient.LSC.Subscriptions.Count}");
            M(enmMessageType.Info, $"SourceUrl: {IGClient.Settings.SourceUrl}");
            M(enmMessageType.Info, $"WebApiUrl: {IGClient.Settings.WebApiUrl}");
            M(enmMessageType.Info, $"Session: {Convert.ToDateTime(IGClient.Settings.Session.ToString().Substring(0, 19)):s}");
            var running = DateTime.Now.Subtract(IGClient.Settings.Session);
            M(enmMessageType.Info, $"Running: {(running.Hours == 1 ? running.Hours + " hour " : "")}{(running.Hours > 1 ? running.Hours + " hours " : "")}{(running.Minutes == 1 ? running.Minutes + " minute " : "")}{(running.Minutes > 1 ? running.Minutes + " minutes " : "")}{(running.Seconds == 1 ? running.Seconds + " second" : "")}{(running.Seconds > 1 ? running.Seconds + " seconds" : "")}");
            M(enmMessageType.Info, $"ApiKey: {IGClient.Authentication.APIKEY}");
            M(enmMessageType.Info, $"CST: {IGClient.Authentication.CST}");
            M(enmMessageType.Info, $"XST: {IGClient.Authentication.XST}");
        }

        public void IsCapture(bool value)
        {
            IGClient.Pause = value;

            if (IGClient.Pause)
                M(enmMessageType.Info, "Subscription collection is now paused");
            else
            {
                IGClient.Started = DateTime.Now;
                M(enmMessageType.Info, "Subscription collection has been restarted");
            }
        }

        public void Summary()
        {
            var valueCounts = new Dictionary<string, long>();

            foreach (var schema in IGClient.WatchFile.Schemas)
            {
                valueCounts.Add(schema.SchemaName, schema.CodeLibrary.Record.Count());
            }

            if (valueCounts.Count > 0)
            {
                foreach (var value in valueCounts)
                {
                    M(enmMessageType.Info, $"{value.Key} @ #{value.Value}");
                }
            }
            else
            {
                M(enmMessageType.Error, "No keys found");
            }
        }

        public void Close(string dealId)
        {
            dealId = dealId.ToUpper();

            var open = IGCommand.AllOpen(IGClient.Authentication.APIKEY, IGClient.Authentication.CST, IGClient.Authentication.XST, IGClient.Settings.WebApiUrl);

            if (IsOk(open))
            {
                if (open.Response.SelectToken("positions").Count() > 0)
                {
                    var found = false;

                    foreach (var positions in open.Response.SelectTokens("positions").Select(x => x))
                    {
                        foreach (var position in positions)
                        {
                            var currentDealId = position["position"]["dealId"].ToString();

                            if (currentDealId == dealId || string.IsNullOrEmpty(dealId))
                            {
                                found = true;
                                var currency = position["position"]["currency"].ToString();
                                var direction = (position["position"]["direction"].ToString() == "BUY" ? "SELL" : "BUY");
                                var size = double.Parse(position["position"]["size"].ToString());

                                var close = IGCommand.Close(IGClient.Authentication.APIKEY, IGClient.Authentication.CST, IGClient.Authentication.XST, IGClient.Settings.WebApiUrl, currentDealId, size, direction);

                                if (IsOk(close))
                                {
                                    var dealReference = close.Response["dealReference"].ToString();
                                    var confirmation = IGCommand.GetConfirmation(IGClient.Authentication.APIKEY, IGClient.Authentication.CST, IGClient.Authentication.XST, IGClient.Settings.WebApiUrl, dealReference);

                                    if (IsOk(confirmation))
                                    {
                                        M(enmMessageType.Info, $"OK, Alias = {GetAlias(confirmation.Response["epic"].ToString())}, Epic = {confirmation.Response["epic"].ToString()}, Size = {size}, Currency = {currency}, DealId = {confirmation.Response["dealId"]}, Reference = {dealReference}, Status = {confirmation.Response["dealStatus"]}, Reason = {confirmation.Response["reason"]}");
                                    }
                                }
                            }
                        }
                    }

                    if (!found) M(enmMessageType.Error, $"ERROR, no position found with that dealId [{dealId}]");
                }
                else M(enmMessageType.Error, $"ERROR, no open positions found");
            }
        }

        public bool IsOk(IGResponse<JObject> json)
        {
            var retval = false;

            if (json != null && json.Response != null)
            {
                if (!json.Response.ContainsKey("errorCode"))
                {
                    retval = true;
                }
                else M(enmMessageType.Error, $"ERROR, {json.Response["errorCode"]}");
            } else M(enmMessageType.Error, $"ERROR, No response from server");

            return retval;
        }

        public void GetWatchList()
        {
            M(enmMessageType.Info, $"WatchFile: {IGClient.WatchFile.WatchFileUri}");
            M(enmMessageType.Info, "WatchFileId: " + IGClient.WatchFile.WatchFileId);
            M(enmMessageType.Info, "WatchName: " + IGClient.WatchFile.WatchName);
            M(enmMessageType.Info, "Currency: " + IGClient.WatchFile.Currency);
            M(enmMessageType.Info, "Checksum: " + IGClient.WatchFile.CheckSum);
            M(enmMessageType.Info, $"Loaded: {Convert.ToDateTime(IGClient.WatchFile.Loaded.ToString().Substring(0, 19)):s}");
            M(enmMessageType.Info, "Captures: " + IGClient.WatchFile.MergeCaptureList.Count);
            M(enmMessageType.Info, "Fields: " + IGClient.WatchFile.MergeFieldList.Count);
            M(enmMessageType.Info, "Schemas: " + IGClient.WatchFile.Schemas.Count);

            var items = string.Empty;
            var fields = string.Empty;
            var fm = string.Empty;
            var co = string.Empty;
            var schemaNames = string.Empty;

            for (var i = 0; i < IGClient.WatchFile.Schemas.Count; i++)
            {
                var schema = IGClient.WatchFile.Schemas[i];
                var formulae = schema.SchemaInstruments.FindAll(x => x.Type == SchemaInstrument.enmType.formula);
                schemaNames += (string.IsNullOrEmpty(schemaNames) ? $"#{i}_{schema.SchemaName}" : $", #{i}_{schema.SchemaName}") + (schema.IsActive ? "_A" : "_I");

                foreach (var formula in formulae)
                {
                    fm += (!fm.Contains(formula.Name) ? (string.IsNullOrEmpty(fm) ? $"#{i}_{formula.Name}" : $", #{i}_{formula.Name}") : "");
                }

                foreach (var column in schema.SchemaInstruments)
                {
                    co += (string.IsNullOrEmpty(co) ? $"#{i}_{column.Key}" : $", #{i}_{column.Key}");
                    co += (column.IsColumn ? "" : "_h");
                }
            }

            for (var i = 0; i < IGClient.LSC.Subscriptions.Count; i++)
            {
                var subscriber = IGClient.LSC.Subscriptions[i];
                foreach (var item in subscriber.Items)
                {
                    items += (string.IsNullOrEmpty(items) ? $"{item}" : $", {item}");
                }

                foreach (var field in subscriber.Fields)
                {
                    fields += (string.IsNullOrEmpty(fields) ? $"{field}" : $", {field}");
                }
            }

            M(enmMessageType.Info, "Schema Names: " + schemaNames);
            M(enmMessageType.Info, $"Columns: {co}");
            fm = (string.IsNullOrEmpty(fm) ? "-" : fm);
            M(enmMessageType.Info, $"Formulae: {fm}");
            M(enmMessageType.Info, "Subscribers: " + IGClient.LSC.Subscriptions.Count);
            M(enmMessageType.Info, $"Captured: {items}");
            M(enmMessageType.Info, $"Fields: {fields}");
        }

        public void GetDetails(string instrument)
        {
            var key = GetInstrument(instrument);
            var details = IGCommand.GetDetails(IGClient.Authentication.APIKEY, IGClient.Authentication.CST, IGClient.Authentication.XST, IGClient.Settings.WebApiUrl, key);

            if (IsOk(details))
            {
                M(enmMessageType.Info, "Instrument = " + instrument);
                M(enmMessageType.Info, $"Epic = {details.Response["instrument"]["epic"]}");
                M(enmMessageType.Info, $"Name = {details.Response["instrument"]["name"]}");
                M(enmMessageType.Info, $"SizeUnit = {details.Response["dealingRules"]["minDealSize"]["unit"]}");
                M(enmMessageType.Info, $"Size = {String.Format("{0:0.00}", Convert.ToDouble(details.Response["dealingRules"]["minDealSize"]["value"]))}");
                M(enmMessageType.Info, $"Type = {details.Response["instrument"]["type"]}");
                M(enmMessageType.Info, $"MarketId = {details.Response["instrument"]["marketId"]}");
                M(enmMessageType.Info, $"Status = {details.Response["snapshot"]["marketStatus"]}");
                M(enmMessageType.Info, $"High = {String.Format("{0:0.00}", details.Response["snapshot"]["high"])}");
                M(enmMessageType.Info, $"Low = {String.Format("{0:0.00}", details.Response["snapshot"]["low"])}");
                M(enmMessageType.Info, $"Bid = {String.Format("{0:0.00}", details.Response["snapshot"]["bid"])}");
                M(enmMessageType.Info, $"Offer = {String.Format("{0:0.00}", details.Response["snapshot"]["offer"])}");
                M(enmMessageType.Info, $"Spread = {String.Format("{0:0.00}", Convert.ToDouble(details.Response["spread"]))}");
                M(enmMessageType.Info, $"% Spread = {String.Format("{0:0.00}%", Convert.ToDouble(details.Response["percspread"]))}");

                var currencyList = string.Empty;

                foreach (var currency in details.Response["instrument"]["currencies"])
                {
                    currencyList += (string.IsNullOrEmpty(currencyList) ? $"{currency["code"]}" : $", {currency["code"]}");
                }

                M(enmMessageType.Info, $"Currencies = {currencyList}");
            }
        }

        public void Open()
        {
            var open = IGCommand.AllOpen(IGClient.Authentication.APIKEY, IGClient.Authentication.CST, IGClient.Authentication.XST, IGClient.Settings.WebApiUrl);

            if (IsOk(open))
            {
                M(enmMessageType.Info, "Alias        | Epic                     | Size | Opened              | Direction | DealId               | Reference            | Currency | Level");
                M(enmMessageType.Info, "-------------+--------------------------+------+---------------------+-----------+----------------------+----------------------+----------+---------------------");

                foreach (var position in open.Response["positions"])
                {
                    M(enmMessageType.Info, $"{GetAlias(position["market"]["epic"].ToString(), false),-10}     {position["market"]["epic"].ToString(),-25}  {position["position"]["size"]:N1}    {Convert.ToDateTime(position["position"]["createdDate"].ToString().Substring(0, 19)):s}   {position["position"]["direction"],-10}  {position["position"]["dealId"].ToString(),-20}   {position["position"]["dealReference"].ToString(),-20}   {position["position"]["currency"]}        {String.Format("{0:0.00}", position["position"]["level"])} >> {String.Format("{0:0.00}", position["position"]["profit"])}");
                }
            }
        }

        public void ReloadWatchFile(string password, string watchFile)
        {
            Authenticate(null, null, watchFile, password);
        }

        public void Empty(string schema)
        {
            var schemaObj = GetSchema(schema);

            if (schemaObj != null)
            {
                schemaObj.CodeLibrary.Reset();
                R("DATASET_EMPTIED");
            }
            else R("NO_SCHEMA");
        }

        public void TypeOut(string schema, int start, int rows, bool includeAllColumns, bool includeAllRows)
        {
            var schemaObj = GetSchema(schema);

            if (schemaObj != null)
            {
                M(enmMessageType.Info, GetHeader(schemaObj, includeAllColumns));

                var max = schemaObj.CodeLibrary.Record.Count;
                start = (start <= 0 ? (max - Math.Min(rows, max) + 1) : start);
                var end = Math.Min((start + rows), max);

                for (var i = (start - 1); i < end; i++)
                {
                    var record = schemaObj.CodeLibrary.Record[i];

                    var complete = !record.Values.Any(x => string.IsNullOrEmpty(x.Value));

                    if (includeAllRows || complete)
                    {
                        var line = BaseCodeLibrary.GetDatasetRecord(record, schemaObj.SchemaInstruments, includeAllColumns, false);
                        M(enmMessageType.Info, line);
                    }
                }
            }
            else R("NO_SCHEMA");
        }

        public void Schemas()
        {
            foreach (var schema in IGClient.WatchFile.Schemas)
            {
                M(enmMessageType.Info, $"{schema.SchemaName} active={schema.IsActive.ToString().ToLower()}");
            }
        }

        //public WatchFile.Schema GetSchema(string schemaName)
        //{
        //    WatchFile.Schema retval = null;

        //    foreach (var schema in IGClient.WatchFile.Schemas)
        //    {
        //        if (schema.SchemaName.ToLower() == schemaName.ToLower())
        //        {
        //            retval = schema;
        //            break;
        //        }
        //    }

        //    return retval;
        //}

        public void SchemaActivate(string schema, bool activate)
        {
            var schemaObj = GetSchema(schema);

            if (schemaObj != null)
            {
                schemaObj.IsActive = activate;

                if (activate)
                    R("ACTIVATE_SCHEMA");
                else
                    R("INACTIVATE_SCHEMA");
            }
            else R("NO_SCHEMA");
        }

        public void Transactions(int days)
        {
            var fromDate = DateTime.Now;
            var toDate = DateTime.Now;

            if (days <= 100)
            {
                if (days > 1) fromDate = DateTime.Now.Subtract(new TimeSpan(days, 0, 0, 0));

                var transactions = IGCommand.Transactions(IGClient.Authentication.APIKEY, IGClient.Authentication.CST, IGClient.Authentication.XST, IGClient.Settings.WebApiUrl, fromDate, toDate, IGCommand.enmTransType.ALL);

                if (IsOk(transactions))
                {
                    M(enmMessageType.Info, "Description                                  | Profit         | Type    | Date    | DealId                                     | Size");
                    M(enmMessageType.Info, "---------------------------------------------+----------------+---------+---------+--------------------------------------------+---------");

                    foreach (var transaction in transactions.Response["transactions"])
                    {
                        var profit = transaction["profitAndLoss"].ToString().Substring(1); //, transaction["profitAndLoss"].ToString().ToString().Length - 1);
                        M(enmMessageType.Info, $"{transaction["instrumentName"].ToString(),-45}  {String.Format("{0:0.00} {1}", profit, CurrencySymbol),14}   {transaction["transactionType"]}      {DateTime.Parse(transaction["date"].ToString()).ToString(ListDateFormat)}     {transaction["reference"],-43}  {transaction["size"].ToString():N1}");
                    }
                }
            } else M(enmMessageType.Error, "ERROR, 100 days or less");
        }

        public void Search(string searchTerm)
        {
            var search = IGCommand.Search(IGClient.Authentication.APIKEY, IGClient.Authentication.CST, IGClient.Authentication.XST, IGClient.Settings.WebApiUrl, searchTerm);

            if (IsOk(search))
            {
                M(enmMessageType.Info, "Epic                     | Name");
                M(enmMessageType.Info, "-------------------------+-----------------------------------------");

                foreach (var found in search.Response["markets"])
                {
                    M(enmMessageType.Info, $"{found["epic"].ToString(),-25}  {found["instrumentName"].ToString(),-40}");
                }
            }
        }

        public void Accounts()
        {
            var accounts = IGCommand.Accounts(IGClient.Authentication.APIKEY, IGClient.Authentication.CST, IGClient.Authentication.XST, IGClient.Settings.WebApiUrl);

            if (IsOk(accounts))
            {
                M(enmMessageType.Info, "Name                          | Currency  | Balance        | Status");

                foreach (var account in accounts.Response["accounts"])
                {
                    M(enmMessageType.Info, $"{account["accountName"].ToString(),-30}  {account["currency"].ToString(),-10}  {account["balance"]["balance"].ToString(),-15}  {account["status"].ToString(),-30}");
                }
            }
        }

        public void Process(string direction, string instrument)
        {
            Process(direction, instrument, 0d, string.Empty);
        }

        public void Process(string direction, string instrument, double size)
        {
            Process(direction, instrument, size, string.Empty);
        }

        public void Process(string direction, string instrument, double size, string currency)
        {
            currency = currency.ToUpper();
            direction = direction.ToUpper();
            direction = (direction == "/S" ? "SELL" : (direction == "/B" ? "BUY" : direction));
            var key = GetInstrument(instrument);
            var details = IGCommand.GetDetails(IGClient.Authentication.APIKEY, IGClient.Authentication.CST, IGClient.Authentication.XST, IGClient.Settings.WebApiUrl, key);

            if (IsOk(details))
            {
                if (size == 0 && details.Response["dealingRules"]["minDealSize"]["unit"].ToString().Equals("POINTS"))
                {
                    size = double.Parse(details.Response["dealingRules"]["minDealSize"]["value"].ToString());
                }

                if (string.IsNullOrEmpty(currency)) currency = IGClient.WatchFile.Currency;

                if (details.Response["instrument"]["currencies"].SelectToken($"[?(@.code == '{currency}')]") == null)
                {
                    M(enmMessageType.Error, $"No matching currency found for [{currency}]");
                } else
                {
                    var json = IGCommand.Open(IGClient.Authentication.APIKEY, IGClient.Authentication.CST, IGClient.Authentication.XST, IGClient.Settings.WebApiUrl, direction, currency, key, size);

                    if (IsOk(json))
                    {
                        var dealReference = json.Response["dealReference"].ToString();
                        var confirmation = IGCommand.GetConfirmation(IGClient.Authentication.APIKEY, IGClient.Authentication.CST, IGClient.Authentication.XST, IGClient.Settings.WebApiUrl, dealReference);

                        if (IsOk(confirmation))
                        {
                            M(enmMessageType.Info, $"OK, Alias = {GetAlias(key)}, Epic = {key}, Size = {size}, Currency = {currency}, DealId = {confirmation.Response["dealId"]}, Reference = {dealReference}, Status = {confirmation.Response["dealStatus"]}, Reason = {confirmation.Response["reason"]}");
                        }
                    }
                }
            }
        }

        public bool Help(string search)
        {
            var help = new List<string>();

            help.Add("date = Todays date (/dt)");
            help.Add("closeall = Close ALL open positions (/ca)");
            help.Add("close <dealId> = Close a specific position using its dealId (/cd)");
            help.Add("model load <model> = Load specific model (/ml)");
            help.Add("model delete <model|wildcards> = Delete specific model (/md)");
            help.Add("model copy <model> <newmodel> = Copy specific model (/mc)");
            help.Add("model rename <model> <newmodel> = Rename model (/mr)");
            help.Add("model info [<schema|wildcards>] = Get additional information for models (/mi)");
            help.Add("model bin = Close currently loaded model (/mb)");
            help.Add("model save <model> = Save current model to specific model name (/ms)");
            help.Add("model = Lists all models (/m)");
            help.Add("train info <dataset> = Display last training results for dataset (/ti)");
            help.Add("train <dataset> <predict> <predictrange> [columns|-c] = Train model from combinations in dataset to predict a specific column, -c uses all columns in dataset only (/t)");
            help.Add("eval <dataset> = Evaluate and check your predictions using current model and specific dataset (/e)");
            help.Add("run <dataset> = Run through a simulated dataset using current model (/e)");
            help.Add("begin = Begin collection of data (/bc)");
            help.Add("end = End collection of data (/ec)");
            help.Add("info [<days>] = Display todays transactions or transaction for last number of days, max 100, default 1 (/i)");
            help.Add("dataset save <schema> [-d] [-a] [-r] = Save dataset for specific schema, -d stops automatic date/time naming, -a includes hidden columns, -r include all rows (/ds)");
            help.Add("dataset delete <dataset|wildcards> = Delete dataset (/dd)");
            help.Add("dataset copy <dataset> <newdataset> = Copy dataset (/dc)");
            help.Add("dataset rename <dataset> <newdataset> = Rename dataset (/dr)");
            help.Add("dataset info [<dataset|wildcards>] = Get additional information for dataset (/di)");
            help.Add("dataset headings [<dataset|wildcards>] = Get column information for dataset (/dh)");
            help.Add("dataset = List all datasets (/d)");
            help.Add("open = Display all current open positions (/o)");
            help.Add("search <value> = Locate an instrument name and return all matches (/se)");
            help.Add("summary = Count of values stored across all schemas (/su)");
            help.Add("buy <instrument> <size> [currency] = Buy a specific instrument or epic (/b)");
            help.Add("buy <instrument> = Buy a specific instrument or epic (/b)");
            help.Add("sell <instrument> <size> [currency] = Sell a specific instrument or epic (/s)");
            help.Add("sell <instrument> = Sell a specific instrument or epic (/s)");
            help.Add("get <instrument> = Get details on a specific instrument (/g)");
            help.Add("cls = Clear screen (/cs)");
            help.Add("> = Show continuous subscription");
            help.Add(">> <schema> [-a] = Show continuous dataset for specific schema, -a include all columns");
            help.Add(">>> <schema> = Show continuous prediction for specific schema");
            help.Add("type <schema> <rows> [-a] = Type out last COMPLETED rows, -a include all columns (/ty)");
            help.Add("watch = Display watch file details (/w)");
            help.Add("client = Display client connection details (/cl)");
            help.Add("empty <schema> = Clear all captured records for a schema (/em)");
            help.Add("reload [<password> [<watchfilepath>]] = Reload a watch file or load a new watch file (/r)");
            help.Add("filter [<text>] = Filter continuous list by specific text, or clear existing filter (/f)");
            help.Add("passwd <currentpassword> <newpassword> = Change current password for settings and source (/p)");
            help.Add("exit = Exit application (/x)");
            help.Add("alias = List of aliases (/a)");
            help.Add("alias set <alias> <value> = Change a value for an alias in the current watch file");
            help.Add("alias delete <alias> = Delete an alias from the current watch file");
            help.Add("log = List all log files (/l)");
            help.Add("log type <lines> <logfile> = Type last specified number of lines (max 250) of specified log file (/lt)");
            help.Add("log delete <logfile|wildcards> = Delete a specific log file (/ld)");
            help.Add("log copy <logfile> <newlogfile> = Copy a specific log file (/lc)");
            help.Add("log rename <logfile> <newlogfile> = Rename a specific log file (/lr)");
            help.Add("log info [<logfile|wildcards>] = Get additional information for log files (/li)");
            help.Add("merge <dataset> <schema> <newdataset> = Merge existing dataset with schema to product new dataset (/m)");
            help.Add("schema = List all schemas and show current details (/sc)");
            help.Add("schema +a <schema> = Activate a schema to allow it to capture incoming values (/sc+)");
            help.Add("schema -a <schema> = InActivate a schema to stop it capturing incoming values (/sc-)");
            help.Add("$ = List all short codes");
            help.Add("$+ = Set a short code i.e. add or update");
            help.Add("$- = Delete a short code");
            help.Add("feed <dataset> <column> <field> <item> = Feed the system with a previously saved dataset add to all schemas (/fe)");

            foreach (var line in help.OrderBy(x => x))
            {
                if (String.IsNullOrEmpty(search) || line.ToLower().Contains(search.ToLower())) M(enmMessageType.Info, line);
            }

            return (help.Count >= 0);
        }

        public void InfoDataSet(string schema)
        {
            schema = (string.IsNullOrEmpty(schema) ? "*" : schema);

            var schemas = Directory.GetFiles(@".\", $"{schema}.csv");

            if (schemas.Length > 0)
            {
                M(enmMessageType.Info, "Name                       | Size      | Lines     | Columns | Updated");
                M(enmMessageType.Info, "---------------------------+-----------+-----------+---------+--------------");

                foreach (var x in schemas)
                {
                    var info = new FileInfo(x);
                    var lines = File.ReadLines(x).Count();
                    var heading = File.ReadLines(x).Skip(1).First();

                    var columnCount = heading.Split(",").Length;

                    M(enmMessageType.Info, $"{x.Substring(2, x.Length - 2 - 4).PadRight(30), -30}  {string.Format("{0:0}", (info.Length < 1024 ? 1 : (info.Length / 1024))), 5}k  {lines, 10}  {columnCount, 3}        {info.LastWriteTimeUtc.ToString(ShortDateFormat)}");
                }
            }
            else R("NO_DATASET");
        }

        public void HeadingsDataSet(string dataset)
        {
            dataset = (string.IsNullOrEmpty(dataset) ? "*" : dataset);

            var schemas = Directory.GetFiles(@".\", $"{dataset}.csv");

            if (schemas.Length > 0)
            {
                foreach (var x in schemas)
                {
                    var info = new FileInfo(x);
                    var lines = File.ReadLines(x);
                    if (lines.Count() > 0)
                    {
                        var split = lines.ToList()[1].Split(',');
                        foreach(var column in split)
                        {
                            M(enmMessageType.Info, $"{x.Substring(2, x.Length - 2 - 4)} - {column}");
                        }
                    }
                }
            }
            else R("NO_DATASET");
        }

        public void DeleteDataSet(string dataset)
        {
            var schemas = Directory.GetFiles(@".\", $"{dataset}.csv");

            if (schemas.Length > 0)
            {
                var confirm = (schemas.Length == 1 || CC($"{schemas.Length} datasets found, Are You Sure (Y/n)? ", 'Y'));

                if (confirm)
                {
                    foreach (var x in schemas)
                    {
                        File.Delete(x);
                        var schemaName = x.Substring(2, x.Length - 2 - 4);
                        M(enmMessageType.Info, $"Deleted dataset [{schemaName}]");
                    }
                }
            }
            else R("NO_DATASET");
        }

        public void RenameDataSet(string dataset, string newDataSet)
        {
            if (File.Exists($@".\{dataset}.csv") && !File.Exists($@".\{newDataSet}.csv"))
            {
                File.Move($@".\{dataset}.csv", $@".\{newDataSet}.csv");
                M(enmMessageType.Info, $"Renamed dataset [{dataset}] to [{newDataSet}]");
            }
            else R("NO_SCHEMA_FOUND");
        }

        public void CopyDataSet(string dataset, string newDataSet)
        {
            if (File.Exists($@".\{dataset}.csv") && !File.Exists($@".\{newDataSet}.csv"))
            {
                File.Copy($@".\{dataset}.csv", $@".\{newDataSet}.csv");
                M(enmMessageType.Info, $"Copied dataset [{dataset}] to [{newDataSet}]");
            }
            else R("NO_SCHEMA_FOUND");
        }

        public void DataSet()
        {
            var files = Directory.GetFiles(@".\", "*.csv");

            if (files.Length > 0)
            {
                foreach (var file in files)
                {
                    M(enmMessageType.Info, $"{file.Substring(2, file.Length - 2 - 4)}");
                }
            }
            else R("NO_DATASET");
        }

        public string GetHeader(WatchFile.Schema schema, bool includeAllColumns = false, bool includePredicted = false)
        {
            var header = string.Empty;

            header += "timestamp,timediff";

            foreach (var instrument in schema.SchemaInstruments)
            {
                if ((instrument.IsColumn || includeAllColumns) && (!includePredicted || !instrument.IsSignal))
                {
                    header += (string.IsNullOrEmpty(header) ? instrument.Key : $",{instrument.Key}{(!instrument.IsColumn ? "_h" : "")}");
                }
            }

            return header;
        }

        public WatchFile.Schema GetSchema(string schema)
        {
            WatchFile.Schema retval = null;
            if (IGClient.WatchFile.Schemas.Exists(x => x.SchemaName == schema)) retval = IGClient.WatchFile.Schemas.Single(x => x.SchemaName.Equals(schema));
            return retval;
        }

        public void SaveDataSet(string dataset, bool noDateTimeNaming, bool includeAllColumns, bool includeAllRows)
        {
            var alreadyPaused = IGClient.Pause;
            var outputSchemaName = (noDateTimeNaming ? $"{dataset}" : $"{dataset}_{DateTime.Now.ToString("yyyyMMdd_HHmmss")}");

            var schemaObj = GetSchema(dataset);

            if (schemaObj != null)
            {
                IGClient.Pause = (alreadyPaused ? alreadyPaused : true);

                if (File.Exists(outputSchemaName)) File.Delete(outputSchemaName);

                using (System.IO.StreamWriter file = new System.IO.StreamWriter($"{outputSchemaName}.csv"))
                {
                    //
                    string schemaString = JsonConvert.SerializeObject(schemaObj.SchemaInstruments);
                    file.WriteLine(schemaString);
                    file.WriteLine(GetHeader(schemaObj, includeAllColumns));

                    var max = (schemaObj.CodeLibrary.Record.Count() - 1);

                    for (var i = 0; i < max; i++)
                    {
                        var record = schemaObj.CodeLibrary.Record.ElementAt(i);
                        var complete = !record.Values.Any(x => string.IsNullOrEmpty(x.Value));

                        if ((includeAllRows || !record.Values["completed"].Contains("X")) && record.Time > 0)
                        {
                            var line = BaseCodeLibrary.GetDatasetRecord(record, schemaObj.SchemaInstruments, includeAllColumns, false);
                            file.WriteLine(line);
                        }
                    }

                    M(enmMessageType.Info, $"Saved dataset schema [{dataset}] to [{outputSchemaName}]");
                }

                IGClient.Pause = (alreadyPaused ? alreadyPaused : false);
            } else R("NO_SCHEMA");
        }

        public void DeleteModel(string model)
        {
            var models = Directory.GetFiles(@".\", $"{model}.zip");

            if (models.Length > 0)
            {
                foreach (var x in models)
                {
                    File.Delete(x);
                    var modelName = x.Substring(2, x.Length - 2 - 4);
                    M(enmMessageType.Info, $"Deleted model [{modelName}]");
                }
            }
            else R("NO_MODEL");
        }

        public void RenameModel(string model, string newModel)
        {
            if (File.Exists($@".\{model}.zip") && !File.Exists($@".\{newModel}.zip"))
            {
                File.Move($@".\{model}.zip", $@".\{newModel}.zip");
                M(enmMessageType.Info, $"Renamed model [{model}] to [{newModel}]");
            }
            else R("NO_MODEL_FOUND");
        }

        public void CopyModel(string model, string newModel)
        {
            if (File.Exists($@".\{model}.zip") && !File.Exists($@".\{newModel}.zip"))
            {
                File.Copy($@".\{model}.zip", $@".\{newModel}.zip");
                M(enmMessageType.Info, $"Copied model [{model}] to [{newModel}]");
            }
            else R("NO_MODEL_FOUND");
        }

        public void Model()
        {
            var files = Directory.GetFiles(@".\", "*.zip");

            if (files.Length > 0)
            {
                foreach (var model in files)
            {
                    M(enmMessageType.Info, $"{model.Substring(2, model.Length - 2 - 4)}");
            }
        }
            else R("NO_MODEL");
        }

        public void InfoModel(string wildcard)
        {
            wildcard = (string.IsNullOrEmpty(wildcard) ? "*" : wildcard);

            var files = Directory.GetFiles(@".\", $"{wildcard}.zip");

            if (files.Length > 0)
            {
                foreach (var model in files)
                {
                    var info = new FileInfo(model);
                    M(enmMessageType.Info, $"{model.Substring(2, model.Length - 2 - 4).PadRight(30),-30}  {string.Format("{0:0}", (info.Length < 1024 ? 1 : (info.Length / 1024))),5}k   {info.CreationTimeUtc}");
                }
            }
            else R("NO_MODEL");
        }

        public void InfoLogFile(string logFile)
        {
            logFile = (string.IsNullOrEmpty(logFile) ? "*" : logFile);

            var logFiles = Directory.GetFiles(@".\", $"{logFile}.log");

            if (logFiles.Length > 0)
            {
                M(enmMessageType.Info, "Name                       | Size      | Lines     | Updated");
                M(enmMessageType.Info, "---------------------------+-----------+-----------+--------------");

                foreach (var x in logFiles)
                {
                    var info = new FileInfo(x);
                    var lines = File.ReadLines(x).Count();
                    M(enmMessageType.Info, $"{x.Substring(2, x.Length - 2 - 4).PadRight(30),-30}  {string.Format("{0:0}", (info.Length < 1024 ? 1 : (info.Length / 1024))),5}k  {lines,10}   {info.CreationTimeUtc.ToString(ShortDateFormat)}");
                }
            }
            else R("NO_LOGFILE");
        }

        public void DeleteLogFile(string logFile)
        {
            var logfiles = Directory.GetFiles(@".\", $"{logFile}.log");

            if (logfiles.Length > 0)
            {
                var confirm = (logfiles.Length == 1 || CC($"{logfiles.Length} logs found, Are You Sure (Y/n)? ", 'Y'));

                if (confirm)
                {
                    foreach (var x in logfiles)
                    {
                        File.Delete(x);
                        var logFileName = x.Substring(2, x.Length - 2 - 4);
                        M(enmMessageType.Info, $"Deleted log file [{logFileName}]");
                    }
                }
            }
            else R("NO_LOGFILE");
        }

        public void RenameLogFile(string logFile, string newLogFile)
        {
            if (File.Exists($@".\{logFile}.log") && !File.Exists($@".\{newLogFile}.log"))
            {
                File.Move($@".\{logFile}.log", $@".\{newLogFile}.log");
                M(enmMessageType.Info, $"Renamed log file [{logFile}] to [{newLogFile}]");
            }
            else R("NO_LOGFILE");
        }

        public void CopyLogFile(string logFile, string newLogFile)
        {
            if (File.Exists($@".\{logFile}.log") && !File.Exists($@".\{newLogFile}.log"))
            {
                File.Copy($@".\{logFile}.log", $@".\{newLogFile}.log");
                M(enmMessageType.Info, $"Copied log file [{logFile}] to [{newLogFile}]");
            }
            else R("NO_LOGFILE");
        }

        public void LogFile()
        {
            var files = Directory.GetFiles(@".\", "*.log");

            if (files.Length > 0)
            {
                foreach (var file in files)
                {
                    M(enmMessageType.Info, $"{file.Substring(2, file.Length - 2 - 4)}");
                }
            }
            else R("NO_LOGFILE");
        }

        public void TypeLogFile(int maxLines, string logFile)
        {
            maxLines = Math.Min(maxLines, 250);
            logFile = $@".\{logFile}.log";

            if (File.Exists(logFile))
            {
                var lines = new List<string>();

                using (StreamReader file = File.OpenText(logFile))
                {
                    var line = string.Empty;

                    do {
                        line = file.ReadLine();
                        lines.Add(line);

                        if (lines.Count > maxLines)
                        {
                            lines.RemoveAt(0);
                        }
                    } while (!string.IsNullOrEmpty(line));
                }

                foreach (var line in lines)
                {
                    M(enmMessageType.Info, line);
                }
            }
            else R("NO_LOGFILE");
        }

        public void DeleteAlias(string watchFile, string key)
        {
            JObject watchFileJson;

            using (StreamReader file = File.OpenText(watchFile))
            using (JsonTextReader reader = new JsonTextReader(file))
            {
                watchFileJson = (JObject)JToken.ReadFrom(reader);
            }

            foreach (JObject alias in watchFileJson["alias"])
            {
                if (alias["alias"].ToString().Equals(key))
                {
                    alias.Remove();
                    break;
                }
            }

            using (StreamWriter file = File.CreateText(watchFile))
            using (JsonTextWriter writer = new JsonTextWriter(file))
            {
                watchFileJson.WriteTo(writer);
            }

            R("RELOAD_WATCH");
        }

        public void SetAlias(string watchFile, string key, string value)
        {
            JObject watchFileJson;

            using (StreamReader file = File.OpenText(watchFile))
            using (JsonTextReader reader = new JsonTextReader(file))
            {
                watchFileJson = (JObject)JToken.ReadFrom(reader);
            }

            bool found = false;

            foreach (JObject alias in watchFileJson["alias"])
            {
                if (alias["alias"].ToString().Equals(key))
                {
                    found = true;
                    alias["name"] = value;
                    break;
                }
            }

            if (!found)
            {
                var alias = JObject.Parse($"{{ \"name\": \"{key}\", \"alias\": \"{value}\"}}");
                watchFileJson["alias"].First.AddAfterSelf(alias);
            }

            using (StreamWriter file = File.CreateText(watchFile))
            using (JsonTextWriter writer = new JsonTextWriter(file))
            {
                watchFileJson.WriteTo(writer);
            }

            R("RELOAD_WATCH");
        }

        public void SetShortCode(string settingsFile, string key, string value)
        {
            JObject watchFileJson;

            using (StreamReader file = File.OpenText(settingsFile))
            using (JsonTextReader reader = new JsonTextReader(file))
            {
                watchFileJson = (JObject)JToken.ReadFrom(reader);
            }

            var found = false;

            foreach (JObject code in watchFileJson["shortcodes"])
            {
                if (code["key"].ToString().Equals(key))
                {
                    found = true;
                    code["value"] = value;
                    break;
                }
            }

            if (!found)
            {
                JObject item = new JObject();
                item.Add("key", key);
                item.Add("value", value);
                ((JArray)watchFileJson["shortcodes"]).Add(item);
            }

            using (StreamWriter file = File.CreateText(settingsFile))
            using (JsonTextWriter writer = new JsonTextWriter(file))
            { 
                watchFileJson.WriteTo(writer);
            }
        }

        public void DeleteShortCode(string settingsFile, string key)
        {
            JObject watchFileJson;

            using (StreamReader file = File.OpenText(settingsFile))
            using (JsonTextReader reader = new JsonTextReader(file))
            {
                watchFileJson = (JObject)JToken.ReadFrom(reader);
            }

            foreach (JObject code in watchFileJson["shortcodes"])
            {
                if (code["key"].ToString().Equals(key))
                {
                    code.Remove();
                    break;
                }
            }

            using (StreamWriter file = File.CreateText(settingsFile))
            using (JsonTextWriter writer = new JsonTextWriter(file))
            {
                watchFileJson.WriteTo(writer);
            }
        }

        public void ListShortCodes(string settingsFile)
        {
            JObject watchFileJson;

            using (StreamReader file = File.OpenText(settingsFile))
            using (JsonTextReader reader = new JsonTextReader(file))
            {
                watchFileJson = (JObject)JToken.ReadFrom(reader);
            }

            var cmdList = ((JArray)watchFileJson["shortcodes"]).ToObject<List<JToken>>();

            foreach (JObject code in cmdList.OrderBy(x => x["key"]).ToList())
            {
                M(enmMessageType.Info, $"{code["key"]} = {code["value"]}");
            }
        }

        public string GetShortCode(string settingsFile, string key)
        {
            var retval = string.Empty;
            JObject settingsFileJson;

            using (StreamReader file = File.OpenText(settingsFile))
            using (JsonTextReader reader = new JsonTextReader(file))
            {
                settingsFileJson = (JObject)JToken.ReadFrom(reader);
            }

            key = key.ToLower();

            foreach (JObject code in settingsFileJson["shortcodes"])
            {
                if (code["key"].ToString().ToLower().Equals(key))
                {
                    retval = code["value"].ToString();
                    break;
                }
            }

            return retval;
        }

        public void FeedDataset(string dataset, string columnName, int speedUp = 100)
        {
            if (IGClient.Pause)
            {
                var datasetFileName = $@".\{dataset}.csv";

                if (File.Exists(datasetFileName))
                {
                    R("DATASET_FOUND");

                    if (speedUp <= 0 || speedUp > 100)
                    {
                        R("WRONG_SPEED");
                    }
                    else
                    {
                        var info = new FileInfo(datasetFileName);
                        var lines = File.ReadLines(datasetFileName);

                        if (lines.Count() > 0)
                        {
                            R("DATASET_LINES", new List<string>() { lines.Count().ToString() });

                            var schemaString = lines.ToList()[0];
                            var schema = (JObject)JsonConvert.DeserializeObject(schemaString);

                            var split = lines.ToList()[1].Split(',');
                            var columnIndex = -1;
                            var timestampIndex = -1;
                            var timeDiffIndex = -1;

                            var fieldName = string.Empty;
                            var itemName = string.Empty;

                            for (var index = 0; index < schema["schemainstruments"].Count(); index++)
                            {
                                var token = schema["schemainstruments"].ElementAt(index);

                                if (token["Key"].ToString().ToLower() == columnName)
                                {
                                    fieldName = token["Value"].ToString();
                                    itemName = token["Name"].ToString();
                                    break;
                                }
                            }

                            if (!string.IsNullOrEmpty(fieldName) && !string.IsNullOrEmpty(itemName))
                            {
                                columnIndex = GetColumnIndex(columnName, split);

                                if (columnIndex == -1) R("COLUMN_NOT_FOUND");
                                else
                                {
                                    timestampIndex = GetColumnIndex("timestamp", split);
                                    timeDiffIndex = GetColumnIndex("timediff", split);

                                    var currentTimestamp = DateTimeOffset.Now.ToUnixTimeMilliseconds();
                                    var currentTimeDiff = 0;
                                    var subscription = (IGSubscriptionListener)IGClient.LSC.Subscriptions[0].Listeners[0];

                                    if (!subscription.CheckKeyExists(itemName, fieldName))
                                    {
                                        R("INVALID_EPIC");
                                    }
                                    else
                                    {
                                        try
                                        {
                                            for (var i = 2; i < lines.Count(); i++)
                                            {
                                                TT();

                                                var line = lines.ElementAt(i);
                                                split = line.Split(',');

                                                currentTimestamp = (timestampIndex >= 0 ? long.Parse(split[timestampIndex]) : currentTimestamp + 1000);

                                                var changedFields = new Dictionary<string, string>();
                                                changedFields.Add(fieldName, split[columnIndex]);

                                                currentTimeDiff = (timestampIndex >= 0 ? int.Parse(split[timeDiffIndex]) : 1000);

                                                if (currentTimeDiff > 60000)
                                                {
                                                    R("INVALID_TIMEDIFF", new List<string>() { Convert.ToInt32(currentTimeDiff / 1000).ToString() });
                                                    break;
                                                }

                                                Thread.Sleep((currentTimeDiff / 100 * speedUp));

                                                subscription.ExecuteUpdate(currentTimestamp, itemName, changedFields);

                                                if (Console.KeyAvailable && CC("Confirm you want to stop (Y/n)? ", 'Y'))
                                                {
                                                    break;
                                                }
                                            }
                                        }
                                        catch (Exception ex)
                                        {
                                            M(enmMessageType.Error, ex.Message);
                                        }
                                    }
                                }
                            }
                            else R("COLUMN_NOT_FOUND");
                        }
                        else R("DATASET NO LINES");
                    }
                }
            } else
                R("END_CAPTURE");
        }

        private int GetColumnIndex(string name, string[] columns, string found_msg = "FOUND", string notfound_msg = "NOTFOUND")
        {
            var retval = -1;

            for (var index = 0; index < columns.Length; index++)
            {
                var column = columns[index];
                var tempColumn = column.ToLower();

                if (tempColumn.Equals(name.ToLower()))
                {
                    retval = index;
                    break;
                }
            }

            if (retval == -1 && !string.IsNullOrEmpty(notfound_msg))
                R(notfound_msg, new List<string>() { name });
            else if (!string.IsNullOrEmpty(found_msg))
                R(found_msg, new List<string>() { name });

            return retval;
        }
    }
}