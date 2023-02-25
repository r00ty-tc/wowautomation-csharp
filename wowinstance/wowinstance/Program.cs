using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.Diagnostics;
using Microsoft.Win32;
using System.Runtime.InteropServices;
using System.Drawing;
using System.Net;
using WindowsInput.Native;
using System.Security.Cryptography.X509Certificates;

[StructLayout(LayoutKind.Sequential)]
public struct POINT
{
    public int X;
    public int Y;

    public static implicit operator Point(POINT point)
    {
        return new Point(point.X, point.Y);
    }
}
public struct Rect
{
    public int Left { get; set; }
    public int Top { get; set; }
    public int Right { get; set; }
    public int Bottom { get; set; }
}
public class WebClientEx : WebClient
{
    public int Timeout { get; set; }

    protected override WebRequest GetWebRequest(Uri address)
    {
        var request = base.GetWebRequest(address);
        request.Timeout = Timeout;
        return request;
    }
}
namespace wowinstance
{
    class Program
    {
        public static int waitTime = 5000;

        public static string folderSeparator => InLinux() ? "/" : "\\";
        private static bool linux;
        private static bool debug;

        [DllImport("user32.dll")]
        public static extern IntPtr FindWindowEx(IntPtr parentWindow, IntPtr previousChildWindow, string windowClass, string windowTitle);

        [DllImport("user32.dll")]
        private static extern IntPtr GetWindowThreadProcessId(IntPtr window, out int process);
        public static bool InLinux()
        {
            string path = Directory.GetCurrentDirectory();
            if (path.IndexOf("/home/") != -1)
            {
                return true;
            }
            return false;
        }
        public static string getDT()
        {
            DateTime dt = DateTime.UtcNow;
            return "["+dt.ToLocalTime()+"] ";
        }
        public static bool InWindowsServer()
        {
            string path = Directory.GetCurrentDirectory();
            if (path.IndexOf("Administrator") != -1)
            {
                return true;
            }
            return false;
        }
        private static IntPtr[] GetProcessWindows(int process)
        {
            IntPtr[] apRet = (new IntPtr[256]);
            int iCount = 0;
            IntPtr pLast = IntPtr.Zero;
            do
            {
                pLast = FindWindowEx(IntPtr.Zero, pLast, null, null);
                int iProcess_;
                GetWindowThreadProcessId(pLast, out iProcess_);
                if (iProcess_ == process) apRet[iCount++] = pLast;
            } while (pLast != IntPtr.Zero);
            System.Array.Resize(ref apRet, iCount);
            return apRet;
        }

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, int wParam,
    StringBuilder lParam);

        static void sendChars(IntPtr wnd, string str)
        {
            const UInt32 WM_CHAR = 0x0102;
            for (var i = 0; i < str.Length; i++)
            {
                PostMessage(wnd, WM_CHAR, str[i], 0);
                System.Threading.Thread.Sleep(50);
            }
        }
        static void sendKeydown(IntPtr wnd, int vk)
        {
            const UInt32 WM_KEYDOWN = 0x0100;
            PostMessage(wnd, WM_KEYDOWN, vk, 0);
        }
        static void sendKeys(IntPtr wnd, int vk)
        {
            const UInt32 WM_KEYDOWN = 0x0100;
            const UInt32 WM_KEYUP = 0x0101;

            PostMessage(wnd, WM_KEYDOWN, vk, 0);
            PostMessage(wnd, WM_KEYUP, vk, 0);
        }

        [DllImport("User32.Dll", EntryPoint = "PostMessageA")]
        static extern bool PostMessage(IntPtr hWnd, uint msg, int wParam, int lParam);
        [DllImport("user32.dll")]
        static extern byte VkKeyScan(char ch);
        static private int[,,] colors_pixels_ws ={
            //title screen  0
            { { 126, 690, 0 }, { 29, 702, 0xFFFBFF } },
            //char select   1
            { { 450,550,0x050d7c },{450,550,0x04046d } },
            //ingame        2
            { { 10,705,0x120C0D },{10,705,0x120C0D } },
            //auction house 3
            { { 171,440,0x0 },{171,440,0x0 } },
            // scanning     4
            { { 166,19,0x4c4c4c },{ 166,19,0x4c4c4c } },
        };

