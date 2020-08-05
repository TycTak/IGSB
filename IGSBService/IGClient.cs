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

    static public class IGClient
    {
        public enum enmMessageType
        {
            Info,
            Warn,
            Error,
            Fatal,
            Exit,
            Debug,
            Trace,
            Highlight
        }

        public enum enmContinuousDisplay
        {
            None,
            Subscription,
            Prediction,
            Dataset,
            DatasetAllColumns
        }

        public enum enmBeep
        {
            OneShort,
            TwoShort
        }

        static public int C { get; set; }

        public delegate void Message(enmMessageType messageType, string message);
        public delegate void Response(string code, List<string> args = null);
        public delegate bool ConfirmText(string message, string accept);
        public delegate bool ConfirmChar(string message, char accept);
        public delegate bool BreakProcess();
        public delegate void Beep();

        static public event Message M;

        public class Properties {

            public string SourceUrl { get; set; } = default(string);

            public string WebApiUrl { get; set; } = default(string);

            public string LightStreamerEndPoint { get; set; } = default(string);

            public DateTime Session { get; set; }
        }

        static public void Initialise(Message msgDelegate, BreakProcess breakDelegate, Beep beepDelegate, Response responseDelegate, ConfirmText confirmDelegate, ConfirmChar characterDelegate)
        {
            if (msgDelegate != null)
            {
                M += msgDelegate;
                BaseCodeLibrary.M += msgDelegate;
                BaseCodeLibrary.B += beepDelegate;
                Commands.M += msgDelegate;
                Commands.R += responseDelegate;
                Commands.CT += confirmDelegate;
                Commands.CC += characterDelegate;
                IGClientListener.M += msgDelegate;
                IGSubscriptionListener.M += msgDelegate;
                WatchFile.M += msgDelegate;
                ModelBuilder.M += msgDelegate;
                ModelBuilder.C += confirmDelegate;
                ModelBuilder.P += breakDelegate;
            }

            M(enmMessageType.Info, "Machine Learning IG Spread Betting");
            M(enmMessageType.Info, "TycTak Ltd (c) 2020");
            M(enmMessageType.Info, "IGSB.Program: Starting service");
        }

        static public bool Pause { get; set; } = true;

        static public DateTime Started { get; set; }

        static public string Filter { get; set; } = default(string);

        static public string SchemaFilterName { get; set; } = default(string);

        static public enmContinuousDisplay StreamDisplay { get; set; } = enmContinuousDisplay.None;

        static public ModelBuilder ML { get; set; } = new ModelBuilder();

        static public string Model { get; set; }

        static public Properties Settings { get; set; }

        static public string SettingsFile { get; set; }

        static public string WatchFileName { get; set; }

        static public string SourceKey { get; set; }

        static public Security Authentication { get; set; }

        static public LightstreamerClient LSC { get; set; }
        
        static public WatchFile WatchFile { get; set; }

        static public string Token { get => "13135290-18CD-48FD-B04D-46F3A4687DF3"; }

        static public bool Authenticate(string settingsFile, string sourceKey, string watchFile, string password)
        {
            var commands = new Commands();
            return commands.Authenticate(settingsFile, sourceKey, watchFile, password);
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
                    M?.Invoke(enmMessageType.Error, $"GClient.InitialiseWatchList: The watch config file [{watchFile}] does not exist");
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

                    retval = true;
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

                if (LSC != null)
                {
                    if (!string.IsNullOrEmpty(LSC.Status) && LSC.Status.StartsWith("CONNECTED")) LSC.disconnect();

                    foreach (var subscriber in LSC.Subscriptions)
                    {
                        foreach (var listener in subscriber.Listeners)
                        {
                            subscriber.removeListener(listener);
                        }
                    }

                    LSC.Listeners.Clear();
                    LSC.Subscriptions.Clear();
                }

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
