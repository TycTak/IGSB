using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static IGSB.IGRestService;

namespace IGSB
{
    class IGCommand
    {
        public enum enmTransType
        {
            ALL,
            ALL_DEAL,
            DEPOSIT,
            WITHDRAWAL
        }

        static public IGResponse<JObject> Open(string apiKey, string cst, string xst, string sourceUrl, string direction, string currency, string epic, double size)
        {
            string json = @"{"
                + "\"epic\": \"" + epic + "\","
                + "\"expiry\": \"DFB\","
                + "\"direction\": \"" + direction.ToUpper() + "\","
                + "\"size\": \"" + size.ToString() + "\","
                + "\"orderType\": \"MARKET\","
                + "\"timeInForce\": null,"
                + "\"level\": null,"
                + "\"guaranteedStop\": \"false\","
                + "\"stopLevel\": null,"
                + "\"stopDistance\": null,"
                + "\"trailingStop\": null,"
                + "\"trailingStopIncrement\": null,"
                + "\"forceOpen\": \"false\","
                + "\"limitLevel\": null,"
                + "\"limitDistance\": null,"
                + "\"quoteId\": null,"
                + "\"currencyCode\": \"" + currency + "\""
            + "}";

            JObject jObj = JObject.Parse(json);

            var url = $"{sourceUrl}/positions/otc";
            var version = "2";

            var connect = new IGRestService();
            var task = Task.Run(async () => await connect.RestfulService<JObject>(apiKey, cst, xst, version, url, enmMethod.POST, jObj));
            return task.Result;
        }

        static public IGResponse<JObject> AllOpen(string apiKey, string cst, string xst, string sourceUrl)
        {
            var url = $"{sourceUrl}/positions";
            //var uri = "https://demo-api.ig.com/gateway/deal/positions";
            var version = "2";

            var connect = new IGRestService();
            var task = Task.Run(async () => await connect.RestfulService<JObject>(apiKey, cst, xst, version, url, enmMethod.GET, null));

            foreach (var position in task.Result.Response["positions"])
            {
                var level = Convert.ToDouble(position["position"]["level"].ToString());
                var compare = Convert.ToDouble((position["position"]["direction"].ToString().Equals("BUY") ? position["market"]["bid"] : position["market"]["offer"]).ToString());
                var profit = ((position["position"]["direction"].ToString().Equals("BUY") ? compare - level : level - compare) * Convert.ToDouble(position["position"]["size"].ToString()));
                var temp = position["position"] as JObject;
                temp.Add("profit", Math.Round(profit, 2));
            }


            return task.Result;
        }

        static public IGResponse<JObject> Transactions(string apiKey, string cst, string xst, string sourceUrl, DateTime fromDate, DateTime toDate, enmTransType transType)
        {
            var url = $"{sourceUrl}/history/transactions/{transType.ToString()}/{fromDate.ToString("dd-MM-yyyy", CultureInfo.InvariantCulture)}/{toDate.ToString("dd-MM-yyyy", CultureInfo.InvariantCulture)}";
            var version = "1";

            var connect = new IGRestService();
            var task = Task.Run(async () => await connect.RestfulService<JObject>(apiKey, cst, xst, version, url, enmMethod.GET, null));
            return task.Result;
        }

        static public IGResponse<JObject> Search(string apiKey, string cst, string xst, string sourceUrl, string searchTerm)
        {
            var url = $"{sourceUrl}/markets?searchTerm={searchTerm}";
            var version = "1";

            var connect = new IGRestService();
            var task = Task.Run(async () => await connect.RestfulService<JObject>(apiKey, cst, xst, version, url, enmMethod.GET, null));
            return task.Result;
        }

        static public IGResponse<JObject> Accounts(string apiKey, string cst, string xst, string sourceUrl)
        {
            var url = $"{sourceUrl}/accounts";
            var version = "1";

            var connect = new IGRestService();
            var task = Task.Run(async () => await connect.RestfulService<JObject>(apiKey, cst, xst, version, url, enmMethod.GET, null));
            return task.Result;
        }

        static public IGResponse<JObject> GetDetails(string apiKey, string cst, string xst, string sourceUrl, string epic)
        {
            var url = $"{sourceUrl}/markets/{epic}";
            var version = "3";

            var connect = new IGRestService();
            var task = Task.Run(async () => await connect.RestfulService<JObject>(apiKey, cst, xst, version, url, enmMethod.GET, null));
            var retval = task.Result;

            if (retval.Response != null)
            {
                if (!retval.Response.ContainsKey("errorCode"))
                { 
                    var bid = double.Parse("0" + retval.Response["snapshot"]["bid"].ToString());
                    var offer = double.Parse("0" + retval.Response["snapshot"]["offer"].ToString());
                    var spread = Math.Round((offer - bid), 2);
                    var spreadPercentage = Math.Round((spread / offer) * 100, 2);

                    retval.Response.Add(new JProperty("spread", spread.ToString()));
                    retval.Response.Add(new JProperty("percspread", spreadPercentage.ToString()));
                }
            }

            return retval;
        }

        static public IGResponse<JObject> GetConfirmation(string apiKey, string cst, string xst, string sourceUrl, string dealReference)
        {
            var url = $"{sourceUrl}/confirms/{dealReference}";
            //var uri = $"https://demo-api.ig.com/gateway/deal/confirms/{dealReference}";
            var version = "1";

            var connect = new IGRestService();
            var task = Task.Run(async () => await connect.RestfulService<JObject>(apiKey, cst, xst, version, url, enmMethod.GET, null));
            return task.Result;
        }

        static public IGResponse<JObject> Close(string apiKey, string cst, string xst, string sourceUrl, string dealId, double size, string direction)
        {
            string json = @"{"
                + "\"dealId\": \"" + dealId + "\","
                + "\"epic\": null,"
                + "\"expiry\": null,"
                + "\"direction\": \"" + direction + "\","
                + "\"size\": \"" + size + "\","
                + "\"level\": null,"
                + "\"orderType\": \"MARKET\","
                + "\"timeInForce\": null,"
                + "\"quoteId\": null"
                + "}";

            JObject jObj = JObject.Parse(json);

            var uri = $"{sourceUrl}/positions/otc";
            var version = "1";

            var connect = new IGRestService();
            var task = Task.Run(async () => await connect.RestfulService<JObject>(apiKey, cst, xst, version, uri, enmMethod.DELETE, jObj));

            return task.Result;
        }
    }
}
