using System;
using System.Collections.Generic;
//using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;

namespace FindImages
{
    /// <summary>
    /// Summary description for Images
    /// </summary>
    public class GoogleImages
    {
        private DataClasses1DataContext d;
        private readonly int minImageSize;
        private string ip = "";
        private int port = 0;
        private List<string> bannedImages;
        private List<string> bannedHosts;
        private readonly string[] chars = new[] { "\\", "/", "*", "'", "\"", "?", "|", "<", ">", ":", "&" };
        private string homeFolder;
        public List<WebResponse> potentialImages;
        public string imageHost = "";

        public GoogleImages(int _minImageSize, string _homeFolder)
        {
            minImageSize = _minImageSize;
            homeFolder = _homeFolder;
            potentialImages = new List<WebResponse>();
        }

        public bool FindNewImages(Product product)
        {
            d = new DataClasses1DataContext();
            bannedHosts = d.BannedImagesSites.Select(t => t.BannedHost).ToList();
            bannedImages = d.BannedImages.Select(t => t.BannedImage1).ToList();
            potentialImages.Clear();

            var productName = product.ProductFullName;
            foreach (var c in chars)
            {
                productName = productName.Replace(c, " ");
            }

            var searchURL = "http://ajax.googleapis.com/ajax/services/search/images?v=1.0&q=" + productName;
            var findResult = PageEncoding.GetPage(searchURL, null, null, ip, port);
            if (findResult == null || findResult.Contains("Bot or Human"))
            {
                return false;
            }

            // выбираем самую большую
            GetImagesList(findResult);
            foreach (var potentialImage in potentialImages.OrderByDescending(t => t.ContentLength).ToList())
            {
                var result = GetImage(potentialImage);
                if (result)
                {
                    //imageHost = potentialImage.ResponseUri.Host;
                    imageHost = potentialImage.ResponseUri.AbsoluteUri.Replace("www.", "");
                    return true;
                }
            }
            return false;
        }

        private void GetImagesList(string findResult)
        {
            while (findResult != "")
            {
                var x = new string[1];
                x[0] = "unescapedUrl\":\"";
                if (!findResult.Contains(x[0])) break;
                findResult = findResult.Split(x, 2, StringSplitOptions.None)[1];
                x[0] = "\"";
                var href = findResult.Split(x, 2, StringSplitOptions.None)[0];
                var host = (new Uri(href)).Host.Replace("www.", "");
                if (!bannedHosts.Contains(host) && !bannedImages.Contains(href.Replace("www.", "")))
                    GetPhoto(href);                 
            }
        }

        private void GetPhoto(string href)
        {
            try
            {
                var request = (HttpWebRequest)WebRequest.Create(href);
                request.Timeout = 16000;
                WebResponse response = request.GetResponse();
                href = response.ResponseUri.AbsoluteUri;
                var host = (new Uri(href)).Host.Replace("www.", "");
                if (response.ContentType.Contains("image/jpeg") && !bannedHosts.Contains(host) 
                    && !bannedImages.Contains(href.Replace("www.", "")))
                {
                    potentialImages.Add(response);
                }
            }
            catch (Exception)
            {
            }
        }

        private bool GetImage(WebResponse response)
        {
            if (response.ContentLength < minImageSize && response.ContentLength > 0) return false;
            try
            {
                var remoteStream = response.GetResponseStream();

                var _filePath = homeFolder + "temp.jpeg";

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
                    return false;
                }
                //imageHost = response.ResponseUri.Host;
                imageHost = response.ResponseUri.AbsoluteUri.Replace("www.", "");
                return true;
            }
            catch 
            {
                return false;
            }
        }
    }
}