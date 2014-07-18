using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Web;
using System.Net.Http;
using System.Web.Http;
using System.Threading;
using HtmlAgilityPack;
using System.Web.Script.Serialization;

namespace ScraperJudicial.Controllers
{
    public class ScraperController : ApiController
    {
        public class ProcessData
        {
            public string Cookie { get; set; }
            public string CaptchaKey { get; set; }
            public string CaptchaPath { get; set; }
            public string ProcessNumber { get; set; }
            public int CaptchaResult { get; set; }
            public string ResultData { get; set; }
        }

        const string WebSiteRama = @"http://procesos.ramajudicial.gov.co/consultaprocesos/";
        private object getCookieLock = new object();
        private object cookieListLock = new object();
        private List<ProcessData> processDataList = new List<ProcessData>();
        private readonly string currentPath = HttpContext.Current.Server.MapPath("~/");
        private readonly string jsonFile = HttpContext.Current.Server.MapPath("~/") + "process.json";

        // GET: api/Scraper
        public List<ProcessData> Get()
        {
            var watch = new System.Diagnostics.Stopwatch();
            var htmlDocument = new HtmlAgilityPack.HtmlDocument();

            var threadList = new List<Thread>();
            watch.Start();
            for (var i = 0; i < 50; i++)
            {
                Thread thread = new Thread(() => GetCookie());
                threadList.Add(thread);
                thread.Start();
            }

            foreach (var thread in threadList)
            {
                thread.Join();
            }

            System.Diagnostics.Debug.WriteLine(watch.ElapsedMilliseconds);

            threadList.Clear();

            var j = 0;

            foreach (var item in processDataList)
            {
                var uri = new Uri(item.CaptchaPath);
                item.CaptchaKey = uri.ParseQueryString()["key"];

                var chaptchaPath = string.Format("{0}captchas/captcha{1}.png", currentPath, item.CaptchaKey);
                Thread thread = new Thread(() => GetCaptcha(item.Cookie, chaptchaPath, item.CaptchaPath));
                threadList.Add(thread);
                thread.Start();
            }

            watch.Stop();

            System.Diagnostics.Debug.WriteLine(watch.ElapsedMilliseconds);
            var serializer = new JavaScriptSerializer();

            var serializeCookie = serializer.Serialize(processDataList);
            File.WriteAllText(jsonFile, serializeCookie);
            return processDataList;
        }

