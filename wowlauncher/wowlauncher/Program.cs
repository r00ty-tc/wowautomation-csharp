using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using Newtonsoft.Json;
using System.Diagnostics;
using System.Collections;
using System.Security.Cryptography.X509Certificates;

class WoWInstance
{
    public int RealmId;
    public string RealmName;
    public string AccountName;
    public string Password;
    public string AuctioneerName;
    public string Faction;
    public int LastModifiedDate;
    public long Size;
    public string ConnectionUrl;
    public Process WowProcess;
}
namespace wowlauncher
{
    class IniFile   // revision 11
    {
        string Path;
        string EXE = Assembly.GetExecutingAssembly().GetName().Name;

        [DllImport("kernel32", CharSet = CharSet.Unicode)]
        static extern long WritePrivateProfileString(string Section, string Key, string Value, string FilePath);

        [DllImport("kernel32", CharSet = CharSet.Unicode)]
        static extern int GetPrivateProfileString(string Section, string Key, string Default, StringBuilder RetVal, int Size, string FilePath);

        public IniFile(string IniPath = null)
        {
            Path = new FileInfo(IniPath ?? EXE + ".ini").FullName.ToString();
        }

        public string Read(string Key, string Section = null)
        {
            var RetVal = new StringBuilder(255);
            GetPrivateProfileString(Section ?? EXE, Key, "", RetVal, 255, Path);
            return RetVal.ToString();
        }

        public void Write(string Key, string Value, string Section = null)
        {
            WritePrivateProfileString(Section ?? EXE, Key, Value, Path);
        }

        public void DeleteKey(string Key, string Section = null)
        {
            Write(Key, null, Section ?? EXE);
        }

        public void DeleteSection(string Section = null)
        {
            Write(null, null, Section ?? EXE);
        }

        public bool KeyExists(string Key, string Section = null)
        {
            return Read(Key, Section).Length > 0;
        }
    }
    class Program
    {
        

        public string Get(string uri)
        {
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(uri);
            request.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;

            using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
            using (Stream stream = response.GetResponseStream())
            using (StreamReader reader = new StreamReader(stream))
            {
                return reader.ReadToEnd();
            }
        }
        
        static void Main(string[] args)
        {
            // Relax certificate check (TLS won't always be accepted)
            ServicePointManager.ServerCertificateValidationCallback += new System.Net.Security.RemoteCertificateValidationCallback(delegate(object sender, X509Certificate cert, X509Chain chain, System.Net.Security.SslPolicyErrors error)
            {
                return true;
            });

            bool linux = false;

            if (args.Length > 0)
                linux = (args[0] == "1");

            ArrayList processData = new ArrayList();
            dynamic realms;
            Console.WriteLine("Reading config file...");
            var ini = new IniFile(Directory.GetCurrentDirectory()+"\\config.ini");
            var baseUrl = ini.Read("base_url", "config");
            var queryUrl = ini.Read("query_url", "config");
            var wowPathWotlk = ini.Read("wow_path_wotlk", "config");
            var wowPathTbc = ini.Read("wow_path_tbc", "config");
            var nextScreenshotClear = DateTime.UtcNow;
            while (true) // loop always
            {
                // Once a day clean up screenshots older than 2 days
                if (DateTime.UtcNow >= nextScreenshotClear)
                {
                    // Get file info for all jpg files in screenshots folder
                    var files = Directory.GetFiles(Directory.GetCurrentDirectory() + "\\Screenshots", "*.jpg").Select(file => new FileInfo(file));
                    Console.WriteLine($"Checking {files.Count()} screenshots for deletion candidates");

                    // Loop each file
                    foreach (var file in files)
                    {
                        // Extract date from filename
                        var datePart = file.Name.Replace("WoWScrnShot_", "").Replace(".jpg", "");

                        // Validate the date
                        if (DateTime.TryParseExact(datePart, "MMddyy_HHmmss", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AssumeLocal, out DateTime fileDate))
                        {
                            // If the filename is more than 48 hours old, we'll delete it
                            if (DateTime.Now.Subtract(fileDate).TotalHours > 48)
                            {
                                // Try to delete failing gracefully)
                                Console.WriteLine($"Deleting {file.Name}");
                                try
                                {
                                    File.Delete(file.FullName);
                                }
                                catch (Exception ex)
                                {
                                    Console.WriteLine($"Failed to delete {file.FullName}: {ex.Message}");
                                }
                            }
                        }
                    }

                    // Schedule another check in 24 hours
                    nextScreenshotClear = DateTime.Now.AddHours(24);
                }

                var startTimeStamp = (Int32)(DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1))).TotalSeconds;
                var scheduleUrl = baseUrl + "scheduler/index";
                var scheduleResponse = new WebClient().DownloadString(scheduleUrl);
                var realmId = "";
                var factionId = "";
                dynamic scheduleJson = JsonConvert.DeserializeObject(scheduleResponse);

                // Search for any running wow
                if (scheduleJson.Count > 0)
                {
                    //var processes = Process.GetProcessesByName("wowclean.exe");
                    var processes = Process.GetProcesses().Where(row => row.ProcessName.ToLower().Contains("warcraft"));
                    foreach (var process in processes)
                    {
                        Console.WriteLine($"Killing existing {process.ProcessName} process with id {process.Id}");
                        process.Kill();
                        if (!process.WaitForExit(5000))
                            Console.WriteLine(" -- Failed!");

                        process.Dispose();
                    }
                }

