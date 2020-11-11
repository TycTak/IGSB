using System;
using System.Collections.Generic;

namespace IGSBShared
{
    public class Delegates
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
            Highlight,
            NoLine
        }

        public delegate void Message(enmMessageType messageType, string message);
        public delegate void Response(string code, List<string> args = null);
        public delegate bool ConfirmText(string message, string accept);
        public delegate bool ConfirmChar(string message, char accept);
        public delegate void TickTock(int total, int current);
        public delegate bool BreakProcess();
        public delegate void Beep();
    }
}