        // GET: api/Scraper/5
        public string Get(string captcha, string result)
        {
            var serializer = new JavaScriptSerializer();
            var serializedData = File.ReadAllText(jsonFile);
            var processDataList = serializer.Deserialize<List<ProcessData>>(serializedData);
            #region Hidden Fields
            var hiddenFieldDictionary = new Dictionary<string, string> {
              {
               "managerScript","managerScript%7CbtnConsultarNum"
              },
              {
                "managerScript_HiddenField",
                ""
              },
              {
                "ddlCiudad",
                "05001"
              },
              {
                "ddlEntidadEspecialidad",
                "119-True-4003-05001-Juzgado%20Municipal-Civil"
              },
              {
                "rblConsulta",
                "1"
              },
              {
                "tbxNumProceso",
                "05001400300220070027700"
              },
              {
                "ddlTipoSujeto",
                "0"
              },
              {
                "ddlTipoPersona",
                "0"
              },
              {
                "txtNatural",
                ""
              },
              {
                "ddlDespacho",
                ""
              },
              {
                "ddlYear",
                "..."
              },
              {
                "tbxRadicacion",
                ""
              },
              {
                "txtConsecutivo",
                ""
              },
              {
                "tbxNumeroConstruido",
                "050014003"
              },
              {
                "ddlTipoSujeto2",
                "0"
              },
              {
                "ddlTipoPersona2",
                "0"
              },
              {
                "txtNombre",
                ""
              },
              {
                "txtResultCaptcha",
                "67"
              },
              {
                "ddlJuzgados",
                "0"
              },
              {
                "hdfNumRadicaion",
                ""
              },
              {
                "hdControl",
                ""
              },
              {
                "__EVENTTARGET",
                ""
              },
              {
                "__EVENTARGUMENT",
                ""
              },
              {
                "__LASTFOCUS",
                ""
              },
              {
                "__VIEWSTATE",
                "%2FwEPDwUJMTAzMTU5ODAwD2QWAgIDD2QWCAIDD2QWAmYPZBYEAgUPEGRkFgECDmQCCQ8QDxYGHg1EYXRhVGV4dEZpZWxkBQpoYWJpbGl0YWRvHg5EYXRhVmFsdWVGaWVsZAUVSWRfcmVsYWNpb25IYWJpbGl0YWRvHgtfIURhdGFCb3VuZGdkEBUUJVNlbGVjY2lvbmUgbGEgRW50aWRhZC9Fc3BlY2lhbGlkYWQuLi5kVFJJQlVOQUwgQURNSU5JU1RSQVRJVk8gREUgQU5USU9RVUlBIChFU0NSSVRVUkFMKSAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgIGRUUklCVU5BTCBTVVBFUklPUiBERSBBTlRJT1FVSUEgLSBTQUxBIFBFTkFMICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgZFRSSUJVTkFMIFNVUEVSSU9SIERFIEFOVElPUVVJQSAtIFNBTEEgQ0lWSUwgRkFNSUxJQSAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICBkVFJJQlVOQUwgU1VQRVJJT1IgREUgQU5USU9RVUlBIC0gU0FMQSBMQUJPUkFMICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgIGVUUklCVU5BTCBTVVBFUklPUiBERSBBTlRJT1FVSUEgLSBTQUxBIENJVklMIGVzcC4gZW4gUmVzdGl0dWNpw7NuIGRlIHRpZXJyYXMgICAgICAgICAgICAgICAgICAgICAgICAgIGRUUklCVU5BTCBTVVBFUklPUiBERSBNRURFTExJTiAtIFNBTEEgUEVOQUwgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgZFRSSUJVTkFMIFNVUEVSSU9SIERFIE1FREVMTElOIC0gU0FMQSBDSVZJTCAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICBkVFJJQlVOQUwgU1VQRVJJT1IgREUgTUVERUxMSU4gLSBTQUxBIEZBTUlMSUEgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgIGRUUklCVU5BTCBTVVBFUklPUiBERSBNRURFTExJTiAtIFNBTEEgTEFCT1JBTCAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgZEpVWkdBRE9TIEFETUlOSVNUUkFUSVZPUyBERSBNRURFTExJTiAoRVNDUklUVVJBTCkgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICBvSlVaR0FET1MgUEVOQUxFUyBERUwgQ0lSQ1VJVE8gREUgTUVERUxMSU4gICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAoSW5hY3Rpdm8pb0pVWkdBRE9TIFBFTkFMRVMgREVMIENJUkNVSVRPIEVTUC4gREUgQU5USU9RVUlBICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgKEluYWN0aXZvKW9KVVpHQURPUyBQRU5BTEVTIERFTCBDSVJDVUlUTyBFU1AuIERFIE1FREVMTElOICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgIChJbmFjdGl2bylkSlVaR0FET1MgQ0lWSUxFUyBERUwgQ0lSQ1VJVE8gREUgTUVERUxMSU4gICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgIGRKVVpHQURPUyBERSBGQU1JTElBIERFTCBDSVJDVUlUTyBERSBNRURFTExJTiAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgZEpVWkdBRE9TIExBQk9SQUxFUyBERUwgQ0lSQ1VJVE8gREUgTUVERUxMSU4gICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICBvSlVaR0FET1MgUEVOQUxFUyBNVU5JQ0lQQUxFUyBERSBNRURFTExJTiAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAoSW5hY3Rpdm8pZEpVWkdBRE9TIENJVklMRVMgTVVOSUNJUEFMRVMgREUgTUVERUxMSU4gICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICBkSlVaR0FETyBDSVZJTCBDSVJDVUlUTyBFU1BFQ0lBTElaQURPIEVOIFJFU1RJVFVDSU9OIERFIFRJRVJSQVMgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgIBUUATA5Mjk4LVRydWUtMjMzMS0wNTAwMS1UcmlidW5hbCBBZG1pbmlzdHJhdGl2by1TSU4gU0VDQ0lPTkVTKzExNS1UcnVlLTIyMDQtMDUwMDAtVHJpYnVuYWwgU3VwZXJpb3ItUGVuYWw4MTE3LVRydWUtMjIxMy0wNTAwMC1UcmlidW5hbCBTdXBlcmlvci1TYWxhIENpdmlsLUZhbWlsaWEtMTE2LVRydWUtMjIwNS0wNTAwMC1UcmlidW5hbCBTdXBlcmlvci1MYWJvcmFsVDM4My1UcnVlLTIyMjEtMDUwMDEtVHJpYnVuYWwgU3VwZXJpb3ItQ2l2aWwgRXNwZWNpYWxpemFkbyBlbiBSZXN0aXR1Y2nDs24gZGUgVGllcnJhcysxMTEtVHJ1ZS0yMjA0LTA1MDAxLVRyaWJ1bmFsIFN1cGVyaW9yLVBlbmFsKzExMy1UcnVlLTIyMDMtMDUwMDEtVHJpYnVuYWwgU3VwZXJpb3ItQ2l2aWwtMTE0LVRydWUtMjIxMC0wNTAwMS1UcmlidW5hbCBTdXBlcmlvci1GYW1pbGlhLTExMi1UcnVlLTIyMDUtMDUwMDEtVHJpYnVuYWwgU3VwZXJpb3ItTGFib3JhbDgxMjUtVHJ1ZS0zMzMxLTA1MDAxLUp1emdhZG8gQWRtaW5pc3RyYXRpdm8tU0lOIFNFQ0NJT05FUy4xMjItRmFsc2UtMzEwNC0wNTAwMS1KdXpnYWRvIGRlIENpcmN1aXRvLVBlbmFsLjM4MC1GYWxzZS0zMTA0LTA1MDAxLUp1emdhZG8gZGUgQ2lyY3VpdG8tUGVuYWwuMjIxLUZhbHNlLTMxMDQtMDUwMDEtSnV6Z2FkbyBkZSBDaXJjdWl0by1QZW5hbC0xMTgtVHJ1ZS0zMTAzLTA1MDAxLUp1emdhZG8gZGUgQ2lyY3VpdG8tQ2l2aWwvMTIxLVRydWUtMzExMC0wNTAwMS1KdXpnYWRvIGRlIENpcmN1aXRvLUZhbWlsaWEvMTIwLVRydWUtMzEwNS0wNTAwMS1KdXpnYWRvIGRlIENpcmN1aXRvLUxhYm9yYWwsMTI0LUZhbHNlLTQwMDQtMDUwMDEtSnV6Z2FkbyBNdW5pY2lwYWwtUGVuYWwrMTE5LVRydWUtNDAwMy0wNTAwMS1KdXpnYWRvIE11bmljaXBhbC1DaXZpbE4zODItVHJ1ZS0wMDIxLTA1MDAxLVNpbiBFbnRpZGFkLUNpdmlsIEVzcGVjaWFsaXphZG8gZW4gUmVzdGl0dWNpw7NuIGRlIFRpZXJyYXMUKwMUZ2dnZ2dnZ2dnZ2dnZ2dnZ2dnZ2dkZAIVDxBkDxYgZgIBAgICAwIEAgUCBgIHAggCCQIKAgsCDAINAg4CDwIQAhECEgITAhQCFQIWAhcCGAIZAhoCGwIcAh0CHgIfFiAQBQMuLi4FAy4uLmcQBQQxOTkwBQQxOTkwZxAFBDE5OTEFBDE5OTFnEAUEMTk5MgUEMTk5MmcQBQQxOTkzBQQxOTkzZxAFBDE5OTQFBDE5OTRnEAUEMTk5NQUEMTk5NWcQBQQxOTk2BQQxOTk2ZxAFBDE5OTcFBDE5OTdnEAUEMTk5OAUEMTk5OGcQBQQxOTk5BQQxOTk5ZxAFBDIwMDAFBDIwMDBnEAUEMjAwMQUEMjAwMWcQBQQyMDAyBQQyMDAyZxAFBDIwMDMFBDIwMDNnEAUEMjAwNAUEMjAwNGcQBQQyMDA1BQQyMDA1ZxAFBDIwMDYFBDIwMDZnEAUEMjAwNwUEMjAwN2cQBQQyMDA4BQQyMDA4ZxAFBDIwMDkFBDIwMDlnEAUEMjAxMAUEMjAxMGcQBQQyMDExBQQyMDExZxAFBDIwMTIFBDIwMTJnEAUEMjAxMwUEMjAxM2cQBQQyMDE0BQQyMDE0ZxAFBDIwMTUFBDIwMTVnEAUEMjAxNgUEMjAxNmcQBQQyMDE3BQQyMDE3ZxAFBDIwMTgFBDIwMThnEAUEMjAxOQUEMjAxOWcQBQQyMDIwBQQyMDIwZ2RkAh4PDxYCHgRUZXh0BTVQcm9jZXNvcyBjb24gcmVnaXN0cm8gZGUgYWN0dWFjaW9uZXMgZGVzZGUgMjAxNC0wNS0wM2RkAigPZBYCZg9kFgICAw88KwARAQEQFgAWABYAZBgBBQ9ndlJlc3VsdGFkb3NOdW0PZ2RN%2BjD2dMMIH8Q4IGP8HRJCDgzGeOGQ2h%2B%2Bjt0LWKC0cA%3D%3D"
              },
              {
                "__EVENTVALIDATION",
                "%2FwEWiAEC4tXKxg8C0rK2vg4Cwt2c0AIC%2B6CqogUCvaH%2BmQUCwqaSoAUC%2B6D%2BmQUCt8mv7QICmKCmpwUCwqaqogUC3qGupQUC9tThnwUCwqb%2BmQUCmKCqogUCk%2F3DiAsCwqa6pgUCvaGipAUC%2F6GqogUCuaCSoAUCgpLL8gIC3qGWowUC%2B6CmpwUCwqbymAUC%2F6G6pgUCuaCupQUC37mFgAEChaf%2BmQUCuaC6pgUC5rnlqQsCmKCeoQUCwqaipAUC%2F6GeoQUC3qGeoQUCupGHvwYCycf1oAQC0J3DyggCt8Hq%2BAsC09TOxwMCpOTlrAgC%2BPO2jQsC19OFjAUCpOKh7QsCr%2Bb%2ByQ0C14yvjAsCsrmoqgYCw8OOLQKe3fXuBAKoypSiAgKKkYi2AgLwjMTwBQLqhfzwCgKptOruAgKFg7y7DAKtpfR%2BAqyl9H4Cr6X0fgKupfR%2BArGK%2FpcDAv7%2F2OIEAuDUto8IAo6ViIYLApGViIYLApCViIYLAqv1k9wIArT1k9wIArX1k9wIAoODk54PAvrV8P8NAuDUzo4IArqjrL4NAq2537YKAtebsacLAtebrcwDAteb2egEAteb9bUNAteb4dIFAtebnf8OAtebiYQHAtebpaEIAtebkZgNAtebjaUGApP45NMDApP4kPgEApP4jIUNApP4uKIGApP41M4OApP4wOsHApP4%2FLAIApP46F0Ck%2FjEtAYCk%2Fjw0Q4C%2BMHCyAkC%2BMH%2BlQIC%2BMHqsgsC%2BMGG3wMC%2BMGy5AQC%2BMGugQ0C%2BMHarQYC%2BMH2yg4C%2BMGiogwC%2BMHezgQC5dag5gcChvTftgUCieXFFwKb3oDsBgKOqaSFAQKOlcCICwKRlcCICwKQlcCICwLM4egaAtPh6BoC0uHoGgKIgMKjDAKk%2BeiPAwKSyun%2FBQKysLP5CAKq1ob%2BBQLQ%2FYaWAgK3%2BKznBwKMsPXiBALLq%2F7RBAKawYSaCAKIrZfMAgLdmsycDAKUjbbgDwK28NzeAQLfrsOKBQL1rsXdAQLpgNLLAQLzjY%2BhDwKipeHdCwL23r%2BXDgKDo8PpDwLZ87%2ByDgKinK2jCQKc1tS7B9ts8FxTL9AsQxXtpoR5gy0rvNlAxEzj6zbREt1UGWPp"
              },
              {
                "__ASYNCPOST",
                "true"
              },
              {
                "btnConsultarNum",
                "Consultar"
              }
            };
            #endregion Hidden Fields
            foreach (var process in processDataList)
            {
                if (process.CaptchaKey == captcha)
                {
                    var request = (HttpWebRequest) WebRequest.Create(WebSiteRama);

                    request.Headers = new WebHeaderCollection()
                    {
                        "Origin: http://procesos.ramajudicial.gov.co",
                        "Cache-Control: no-cache",
                        "X-Requested-With: XMLHttpRequest",
                        "X-MicrosoftAjax: Delta=true"
                    };
                    request.Method = "POST";
                    request.Host = "procesos.ramajudicial.gov.co";
                    request.UserAgent = "Mozilla/5.0 (Windows NT 6.1; WOW64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/30.0.1599.101 Safari/537.36";
                    request.Accept = "*/*";
                    request.Referer = "http://procesos.ramajudicial.gov.co/consultaprocesos/";
                    
                    request.Headers.Add(HttpRequestHeader.Cookie, process.Cookie);
                    hiddenFieldDictionary["tbxNumProceso"] = "05001400300220070027700";
                    hiddenFieldDictionary["txtResultCaptcha"] = result;

                    var postData = hiddenFieldDictionary.Aggregate("", (current, field) => current + string.Format("{0}={1}&", field.Key, field.Value));
                    postData = postData.Substring(0, postData.Length - 1);
                    var byteArray = Encoding.UTF8.GetBytes(postData);

                    request.ContentType = "application/x-www-form-urlencoded; charset=UTF-8";
                    request.ContentLength = byteArray.LongLength;
                    var dataStream = request.GetRequestStream();
                    dataStream.Write(byteArray, 0, byteArray.Length);
                    dataStream.Close();
                    var response = (HttpWebResponse)request.GetResponse();

                    var htmlDocument = new HtmlAgilityPack.HtmlDocument();

                    htmlDocument.Load(response.GetResponseStream());
                    
                }
            }

            return "Not Found";

        }

