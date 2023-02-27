﻿using System;
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
using System.Net.Http;
using System.Dynamic;
using System.Runtime.CompilerServices;
using System.Reflection.Metadata;

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

public class ScheduleItem
{
    public int realm_id { get; set; }
    public string faction_id { get; set; }
}

public class RealmConfigItem
{
    public int id { get; set;}
    public string expansion { get; set; }
    public string name { get; set; }
    public string connection_url { get; set; }
    public string faction { get; set; }
    public RealmConfigDataItem data { get; set; }

}

public class RealmConfigDataItem
{
    public string username { get; set; }
    public string password { get; set; }
    public string auctioneer { get; set; }
    public string keybind { get; set; }
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
        static HttpClient client;

        // Perform blocking http Get using async methods
        public static string Get(string uri)
        {
            // Initialise return value
            string returnValue;
            // Perform get action
            using (var responseTask = client.GetAsync(uri))
            {
                // Wait for response, and ensure there wasn't an error
                responseTask.Wait();
                responseTask.Result.EnsureSuccessStatusCode();

                // Get response string
                var response = responseTask.Result;
                using (var resultTask = response.Content.ReadAsStringAsync())
                {
                    resultTask.Wait();

                    // Set return value to response string
                    returnValue = resultTask.Result;
                }
            }

            return returnValue;
        }

        public static FileStream SetLockFile(int realmId, string factionId)
        {
            var fileName = Directory.GetCurrentDirectory() + $"\\{realmId}-{factionId}.LCK";
            FileStream result;

            try
            {
                // If the file exists, try to delete. It won't let us if really open
                if (File.Exists(fileName))
                    File.Delete(fileName);

                // Else create the file with full locking, this will fail if someone already did the same
                result = File.Open(fileName, FileMode.Create, FileAccess.ReadWrite, FileShare.None);
            }
            catch
            {
                // Any error, return null (we don't have lock)
                return null;
            }

            // Else return the stream to enable unlocking later
            return result;
        }

        public static void ClearLockFile(FileStream fs)
        {
            var fileName = fs.Name;
            fs.Close();
            File.Delete(fileName);
        }

