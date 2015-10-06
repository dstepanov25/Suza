using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.IO;
using System.Threading;
using System.Xml;
using Jelovic.FtpLib;

namespace FindImages
{
    class Program
    {
        private static DataClasses1DataContext d;
        private static string homeFolder;
        public static string imageHost = "";
            
        static void Main(string[] args)
        {
            if (!LoadSettings()) return;
            d = new DataClasses1DataContext();
            FindImages();
            ClearImages();
            MissingImages();
            FindYoutubes();
        }

        private static void ClearImages()
        {
            Console.WriteLine("Clear Images");
            var di = new DirectoryInfo(homeFolder);
            var filesCount = di.GetFiles().Where(t => !t.Name.Contains("_med") && !t.Name.Contains("_sm")).Count();
            var dbImagesCount = d.ProductsImages.Count();
            if (dbImagesCount != filesCount)
            {
                var internetAccess = new InternetAccess();
                var siteURL = d.Settings.Where(t => t.SettingName == "URL").FirstOrDefault().SettingValue;
                var site = (from s in d.Sites where s.SiteURL == siteURL select s).FirstOrDefault();
                try
                {
                    var connection = internetAccess.FtpConnect(site.FTPServer, 21, site.FTPUsername, site.FTPPass, true);
                    connection.SetDirectory(site.ImagesPath);
                    var productsImages = d.ProductsImages.Select(t=>t.ImageID.ToString()).ToList();
                    var files = di.GetFiles().Where(t => !t.Name.Contains("_med") && !t.Name.Contains("_sm"));
                    foreach (var file in files)
                    {
                        var imageId = file.Name.Replace(".jpeg", "");
                        if (!productsImages.Contains(imageId))
                        {
                            var medfile = new FileInfo(file.Name.Replace(".jpeg", "_med.jpeg"));
                            var smfile = new FileInfo(file.Name.Replace(".jpeg", "_sm.jpeg"));
                            file.Delete();
                            medfile.Delete();
                            smfile.Delete();
                            // удалить по ftp
                            try
                            {
                                connection.DeleteFile(imageId + ".jpeg");
                                connection.DeleteFile(imageId + "_med.jpeg");
                                connection.DeleteFile(imageId + "_sm.jpeg");
                            }
                            catch { }

                        }
                    }
                    foreach (var img in d.ProductsImages)
                    {
                        if (!new FileInfo(di.FullName + "\\" + img.ImageID + ".jpeg").Exists)
                        {
                            var product = d.Products.Where(t => t.ImageId == img.ImageID).FirstOrDefault();
                            if (product != null)
                                product.ImageId = null;
                            d.ProductsImages.DeleteOnSubmit(img);
                        }
                    }
                    d.SubmitChanges();
                }
                catch { }
            }
        }

        private static void MissingImages()
        {
            var checkedImages = d.ProductsImages.Where(t => t.ImageChecked).ToList();
            var internetAccess = new InternetAccess();
            var siteURL = d.Settings.Where(t => t.SettingName == "URL").FirstOrDefault().SettingValue;
            var site = (from s in d.Sites where s.SiteURL == siteURL select s).FirstOrDefault();
            var connection = internetAccess.FtpConnect(site.FTPServer, 21, site.FTPUsername, site.FTPPass, true);
            connection.SetDirectory(site.ImagesPath);
            var files = connection.GetDirectoryContents("*.jpeg", CachingFlags.None);
            var filesNames = files.Where(t => !t.Name.Contains("_med") && !t.Name.Contains("_sm")).Select(t => t.Name.Replace(".jpeg", "")).ToList();

            if (checkedImages.Count != filesNames.Count)
            {
                foreach (var file in filesNames)
                {
                    if (checkedImages.Where(t => t.ImageID.ToString() == file).FirstOrDefault() == null)
                    {
                        // удалить по ftp
                        try
                        {
                            connection.DeleteFile(file + ".jpeg");
                            connection.DeleteFile(file + "_med.jpeg");
                            connection.DeleteFile(file + "_sm.jpeg");
                        }
                        catch { }
                    }
                }
                files = connection.GetDirectoryContents("*.jpeg", CachingFlags.None);
                filesNames = files.Select(t => t.Name.Replace(".jpeg", "")).ToList();
            }
            if (checkedImages.Count != filesNames.Count)
            {
                foreach (var image in checkedImages)
                {
                    if (!filesNames.Contains(image.ImageID.ToString()))
                    {
                        var originalImageName = homeFolder + image.ImageID.ToString(); ;
                        var ftpPath = site.ImagesPath + "/" + image.ImageID.ToString();
                        try
                        {
                            connection.UploadFile(originalImageName + ".jpeg", ftpPath + ".jpeg", TransferType.Binary, CachingFlags.None);
                            if (site.ResizeImages && site.SmallBigSize != null)
                            {
                                connection.UploadFile(originalImageName + "_sm.jpeg", ftpPath + "_sm.jpeg", TransferType.Binary, CachingFlags.None);
                            }
                            if (site.ResizeImages && site.MedBigSize != null)
                            {
                                connection.UploadFile(originalImageName + "_med.jpeg", ftpPath + "_med.jpeg", TransferType.Binary, CachingFlags.None);
                            }
                        }
                        catch { }
                    }
                }
            }
            connection.Dispose();
            internetAccess.Dispose();
        }

