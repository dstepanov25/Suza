using System;
//using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Collections.Generic;

namespace FindImages
{

    public class YouTube
    {
        private readonly DataClasses1DataContext d;
        private string ip = "";
        private int port = 0;
            
        public YouTube(DataClasses1DataContext _d)
        {
            d = _d;
            GetProxy();
        }

        public bool GetDescriptions(Product currentProduct, string searchURL)
        {
            var findResult = PageEncoding.GetUTFPage(searchURL, ip, port);
            if (findResult == null)
            {
                GetProxy();
                return false;
            }

            //if (findResult.Contains("margin-top: 10px; font-size: 15px"))
            if (findResult.Contains("<div class=\"result-item *sr\">"))
            {
                findResult = findResult.Split(new[] { "<div class=\"result-item *sr\">" }, 2, StringSplitOptions.None)[1];
                findResult = findResult.Split(new[] { "href=\"" }, 2, StringSplitOptions.None)[1];
                if (findResult.Contains("\" class=\"ux-thumb-wrap"))
                {
                    var firstModelURL = findResult.Split(new[] { "\" class=\"ux-thumb-wrap" }, 2, StringSplitOptions.None)[0];
                    if (firstModelURL.Contains("&"))
                    {
                        firstModelURL = firstModelURL.Split(new[] { "&" }, 2, StringSplitOptions.None)[0];
                    }
                    firstModelURL = "http://youtube.com/v/" + firstModelURL.Replace("/watch?v=", "") + "?fs=1&amp;hl=ru_RU";
                    d.ProductsVideos.InsertOnSubmit(new ProductsVideo { VideoChecked = false, Link = firstModelURL });
                    d.SubmitChanges();
                    currentProduct.VideoId = d.ProductsVideos.Max(t => t.VideoId);
                    d.SubmitChanges();
                    Console.WriteLine(currentProduct.ProductFullName);
                    return true;
                }
                else
                {
                    currentProduct.VideoId = -1;
                    d.SubmitChanges();
                    return false;
                }
            }
            else
            {
                //var color = d.ProductsColors.ToList().Where(t => searchURL.Contains(t.ColorName)).FirstOrDefault();
                //if (color != null)
                //{
                //    searchURL = searchURL.Replace(color.ColorName, "");
                //    return GetDescriptions(currentProduct, searchURL);
                //}
                currentProduct.VideoId = -1;
                d.SubmitChanges();
                return false;
            }
        }

        private void GetProxy()
        {
            var url = "http://www.youtube.com/";
            var proxy = Proxy.GetGoodProxy(url);
            if (proxy != "")
            {
                ip = proxy.Split(new[] { ":" }, StringSplitOptions.None)[0];
                port = Int32.Parse(proxy.Split(new[] { ":" }, StringSplitOptions.None)[1]);
            }
        }

    }
}