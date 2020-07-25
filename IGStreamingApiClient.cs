using Lightstreamer.DotNet.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IGSB
{
    // https://lightstreamer.com/docs/client_silverlight_api/frames.html?frmname=topic&frmfile=00111.html
    public class IGStreamingApiClient : IConnectionListener
    {
        private LSClient lsClient;

        public IGStreamingApiClient()
        {
            lsClient = new LSClient();
        }

        public void OnActivityWarning(bool warningOn)
        {
            Log.Trace(String.Format("IGStreamingApiClient.OnActivityWarning: {0}", warningOn));
        }

        public void OnClose()
        {
            Log.Trace("IGStreamingApiClient.OnClose");
        }

        public virtual void OnConnectionEstablished()
        {
            Log.Trace("IGStreamingApiClient.OnConnectionEstablished");
        }

        public void OnDataError(PushServerException e)
        {
            Log.Trace(String.Format("IGStreamingApiClient.OnDataError.PushServerException: {0}", e.Message));
        }

        public void OnEnd(int cause)
        {
            Log.Trace(String.Format("IGStreamingApiClient.OnEnd: {0}", cause));
        }

        public void OnFailure(PushServerException e)
        {
            Log.Trace(String.Format("IGStreamingApiClient.OnFailure.PushServerException: {0}", e.Message));
        }

        public void OnFailure(PushConnException e)
        {
            Log.Trace(String.Format("IGStreamingApiClient.OnFailure.PushConnException: {0}", e.Message));
        }

        public void OnNewBytes(long bytes)
        {
            Log.Trace(String.Format("IGStreamingApiClient.OnNewBytes: {0}", bytes));
        }

        public void OnSessionStarted(bool isPolling)
        {
            Log.Trace(String.Format("IGStreamingApiClient.OnSessionStarted: {0}", isPolling));
        }
    }
}