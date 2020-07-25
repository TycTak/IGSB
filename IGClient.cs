using com.lightstreamer.client;
using ConsoleApp6ML.ConsoleApp;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;

namespace IGSB
{
    public class Security
    {
        public string CST { get; set; } = default(string);

        public string XST { get; set; } = default(string);

        public string APIKEY { get; set; } = default(string);
    }

    static class IGClient
    {
        public enum enmMessageType
        {
            Info,
            Warn,
            Error,
            Fatal,
            Exit,
            Trace
        }
        public enum enmContinuousDisplay
        {
            None,
            Subscription,
            Prediction,
            Dataset,
            DatasetAllColumns
        }

        public delegate void Message(enmMessageType messageType, string message);

        static public event Message M;

        public class Properties {

            public string SourceUrl { get; set; } = default(string);

            public string WebApiUrl { get; set; } = default(string);

            public string LightStreamerEndPoint { get; set; } = default(string);

            public DateTime Session { get; set; }
        }

        static public void Initialise(Message msgDelegate)
        {
            M += msgDelegate;
            BaseCodeLibrary.M += msgDelegate;
            Commands.M += msgDelegate;
        }

        static public bool Pause { get; set; }

        static public string Filter { get; set; } = default(string);

        static public enmContinuousDisplay StreamDisplay { get; set; } = enmContinuousDisplay.None;

        static public ModelBuilder ML { get; set; } = new ModelBuilder();

        static public string Model { get; set; }

        static public Properties Settings { get; set; }

        static public Security Authentication { get; set; }

        static public LightstreamerClient LSC { get; set; }
        
        static public WatchFile WatchFile { get; set; }

        static private string SaveSettings(string settingsFile, string password, JToken setting)
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

        static private string Token { get => "13135290-18CD-48FD-B04D-46F3A4687DF3"; }

        static private int CheckStrength(string password)
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
        static public JToken GetSettings(string settingsFile, string access)
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

        static public bool Authenticate(string settingsFile, string sourceKey, string watchFile, string password)
        {
            bool retval = false;

            M(enmMessageType.Info, "Authenticating access");

            if (CheckStrength(password) >= 4)
            {
                var setting = IGClient.GetSettings(settingsFile, sourceKey);

                var unencryptedToken = setting["token"].ToString();

                if (unencryptedToken.Equals(Token) || string.IsNullOrEmpty(unencryptedToken))
                {
                    unencryptedToken = SaveSettings(settingsFile, password, setting);
                }

                var token = EncryptionHelper.Decrypt(unencryptedToken, password);
                retval = token.Equals(Token);

                if (retval)
                {
                    M(enmMessageType.Info, "Initialising watch file");

                    InitialiseWatchList(watchFile);

                    var apikey = (setting["X-IG-API-KEY"] != null ? setting["X-IG-API-KEY"].ToString() : null);
                    var sourceUrl = (setting["sourceurl"] != null ? setting["sourceurl"].ToString() : null);
                    var identifier = EncryptionHelper.Decrypt((setting["identifier"] != null ? setting["identifier"].ToString() : null), password);
                    var sourcePassword = EncryptionHelper.Decrypt((setting["password"] != null ? setting["password"].ToString() : null), password);

                    if (string.IsNullOrEmpty(apikey) || string.IsNullOrEmpty(sourceUrl) || string.IsNullOrEmpty(identifier) || string.IsNullOrEmpty(password))
                    {
                        M(enmMessageType.Warn, $"Missing some config arguments in the [{settingsFile}] file: X-IG-API-KEY, sourceUrl, identifier, password");
                    } else
                    {
                        M(enmMessageType.Info, "Settings parameters seem ok");
                        retval = (GetSession(sourceUrl, identifier, sourcePassword, apikey) && InitialiseLSC() && InitialiseWEB());
                    }
                } else M(enmMessageType.Exit, "Password has failed");
            } else M(enmMessageType.Exit, "A weak password has been supplied");

            return retval;
        }

