using com.lightstreamer.client;
using System;
using static IGSB.IGClient;
using static IGSBShared.Delegates;

namespace IGSB
{
    class IGClientListener : ClientListener
    {
        static public event Message M;

        public void onListenEnd(LightstreamerClient client)
        {
            M(enmMessageType.Debug, "IGClientListener.onListenEnd date={DateTime.Now}, status={client.Status}, listeners={client.Listeners.Count}, subscriptions={client.Subscriptions.Count}");
        }

        public void onListenStart(LightstreamerClient client)
        {
            M(enmMessageType.Debug, $"IGClientListener.onListenStart date={DateTime.Now}, status={client.Status}, listeners={client.Listeners.Count}, subscriptions={client.Subscriptions.Count}");
        }

        public void onServerError(int errorCode, string errorMessage)
        {
            M(enmMessageType.Debug, $"IGClientListener.onListenStart FAILED: {errorCode} - {errorMessage}");
            M(enmMessageType.Error, $"IGClientListener.onListenStart FAILED: {errorCode} - {errorMessage}");
        }

        public void onStatusChange(string status)
        {
            M(enmMessageType.Debug, $"IGClientListener.onStatusChange: status={status}");
        }

        public void onPropertyChange(string property)
        {
            M(enmMessageType.Debug, $"IGClientListener.onPropertyChange: {property}"); 
        }
    }
}