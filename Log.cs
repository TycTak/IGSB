using NLog;
using System;
using System.Resources;
using System.Threading;
using static IGSB.IGClient;
// test

namespace IGSB
{
    class Log
    {
        static private readonly Logger log = LogManager.GetCurrentClassLogger();

        static public void Info(string message)
        {
            log.Info(message);
        }

        static public void Trace(string message)
        {
            log.Trace(message);
        }

        static public void Debug(string message)
        {
            log.Debug(message);
        }

        static public void Error(string message)
        {
            log.Error(message);
        }

        static public void Fatal(string message)
        {
            log.Fatal(message);
        }

        static public void Warn(string message)
        {
            log.Warn(message);
        }

        static public ConsoleKeyInfo RK(string message, bool displayCursor = true)
        {
            if (!string.IsNullOrEmpty(message))
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.Write(message);
                Console.ForegroundColor = ConsoleColor.White;
            }

            if (displayCursor) Console.CursorVisible = true;
            ConsoleKeyInfo cki = Console.ReadKey(displayCursor);

            if (displayCursor)
            {
                Console.CursorVisible = false;
                Console.WriteLine(cki.KeyChar);
            } else {
                Console.WriteLine("");
            }

            return cki;
        }

        static public string GetPassword(String message)
        {
            var password = string.Empty;

            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write(message);
            Console.ForegroundColor = ConsoleColor.White;
            ConsoleKeyInfo key;
            Console.CursorVisible = true;

            do
            {
                key = Console.ReadKey(true);

                if (!char.IsControl(key.KeyChar))
                {
                    password += key.KeyChar;
                    Console.Write("*");
                }
                else
                {
                    if (key.Key == ConsoleKey.Backspace && password.Length > 0)
                    {
                        password = password.Remove(password.Length - 1, 1);
                        Console.Write("\b \b");
                    }
                }
            }
            while (key.Key != ConsoleKey.Enter);

            Console.CursorVisible = false;

            Console.WriteLine("");

            return password;
        }

        static public void M(enmMessageType messageType, string message)
        {
            switch (messageType)
            {
                case enmMessageType.Info: Info(message); break;
                case enmMessageType.Fatal: Fatal(message); break;
                case enmMessageType.Warn: Warn(message); break;
                case enmMessageType.Error: Error(message); break;
                case enmMessageType.Debug: Debug(message); break;
                case enmMessageType.Trace: Trace(message); break;
                case enmMessageType.Exit:
                    Warn(message);
                    Log.RK("Press ANY key to exit", false);
                    Environment.Exit(1);
                    break;
                default:
                    Error(message);
                    break;
            }
        }

        static public void R(string code)
        {
            var rm = new ResourceManager(typeof(Language));
            var text = rm.GetString(code).Split(";");
            var message = text[1] + $" ({code})";

            var messageType = (enmMessageType)Enum.Parse(typeof(enmMessageType), text[0].Substring(0, 1).ToUpper() + text[0].ToLower().Substring(1));

            M(messageType, message);
        }

        static public bool ConfirmChar(string message, char accept)
        {
            var confirm = default(string);

            if (string.IsNullOrEmpty(message))
                confirm = "Are you sure? ";
            else
                confirm = message;

            var cki = Log.RK($"{confirm}");
            return (cki.Key.ToString().ToUpper().Equals(accept.ToString().ToUpper()));
        }

        static public bool ConfirmText(string message, string accept)
        {
            var confirm = default(string);

            if (string.IsNullOrEmpty(message))
                confirm = $"Are you sure ({accept})? ";
            else
                confirm = message;

            var cki = Log.RL($"{confirm}");
            return (cki.ToString().ToUpper().Equals(accept.ToString().ToUpper()));
        }

        static public void Beep()
        {
            new Thread(() => System.Media.SystemSounds.Beep.Play()).Start();
        }

        static public bool KeyPressed()
        {
            return Console.KeyAvailable;
        }

        static public string RL(string message)
        {
            var retval = default(string);

            if (!string.IsNullOrEmpty(message))
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.Write(message);
                Console.ForegroundColor = ConsoleColor.White;
            }

            Console.CursorVisible = true;
            retval = Console.ReadLine();
            Console.CursorVisible = false;

            return retval;
        }
    }
}
