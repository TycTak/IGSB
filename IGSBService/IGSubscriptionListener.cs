using com.lightstreamer.client;
using System;
using static IGSB.IGClient;
using System.Collections.Generic;

namespace IGSB
{
    class IGSubscriptionListener : SubscriptionListener
    {
        private WatchFile watchList;

        static public event Message M;

        public IGSubscriptionListener(WatchFile watchList)
        {
            this.watchList = watchList;
        }

        public void onClearSnapshot(string itemName, int itemPos)
        {
            M(enmMessageType.Debug, String.Format("IGSubscriptionListener.onClearSnapshot {0} - {1}", itemName, itemPos));
        }

        public void onCommandSecondLevelItemLostUpdates(int lostUpdates, string key)
        {
            M(enmMessageType.Debug, String.Format("IGSubscriptionListener.onCommandSecondLevelItemLostUpdates {0} - {1}", lostUpdates, key));
        }

        public void onCommandSecondLevelSubscriptionError(int code, string message, string key)
        {
            M(enmMessageType.Debug, String.Format("IGSubscriptionListener.onCommandSecondLevelSubscriptionError {0} - {1} - {2}", code, message, key));
            M(enmMessageType.Error, String.Format("IGSubscriptionListener.onCommandSecondLevelSubscriptionError {0} - {1} - {2}", code, message, key));
        }

        public void onEndOfSnapshot(string itemName, int itemPos)
        {
            M(enmMessageType.Debug, String.Format("IGSubscriptionListener.onEndOfSnapshot {0} - {1}", itemName, itemPos));
        } 

        public void onItemLostUpdates(string itemName, int itemPos, int lostUpdates)
        {
            M(enmMessageType.Debug, String.Format("IGSubscriptionListener.onItemLostUpdates {0} - {1} - {2}", itemName, itemPos, lostUpdates));
        }

        public bool CheckKeyExists(string itemName, string fieldName)
        {
            var retval = true;

            for (var i = 0; i < watchList.Schemas.Count; i++)
            {
                if (watchList.Schemas[i].IsActive)
                {
                    if (!watchList.Schemas[i].CodeLibrary.CheckKeyExists(itemName, fieldName))
                    {
                        retval = false;
                        break;
                    }
                }
            }

            return retval;
        }

        public void ExecuteUpdate(long timeStamp, string itemName, IDictionary<string, string> changeFields)
        {
            //M(enmMessageType.Info, "+");

            foreach (var changed in changeFields)
            {
                for (var i = 0; i < watchList.Schemas.Count; i++)
                {
                    if (watchList.Schemas[i].IsActive)
                    {
                        var pushed = watchList.Schemas[i].CodeLibrary.Push(timeStamp, itemName, changed.Key, changed.Value);

                        if (pushed && IGClient.StreamDisplay == enmContinuousDisplay.Subscription)
                        {
                            var message = String.Format("{0} {1}:{2}:{3}", watchList.Schemas[i].SchemaName, itemName, changed.Key, changed.Value);

                            if (string.IsNullOrEmpty(IGClient.Filter) || message.ToLower().Contains(IGClient.Filter.ToLower()))
                            {
                                M(enmMessageType.Info, message);
                            }
                        }

                        //M(enmMessageType.Debug, String.Format("IGClientListener.onItemUpdate {0} {1} {2} {3}", pushed, itemUpdate.ItemName, changed.Key, changed.Value));
                    }
                }
            }
        }

        public void onItemUpdate(ItemUpdate itemUpdate)
        {
            if (!IGClient.Pause)
            {
                ExecuteUpdate(0, itemUpdate.ItemName, itemUpdate.ChangedFields);
            }
        }

        public void onListenEnd(Subscription subscription)
        {
            M(enmMessageType.Debug, String.Format("IGSubscriptionListener.onListenEnd: {0}", subscription.DataAdapter));
        }

        public void onListenStart(Subscription subscription)
        {
            M(enmMessageType.Debug, String.Format("IGSubscriptionListener.onListenStart: {0}", subscription.DataAdapter));
        }

        public void onRealMaxFrequency(string frequency)
        {
            M(enmMessageType.Debug, String.Format("IGSubscriptionListener.onRealMaxFrequency: {0}", frequency));
        }

        public void onSubscription()
        {
            M(enmMessageType.Debug, "IGSubscriptionListener.onSubscription");
        }

        public void onSubscriptionError(int code, string message)
        {
            M(enmMessageType.Debug, String.Format("IGSubscriptionListener.onSubscriptionError FAILED: {0} - {1}", code, message));
            M(enmMessageType.Error, String.Format("IGSubscriptionListener.onSubscriptionError FAILED: {0} - {1}", code, message));
        }

        public void onUnsubscription()
        {
            M(enmMessageType.Debug, "IGSubscriptionListener.onUnsubscription");
        }
    }
}