                for (var c = 0; c < scheduleJson.Count; c++)
                {
                    realmId = scheduleJson[c].realm_id;
                    factionId = scheduleJson[c].faction_id;
                    var realmConfigUrl = baseUrl + queryUrl + "?&id=" + realmId + "&f=" + factionId;
                    try
                    {
                        var realmConfigResponse = new WebClient().DownloadString(realmConfigUrl);
                        dynamic realmConfigJson = JsonConvert.DeserializeObject(realmConfigResponse);
                        realms = realmConfigJson;
                        for (var i = 0; i < realmConfigJson.Count; i++)
                        {
                            var expac = realmConfigJson[i].expansion;
                            var wowPath = wowPathWotlk;
                            if (expac == "3.3.5")
                                wowPath = wowPathWotlk;
                            else if (expac == "2.4.3")
                                wowPath = wowPathTbc;

                            WoWInstance wowdata = new WoWInstance();
                            wowdata.WowProcess = new Process();
                            wowdata.WowProcess.StartInfo.FileName = wowPath + "wowinstance.exe";
                            wowdata.WowProcess.StartInfo.WorkingDirectory = wowPath;
                            wowdata.RealmName = realmConfigJson[i].name;
                            wowdata.ConnectionUrl = realmConfigJson[i].connection_url;
                            wowdata.RealmId = realmConfigJson[i].id;

                            wowdata.AccountName = realmConfigJson[i].data.username;
                            wowdata.AuctioneerName = realmConfigJson[i].data.auctioneer;
                            wowdata.Faction = realmConfigJson[i].faction;
                            wowdata.Password = realmConfigJson[i].data.password;
                            string auctionData = wowPath + "WTF\\Account\\" + wowdata.AccountName.ToUpper() + "\\SavedVariables\\Auc-ScanData.lua";
                            if (File.Exists(auctionData))
                            {
                                wowdata.Size = new System.IO.FileInfo(auctionData).Length;
                            }
                            else
                            {
                                wowdata.LastModifiedDate = 0;
                                wowdata.Size = 0;
                            }
                            wowdata.WowProcess.StartInfo.Arguments = wowdata.RealmId + " \"" + wowdata.RealmName + "\" \"" + wowdata.ConnectionUrl + "\" " + wowdata.AccountName + " \"" + wowdata.Password + "\" \"" + wowdata.AuctioneerName + "\" \"" + baseUrl + "\" " + wowdata.Faction;
                            if (linux)
                                wowdata.WowProcess.StartInfo.Arguments += " 1";

                            Console.WriteLine("Start ..." + wowdata.WowProcess.StartInfo.Arguments);
                            if (wowdata.WowProcess.Start())
                            {
                                Console.WriteLine("Process for realm id " + realmConfigJson[i].id);
                                processData.Add(wowdata);
                            }

                            // Wait for process to exit or kill after 2 hours
                            if (wowdata.WowProcess.WaitForExit(65 * 60 * 1000))     // Wait just over 1 hour
                            {
                                if (wowdata.WowProcess.ExitCode == 0) // everything OK
                                {
                                    Console.WriteLine("Realm " + wowdata.RealmName + " " + wowdata.Faction + " eneded successfully");
                                }
                                else if (wowdata.WowProcess.ExitCode == 1) // start failure
                                {
                                    Console.WriteLine("Realm " + wowdata.RealmName + " " + wowdata.Faction + " start failed");
                                }
                                else if (wowdata.WowProcess.ExitCode == 2) // login or script problem
                                {
                                    Console.WriteLine("Realm " + wowdata.RealmName + " " + wowdata.Faction + " scripting problem");
                                }
                            }
                            else
                            {
                                Console.WriteLine("Process failed to exit within 1 hour");
                                wowdata.WowProcess.Kill();
                                wowdata.WowProcess.WaitForExit();
                            }
                            processData.Remove(wowdata);
                            wowdata.WowProcess.Dispose();

                            //System.Threading.Thread.Sleep(60000);
                        }
                    }
                    catch (WebException e) 
                    {
                        Console.WriteLine(e.Message);
                    }
                }
                while (processData.Count > 0)
                {
                    for (var i = 0; i < processData.Count; i++)
                    {
                        WoWInstance wow = (WoWInstance)processData[i];
                        if (wow.WowProcess.HasExited)
                        {
                            if (wow.WowProcess.ExitCode == 0) // everything OK
                            {
                                Console.WriteLine("Realm " + wow.RealmName + " " + wow.Faction + " eneded successfully");
                            }
                            else if (wow.WowProcess.ExitCode == 1) // start failure
                            {
                                Console.WriteLine("Realm " + wow.RealmName + " " + wow.Faction + " start failed");
                            }
                            else if (wow.WowProcess.ExitCode == 2) // login or script problem
                            {
                                Console.WriteLine("Realm " + wow.RealmName + " " + wow.Faction + " scripting problem");
                            }
                            processData.RemoveAt(i);
                            wow.WowProcess.Dispose();
                        }
                    }
                    System.Threading.Thread.Sleep(1000);
                }

                System.Threading.Thread.Sleep(60 * 1000); // Wait a minute
            }
            
        }
    }
}
