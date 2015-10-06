using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;

namespace FindImages
{
    /// <summary>
    /// Summary description for Images
    /// </summary>
    public class YandexImages
    {
        private DataClasses1DataContext d;
        private readonly int minImageSize;
        private string ip = "";
        private int port = 0;
        public string imageHost = "";
        private List<string> bannedHosts;
        private List<string> bannedImages;
        private readonly string[] chars = new[] { "\\", "/", "*", "'", "\"", "?", "|", "<", ">", ":", "&" };
        private string homeFolder;
        public List<WebResponse> potentialImages;

        public YandexImages(int _minImageSize, string _homeFolder)
        {
            minImageSize = _minImageSize;
            homeFolder = _homeFolder;
            GetNewProxy();
            potentialImages = new List<WebResponse>();
        }

        private void GetNewProxy()
        {
            var proxy = Proxy.GetGoodProxy("http://images.yandex.ru/yandsearch?text=Hotpoint-Ariston LZ 705 IX EXTRA&rpt=simage");
            if (proxy != "")
            {
                ip = proxy.Split(new[] { ":" }, StringSplitOptions.None)[0];
                port = Int32.Parse(proxy.Split(new[] { ":" }, StringSplitOptions.None)[1]);
            }
        }

        public bool FindNewImages(Product product)
        {
            if (ip == "") return false;
            d = new DataClasses1DataContext();
            bannedHosts = d.BannedImagesSites.Select(t => t.BannedHost).ToList();
            bannedImages = d.BannedImages.Select(t => t.BannedImage1).ToList();
            potentialImages.Clear();

            var productName = product.ProductFullName;
            foreach (var c in chars)
            {
                productName = productName.Replace(c, " ");
            }

            var searchURL = "http://images.yandex.ru/yandsearch?text=" + productName + "&rpt=simage";
            var result = GetPhoto(searchURL, 1);
            if (result != 0) return false;
            return true;
        }

        private int GetPhoto(string searchURL, int count)
        {
            var findResult = PageEncoding.GetPage(searchURL, null, null, ip, port);
            if (findResult == null || findResult.Contains("checkcaptcha"))
            {
                GetNewProxy();
                return 1;
            }

            var x = new string[1];
            x[0] = "<img id=\"i-main-pic\" src=\"";
            if (!findResult.Contains(x[0])) return 1;
            var href = findResult.Split(x, StringSplitOptions.None)[1];
            x[0] = "\" onload=";
            href = href.Split(x, StringSplitOptions.None)[0].Replace("www.", "");
            try
            {
                var host = (new Uri(href)).Host;
                if (bannedHosts.Contains(host) || bannedImages.Contains(href))
                {
                    return FindAgain(searchURL, count, findResult, ref x, ref href);
                }

                var _filePath = homeFolder + "temp.jpeg";
                // запрос по ссылке 
                WebResponse response;
                var request = (HttpWebRequest)WebRequest.Create(href);
                request.Timeout = 16000;
                response = request.GetResponse();

                href = response.ResponseUri.AbsoluteUri;
                host = (new Uri(href)).Host.Replace("www.", "");
                if (response.ContentType.Contains("image/jpeg") && !bannedHosts.Contains(host)
                    && !bannedImages.Contains(href.Replace("www.", "")))
                {
                    if (response.ContentLength > minImageSize || response.ContentLength < 0)
                    {
                        var remoteStream = response.GetResponseStream();

                        // Create the local file
                        var localStream = File.Create(_filePath);

                        var buffer = new byte[2048];
                        int bytesRead;
                        var counter = 0;
                        remoteStream.ReadTimeout = 5000;
                        do
                        {
                            bytesRead = remoteStream.Read(buffer, 0, buffer.Length);
                            localStream.Write(buffer, 0, bytesRead);
                            counter++;
                        } while (bytesRead > 0 && counter < 800);
                        localStream.Close();
                        remoteStream.Close();
                        if (counter == 800 || counter == 1)
                        {
                            return FindAgain(searchURL, count, findResult, ref x, ref href);
                        }
                        //imageHost = (new Uri(href)).Host;
                        imageHost = href;
                    }
                    else
                    {
                        potentialImages.Add(response);
                        return FindAgain(searchURL, count, findResult, ref x, ref href);
                    }
                }
                else
                {
                    return FindAgain(searchURL, count, findResult, ref x, ref href);
                }
            }
            catch (WebException)
            {
                return FindAgain(searchURL, count, findResult, ref x, ref href);
            }
            catch (Exception)
            {
                return 1;
            }
            return 0;
        }

        private int FindAgain(string searchURL, int count, string findResult, ref string[] x, ref string href)
        {
            if (searchURL.Contains("yandex") && findResult.Contains("a id=\"next_page\" href=\"") && count < 12)
            {
                x = new string[1];
                //x[0] = "\" class=\"arrow\">&#8594;";
                x[0] = "a id=\"next_page\" href=\"";
                href = findResult.Split(x, StringSplitOptions.None)[1];
                x[0] = "\" onmousedown=\"eval";
                href = href.Split(x, StringSplitOptions.None).ToList().First();
                System.Threading.Thread.Sleep(5000);
                return GetPhoto("http://images.yandex.ru" + href.Replace("amp;", ""), count + 1);
            }
            //else
            //{
            //    if (searchURL.Contains("&icolor=white"))
            //    {
            //        searchURL = searchURL.Replace("&icolor=white", "");
            //        return GetPhoto(searchURL, 1);
            //    }
            //}
            return 1;
        }

        /*private static void ResizeImage(string currentDir)
        {
            var img = Image.FromFile(currentDir + ".jpeg");
            if (File.Exists(currentDir + "_sm.jpeg"))
                File.Delete(currentDir + "_sm.jpeg");
            var k = 90.0 / (img.Width > img.Height ? img.Width : img.Height);
            var bmp = new Bitmap((int)(img.Width * k), (int)(img.Height * k));
            var graphic = Graphics.FromImage(bmp);
            graphic.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.High;
            graphic.DrawImage(img, 0, 0, (int)(img.Width * k), (int)(img.Height * k));
            graphic.Dispose();
            bmp.Save(currentDir + "_sm.jpeg", System.Drawing.Imaging.ImageFormat.Jpeg);

            if (File.Exists(currentDir + "_med.jpeg"))
                File.Delete(currentDir + "_med.jpeg");
            k = 150.0 / (img.Width > img.Height ? img.Width : img.Height);
            bmp = new Bitmap((int)(img.Width * k), (int)(img.Height * k));
            graphic = Graphics.FromImage(bmp);
            graphic.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBilinear;
            graphic.DrawImage(img, 0, 0, (int)(img.Width * k), (int)(img.Height * k));
            graphic.Dispose();
            bmp.Save(currentDir + "_med.jpeg", System.Drawing.Imaging.ImageFormat.Jpeg);
        }*/
    }
}