        // POST: api/Scraper
        public void Post([FromBody]string value)
        {
        }

        // PUT: api/Scraper/5
        public void Put(int id, [FromBody]string value)
        {
        }

        // DELETE: api/Scraper/5
        public void Delete(int id)
        {
        }

        private void GetCookie()
        {

            var htmlDocument = new HtmlAgilityPack.HtmlDocument();

            var request = WebRequest.Create(WebSiteRama);
            HttpWebResponse response;
            lock (getCookieLock)
            {
                response = (HttpWebResponse)request.GetResponse();
            }

            htmlDocument.Load(response.GetResponseStream());

            string captchaSelector = "//img[starts-with(@src, 'Captcha')]";

            HtmlAgilityPack.HtmlNode imageTag = htmlDocument.DocumentNode.SelectSingleNode(captchaSelector);

            var captchaUrl = string.Format("{0}{1}", WebSiteRama, imageTag.Attributes["src"].Value);
            lock (cookieListLock)
            {
                processDataList.Add(new ProcessData
                {
                    Cookie = response.Headers["set-cookie"],
                    CaptchaPath = captchaUrl
                });
            }
        }

        private void GetCaptcha(string cookie, string captchaPath, string urlCaptcha)
        {
            var requestCaptcha = new WebClient();
            requestCaptcha.Headers.Add(HttpRequestHeader.Cookie, cookie);
            requestCaptcha.DownloadFile(urlCaptcha, captchaPath);
        }

