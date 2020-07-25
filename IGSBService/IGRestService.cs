using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;
using Newtonsoft.Json.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace IGSB
{
    public class IGRestService
    {
        public enum enmMethod
        {
            GET,
            POST,
            PUT,
            DELETE
        }

        public void ParseHeaders(Security authentication, HttpResponseHeaders headers)
        {
            foreach (var header in headers)
            {
                if (header.Key.Equals("CST")) authentication.CST = header.Value.First();
                if (header.Key.Equals("X-SECURITY-TOKEN")) authentication.XST = header.Value.First();
            }
        }

        public void SetDefaultRequestHeaders(HttpClient client, string apiKey, string cst, string xst, string version)
        {
            if (!string.IsNullOrEmpty(apiKey)) client.DefaultRequestHeaders.Add("X-IG-API-KEY", apiKey);
            if (!string.IsNullOrEmpty(cst)) client.DefaultRequestHeaders.Add("CST", cst);
            if (!string.IsNullOrEmpty(xst)) client.DefaultRequestHeaders.Add("X-SECURITY-TOKEN", xst);

            client.DefaultRequestHeaders.Add("VERSION", version);
        }

        async public Task<IGResponse<T>> RestfulService<T>(string apiKey, string cst, string xst, string version, string uri, enmMethod method, JObject json = null)
        {
            StringContent content = default(StringContent);
            var client = new HttpClient();
            var httpResponse = new HttpResponseMessage();

            var retval = new IGResponse<T> { Response = default(T), StatusCode = HttpStatusCode.OK, Authentication = new Security() { APIKEY = apiKey, CST = cst, XST = xst } };
            if (json != null) content = new StringContent(JsonConvert.SerializeObject(json), Encoding.UTF8, "application/json");

            SetDefaultRequestHeaders(client, apiKey, cst, xst, version);

            switch (method)
            {
                case enmMethod.POST:
                    httpResponse = client.PostAsync(uri, content).Result;
                    break;

                case enmMethod.GET:
                    httpResponse = client.GetAsync(uri).Result;
                    break;

                case enmMethod.PUT:
                    httpResponse = client.PutAsync(uri, content).Result;
                    break;

                case enmMethod.DELETE:
                    if (content != null)
                    {
                        content.Headers.Add("_method", "DELETE");
                        httpResponse = client.PostAsync(uri, content).Result;
                    }
                    else
                    {
                        httpResponse = client.DeleteAsync(uri).Result;
                    }

                    break;
            }

            string response = null;

            ParseHeaders(retval.Authentication, httpResponse.Headers);
            retval.StatusCode = httpResponse.StatusCode;
            response = await httpResponse.Content.ReadAsStringAsync();

            if (response != null)
            {
                var jss = new JsonSerializerSettings();
                jss.Converters.Add(new StringEnumConverter());
                jss.MissingMemberHandling = MissingMemberHandling.Ignore;
                jss.FloatFormatHandling = FloatFormatHandling.String;
                jss.NullValueHandling = NullValueHandling.Ignore;
                jss.Error += DeserializationError;
                client.Dispose();

                try
                {
                    retval.Response = JsonConvert.DeserializeObject<T>(response, jss);
                }
                catch (Exception ex)
                {
                    //eventDispatcher.addEventMessage(ex.Message);
                }
            }

            return retval;
        }

        private void DeserializationError(object sender, ErrorEventArgs errorEventArgs)
        {
            errorEventArgs.ErrorContext.Handled = true;
        }
    }
}