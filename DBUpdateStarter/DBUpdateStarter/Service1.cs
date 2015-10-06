using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Xml;

namespace DBUpdateStarter
{
    public partial class Service1 : ServiceBase
    {
        private StreamWriter file;
        private System.Timers.Timer timer1;
        private System.Timers.Timer timer2;
        private System.Timers.Timer timer3;
        private System.Timers.Timer timer4;
        List<ProjectSettings> ListOfSettings;

        public Service1()
        {
            InitializeComponent();
        }

        protected override void OnStart(string[] args)
        {
            if (!LoadSettings()) return;

            //Создаем таймер и выставляем его параметры
            timer1 = new System.Timers.Timer();
            timer1.Enabled = true;
            timer1.Interval = 60000;
            timer1.Elapsed += new System.Timers.ElapsedEventHandler(this.timer1_Elapsed);
            timer1.AutoReset = true;
            timer1.Start();

            timer2 = new System.Timers.Timer();
            timer2.Enabled = true;
            timer2.Interval = 300000;
            timer2.Elapsed += new System.Timers.ElapsedEventHandler(this.timer2_Elapsed);
            timer2.AutoReset = true;
            timer2.Start();

            timer3 = new System.Timers.Timer();
            timer3.Enabled = true;
            timer3.Interval = 600000;
            timer3.Elapsed += new System.Timers.ElapsedEventHandler(this.timer3_Elapsed);
            timer3.AutoReset = true;
            timer3.Start();

            timer4 = new System.Timers.Timer();
            timer4.Enabled = true;
            timer4.Interval = 600000;
            timer4.Elapsed += new System.Timers.ElapsedEventHandler(this.timer4_Elapsed);
            timer4.AutoReset = true;
            timer4.Start();
        }

        protected override void OnStop()
        {
            file.Close();
            timer1.Stop();
            timer2.Stop();
        }

        private void timer1_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            foreach (var project in ListOfSettings)
            {
                // запускать проверку прихода новых прайсов
                try
                {
                    if (!HavingPricesToUpdate(project)) continue;
                    file.WriteLine(DateTime.Now + "\tПроцесс обновления запускается");
                    file.Flush();

                    // запустить SuzaDBUpdator если он в данный момент не работает
                    var pProcess = Process.GetProcessesByName("SuzaDBUpdator");
                    if (pProcess.Count() > 0)
                    {
                        file.WriteLine(DateTime.Now + "\tПроцесс обновления уже запущен");
                        file.Flush();
                        continue;
                    }
                    var path = project.DirWithProgramm + "\\SuzaDBUpdator";
                    var programFile = new FileInfo(path + ".exe");
                    if (!programFile.Exists)
                    {
                        file.WriteLine(DateTime.Now + "\tПрограмма SuzaDBUpdator отсутствует: " + path + ".exe");
                        file.Flush();
                        continue;
                    }
                    System.Diagnostics.ProcessStartInfo pSI = new System.Diagnostics.ProcessStartInfo(path);
                    pSI.UseShellExecute = false;
                    pSI.RedirectStandardOutput = true;
                    pSI.RedirectStandardInput = true;
                    pSI.RedirectStandardError = true;
                    System.Diagnostics.Process proc = System.Diagnostics.Process.Start(pSI);
                    file.WriteLine(DateTime.Now + "\t" + project.DirWithProgramm + "\tЗапущено обновление " + project.DirWithProgramm);
                    file.Flush();
                }
                catch (Exception ex)
                {
                    file.WriteLine(DateTime.Now + "\t" + project.DirWithProgramm + "\t" + ex.Message);
                    file.Flush();
                    continue;
                }
            }
        }

