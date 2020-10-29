using com.lightstreamer.client;
using System;
using static IGSB.IGClient;

namespace IGSB
{
    class IGClientListener : ClientListener
    {
        static public event Message M;

        public void onListenEnd(LightstreamerClient client)
        {
            M(enmMessageType.Debug, "IGClientListener.onListenEnd");
        }

        public void onListenStart(LightstreamerClient client)
        {
            M(enmMessageType.Debug, "IGClientListener.onListenStart");
        }

        public void onServerError(int errorCode, string errorMessage)
        {
            M(enmMessageType.Debug, String.Format("IGClientListener.onListenStart FAILED: {0} - {1}", errorCode, errorMessage));
            M(enmMessageType.Error, String.Format("IGClientListener.onListenStart FAILED: {0} - {1}", errorCode, errorMessage));
        }

        public void onStatusChange(string status)
        {
            M(enmMessageType.Debug, String.Format("IGClientListener.onStatusChange: {0}", status));
        }

        public void onPropertyChange(string property)
        {
            M(enmMessageType.Debug, String.Format("IGClientListener.onPropertyChange: {0}", property)); 
        }
    }
}