        /*
         * 
         // Already sent POST request with username and password to get session id, cookie etc
// Create POST data and convert it to a byte array. This includes viewstate, eventvalidation etc.
postData = String.Format("ctl00%24ScriptManager1=ctl00%24uxContentPlaceHolder%24Panel%7Cctl00%24uxContentPlaceHolder%24uxTimer&__EVENTTARGET=ctl00%24uxContentPlaceHolder%24uxTimer");
postData = hiddenFields.Aggregate(postData, (current, field) => current + ("&" + Uri.EscapeDataString(field.Key) + "=" + Uri.EscapeDataString(field.Value)));

byteArray = Encoding.UTF8.GetBytes(postData);

// Set the ContentType property of the WebRequest.
request.Headers.Add("X-MicrosoftAjax", "Delta=true");
request.ContentType = "application/x-www-form-urlencoded";
request.UserAgent = "Mozilla/5.0 (Windows NT 6.1; WOW64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/30.0.1599.101 Safari/537.36";
request.Referer = "https://www.example.com/Registered/MyAcount.aspx?menu=My%20account";
request.Host = "www.example.com";
// Set the ContentLength property of the WebRequest.
request.ContentLength = byteArray.Length;
// Get the request stream.
dataStream = request.GetRequestStream();
// Write the data to the request stream.
dataStream.Write(byteArray, 0, byteArray.Length);
// Close the Stream object.
dataStream.Close();
// Get the response.

response = (HttpWebResponse)request.GetResponse();
_container.Add(response.Cookies);

using (var reader = new StreamReader(response.GetResponseStream()))
{
    // Read the content.
    responseFromServer = reader.ReadToEnd();
}

response.Close();

         */
    }
}
