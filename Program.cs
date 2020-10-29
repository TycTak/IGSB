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
                if (!parsedArgs.Exists(x => x.Key == "ss") || !parsedArgs.Exists(x => x.Key == "wf")) Log.Response("MISSING_ARGS");

                var watchFile = parsedArgs.Single(x => x.Key == "wf").Value;
                var sourceKey = parsedArgs.Single(x => x.Key == "ss").Value;
                var appPassword = (parsedArgs.Exists(x => x.Key == "cp") ? parsedArgs.Single(x => x.Key == "cp").Value : "");
                var settingsFile = (parsedArgs.Exists(x => x.Key == "st") ? parsedArgs.Single(x => x.Key == "st").Value : "settings.json");

                if (string.IsNullOrEmpty(appPassword)) appPassword = Log.GetPassword("GET_PASSWORD", new List<string>() { watchFile, sourceKey });

                IGClient.Initialise(Log.Message, Log.KeyPressed, Log.Beep, Log.Response, Log.ConfirmText, Log.ConfirmChar, Log.TickTock);

                if (!IGClient.Authenticate(settingsFile, sourceKey, watchFile, appPassword))
                    Log.Response("NO_AUTHENTICATION");
                else
                {
                    Log.Response("START");
                    Log.Response("HELP");

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