        private void timer2_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            foreach (var project in ListOfSettings)
            {
                try
                {
                    var path = project.DirWithProgramm + "\\MailReader";
                    var programFile = new FileInfo(path + ".exe");
                    if (!programFile.Exists) continue;                    
                    System.Diagnostics.ProcessStartInfo pSI = new System.Diagnostics.ProcessStartInfo(path);                    
                    pSI.UseShellExecute = false;
                    pSI.RedirectStandardOutput = true;
                    pSI.RedirectStandardInput = true;
                    pSI.RedirectStandardError = true;
                    System.Diagnostics.Process proc = System.Diagnostics.Process.Start(pSI);
                }
                catch (Exception ex)
                {
                    continue;
                }
            }
        }

        private void timer3_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            foreach (var project in ListOfSettings)
            {
                try
                {
                    var path = project.DirWithProgramm + "\\PricesGenerator";
                    var programFile = new FileInfo(path + ".exe");
                    if (!programFile.Exists) continue;
                    System.Diagnostics.ProcessStartInfo pSI = new System.Diagnostics.ProcessStartInfo(path);
                    pSI.UseShellExecute = false;
                    pSI.RedirectStandardOutput = true;
                    pSI.RedirectStandardInput = true;
                    pSI.RedirectStandardError = true;
                    System.Diagnostics.Process proc = System.Diagnostics.Process.Start(pSI);
                }
                catch (Exception ex)
                {
                    continue;
                }
            }
        }

        private void timer4_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            foreach (var project in ListOfSettings)
            {
                try
                {
                    var path = project.DirWithProgramm + "\\PlazaDBUpdator";
                    var programFile = new FileInfo(path + ".exe");
                    if (!programFile.Exists) continue;
                    System.Diagnostics.ProcessStartInfo pSI = new System.Diagnostics.ProcessStartInfo(path);
                    pSI.UseShellExecute = false;
                    pSI.RedirectStandardOutput = true;
                    pSI.RedirectStandardInput = true;
                    pSI.RedirectStandardError = true;
                    System.Diagnostics.Process proc = System.Diagnostics.Process.Start(pSI);
                }
                catch (Exception ex)
                {
                    continue;
                }
            }
        }

        private bool HavingPricesToUpdate(ProjectSettings project)
        {
            Properties.Settings.Default.PropertyValues["suzadbConnectionString"].PropertyValue = project.ConnectionString;
            var d = new DataClasses1DataContext();
            d.Connection.Open();

            var di = new DirectoryInfo(project.DirWithPrices);

            var pricesToUpdate = (from f in di.GetFiles("*", SearchOption.AllDirectories)
                                  join pl in d.PriceLists on f.Name.Replace(f.Extension, "").ToLower() equals pl.PriceListName.ToLower()
                                  where (pl.UpdateMode > 0 /*|| pl.LastUpdate < f.LastWriteTime*/) && pl.UpdateFromMail
                                  orderby pl.PriceListName
                                  select new { f, pl }).Distinct();
            if (pricesToUpdate.Count() > 0)
            {
                file.WriteLine(DateTime.Now + "\t" + project.DirWithProgramm + "\tНовых прайсов: " + pricesToUpdate.Count());
                
                /*file.WriteLine(DateTime.Now + "\t" + pricesToUpdate.FirstOrDefault().f.FullName
                    + "\t" + pricesToUpdate.FirstOrDefault().pl.PriceListID                    
                    + "\t" + pricesToUpdate.FirstOrDefault().pl.LastUpdate
                    + "\t" + pricesToUpdate.FirstOrDefault().f.LastWriteTime);*/
                file.Flush();
                return true;
            }
            //file.WriteLine(DateTime.Now + "\tНовых прайсов: 0");
            //file.Flush();
            return false;
        }

        private bool LoadSettings()
        {
            file = new StreamWriter(new FileStream("Service1.log", System.IO.FileMode.Append));
            try
            {
                ListOfSettings = new List<ProjectSettings>();
                var disk = Environment.GetEnvironmentVariable("windir").Split(':')[0];
                var di = new DirectoryInfo(disk + ":\\Inetpub\\wwwroot");
                if (!di.Exists) 
                {
                    file.WriteLine(DateTime.Now + "\tПапка " + disk + ":\\Inetpub\\wwwroot не найден");
                    file.Flush();
                    file.Close();
                    return false;
                }
                var projectsSettingsFiles = di.GetFiles("Settings.xml", SearchOption.AllDirectories);
                foreach (var settingsFile in projectsSettingsFiles)
                {
                    XmlDocument xd = new XmlDocument();
                    xd.Load(settingsFile.FullName);
                    XmlNode root = xd.DocumentElement;
                    var x = Properties.Settings.Default.suzadbConnectionString;
                    Properties.Settings.Default.PropertyValues["suzadbConnectionString"].PropertyValue = root.SelectSingleNode("ConnectionString").InnerText;
                    var d = new DataClasses1DataContext();
                    d.Connection.Open();

                    ListOfSettings.Add(new ProjectSettings
                    {
                        ConnectionString = root.SelectSingleNode("ConnectionString").InnerText,
                        DirWithPrices = root.SelectSingleNode("DirWithPrices").InnerText,
                        DirWithProgramm = settingsFile.DirectoryName
                    });
                }
                file.WriteLine("Настройки загружены: " + ListOfSettings.Count);
                file.Flush();
            }
            catch (Exception ex)
            {
                file.WriteLine(DateTime.Now + "\tНастройки не загружены: " + ex.Message);
                file.Flush();
                file.Close();
                System.Threading.Thread.Sleep(60000);
                LoadSettings();
            }
            return true;
        }

        class ProjectSettings
        {
            public string DirWithPrices { get; set; }
            public string ConnectionString { get; set; }
            public string DirWithProgramm { get; set; }
        }
    }
}
