using System;

namespace IGSB
{
    public interface IPropertyEventDispatcher
    {
        void BeginInvoke(Action a);

        void addEventMessage(string message);
    }
}