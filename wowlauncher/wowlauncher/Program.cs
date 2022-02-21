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

class WoWInstance
{
    public int realm_id;
    public string realm_name;
    public string account_name;
    public string password;
    public string auctioneer_name;
    public string faction;
    public int last_modified_date;
    public long  size;
    public string connection_url;
    public Process wowprocess;
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
            string id = "";
            string faction = "";

            if (args.Length > 0)
                id = args[0];
            if (args.Length > 1)
                faction = args[1];

            ArrayList process_data = new ArrayList();
            dynamic realms;
            Console.WriteLine("Reading config file...");
            var ini = new IniFile(Directory.GetCurrentDirectory()+"\\config.ini");
            var base_url = ini.Read("base_url", "config");
            var query_url = ini.Read("query_url", "config");
            var wow_path_wotlk = ini.Read("wow_path_wotlk", "config");
            var wow_path_tbc = ini.Read("wow_path_tbc", "config");
            while (true) // loop always
            {
                var start_time_stamp = (Int32)(DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1))).TotalSeconds;
                var fetch_url = base_url + "scheduler/index";
                var json_str2 = new WebClient().DownloadString(fetch_url);
                var realm_id = "";
                var faction_id = "";
                dynamic schedule_json = JsonConvert.DeserializeObject(json_str2);
                for (var c = 0; c < schedule_json.Count; c++)
                {
                    realm_id = schedule_json[c].realm_id;
                    faction_id = schedule_json[c].faction_id;
                    var final_url = base_url + query_url + "?&id=" + realm_id + "&f=" + faction_id;
                    try
                    {
                        var json_str = new WebClient().DownloadString(final_url);
                        dynamic json = JsonConvert.DeserializeObject(json_str);
                        realms = json;
                        for (var i = 0; i < json.Count; i++)
                        {
                            var expac = json[i].expansion;
                            var wow_path = wow_path_wotlk;
                            if (expac == "3.3.5")
                                wow_path = wow_path_wotlk;
                            else if (expac == "2.4.3")
                                wow_path = wow_path_tbc;

                            WoWInstance wowdata = new WoWInstance();
                            wowdata.wowprocess = new Process();
                            wowdata.wowprocess.StartInfo.FileName = wow_path + "wowinstance.exe";
                            wowdata.wowprocess.StartInfo.WorkingDirectory = wow_path;
                            wowdata.realm_name = json[i].name;
                            wowdata.connection_url = json[i].connection_url;
                            wowdata.realm_id = json[i].id;

                            wowdata.account_name = json[i].data.username;
                            wowdata.auctioneer_name = json[i].data.auctioneer;
                            wowdata.faction = json[i].faction;
                            wowdata.password = json[i].data.password;
                            string aucdata = wow_path + "WTF\\Account\\" + wowdata.account_name.ToUpper() + "\\SavedVariables\\Auc-ScanData.lua";
                            if (File.Exists(aucdata))
                            {
                                wowdata.size = new System.IO.FileInfo(aucdata).Length;
                            }
                            else
                            {
                                wowdata.last_modified_date = 0;
                                wowdata.size = 0;
                            }
                            wowdata.wowprocess.StartInfo.Arguments = wowdata.realm_id + " \"" + wowdata.realm_name + "\" \"" + wowdata.connection_url + "\" " + wowdata.account_name + " \"" + wowdata.password + "\" \"" + wowdata.auctioneer_name + "\" \"" + base_url + "\" " + wowdata.faction;
                            Console.WriteLine("Start ..." + wowdata.wowprocess.StartInfo.Arguments);
                            if (wowdata.wowprocess.Start())
                            {
                                Console.WriteLine("Process for realm id " + json[i].id);
                                process_data.Add(wowdata);
                            }
                            System.Threading.Thread.Sleep(60000);
                        }
                    }
                    catch (WebException e) 
                    {
                        Console.WriteLine(e.Message);
                    }
                }
                while (process_data.Count > 0)
                {
                    for (var i = 0; i < process_data.Count; i++)
                    {
                        WoWInstance wow = (WoWInstance)process_data[i];
                        if (wow.wowprocess.HasExited)
                        {
                            if (wow.wowprocess.ExitCode == 0) // everything OK
                            {
                                Console.WriteLine("Realm " + wow.realm_name + " " + wow.faction + " eneded successfully");
                            }
                            else if (wow.wowprocess.ExitCode == 1) // start failure
                            {
                                Console.WriteLine("Realm " + wow.realm_name + " " + wow.faction + " start failed");
                            }
                            else if (wow.wowprocess.ExitCode == 2) // login or script problem
                            {
                                Console.WriteLine("Realm " + wow.realm_name + " " + wow.faction + " scripting problem");
                            }
                            process_data.RemoveAt(i);
                        }
                    }
                    System.Threading.Thread.Sleep(1000);
                }
                var current_timestamp = (Int32)(DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1))).TotalSeconds;
                var timeleft = (current_timestamp - start_time_stamp) - 3600; // time the process started minus now minus 1 hr in seconds we sleep for the remaining amount. 
                if (timeleft < 0) // if below 0 just dont sleep and try again
                    timeleft = 0;
                System.Threading.Thread.Sleep(timeleft*1000); // every hour
            }
            
        }
    }
}