        static void Main(string[] args)
        {
            // Create client handler to accept and decompress GZip and Deflate compressed data
            HttpClientHandler handler = new HttpClientHandler();
            handler.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;

            // Initialize client
            client = new HttpClient(handler);

            // Set linux flag depending on command line
            bool linux = false;
            string[] limitRealms = null;
            string limitFaction = "";

            if (args.Length > 0)
                linux = (args[0] == "1");

            if (args.Length > 1)
                limitFaction = args[1].ToLower();

            if (args.Length > 2)
                limitRealms = args[2].Split(",");


            ArrayList processData = new ArrayList();
            dynamic realms;
            Console.WriteLine("Reading config file...");
            var ini = new IniFile(Directory.GetCurrentDirectory()+"\\config.ini");
            var baseUrl = ini.Read("base_url", "config");
            var queryUrl = ini.Read("query_url", "config");
            var wowPathWotlk = ini.Read("wow_path_wotlk", "config");
            var wowPathTbc = ini.Read("wow_path_tbc", "config");
            if (!int.TryParse(ini.Read("wow_timeout", "config"), out int wowTimeout) || wowTimeout == 0)
                wowTimeout = 120;

            // Add some time for wowinstance to finish before we force its hand
            wowTimeout += 10;

            var nextScreenshotClear = DateTime.UtcNow;
            while (true) // loop always
            {
                // Once a day clean up screenshots older than 2 days
                if (DateTime.UtcNow >= nextScreenshotClear)
                {
                    // Get file info for all jpg files in screenshots folder
                    var files = Directory.GetFiles(Directory.GetCurrentDirectory() + "\\Screenshots", "*.jpg").Select(file => new FileInfo(file));
                    Console.WriteLine($"Checking {files.Count()} screenshots for deletion candidates");
                    int deletedFiles = 0;

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
                                    deletedFiles++;
                                }
                                catch (Exception ex)
                                {
                                    Console.WriteLine($"Failed to delete {file.FullName}: {ex.Message}");
                                }
                            }
                        }
                    }
                    Console.WriteLine($"Deleted {deletedFiles} screenshot files");

                    // Schedule another check in 24 hours
                    nextScreenshotClear = DateTime.Now.AddHours(24);
                }

                var startTimeStamp = (Int32)(DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1))).TotalSeconds;
                var scheduleUrl = baseUrl + "scheduler/index";
                var scheduleResponse = Get(scheduleUrl);
                var realmId = "";
                var factionId = "";
                var scheduleJson = JsonConvert.DeserializeObject<IEnumerable<ScheduleItem>>(scheduleResponse);

                foreach (var scheduleItem in scheduleJson)
                {
                    realmId = scheduleItem.realm_id.ToString();
                    factionId = scheduleItem.faction_id;

                    if (!string.IsNullOrWhiteSpace(limitFaction) && limitFaction != factionId)
                        continue;

                    if (limitRealms != null && limitRealms.Any() && !limitRealms.Contains(realmId))
                        continue;

                    // Try to get a lock for this realm/faction
                    var lockFile = SetLockFile(scheduleItem.realm_id, factionId);

                    // If we couldn't get a lock, move to next entry
                    if (lockFile == null)
                    {
                        Console.WriteLine($"Realm {realmId} for faction {factionId} locked by another process, skipping.");
                        continue;
                    }

                    // Try to get a lock file for this realm/faction (in case of multi launchers)                    
                    var realmConfigUrl = baseUrl + queryUrl + "?&id=" + realmId + "&f=" + factionId;
                    try
                    {
                        var realmConfigResponse = Get(realmConfigUrl);
                        var realmConfigJson = JsonConvert.DeserializeObject<IEnumerable<RealmConfigItem>>(realmConfigResponse);
                        realms = realmConfigJson;
                        foreach (var realmConfig in realmConfigJson)
                        {
                            var expac = realmConfig.expansion;
                            var wowPath = wowPathWotlk;
                            if (expac == "3.3.5")
                                wowPath = wowPathWotlk;
                            else if (expac == "2.4.3")
                                wowPath = wowPathTbc;

                            WoWInstance wowdata = new WoWInstance();
                            wowdata.WowProcess = new Process();
                            wowdata.WowProcess.StartInfo.FileName = wowPath + "wowinstance.exe";
                            wowdata.WowProcess.StartInfo.WorkingDirectory = wowPath;
                            wowdata.RealmName = realmConfig.name;
                            wowdata.ConnectionUrl = realmConfig.connection_url;
                            wowdata.RealmId = realmConfig.id;

                            wowdata.AccountName = realmConfig.data.username;
                            wowdata.AuctioneerName = realmConfig.data.auctioneer;
                            wowdata.Faction = realmConfig.faction;
                            wowdata.Password = realmConfig.data.password;
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
                            wowdata.WowProcess.StartInfo.Arguments = wowdata.RealmId + " \"" + wowdata.RealmName + "\" \"" + wowdata.ConnectionUrl + "\" " + wowdata.AccountName + " \"";
                            wowdata.WowProcess.StartInfo.Arguments += wowdata.Password + "\" \"" + wowdata.AuctioneerName + "\" \"" + baseUrl + "\" " + wowdata.Faction + " " + wowTimeout.ToString();
                            if (linux)
                                wowdata.WowProcess.StartInfo.Arguments += " 1";

                            Console.WriteLine("Start ..." + wowdata.WowProcess.StartInfo.Arguments);
                            if (wowdata.WowProcess.Start())
                            {
                                Console.WriteLine("Process for realm id " + realmConfig.id);
                                processData.Add(wowdata);
                            }

                            // Wait for process to exit or kill after 2 hours
                            if (wowdata.WowProcess.WaitForExit(wowTimeout * 60 * 1000))     // Wait just over 1 hour
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
                            ClearLockFile(lockFile);
                            processData.Remove(wowdata);
                            wowdata.WowProcess.Dispose();
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
