using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.IO;

namespace FindImages
{
    class Proxy
    {
        public static string GetGoodProxy(string url)
        {
            var proxyList = GetProxyList();
            var proxy = TestProxies(proxyList, url);
            if (proxy == "")
            {
                Environment.Exit(0);
                new FileInfo("BannedProxies.txt").Delete();
            }
            return proxy;
        }

        private static List<string> GetProxyList()
        {
            var proxyList = new List<string>();
            var proxyPage = PageEncoding.GetPage("http://www.checker.freeproxy.ru/checker/last_checked_proxies.php", "", "", "", 0);
            if (proxyPage != null)
            {
                foreach (var proxyText in proxyPage.Split(new[] { "Don't try to parse this code! This is second notification!" }, StringSplitOptions.None))
                {
                    var proxy = ParceProxy(proxyText);
                    if (proxy != "" )
                        proxyList.Add(proxy);
                }
            }
            // убрать из списка неподходящие прокси
            if (new FileInfo("BannedProxies.txt").Exists)
            {
                var fr = new StreamReader("BannedProxies.txt");
                while(!fr.EndOfStream)
                    proxyList.Remove(fr.ReadLine());
                fr.Close();
            }
            return proxyList;
        }

        private static string ParceProxy(string proxyText)
        {
            if (!proxyText.Contains("name = '")) return "";

            var ip = proxyText.Split(new[] { "name = '" }, StringSplitOptions.None)[1];
            ip = ip.Split(new[] { "';" }, StringSplitOptions.None)[0];

            var portFormulaName = proxyText.Split(new[] { "document.write(name + ':' + " }, StringSplitOptions.None)[1];
            portFormulaName = portFormulaName.Split(new[] { ")" }, StringSplitOptions.None)[0];

            var portName = proxyText.Split(new[] { portFormulaName + " = " }, StringSplitOptions.None)[1];
            portName = portName.Split(new[] { " + " }, StringSplitOptions.None)[0];

            var port = proxyText.Split(new[] { portName + " = " }, StringSplitOptions.None)[1];
            port = port.Split(new[] { ";" }, StringSplitOptions.None)[0];

            var firstVar = proxyText.Split(new[] { portFormulaName + " = " + portName + " + (" }, StringSplitOptions.None)[1];
            firstVar = firstVar.Split(new[] { "-" }, StringSplitOptions.None)[0];

            var secondVar = proxyText.Split(new[] { portFormulaName + " = " + portName + " + (" + firstVar + "-" }, StringSplitOptions.None)[1];
            secondVar = secondVar.Split(new[] { ")" }, StringSplitOptions.None)[0];

            var thirdVar = proxyText.Split(new[] { portName + " + (" + firstVar + "-" + secondVar + ") / " }, StringSplitOptions.None)[1];
            thirdVar = thirdVar.Split(new[] { ";" }, StringSplitOptions.None)[0];

            port = (Int32.Parse(port) + (Int32.Parse(firstVar) - Int32.Parse(secondVar)) / Int32.Parse(thirdVar)).ToString();

            return ip + ":" + port;
        }

        private static string TestProxies(List<string> proxyList, string url)
        {
            var proxyList2 = new List<string>();
            foreach (var proxy in proxyList)
            {
                var ip = proxy.Split(new[] { ":" }, StringSplitOptions.None)[0];
                var port = Int32.Parse(proxy.Split(new[] { ":" }, StringSplitOptions.None)[1]);
                if (!IsGoodProxy(url, ip, port))
                {
                    var sw = new StreamWriter("BannedProxies.txt", true);
                    //sw.WriteLine(ip + ":" + port);
                    sw.Close();
                    continue;
                }
                if (!IsGoodProxy2(url, ip, port))
                {
                    var sw = new StreamWriter("BannedProxies.txt", true);
                    sw.WriteLine(ip + ":" + port);
                    sw.Close();
                    continue;
                }
                return proxy;
            }
            return "";
        }

        private static bool IsGoodProxy(string url, string ip, int port)
        {
            WebResponse response = null;
            Stream stream = null;
            StreamReader reader = null;
            try
            {
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
                //request.Timeout = 15000;
                request.Proxy = new WebProxy(ip, port);
                response = request.GetResponse();
                stream = response.GetResponseStream();
                reader = new StreamReader(stream, System.Text.Encoding.UTF8);
                string buffer = reader.ReadToEnd();
                return buffer == "" ? false : true;
            }
            catch (WebException e)
            {
                //System.Console.WriteLine("Can't download:" + e);
                return false;
            }
        }

        private static bool IsGoodProxy2(string url, string ip, int port)
        {
            WebResponse response = null;
            Stream stream = null;
            try
            {
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
                request.Timeout = 25000;
                request.UserAgent = "MSIE 6.0";
                if (ip != "" && port != 0)
                    request.Proxy = new WebProxy(ip, port);
                response = request.GetResponse();
                stream = response.GetResponseStream();
                var b = PageEncoding.ReadFully(stream);
                Encoding enc = PageEncoding.def_code(b, 100);        
                string content = PageEncoding.DecodeContent(b, enc);
                if (!content.Contains("Bot or Human") && !content.Contains("checkcaptcha"))
                    return true;
            }
            catch (WebException e)
            {
                return false;
            }
            catch (Exception e)
            {
                return false;
            }
            return false;
        }
    }
}
