using Akka.Dispatch.SysMsg;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Tensorflow.Summaries;
using static IGSB.IGClient;

namespace IGSB
{
    public class Commands
    {
        static public event Message M;
        static public event Response R;
        static public event ConfirmText CT;
        static public event ConfirmChar CC;

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

        public bool CommandParse(string command)
        {
            var @continue = true;

            var args = command.Trim().Split(' ');

            if (args.Length > 0 && args[0].StartsWith("/") && args[0].Length > 2)
            {
                var cmds = "[/ty][/em][/ec][/bc][/se][/cs][/cl][/su][/ca][/cd]";

                if (!cmds.Contains(args[0]))
                {
                    var len = (args[0].Length - 1);
                    var tmpArgs = new String[len + (args.Length - 1)];
                    Array.Copy(args, 1, tmpArgs, len, args.Length - 1);
                    tmpArgs[0] = args[0].Substring(0, 2);
                    tmpArgs[1] = args[0].Substring(2, 1);
                    args = tmpArgs;
                }
            }

            var cmdArgs = args.ToList<string>();

            switch (cmdArgs[0].ToLower())
            {
                case "":
                    break;
                case "/?":
                case "help":
                    if (Validate("", cmdArgs)) Help();
                    break;
                case "/ty":
                case "type":
                    if (Validate("MS;ON;O~^-a$", cmdArgs)) TypeOut(cmdArgs[1], 0, Convert.ToInt32(cmdArgs[2]), cmdArgs[3] == "-a");
                    break;
                case "/r":
                case "reload":
                    if (Validate("MS;OS", cmdArgs)) Reload(cmdArgs[1], cmdArgs[2]);
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
                    if (Validate("MS;MS;MN", cmdArgs, false))
                        Process(cmdArgs[0], cmdArgs[1], cmdArgs[2], double.Parse(cmdArgs[3]));
                    else if (Validate("MS", cmdArgs))
                        Process(cmdArgs[0], cmdArgs[1]);
                    break;
                case ">":
                    if (!IGClient.Pause)
                    {
                        if (Validate("", cmdArgs))
                        {
                            IGClient.StreamDisplay = enmContinuousDisplay.Subscription;
                            R($"<SUBSCRIPTION>, <CONTINUOUS_DISPLAY>{(string.IsNullOrEmpty(IGClient.Filter) ? "" : $", <FILTERED_ON>[{IGClient.Filter}]")} >");
                        }
                    }
                    else
                        R("NEED_TO_RESTART");
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

                                IGClient.StreamDisplay = (includeAllColumns ? enmContinuousDisplay.DatasetAllColumns : enmContinuousDisplay.Dataset);
                                M(enmMessageType.Info, $"DATASET, continuous display mode{(string.IsNullOrEmpty(IGClient.Filter) ? "" : $", filtered on [{IGClient.Filter}]")} >>");
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
                                M(enmMessageType.Info, $"PREDICTION, continuous display mode{(string.IsNullOrEmpty(IGClient.Filter) ? "" : $", filtered on [{IGClient.Filter}]")} >>>");
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
                        GetAliases();
                    } else
                    {
                        switch (cmdArgs[1].ToLower())
                        {
                            case "s":
                            case "set":
                                //var isReload = (cmdArgs[2] == "-r");
                                if (Validate("MS;MS;MS;O~^-r$", cmdArgs)) SetAlias(IGClient.WatchFileName, $"{cmdArgs[2]}", $"{cmdArgs[3]}");
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
                                if (Validate("MS;MS;O~^-d|-a$;O~^-d|-a$", cmdArgs)) SaveDataSet(cmdArgs[2], (cmdArgs[3] == "-d" || cmdArgs[4] == "-d"), (cmdArgs[3] == "-a" || cmdArgs[4] == "-a"));
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
                    R("UNKNOWN");
                    break;
            }

            return @continue;
        }

        public bool Authenticate(string settingsFile, string sourceKey, string watchFile, string password)
        {
            bool retval = false;

            M(enmMessageType.Info, "Authenticating access");

            if (CheckStrength(password) >= 4)
            {
                settingsFile = (string.IsNullOrEmpty(settingsFile) ? SettingsFile : settingsFile);
                sourceKey = (string.IsNullOrEmpty(sourceKey) ? SourceKey : sourceKey);
                watchFile = (string.IsNullOrEmpty(watchFile) ? WatchFileName : watchFile);

                var setting = GetSettings(settingsFile, sourceKey);

                var unencryptedToken = setting["token"].ToString();

                if (unencryptedToken.Equals(Token) || string.IsNullOrEmpty(unencryptedToken))
                {
                    unencryptedToken = SaveSettings(settingsFile, password, setting);
                }

                var token = EncryptionHelper.Decrypt(unencryptedToken, password);
                retval = token.Equals(Token);

                if (retval)
                {
                    SourceKey = sourceKey;
                    SettingsFile = settingsFile;
                    WatchFileName = watchFile;

                    M(enmMessageType.Info, "Initialising watch file");

                    if (InitialiseWatchList(watchFile))
                    {
                        var apikey = (setting["X-IG-API-KEY"] != null ? setting["X-IG-API-KEY"].ToString() : null);
                        var sourceUrl = (setting["sourceurl"] != null ? setting["sourceurl"].ToString() : null);
                        var identifier = EncryptionHelper.Decrypt((setting["identifier"] != null ? setting["identifier"].ToString() : null), password);
                        var sourcePassword = EncryptionHelper.Decrypt((setting["password"] != null ? setting["password"].ToString() : null), password);

                        if (string.IsNullOrEmpty(apikey) || string.IsNullOrEmpty(sourceUrl) || string.IsNullOrEmpty(identifier) || string.IsNullOrEmpty(password))
                        {
                            M(enmMessageType.Error, $"Missing some config arguments in the [{settingsFile}] file: X-IG-API-KEY, sourceUrl, identifier, password");
                        }
                        else
                        {
                            M(enmMessageType.Info, "Settings parameters seem ok");
                            retval = (GetSession(sourceUrl, identifier, sourcePassword, apikey) && InitialiseLSC() && InitialiseWEB());
                        }
                    }
                    else retval = false;
                }
                else M(enmMessageType.Error, "Password has failed");
            }
            else M(enmMessageType.Error, "A weak password has been supplied");

            return retval;
        }

        private int CheckStrength(string password)
        {
            int score = 0;

            M(enmMessageType.Info, "Checking password strength");

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

            return score;
        }

        public bool ChangePassword(string settingsFile, string sourceKey, string existingPassword, string newPassword)
        {
            bool retval = false;

            if (CheckStrength(existingPassword) >= 4 && CheckStrength(newPassword) >= 4)
            {
                settingsFile = (string.IsNullOrEmpty(settingsFile) ? SettingsFile : settingsFile);
                sourceKey = (string.IsNullOrEmpty(sourceKey) ? SourceKey : sourceKey);

                var setting = GetSettings(settingsFile, sourceKey);

                var token = setting["token"].ToString();

                if (token.Equals(Token) || string.IsNullOrEmpty(token))
                {
                    token = SaveSettings(settingsFile, newPassword, setting);
                }
                else
                {
                    var checkExistingPassword = EncryptionHelper.Decrypt(setting["token"].ToString(), existingPassword);

                    if (checkExistingPassword.Equals(Token))
                    {
                        setting["token"] = EncryptionHelper.Decrypt(setting["token"].ToString(), existingPassword);
                        setting["identifier"] = EncryptionHelper.Decrypt(setting["identifier"].ToString(), existingPassword);
                        setting["password"] = EncryptionHelper.Decrypt(setting["password"].ToString(), existingPassword);

                        token = SaveSettings(settingsFile, newPassword, setting);
                    }
                    else
                    {
                        token = string.Empty;
                    }
                }

                var unencryptedToken = EncryptionHelper.Decrypt(token, newPassword);
                retval = unencryptedToken.Equals(Token);

                if (retval)
                {
                    M(enmMessageType.Info, $"Password changed");
                    M(enmMessageType.Warn, "NB * You must now change the password you\nuse to login to using that watchfile.");
                }
                else
                {
                    M(enmMessageType.Error, $"Unable to change password");
                }
            }
            else M(enmMessageType.Error, "Invalid password");

            return retval;
        }

        public JToken GetSettings(string settingsFile, string access)
        {
            JObject settings;

            M(enmMessageType.Info, "Loading settings");

            using (StreamReader file = File.OpenText(settingsFile))
            using (JsonTextReader reader = new JsonTextReader(file))
            {
                settings = (JObject)JToken.ReadFrom(reader);
            }

            return settings["sources"].SelectToken("$[?(@.key == '" + access + "')]");
        }

        private string SaveSettings(string settingsFile, string password, JToken setting)
        {
            JObject settings;

            M(enmMessageType.Info, "Encrypting settings file");

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

        public void GetAliases()
        {
            foreach (var alias in IGClient.WatchFile.Alias)
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
                valueCounts.Add(schema.SchemaName, schema.CodeLibrary.Values.Count());
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

            if (json != null)
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

            foreach (var schema in IGClient.WatchFile.Schemas)
            {
                var formulae = schema.SchemaInstruments.FindAll(x => x.Type == WatchFile.SchemaInstrument.enmType.formula);

                foreach (var formula in formulae)
                {
                    fm += (!fm.Contains(formula.Name) ? (string.IsNullOrEmpty(fm) ? formula.Name : $", {formula.Name}") : "");
                }

                foreach (var column in schema.SchemaInstruments)
                {
                    co += (string.IsNullOrEmpty(co) ? column.Key : $", {column.Key}");
                    co += (column.IsColumn ? "" : "^");
                }
            }

            for (var i = 0; i < IGClient.LSC.Subscriptions.Count; i++)
            {
                var subscriber = IGClient.LSC.Subscriptions[i];
                foreach (var item in subscriber.Items)
                {
                    items += (string.IsNullOrEmpty(items) ? $"#{i}-{item}" : $", #{i}-{item}");
                }

                foreach (var field in subscriber.Fields)
                {
                    fields += (string.IsNullOrEmpty(fields) ? $"#{i}-{field}" : $", #{i}-{field}");
                }
            }

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

                foreach (var position in open.Response["positions"])
                {
                    M(enmMessageType.Info, $"{GetAlias(position["market"]["epic"].ToString(), false),-10}     {position["market"]["epic"].ToString(),-25}  {position["position"]["size"]:N1}    {Convert.ToDateTime(position["position"]["createdDate"].ToString().Substring(0, 19)):s}   {position["position"]["direction"],-10}  {position["position"]["dealId"].ToString(),-20}   {position["position"]["dealReference"].ToString(),-20}   {position["position"]["currency"]}        {String.Format("{0:0.00}", position["position"]["level"])} >> {String.Format("{0:0.00}", position["position"]["profit"])}");
                }
            }
        }

        public void Reload(string password, string watchFile)
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

        public void TypeOut(string schema, int start = 1, int rows = 10, bool includeAllColumns = false)
        {
            var schemaObj = GetSchema(schema);

            if (schemaObj != null)
            {
                M(enmMessageType.Info, GetHeader(schemaObj, includeAllColumns));

                var max = schemaObj.CodeLibrary.Values.Count;
                start = (start <= 0 ? (max - Math.Min(rows, max) + 1) : start);
                var end = Math.Min((start + rows), max);

                for (var i = (start - 1); i < end; i++)
                {
                    var record = schemaObj.CodeLibrary.Values[i];

                    var complete = !record.Values.Any(x => string.IsNullOrEmpty(x.Value));

                    if (complete)
                    {
                        var line = BaseCodeLibrary.GetDatasetRecord(record, schemaObj.SchemaInstruments, includeAllColumns, false);
                        M(enmMessageType.Info, line);
                    }
                }
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
                    M(enmMessageType.Info, "Description                             | Profit         | Type    | Date       | DealId                                     | Size");

                    foreach (var transaction in transactions.Response["transactions"])
                    {
                        var profit = transaction["profitAndLoss"].ToString().Substring(1); //, transaction["profitAndLoss"].ToString().ToString().Length - 1);
                        M(enmMessageType.Info, $"{transaction["instrumentName"].ToString(),-40}  {String.Format("{0:0.00}", profit),-15}  {transaction["transactionType"]}      {transaction["date"].ToString():s}     {transaction["reference"],-43}  {transaction["size"].ToString():N1}");
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

                foreach (var found in search.Response["markets"])
                {
                    M(enmMessageType.Info, $"{found["epic"].ToString(),-25}  {found["instrumentName"].ToString(),-40}");
                }
            }
        }

        public void Process(string direction, string instrument)
        {
            Process(direction, instrument, string.Empty, 0d);
        }

        public void Process(string direction, string instrument, string currency, double size)
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

        public void Help()
        {
            M(enmMessageType.Info, "closeall = Close ALL open positions (/ca)");
            M(enmMessageType.Info, "close <dealId> = Close a specific position using its dealId (/cd)");
            M(enmMessageType.Info, "model load <model> = Load specific model (/ml)");
            M(enmMessageType.Info, "model delete <model> = Delete specific model (/md)");
            M(enmMessageType.Info, "model copy <model> <newmodel> = Copy specific model (/mc)");
            M(enmMessageType.Info, "model rename <model> <newmodel> = Rename model (/mr)");
            M(enmMessageType.Info, "model info [<schema|wildcards>] = Get additional information for models (/mi)");
            M(enmMessageType.Info, "model bin = Close currently loaded model (/mb)");
            M(enmMessageType.Info, "model save <model> = Save current model to specific model name (/ms)");
            M(enmMessageType.Info, "model = Lists all models (/m)");
            M(enmMessageType.Warn, "train info <dataset> = Display last training results for dataset (/ti)");
            M(enmMessageType.Info, "train <dataset> <predict> [columns|-c] = Train model from combinations in dataset to predict a specific column, -c uses all columns in dataset only (/t)");
            M(enmMessageType.Info, "eval <dataset> = Evaluate and check your predictions using current model and specific dataset (/e)");
            M(enmMessageType.Info, "begin = Begin collection of data (/bc)");
            M(enmMessageType.Info, "end = End collection of data (/ec)");
            M(enmMessageType.Info, "info [<days>] = Display todays transactions or transaction for last number of days, max 100 (/i)");
            M(enmMessageType.Info, "dataset save <schema> [-d] [-a] = Save dataset for specific schema, -d stops automatic date/time naming, -a includes hidden columns (/ds)");
            M(enmMessageType.Info, "dataset delete <schema|wildcards> = Delete dataset schema (/dd)");
            M(enmMessageType.Info, "dataset copy <schema> <newschema> = Copy dataset schema (/dc)");
            M(enmMessageType.Info, "dataset rename <schema> <newschema> = Rename dataset schema (/dr)");
            M(enmMessageType.Info, "dataset info [<schema|wildcards>] = Get additional information for schema (/di)");
            M(enmMessageType.Info, "dataset headings [<schema|wildcards>] = Get column information for schema (/dh)");
            M(enmMessageType.Info, "dataset = List all dataset schemas (/d)");
            M(enmMessageType.Info, "open = Display all current open positions (/o)");
            M(enmMessageType.Info, "search <value> = Locate an instrument name and return all matches (/se)");
            M(enmMessageType.Info, "summary = Count of values stored across all schemas (/su)");
            M(enmMessageType.Info, "buy <instrument> <size> <currency> = Buy a specific epic (/b)");
            M(enmMessageType.Info, "buy <instrument> (/b)");
            M(enmMessageType.Info, "sell <instrument> <size> <currency> = Sell a specific epic (/s)");
            M(enmMessageType.Info, "sell <instrument> = Sell a specific instrument (/s)");
            M(enmMessageType.Info, "get <instrument> = Get details on a specific instrument (/g)");
            M(enmMessageType.Info, "cls = Clear screen (/cs)");
            M(enmMessageType.Info, "> = Show continuous subscription");
            M(enmMessageType.Info, ">> <schema> [-a] = Show continuous dataset for specific schema, -a include all columns");
            M(enmMessageType.Info, ">>> <schema> = Show continuous prediction for specific schema");
            M(enmMessageType.Info, "type <schema> <rows> [-a] = Type out last rows, -a include all columns (/ty)");
            M(enmMessageType.Info, "watch = Display watch file details (/w)");
            M(enmMessageType.Info, "client = Display client connection details (/cl)");
            M(enmMessageType.Info, "empty <schema> = Clear all captured records for a schema (/em)");
            M(enmMessageType.Info, "reload <password> [watchfilepath] = Reload a watch file or load a new watch file (/r)");
            M(enmMessageType.Info, "filter [<text>] = Filter continuous list by specific text, or clear existing filter (/f)");
            M(enmMessageType.Info, "passwd <existingpassword> <newpassword> = Change password for settings and source (/p)");
            M(enmMessageType.Info, "exit = Exit application (/x)");
            M(enmMessageType.Info, "alias = List of aliases (/a)");

            M(enmMessageType.Info, "alias set <alias> <value> -r = Change a value for an alias in the current watch file, -r specifies that you reload the watch file (/as)");
            M(enmMessageType.Info, "log = List all log files (/l)");
            M(enmMessageType.Info, "log type <lines> <logfile> = Type last specified number of lines (max 250) of specified log file (/lt)");
            M(enmMessageType.Info, "log delete <logfile> = Delete a specific log file (/ld)");
            M(enmMessageType.Info, "log copy <logfile> <newlogfile> = Copy a specific log file (/ld)");
            M(enmMessageType.Info, "log rename <logfile> <newlogfile> = Rename a specific log file (/lr)");
            M(enmMessageType.Info, "log info [<logfile|wildcards>] = Get additional information for log files (/li)");
        }

        public void InfoDataSet(string schema)
        {
            schema = (string.IsNullOrEmpty(schema) ? "*" : schema);

            var schemas = Directory.GetFiles(@".\", $"{schema}.csv");

            if (schemas.Length > 0)
            {
                foreach (var x in schemas)
                {
                    var info = new FileInfo(x);
                    var lines = File.ReadLines(x).Count();
                    M(enmMessageType.Info, $"{x.Substring(2, x.Length - 2 - 4).PadRight(30), -30}  {string.Format("{0:0}", (info.Length < 1024 ? 1 : (info.Length / 1024))), 5}k  {lines, 10} line{(lines > 1 ? "s" : " ")}   {info.CreationTimeUtc}");
                }
            }
            else R("NO_DATASET");
        }

        public void HeadingsDataSet(string schema)
        {
            schema = (string.IsNullOrEmpty(schema) ? "*" : schema);

            var schemas = Directory.GetFiles(@".\", $"{schema}.csv");

            if (schemas.Length > 0)
            {
                foreach (var x in schemas)
                {
                    var info = new FileInfo(x);
                    var lines = File.ReadLines(x);
                    if (lines.Count() > 0)
                    {
                        var split = lines.ToList()[0].Split(',');
                        foreach(var column in split)
                        {
                            M(enmMessageType.Info, $"{x.Substring(2, x.Length - 2 - 4)} - {column}");
                        }
                    }
                }
            }
            else R("NO_DATASET");
        }

        public void DeleteDataSet(string schema)
        {
            var schemas = Directory.GetFiles(@".\", $"{schema}.csv");

            if (schemas.Length > 0)
            {
                foreach (var x in schemas)
                {
                    File.Delete(x);
                    var schemaName = x.Substring(2, x.Length - 2 - 4);
                    M(enmMessageType.Info, $"Deleted dataset [{schemaName}]");
                }
            }
            else R("NO_DATASET");
        }

        public void RenameDataSet(string schema, string newSchema)
        {
            if (File.Exists($@".\{schema}.csv") && !File.Exists($@".\{newSchema}.csv"))
            {
                File.Move($@".\{schema}.csv", $@".\{newSchema}.csv");
                M(enmMessageType.Info, $"Renamed dataset [{schema}] to [{newSchema}]");
            }
            else R("NO_SCHEMA_FOUND");
        }

        public void CopyDataSet(string schema, string newSchema)
        {
            if (File.Exists($@".\{schema}.csv") && !File.Exists($@".\{newSchema}.csv"))
            {
                File.Copy($@".\{schema}.csv", $@".\{newSchema}.csv");
                M(enmMessageType.Info, $"Copied dataset [{schema}] to [{newSchema}]");
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

            foreach (var instrument in schema.SchemaInstruments)
            {
                if ((instrument.IsColumn || includeAllColumns) && (!includePredicted || instrument.IsPredict))
                {
                    header += (string.IsNullOrEmpty(header) ? instrument.Key : $",{instrument.Key}{(!instrument.IsColumn ? "^" : "")}");
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

        public void SaveDataSet(string schema, bool noDateTimeNaming, bool includeAllColumns)
        {
            var alreadyPaused = IGClient.Pause;
            var outputSchemaName = (noDateTimeNaming ? $"{schema}" : $"{schema}_{DateTime.Now.ToString("yyyyMMdd_HHmmss")}");

            var schemaObj = GetSchema(schema);

            if (schemaObj != null)
            {
                IGClient.Pause = (alreadyPaused ? alreadyPaused : true);

                if (File.Exists(outputSchemaName)) File.Delete(outputSchemaName);

                using (System.IO.StreamWriter file = new System.IO.StreamWriter($"{outputSchemaName}.csv"))
                {
                    file.WriteLine(GetHeader(schemaObj, includeAllColumns));

                    foreach (var record in schemaObj.CodeLibrary.Values)
                    {
                        var complete = !record.Values.Any(x => string.IsNullOrEmpty(x.Value));

                        if (complete && !record.Values["completed"].Contains("X"))
                        {
                            var line = BaseCodeLibrary.GetDatasetRecord(record, schemaObj.SchemaInstruments, includeAllColumns, false);

                            file.WriteLine(line);
                        }
                    }

                    M(enmMessageType.Info, $"Saved dataset schema [{schema}] to [{outputSchemaName}]");
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
                foreach (var x in logFiles)
                {
                    var info = new FileInfo(x);
                    var lines = File.ReadLines(x).Count();
                    M(enmMessageType.Info, $"{x.Substring(2, x.Length - 2 - 4).PadRight(30),-30}  {string.Format("{0:0}", (info.Length < 1024 ? 1 : (info.Length / 1024))),5}k  {lines,10} line{(lines > 1 ? "s" : " ")}   {info.CreationTimeUtc}");
                }
            }
            else R("NO_LOGFILE");
        }


        public void DeleteLogFile(string logFile)
        {
            var logfiles = Directory.GetFiles(@".\", $"{logFile}.log");

            if (logfiles.Length > 0)
            {
                foreach (var x in logfiles)
                {
                    File.Delete(x);
                    var logFileName = x.Substring(2, x.Length - 2 - 4);
                    M(enmMessageType.Info, $"Deleted log file [{logFileName}]");
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

        public void SetAlias(string watchFile, string aliasKey, string value)
        {
            JObject watchFileJson;

            using (StreamReader file = File.OpenText(watchFile))
            using (JsonTextReader reader = new JsonTextReader(file))
            {
                watchFileJson = (JObject)JToken.ReadFrom(reader);
            }

            foreach (JObject alias in watchFileJson["alias"])
            {
                if (alias["alias"].ToString().Equals(aliasKey))
                {
                    alias["name"] = value;
                    break;
                }
            }

            using (StreamWriter file = File.CreateText(watchFile))
            using (JsonTextWriter writer = new JsonTextWriter(file))
            {
                watchFileJson.WriteTo(writer);
            }
        }
    }
}