        private static bool LoadSettings()
        {
            Console.WriteLine("Load settings");
            try
            {
                XmlDocument xd = new XmlDocument();
                xd.Load(Thread.GetDomain().BaseDirectory + "Settings.xml");
                XmlNode root = xd.DocumentElement;
                var x = Properties.Settings.Default.suzadbConnectionString;
                Properties.Settings.Default.PropertyValues["suzadbConnectionString"].PropertyValue = root.SelectSingleNode("ConnectionString").InnerText;
                homeFolder = root.SelectSingleNode("ProductsImages").InnerText;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error. Settings not loaded");
                Console.WriteLine(ex.Message);
                Console.ReadLine();
                return false;
            }
            return true;
        }

        private static void FindYoutubes()
        {
            Console.WriteLine("Find Youtubes");
            var nextProduct = d.GetProductWithOutYoutube().FirstOrDefault();
            if (nextProduct == null) return;
            var yt = new YouTube(d);
            while (nextProduct != null)
            {
                var currentProduct = d.Products.Where(t => t.SiteProductId == nextProduct.SiteProductId).FirstOrDefault();
                if (currentProduct == null) continue;
                var searchURL = "http://www.youtube.com/results?search_query=" + currentProduct.ProductFullName.Replace("&", "%26");
                yt.GetDescriptions(currentProduct, searchURL);
                nextProduct = d.GetProductWithOutYoutube().FirstOrDefault();
                Thread.Sleep(3000);
            }
        }

        private static void FindImages()
        {
            Console.WriteLine("Find Images");
            var di = new DirectoryInfo(homeFolder);
            if (!di.Exists) di.Create();

            GoogleImages googleImages = null;
            YandexImages yandexImages = null;

            var nextProduct = d.GetProductWithOutImage().FirstOrDefault();
            while (nextProduct != null)
            {
                var currentProduct = d.Products.Where(t => t.SiteProductId == nextProduct.SiteProductId).FirstOrDefault();
                if (currentProduct == null) continue;
                if (googleImages == null)
                {
                    googleImages = new GoogleImages(15000, homeFolder);
                }
                if (yandexImages == null)
                {
                    yandexImages = new YandexImages(15000, homeFolder);
                }

                var potentialImages = new List<WebResponse>();
                var result = googleImages.FindNewImages(currentProduct);
                if (!result)
                {
                    result = yandexImages.FindNewImages(currentProduct);
                    if (!result)
                    {
                        potentialImages.AddRange(googleImages.potentialImages);
                        potentialImages.AddRange(yandexImages.potentialImages);
                        foreach (var potentialImage in potentialImages.OrderByDescending(t => t.ContentLength).ToList())
                        {
                            result = GetImage(potentialImage);
                            if (result)
                            {
                                imageHost = potentialImage.ResponseUri.Host;
                                imageHost = potentialImage.ResponseUri.AbsoluteUri.Replace("www.", "");
                                break;
                            }
                        }
                        if (result)
                            SaveImage(currentProduct);
                        else
                        {
                            currentProduct.ImageId = -2;
                            d.SubmitChanges();
                        }
                    }
                    else
                    {
                        imageHost = yandexImages.imageHost;
                        SaveImage(currentProduct);
                    }
                }
                else
                {
                    imageHost = googleImages.imageHost;
                    SaveImage(currentProduct);
                }
                nextProduct = d.GetProductWithOutImage().FirstOrDefault();
                Thread.Sleep(8000);
            }
        }

        private static void SaveImage(Product product)
        {
            Console.WriteLine(product.ProductFullName);
            var newProductImage = new ProductsImage
            {
                ImageChecked = false,
                ImageHost = imageHost, 
                LastModified = DateTime.Now
            };
            d.ProductsImages.InsertOnSubmit(newProductImage);
            d.SubmitChanges();
            imageHost = "";
            var imageId = d.ProductsImages.Max(@t => @t.ImageID);

            // gen filePath
            var _filePath = homeFolder + imageId + ".jpeg";

            var fi = new FileInfo(homeFolder + "temp.jpeg");
            if (fi.Exists)
            {
                var fi2 = new FileInfo(_filePath);
                if (fi2.Exists) fi2.Delete();
                fi.MoveTo(_filePath);
                d.SetProductImage(product.SiteProductId, imageId);
            }
            else
            {
                d.ProductsImages.DeleteAllOnSubmit(d.ProductsImages.Where(t => t.ImageID == imageId));
                d.SubmitChanges();
            }
        }

        private static bool GetImage(WebResponse response)
        {
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
                imageHost = response.ResponseUri.Host;
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