        static private int[,,] colors_pixels_linux={
            //title screen  0
            { { 126, 690, 0 }, { 29, 702, 0xFFFBFF } },
            //char select   1
            { { 450,550,0x050d7b },{ 450,550,0x070671 } },
            //ingame        2
            { { 780,594,0x177cb9 },{ 780,594,0x177cb9 } },
            //auction house 3
            { { 225,17,0x000062 },{ 225,17,0x000062 } },
            // scanning     4
            { { 166,19,0x4b4c4b },{ 166,19,0x4b4c4b } },
        };

        static private int[,,] colors_pixels_linux_alt ={
            //title screen  0
            { { 126, 690, 0 }, { 29, 702, 0xFFFBFF } },
            //char select   1
            { { 450,550,0x050d7b },{ 450,550,0x070671 } },
            //ingame        2
            { { 780,594,0x187dba },{ 780,594,0x187dba } },
            //auction house 3
            { { 225,17,0x000063 },{ 225,17,0x000063 } },
            // scanning     4
            { { 166,19,0x4b4c4b },{ 166,19,0x4b4c4b } },
        };

        static private int[,,] colors_pixels = { 
            //title screen  0
            { { 126, 690, 0 }, { 29, 702, 0xFFFBFF } },
            //char select   1
            { { 450,550,0x050d7c },{ 450,550,0x070672 } },
            //ingame        2
            { { 780,594,0x187dbb },{ 780,594,0x187dbb } },
            //auction house 3
            { { 225,17,0x000063 },{ 225,17,0x000063 } },
            // scanning     4
            { { 166,19,0x4c4c4c },{ 166,19,0x4b4c4b } },

        };
        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;        // x position of upper-left corner
            public int Top;         // y position of upper-left corner
            public int Right;       // x position of lower-right corner
            public int Bottom;      // y position of lower-right corner
        }
        RECT r = new RECT();

        [DllImport("user32.dll", EntryPoint = "SetCursorPos")]
        static extern bool SetCursorPosition(int x, int y);

        [DllImport("user32.dll")]
        static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern bool GetWindowRect(IntPtr hwnd, out RECT rectangle);

        [DllImport("user32.dll")]
        static extern IntPtr GetDC(IntPtr hwnd);

        [DllImport("user32.dll")]
        static extern Int32 ReleaseDC(IntPtr hwnd, IntPtr hdc);

        [DllImport("gdi32.dll")]
        static extern uint GetPixel(IntPtr hdc, int nXPos, int nYPos);

        [DllImport("user32.dll")]
        public static extern bool GetCursorPos(out POINT lpPoint);

        [DllImport("gdi32.dll")]
        public static extern uint SetPixel(IntPtr hdc, int X, int Y, int crColor);

        private static void ClearAucData(string accname)
        {
            var current_path = Directory.GetCurrentDirectory();
            current_path += $"{folderSeparator}WTF{folderSeparator}Account{folderSeparator}" + accname.ToUpper() + "{folderSeparator}SavedVariables{folderSeparator}";
            string final_path = current_path + "Auc-ScanData.lua";
            if (File.Exists(final_path))
                File.Delete(final_path);
            final_path = current_path + "Auc-ScanData.lua.bak";
            if (File.Exists(final_path))
                File.Delete(final_path);

            Trace.WriteLine(getDT()+"Deleted Auction Data");
        }
        private static void SetupRealm(string realmname,string realm_url)
        {
            var current_path = Directory.GetCurrentDirectory();
            if (File.Exists(current_path + $"{folderSeparator}Data{folderSeparator}enUS{folderSeparator}realmlist.wtf"))
            {
                File.Delete(current_path + $"{folderSeparator}Data{folderSeparator}enUS{folderSeparator}realmlist.wtf");
            }
            current_path += $"{folderSeparator}WTF{folderSeparator}Config.wtf";
            if (!File.Exists(current_path))
            {
                throw new Exception(current_path + " NOT FOUND!");
            }

            StreamReader f = File.OpenText(current_path);
            string final_out = "";
            while (!f.EndOfStream)
            {
                string line = f.ReadLine();
                if (line.Contains("realmList"))
                {
                    line = "SET realmList \""+realm_url+"\"";
                }
                if (line.Contains("realmName"))
                {
                    line = "SET realmName \"" + realmname+ "\"";
                }
                final_out += line + "\n";
            }
            f.Close();
            
            File.WriteAllText(current_path, final_out);
            Trace.WriteLine(getDT() + "Written to realm file");
        }
        static public uint GetPixelColor(IntPtr hwnd, int x, int y)
        {
            IntPtr hdc = GetDC(hwnd);
            uint pixel = GetPixel(hdc, x, y);
            
            ReleaseDC(hwnd, hdc);
          
            return pixel;
        }
        static public bool TestPixel(IntPtr wnd,int index, int factionIdx)
        {
            int x = colors_pixels[index, factionIdx, 0];
            int y = colors_pixels[index, factionIdx, 1];
            int color = 0;
            int altColor = 0;
            if (linux)
            {   
                color = colors_pixels_linux[index, factionIdx, 2];
                altColor = colors_pixels_linux_alt[index, factionIdx, 2];
            }
            else if (InWindowsServer())
            {
                color = colors_pixels_ws[index, factionIdx, 2];
            }
            else
                color = colors_pixels[index, factionIdx, 2];

            uint col = GetPixelColor(wnd, x, y);

            if (debug)
                Trace.WriteLine("Testing pixel against " + col + " " + color + (altColor != 0 ? " / " + altColor : ""));

            if (col == color || col == altColor)
                return true;
            else
                return false;
        }

        static public bool WaitForPixel(IntPtr wnd, int index, int factionIdx, int maxSecs = 60)
        {
            int count = 0;
            while (!TestPixel(wnd, index, factionIdx))
            {
                System.Threading.Thread.Sleep(1000);
                count++;
                if (count > maxSecs)
                    return false;
            }
            return true;
        }

        static void rightclick(IntPtr wnd, int x, int y)
        {
            const UInt32 WM_RBUTTONDOWN = 0x0204;
            const UInt32 WM_RBUTTONUP = 0x0205;
            var point = ((int)y << 16) & (int)x;

            PostMessage(wnd, WM_RBUTTONDOWN, 0, point);
            System.Threading.Thread.Sleep(100);
            PostMessage(wnd, WM_RBUTTONUP, 0, point);
        }

        static int Main(string[] args)
        {
            // Dirty hack to let us use https. Big reason to move to net6
            ServicePointManager.ServerCertificateValidationCallback += new System.Net.Security.RemoteCertificateValidationCallback(delegate (object sender, X509Certificate cert, X509Chain chain, System.Net.Security.SslPolicyErrors error)
            {
                return true;
            });

            try
            {
                if (args.Count() == 0)
                {
                    Trace.WriteLine("No arguments passed.");
                    Environment.Exit(1);
                    return 1;
                }

                string realm_id = args[0];
                string realm_name = args[1];
                string realm_url = args[2];
                string accountname = args[3];
                string password = args[4];
                string auctioneer = args[5];
                string base_url = args[6];
                string faction = args[7];
                int factionIdx = (faction == "a") ? 1 : 0;
                linux = false;
                if (args.Count() > 8 && args[8] == "1")
                {
                    linux = true;
                    waitTime = 30000;
                }

                const int EXPAC_WOTLK = 1;
                const int EXPAC_TBC = 2;
                Process p = new Process();
                int expac_mode = EXPAC_WOTLK;
                Trace.Listeners.Clear();
                File.Delete(realm_id + "_" + realm_name + "_out.txt");
                TextWriterTraceListener twtl = new TextWriterTraceListener(realm_id + "_" +faction+"_"+ realm_name + "_out.txt");
                twtl.Name = "TextLogger";
                twtl.TraceOutputOptions = TraceOptions.ThreadId | TraceOptions.DateTime;

                ConsoleTraceListener ctl = new ConsoleTraceListener(false);
                ctl.TraceOutputOptions = TraceOptions.DateTime;

                Trace.Listeners.Add(twtl);
                Trace.Listeners.Add(ctl);
                Trace.AutoFlush = true;

                Trace.WriteLine(getDT() + args[0]);
                Trace.WriteLine(getDT() + args[1]);
                Trace.WriteLine(getDT() + args[2]);
                Trace.WriteLine(getDT() + args[3]);
                Trace.WriteLine(getDT() + args[4]);
                Trace.WriteLine(getDT() + args[5]);
                Trace.WriteLine(getDT() + args[6]);
                Trace.WriteLine(getDT() + args[7]);

                // Get linux flag
                if (args.Count() > 8 && args[8] == "1")
                    Trace.WriteLine(getDT() + args[8] + " / " + linux.ToString());

                // Get debug flag
                debug = args.Count() > 9 && args[9] == "1";

                string exename = InLinux() ? "wine" : "wowclean.exe";
                if (File.Exists("wowtbc.exe"))
                {
                    expac_mode = EXPAC_TBC;
                    exename = "wowtbc.exe";
                }
                p.StartInfo.FileName = exename;
                //p.StartInfo.UseShellExecute = false;
                //p.StartInfo.RedirectStandardError = true;
                if (InLinux())
                    p.StartInfo.Arguments = "wowclean.exe";

                var current_path = Directory.GetCurrentDirectory();
                Trace.WriteLine($"Current folder: {current_path}");
                ClearAucData(accountname);
                SetupRealm(realm_name, realm_url);
                if (p.Start())
                {
                    WebClientEx status_client = new WebClientEx();
                    status_client.Timeout = 6000;
                    status_client.DownloadString(new Uri((base_url + "scheduler/start?realm_id=" + realm_id + "&faction_id=" + faction)));
                    System.Threading.Thread.Sleep(waitTime);

                    IntPtr wnd;

                    if (p.MainWindowHandle == IntPtr.Zero)
                    {
                        p.Refresh();
                        IntPtr[] wnds = GetProcessWindows(p.Id);
                        wnd = wnds[0];
                    }
                    else
                        wnd = p.MainWindowHandle;

                    //SetForegroundWindow(p.MainWindowHandle);
                    sendChars(wnd, accountname);
                    System.Threading.Thread.Sleep(100);
                    sendKeys(wnd, (int)VirtualKeyCode.TAB);
                    //InputSimulator.SimulateKeyPress(VirtualKeyCode.TAB);
                    System.Threading.Thread.Sleep(100);
                    sendChars(wnd, password);
                    //InputSimulator.SimulateTextEntry(password);
                    System.Threading.Thread.Sleep(1000);
                    sendKeys(wnd, (int)VirtualKeyCode.RETURN);
                    //InputSimulator.SimulateKeyPress(VirtualKeyCode.RETURN);
                    //System.Threading.Thread.Sleep(waitTime);
                    System.Threading.Thread.Sleep(2000);
                    //SetForegroundWindow(p.MainWindowHandle);
                    if (!WaitForPixel(wnd, 1, factionIdx))
                    {
                        Trace.WriteLine(getDT() + "Not at char select window");
                        p.Kill();
                        status_client.DownloadString(new Uri((base_url + "scheduler/fail?realm_id=" + realm_id + "&faction_id=" + faction + "&reason=not+at+char+select+window")));
                        //Console.ReadKey();
                        p.WaitForExit();
                        p.Dispose();
                        Environment.Exit(2);
                        return 2;
                    }
                    Trace.WriteLine("Passing char select screen");
                    System.Threading.Thread.Sleep(2000);
                    sendKeys(wnd, (int)VirtualKeyCode.RETURN);
                    //InputSimulator.SimulateKeyPress(VirtualKeyCode.RETURN);
                    //System.Threading.Thread.Sleep(waitTime);
                    //SetForegroundWindow(p.MainWindowHandle);
                    if (!WaitForPixel(wnd, 2, factionIdx))
                    {
                        Trace.WriteLine(getDT() + "Not in game");
                        p.Kill();
                        status_client.DownloadString(new Uri((base_url + "scheduler/fail?realm_id=" + realm_id + "&faction_id=" + faction + "&reason=Not+in+game")));
                        //Console.ReadKey();
                        p.WaitForExit();
                        p.Dispose();
                        Environment.Exit(2);
                        return 2;
                    }
                    Trace.WriteLine("In game");
                    System.Threading.Thread.Sleep(2000);
                    var start_url = base_url + "scheduler/start?realm_id=" + realm_id + "&faction_id=" + faction;

                    sendKeys(wnd, (int)VirtualKeyCode.RETURN);
                    System.Threading.Thread.Sleep(100);
                    //InputSimulator.SimulateKeyPress(VirtualKeyCode.RETURN);
                    sendKeydown(wnd, (int)VirtualKeyCode.OEM_2);
                    System.Threading.Thread.Sleep(100);
                    sendChars(wnd, "tar " + auctioneer);
                    //InputSimulator.SimulateTextEntry("/tar "+auctioneer);
                    sendKeys(wnd, (int)VirtualKeyCode.RETURN);
                    //InputSimulator.SimulateKeyPress(VirtualKeyCode.RETURN);
                    System.Threading.Thread.Sleep(500);
                    if (expac_mode == EXPAC_WOTLK)
                    {
                        sendKeys(wnd, (int)VirtualKeyCode.OEM_6);
                        //InputSimulator.SimulateKeyPress(VirtualKeyCode.OEM_6);
                    }
                    else if (expac_mode == EXPAC_TBC)
                    {
                        RECT r = new RECT();
                        GetWindowRect((IntPtr)wnd, out r);
                        SetForegroundWindow(wnd);
                        SetCursorPosition(r.Left + ((r.Right - r.Left) / 2), r.Top + ((r.Bottom - r.Top) / 2));
                        rightclick(wnd, 0, 0);
                    }
                    //System.Threading.Thread.Sleep(3000);
                    if (!WaitForPixel(wnd, 3, factionIdx))
                    {
                        Trace.WriteLine(getDT() + "Auction house not opened");
                        p.Kill();
                        status_client.DownloadString(new Uri((base_url + "scheduler/fail?realm_id=" + realm_id + "&faction_id=" + faction + "&reason=auction+house+not+opened")));
                        p.WaitForExit();
                        p.Dispose();
                        Environment.Exit(2);
                        return 2;
                    }
                    Trace.WriteLine("Auction house opened");
                    System.Threading.Thread.Sleep(5000);
                    sendKeys(wnd, (int)VirtualKeyCode.RETURN);
                    System.Threading.Thread.Sleep(100);
                    sendKeydown(wnd, (int)VirtualKeyCode.OEM_2);
                    System.Threading.Thread.Sleep(100);
                    if (expac_mode == EXPAC_TBC)
                        sendChars(wnd, "script AucAdvanced.Scan.StartScan()");
                    else
                        sendChars(wnd, "auc scan");

                    System.Threading.Thread.Sleep(100);
                    sendKeys(wnd, (int)VirtualKeyCode.RETURN);
                    //InputSimulator.SimulateKeyPress(VirtualKeyCode.RETURN);
                    //InputSimulator.SimulateTextEntry("/auc scan");
                    //InputSimulator.SimulateKeyPress(VirtualKeyCode.RETURN);

                    // Check if scanning started
                    var maxtries = 5;
                    while (!WaitForPixel(wnd, 4, factionIdx, 5) && maxtries > 0)
                    {
                        // If not, keep trying to initiate scan
                        Trace.WriteLine("Retrying auction scan");
                        System.Threading.Thread.Sleep(1000);
                        sendKeys(wnd, (int)VirtualKeyCode.RETURN);
                        System.Threading.Thread.Sleep(100);
                        sendKeydown(wnd, (int)VirtualKeyCode.OEM_2);
                        System.Threading.Thread.Sleep(100);
                        if (expac_mode == EXPAC_TBC)
                            sendChars(wnd, "script AucAdvanced.Scan.StartScan()");
                        else
                            sendChars(wnd, "auc scan");

                        System.Threading.Thread.Sleep(100);
                        sendKeys(wnd, (int)VirtualKeyCode.RETURN);
                        maxtries--;
                    }
                    Trace.WriteLine("Auction scan started");

                    bool myswitch = false;
                    Random rand = new System.Random();
                    int timeout = rand.Next(240, 300);
                    int counter = 0;
                    int failcount = 0;
                    while (!p.HasExited)
                    {
                        /*POINT pp;
                        GetCursorPos(out pp);
                        Rect wind_rect = new Rect();
                        GetWindowRect(p.MainWindowHandle, out wind_rect);
                        int x = pp.X - wind_rect.Left - 8;
                        int y = pp.Y - wind_rect.Top - 32;
                        uint pixel = GetPixelColor(p.MainWindowHandle, x, y);
                        Console.Write("{0:X} [" + x + "," + y + "] \n", pixel);
                        */
                        if (!TestPixel(wnd, 2, factionIdx) && (counter % 10) == 0)
                        {
                            Trace.WriteLine(getDT() + "We are not ingame any more , mostly restart or crash");
                            if (failcount >= 3)
                            {
                                status_client.DownloadString(new Uri((base_url + "scheduler/fail?realm_id=" + realm_id + "&faction_id=" + faction + "&reason=not+in+game+anymore+mostly+crash+or+restart")));
                                p.Kill();
                                p.WaitForExit();
                                p.Dispose();
                                Environment.Exit(3);
                                return 3;
                            }
                            else
                            {
                                failcount++;
                            }

                        }
                        if (!TestPixel(wnd, 3, factionIdx) && (counter % 10) == 0)
                        {
                            //SetForegroundWindow(p.MainWindowHandle);
                            Trace.WriteLine(getDT() + "Auction House not opened, trying to reopen and continue");
                            sendKeys(wnd, (int)VirtualKeyCode.RETURN);
                            System.Threading.Thread.Sleep(100);
                            sendKeydown(wnd, (int)VirtualKeyCode.OEM_2);
                            System.Threading.Thread.Sleep(100);
                            sendChars(wnd, "tar " + auctioneer);
                            System.Threading.Thread.Sleep(100);
                            sendKeys(wnd, (int)VirtualKeyCode.RETURN);
                            //InputSimulator.SimulateKeyPress(VirtualKeyCode.RETURN);
                            //InputSimulator.SimulateTextEntry("/tar " + auctioneer);
                            //InputSimulator.SimulateKeyPress(VirtualKeyCode.RETURN);
                            if (expac_mode == EXPAC_WOTLK)
                            {
                                System.Threading.Thread.Sleep(500);
                                sendKeys(p.MainWindowHandle, (int)VirtualKeyCode.OEM_6);
                            }
                            else if (expac_mode == EXPAC_TBC)
                            {
                                RECT r = new RECT();
                                GetWindowRect((IntPtr)wnd, out r);
                                SetForegroundWindow(wnd);
                                SetCursorPosition(r.Left + ((r.Right - r.Left) / 2), r.Top + ((r.Bottom - r.Top) / 2));
                                rightclick(wnd, 0, 0);
                            }
                            //InputSimulator.SimulateKeyPress(VirtualKeyCode.OEM_6);
                            System.Threading.Thread.Sleep(3000);
                            sendKeys(wnd, (int)VirtualKeyCode.RETURN);
                            System.Threading.Thread.Sleep(100);
                            sendKeydown(wnd, (int)VirtualKeyCode.OEM_2);
                            System.Threading.Thread.Sleep(100);
                            sendChars(wnd, "auc scan");
                            System.Threading.Thread.Sleep(100);
                            sendKeys(wnd, (int)VirtualKeyCode.RETURN);
                            /*InputSimulator.SimulateKeyPress(VirtualKeyCode.RETURN);
                            InputSimulator.SimulateTextEntry("/auc scan");
                            InputSimulator.SimulateKeyPress(VirtualKeyCode.RETURN);*/
                        }

                        System.Threading.Thread.Sleep(1000);
                        if (counter >= timeout)
                        {
                            if ((DateTime.Now.ToUniversalTime()-p.StartTime.ToUniversalTime()).TotalSeconds > 60 * 60)
                            {
                                Trace.WriteLine(getDT() + "Client has been running too long, must be stuck. Killing it.");
                                status_client.DownloadString(new Uri((base_url + "scheduler/fail?realm_id=" + realm_id + "&faction_id=" + faction + "&reason=timed+out+in+auction+screen")));
                                p.Kill();
                                p.WaitForExit();
                            }

                            // Screenshot
                            sendKeys(wnd, (int)VirtualKeyCode.OEM_4);

                            status_client.DownloadString(new Uri((base_url + "scheduler/ping?realm_id=" + realm_id + "&faction_id=" + faction)));
                            Trace.WriteLine(getDT() + "Time to wake up");
                            //SetForegroundWindow(p.MainWindowHandle);
                            if (myswitch)
                            {
                                //sendChars(p.MainWindowHandle, "s");
                                sendKeys(wnd, (int)VirtualKeyCode.UP);
                                /*InputSimulator.SimulateKeyDown(VirtualKeyCode.VK_W);
                                System.Threading.Thread.Sleep(50);
                                InputSimulator.SimulateKeyUp(VirtualKeyCode.VK_W);
                                */
                            }
                            else
                            {
                                //sendKeys(p.MainWindowHandle, (int)VirtualKeyCode.VK_S);
                                sendKeys(wnd, (int)VirtualKeyCode.DOWN);
                                /*InputSimulator.SimulateKeyDown(VirtualKeyCode.VK_S);
                                System.Threading.Thread.Sleep(50);
                                InputSimulator.SimulateKeyUp(VirtualKeyCode.VK_S);
                                */
                            }
                            myswitch = !myswitch;
                            counter = 0;
                        }
                        else
                            counter++;
                    }
                    Trace.WriteLine(getDT() + "Process Complete...");
                    System.Threading.Thread.Sleep(5000);
                    current_path = Directory.GetCurrentDirectory();
                    current_path += $"{folderSeparator}WTF{folderSeparator}Account{folderSeparator}" + accountname.ToUpper() + $"{folderSeparator}SavedVariables{folderSeparator}Auc-ScanData.lua";
                    p.Dispose();
                    if (File.Exists(current_path))
                    {
                        Trace.WriteLine(getDT() + "Uploading file");
                        WebClientEx client1 = new WebClientEx();
                        client1.Timeout = 600000;
                        string myfile1 = @current_path;
                        var base_url_insecure = base_url.Replace("https", "http");
                        byte[] ret = client1.UploadFile(base_url_insecure + "private-api/takefile?id=" + realm_id + "&faction=" + faction, myfile1);
                        //byte[] ret = client1.UploadFile(base_url + "private-api/takefile?id=" + realm_id + "&faction=" + faction, myfile1);
                        Trace.WriteLine(System.Text.Encoding.ASCII.GetString(ret));
                        status_client.DownloadString(new Uri((base_url + "scheduler/end?realm_id=" + realm_id + "&faction_id=" + faction)));
                        //return 0;
                        Environment.Exit(0);
                        return 0;
                    }
                    else
                    {
                        status_client.DownloadString(new Uri((base_url + "scheduler/fail?realm_id=" + realm_id + "&faction_id=" + faction + "&reason=Failed+to+upload+file")));
                        Environment.Exit(2);
                        return 2;
                    }
                }
                else
                {
                    Trace.WriteLine(getDT() + "Opps wowclean.exe could not be started");
                    Environment.Exit(1);
                    return 1;
                }
            }
            catch(Exception e)
            {
                Trace.WriteLine(e.Message);
                Trace.WriteLine(e.Source);
                Trace.WriteLine(e.StackTrace);
                Environment.Exit(1);
                return 1;
            }
        }
    }
}
