using System;
using System.Collections.Generic;
using System.Linq;
using static IGSB.IGClient;

namespace IGSB
{
    class Program
    {
        static public List<KeyValuePair<string, string>> GetArgs(string[] args)
        {
            var argsList = args.ToList<string>();

            var parsedArgs = argsList.Select(s => s).Select(s => s.Split(new[] { ':' })).ToDictionary(s => s[0].ToLower(), s => s[1]).ToList();
            return parsedArgs;
        }

        static void Main(string[] args)
        {
            Console.CursorVisible = false;

            try
            {
                var parsedArgs = GetArgs(args);
                if (!parsedArgs.Exists(x => x.Key == "ss") || !parsedArgs.Exists(x => x.Key == "wf")) Log.Message(IGClient.enmMessageType.Exit, "IGSB.Program ERROR: Missing some or all command line arguments\nss = Maps to source key in settings file (mandatory), case sensitive\nwf = Name of watch file to use (mandatory)\nst = Settings file name (optional, defaults to settings.json)\ncp = Password to use for encrypting settings file (optional, but will be prompted if not supplied)");

                var watchFile = parsedArgs.Single(x => x.Key == "wf").Value;
                var sourceKey = parsedArgs.Single(x => x.Key == "ss").Value;
                var appPassword = (parsedArgs.Exists(x => x.Key == "cp") ? parsedArgs.Single(x => x.Key == "cp").Value : "");
                var settingsFile = (parsedArgs.Exists(x => x.Key == "st") ? parsedArgs.Single(x => x.Key == "st").Value : "settings.json");

                if (string.IsNullOrEmpty(appPassword)) appPassword = Log.GetPassword($"Loading {watchFile} and source {sourceKey}, enter authentication password to confirm\nPassword: ");

                IGClient.Initialise(Log.Message, Log.KeyPressed, Log.Beep, Log.Response, Log.ConfirmText, Log.ConfirmChar);

                if (!IGClient.Authenticate(settingsFile, sourceKey, watchFile, appPassword))
                    Log.Message(IGClient.enmMessageType.Exit, $"Unable to authenticate and start");
                else
                {
                    Log.Message(enmMessageType.Info, "Type 'begin' or '/bc' to begin collecting data");
                    Log.Message(enmMessageType.Info, "Type 'help' or '/?' for available commands");

                    var commands = new Commands();

                    var @continue = true;

                    while (@continue)
                    {
                        try
                        {
                            var prompt = (IGClient.StreamDisplay == enmContinuousDisplay.None ? "$> " : "");
                            var cmd = Log.RL(prompt);

                            if (IGClient.StreamDisplay != enmContinuousDisplay.None)
                            {
                                IGClient.StreamDisplay = enmContinuousDisplay.None;
                                prompt = "$> ";
                                continue;
                            }

                            @continue = commands.CommandParse(cmd);
                        }
                        catch (Exception ex)
                        {
                            throw ex;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.RK($"EXCEPTION: {ex.Message}", false);
            }
        }
    }
}