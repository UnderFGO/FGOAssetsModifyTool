using Newtonsoft.Json.Linq;
using System.IO;
using System.Net;
using System.Text;

namespace FGOAssetsModifyTool
{
    static class HttpRequest
    {
        private const string METHOD_GET = "GET";
        private const string METHOD_POST = "POST";

        public static WebResponse Get(string url)
        {
            HttpWebRequest request = SetupRequest(url);
            request.Method = METHOD_GET;
            return request.GetResponse();
        }

        private static HttpWebRequest SetupRequest(string url)
        {
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
            request.CookieContainer = new CookieContainer();
            request.AllowAutoRedirect = true;
            request.KeepAlive = true;
            request.ServicePoint.Expect100Continue = false;
            request.Accept = "gzip, identity";
            request.ContentType = "application/x-www-form-urlencoded";
            request.UserAgent = "Dalvik/2.1.0 (Linux; U; Android 10; SM-G960W Build/QP1A.190711.020)";
            request.Timeout = 10000;
            return request;
        }

        public static string ToText(this WebResponse response)
        {
            using (Stream stream = response.GetResponseStream())
            {
                StreamReader streamReader = new StreamReader(stream, Encoding.UTF8);
                return streamReader.ReadToEnd();
            }
        }

        public static JObject ToJson(this WebResponse response)
        {
            string text = response.ToText();
            return JObject.Parse(text);
        }

        public static byte[] ToBinary(this WebResponse response)
        {
            using (Stream stream = response.GetResponseStream())
            {
                int readCount = 0;

                int bufferSize = 1 << 17;

                var buffer = new byte[bufferSize];
                using (var memory = new MemoryStream())
                {
                    while ((readCount = stream.Read(buffer, 0, bufferSize)) > 0)
                    {
                        memory.Write(buffer, 0, readCount);
                    }
                    return memory.ToArray();
                }

            }
        }
    }
}