        static public bool GetSession(string sourceUrl, string identifier, string password, string apikey)
        {
            var retval = false;

            try
            {
                M(enmMessageType.Info, "Opening connection to session server");
                var client = new HttpClient();

                var sessionUrl = $"{sourceUrl}/gateway/deal/session";

                var values = new Dictionary<string, string>
                {
                    { "identifier", identifier },
                    { "password", password }
                };

                string content = JsonConvert.SerializeObject(values);
                var data = new StringContent(content, Encoding.UTF8, "application/json");

                client.DefaultRequestHeaders.Add("X-IG-API-KEY", apikey);

                var response = client.PostAsync(sessionUrl, data);
                var responseString = response.Result.Content.ReadAsStringAsync();

                M(enmMessageType.Info, "Response received");

                JObject json = JObject.Parse(responseString.Result);

                Authentication = new Security();
                Authentication.XST = response.Result.Headers.GetValues("X-SECURITY-TOKEN").First();
                Authentication.CST = response.Result.Headers.GetValues("CST").First();
                Authentication.APIKEY = apikey;

                M(enmMessageType.Info, "Session variables returned");

                Settings = new Properties();
                Settings.SourceUrl = sourceUrl;
                Settings.WebApiUrl = $"{sourceUrl}/gateway/deal";
                Settings.LightStreamerEndPoint = json["lightstreamerEndpoint"].ToString();
                Settings.Session = DateTime.Now;

                M?.Invoke(enmMessageType.Info, "Session setup");

                retval = true;
            }
            catch (Exception ex)
            {
                M?.Invoke(enmMessageType.Error, String.Format("IGClient.GetSession EXCEPTION: {0}", ex.Message));
            }

            return retval;
        }

        static public bool InitialiseWatchList(string watchFile)
        {
            var retval = false;
            JObject watchListJson;

            try {
                if (!File.Exists(watchFile))
                    M?.Invoke(enmMessageType.Exit, $"GClient.InitialiseWatchList: The watch config file [{watchFile}] does not exist");
                else
                {
                    M(enmMessageType.Info, $"Loading file {watchFile}");

                    using (StreamReader file = File.OpenText(watchFile))
                    using (JsonTextReader reader = new JsonTextReader(file))
                    {
                        watchListJson = (JObject)JToken.ReadFrom(reader);
                    }

                    M?.Invoke(enmMessageType.Info, $"Watch file {watchFile} loaded");

                    WatchFile = new WatchFile(watchFile, watchListJson);

                    M?.Invoke(enmMessageType.Info, $"Watch file {watchFile} initialised");
                }
            }
            catch (Exception ex)
            {
                M?.Invoke(enmMessageType.Exit, String.Format("IGClient.InitialiseWatchList EXCEPTION: {0}", ex.Message));
            }

            return retval;
        }

        static public bool InitialiseLSC()
        {
            var retval = false;

            M?.Invoke(enmMessageType.Info, "Streaming initialisation");

            if (!string.IsNullOrEmpty(Settings.LightStreamerEndPoint)
                && !string.IsNullOrEmpty(Authentication.CST)
                && !string.IsNullOrEmpty(Authentication.XST)
                && WatchFile != null)
            {
                M(enmMessageType.Info, $"All streaming variables available");

                LSC = new LightstreamerClient(Settings.LightStreamerEndPoint, null);
                LSC.connectionDetails.Password = "CST-" + Authentication.CST + "|XST-" + Authentication.XST;
                LSC.addListener(new IGClientListener());

                M(enmMessageType.Info, $"Connecting to streamer server");

                LSC.connect();

                if (WatchFile.MergeCaptureList.Count > 0)
                {
                    var subs = new Subscription(
                        "MERGE",
                        WatchFile.MergeCaptureList.ToArray(),
                        WatchFile.MergeFieldList.ToArray()
                    );

                    LSC.subscribe(subs);
                    var listener = new IGSubscriptionListener(WatchFile);
                    listener.M += Log.M;
                    subs.addListener(listener);

                    M(enmMessageType.Info, $"Subscribed to MERGE");
                }

                if (WatchFile.DistinctCaptureList.Count > 0)
                {
                    var subs = new Subscription(
                        "DISTINCT",
                        WatchFile.DistinctCaptureList.ToArray(),
                        WatchFile.DistinctFieldList.ToArray()
                    );

                    LSC.subscribe(subs);
                    subs.addListener(new IGSubscriptionListener(WatchFile));

                    M(enmMessageType.Info, $"Subscribed to TICK");
                }

                if (WatchFile.ChartCaptureList.Count > 0)
                {
                    var subs = new Subscription(
                        "MERGE",
                        WatchFile.ChartCaptureList.ToArray(),
                        WatchFile.ChartFieldList.ToArray()
                    );

                    LSC.subscribe(subs);
                    subs.addListener(new IGSubscriptionListener(WatchFile));

                    M(enmMessageType.Info, $"Subscribed to CHART");
                }

                M?.Invoke(enmMessageType.Info, "Streamer subscribers and listeners started");

                retval = true;
            } else
                M?.Invoke(enmMessageType.Exit, "Streamer session parameters not found");

            return retval;
        }

        static public bool InitialiseWEB()
        {
            var retval = true;
            M(enmMessageType.Info, $"WEB API initialised");
            return retval;
        }
    }
}
