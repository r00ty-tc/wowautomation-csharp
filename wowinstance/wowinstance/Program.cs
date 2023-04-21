using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.Diagnostics;
using Microsoft.Win32;
using System.Runtime.InteropServices;
using System.Drawing;
using System.Drawing.Imaging;
using System.Net;
using WindowsInput.Native;
using System.Security.Cryptography.X509Certificates;
using System.Net.Http;
using System.ComponentModel;
using System.Reflection.Metadata;
using System.Reflection;

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

[StructLayout(LayoutKind.Sequential)]
public struct Rect
{
    public int Left;
    public int Top;
    public int Right;
    public int Bottom;
}
public class HttpClientEx : HttpClient
{
    public HttpResponseMessage Get(Uri address)
    {
        var responseTask = GetAsync(address);
        responseTask.Wait(((int)Timeout.TotalMilliseconds));
        responseTask.Result.EnsureSuccessStatusCode();
        var result = responseTask.Result;
        return result;
    }

    public async Task<string> DownloadString(Uri address)
    {
        var response = Get(address);
        return await response.Content.ReadAsStringAsync();
    }

    public string UploadFile(string address, string fileName)
    {
        FileInfo fileInfo = new FileInfo(fileName);
        var fileStream = new FileStream(fileName, FileMode.Open);
        var uploadRequest = new HttpRequestMessage(HttpMethod.Post, address);
        HttpContent webContent = new MultipartFormDataContent
        {
            {new StreamContent(fileStream), "file", fileInfo.Name }
        };

        uploadRequest.Content = webContent;
        string responseText;
        using (var webResponse = Send(uploadRequest))
        {
            // Check response was successful
            if (!webResponse.IsSuccessStatusCode)
                return null;

            // Extract response as text
            using var resp = new StreamReader(webResponse.Content.ReadAsStream());
            responseText = resp.ReadToEnd();
        }
        return responseText;
    }
}
namespace wowinstance
{
    public enum PixelIndex
    {
        TitleScreen = 0,
        CharacterSelect = 1,
        InGame = 2,
        AuctionHouseOpen = 3,
        AuctionHouseScanning = 4,
        AuctionHouseProcessing = 5,
        LoginInQueueNoEstimate = 6,
        LoginInQueueEstimate = 7
    }

    public class PixelScan
    {
        [DllImport("user32.dll")]
        static extern IntPtr GetDC(IntPtr hwnd);

        [DllImport("user32.dll")]
        static extern Int32 ReleaseDC(IntPtr hwnd, IntPtr hdc);

        [DllImport("gdi32.dll")]
        static extern uint GetPixel(IntPtr hdc, int nXPos, int nYPos);

        // Actual properties
        public Dictionary<PixelIndex, PixelScanData[]> PixelData { get; set; }
        public bool IsAnd { get; set; }

        public PixelScan(bool isAnd = false)
        {
            PixelData = new Dictionary<PixelIndex, PixelScanData[]>();
            IsAnd = isAnd;
        }

        public PixelScan(Tuple<PixelIndex, PixelScanData[]>[] pixelData, bool isAnd = false)
        {
            PixelData = pixelData.ToDictionary(pixel => pixel.Item1, pixel => pixel.Item2);
            IsAnd = isAnd;
        }

        public void Add(PixelIndex index, PixelScanData[] scanData)
        {
            PixelData.Add(index, scanData);
        }

        static public uint GetPixelColor(IntPtr hwnd, int x, int y)
        {
            IntPtr hdc = GetDC(hwnd);
            uint pixel = GetPixel(hdc, x, y);

            ReleaseDC(hwnd, hdc);

            return pixel;
        }

        // Test a single specified pixel against a list of colours
        private static bool testSinglePixel(IntPtr wnd, int x, int y, uint[] colours)
        {
            var colour = GetPixelColor(wnd, x, y);

            if (Program.debug)
                Trace.WriteLine(Program.getDT() + $"Testing {x},{y} for the colours ({string.Join(", ", colours.Select(item => "0x" + item.ToString("X6").ToLower()))}) against 0x{colour.ToString("X6").ToLower()}");

            // True if any colour in colours matches
            return colours.Any(entry => entry == colour);
        }

        // Test a single pixel against a colour index
        public bool TestPixel(IntPtr wnd, PixelIndex index)
        {
            if (IsAnd)
                return PixelData[index].All(entry => testSinglePixel(wnd, entry.X, entry.Y, entry.Colours));
            else
                return PixelData[index].Any(entry => testSinglePixel(wnd, entry.X, entry.Y, entry.Colours));
        }

        // Test multiple pixels against multiple colour indexes and return which ones matched
        public PixelIndex[] TestPixels(IntPtr wnd, PixelIndex[] indexes, bool all = false)
        {
            if (all && indexes.All(index => TestPixel(wnd, index)))
            {
                return indexes.Where(index => TestPixel(wnd, index)).ToArray();
            }
            else if (!all && indexes.Any(index => TestPixel(wnd, index)))
            {
                return indexes.Where(index => TestPixel(wnd, index)).ToArray();
            }
            else
                return null;
        }

        // Wait for a specific pixel to be set to a colour specified by the supplied index
        // wait for up to maxSecs seconds (1 minute by default)
        public bool WaitForPixel(IntPtr wnd, PixelIndex index, int maxSecs = 60)
        {
            int count = 0;
            while (!TestPixel(wnd, index))
            {
                Thread.Sleep(1000);
                count++;
                if (count > maxSecs)
                    return false;
            }
            return true;
        }

        // Wait for multiple pixels to be set to a colour specified by the supplied indexes
        // wait for up to maxSecs seconds (1 minute by default), return the indexes matching
        public PixelIndex[] WaitForPixels(IntPtr wnd, PixelIndex[] indexes, bool all = false, int maxSecs = 60)
        {
            int count = 0;
            while (true)
            {
                var result = TestPixels(wnd, indexes, all);
                if (result != null)
                    return result;

                Thread.Sleep(1000);
                count++;
                if (count > maxSecs)
                    return null;
            }
        }
    }

    public class PixelScanData
    {
        public int X { get; set; }
        public int Y { get; set; }
        public uint[] Colours { get; set; }
        public PixelScanData(int x, int y, uint[] colours)
        {
            X = x;
            Y = y;
            Colours = colours;
        }
    }

    class Program
    {
        public static int waitTime = 5000;

        public static string folderSeparator => InLinux() ? "/" : "\\";
        private static bool linux;
        public static bool debug;

        [DllImport("user32.dll")]
        public static extern IntPtr FindWindowEx(IntPtr parentWindow, IntPtr previousChildWindow, string windowClass, string windowTitle);

        [DllImport("user32.dll")]
        private static extern IntPtr GetWindowThreadProcessId(IntPtr window, out int process);

        [DllImport("user32.dll")]
        private static extern IntPtr GetWindowRect(IntPtr hWnd, ref global::Rect rect);

        public static bool InLinux()
        {
            string path = Directory.GetCurrentDirectory();
            if (path.IndexOf("/home/") != -1)
            {
                return true;
            }
            return false;
        }

        public static void SaveScreenshot(IntPtr wnd, string fileName = null)
        {
            if (!OperatingSystem.IsWindows())
                return;

            if (fileName == null)
                fileName = $"{Directory.GetCurrentDirectory()}\\Screenshots\\WoWScrnShot_{DateTime.Now.ToString("MMddyy_HHmmss")}.png";

            var rect = new global::Rect();
            GetWindowRect(wnd, ref rect);
            var bounds = new Rectangle(rect.Left, rect.Top, rect.Right - rect.Left, rect.Bottom - rect.Top);
            using (var result = new Bitmap(bounds.Width, bounds.Height))
            {
                using (var graphics = Graphics.FromImage(result))
                {
                    graphics.CopyFromScreen(new Point(bounds.Left, bounds.Top), Point.Empty, bounds.Size);
                }
                result.Save(fileName, ImageFormat.Png);
            }
        }

        public static string getDT()
        {
            DateTime dt = DateTime.UtcNow;
            return "["+dt.ToLocalTime()+"] ";
        }

        static void sendChars(IntPtr wnd, string str)
        {
            const UInt32 WM_CHAR = 0x0102;
            for (var i = 0; i < str.Length; i++)
            {
                PostMessage(wnd, WM_CHAR, str[i], 0);
                Thread.Sleep(50);
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

        [DllImport("user32.dll", EntryPoint = "SetCursorPos")]
        static extern bool SetCursorPosition(int x, int y);

        [DllImport("user32.dll")]
        static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        static extern IntPtr GetDC(IntPtr hwnd);

        [DllImport("user32.dll")]
        static extern Int32 ReleaseDC(IntPtr hwnd, IntPtr hdc);

        [DllImport("gdi32.dll")]
        static extern uint GetPixel(IntPtr hdc, int nXPos, int nYPos);

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

        static void rightclick(IntPtr wnd, int x, int y)
        {
            const UInt32 WM_RBUTTONDOWN = 0x0204;
            const UInt32 WM_RBUTTONUP = 0x0205;
            var point = ((int)y << 16) & (int)x;

            PostMessage(wnd, WM_RBUTTONDOWN, 0, point);
            Thread.Sleep(100);
            PostMessage(wnd, WM_RBUTTONUP, 0, point);
        }

        static int Main(string[] args)
        {
            // Setup pixels to check
            // 0: Title screen
            var pixels = new PixelScan(true);
            pixels.Add(PixelIndex.TitleScreen, new PixelScanData[]
                {
                    new PixelScanData(430, 450, new uint[] { 0x8a5418 })
                });

            // 1: Character select
            pixels.Add(PixelIndex.CharacterSelect, new PixelScanData[]
                {
                    new PixelScanData(450, 550, new uint[] { 0x050d7c, 0x04046d, 0x050d7b, 0x070671, 0x070672 })
                });

            // 2: In game
            pixels.Add(PixelIndex.InGame, new PixelScanData[]
                {
                    new PixelScanData(780, 594, new uint[] { 0x177cb9, 0x187dba, 0x187dbb })
                });

            // 3: Auction house open
            pixels.Add(PixelIndex.AuctionHouseOpen, new PixelScanData[]
                {
                    new PixelScanData(225, 17, new uint[] { 0x000062, 0x000063 })
                });

            // 4: Auction house scanning
            pixels.Add(PixelIndex.AuctionHouseScanning, new PixelScanData[]
                {
                    new PixelScanData(166, 19, new uint[] { 0x4b4c4b })
                });

            // 5: Auction house processing (also requires scanning)
            pixels.Add(PixelIndex.AuctionHouseProcessing, new PixelScanData[]
                {
                    new PixelScanData(166, 19, new uint[] { 0x4b4c4b }),
                    new PixelScanData(203, 18, new uint[] { 0xf8b04b })
                });

            // 6: Queue screen (No estimated time)
            pixels.Add(PixelIndex.LoginInQueueNoEstimate, new PixelScanData[]
                {
                    new PixelScanData(450, 340, new uint[] { 0x8f5614, 0x8f5613 })
                });

            // 7: Queue screen (Estimated time)
            pixels.Add(PixelIndex.LoginInQueueEstimate, new PixelScanData[]
                {
                    new PixelScanData(450, 332, new uint[] { 0x8f5614, 0x8f5613 })
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
                if (!int.TryParse(args[8], out int wowTimeout))
                    wowTimeout = 120;

                int factionIdx = (faction == "a") ? 1 : 0;
                linux = false;
                if (args.Count() > 9 && args[9] == "1")
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
                if (args.Count() > 9 && args[9] == "1")
                    Trace.WriteLine(getDT() + args[9] + " / " + linux.ToString());

                // Get debug flag
                debug = args.Count() > 10 && args[10] == "1";

                string exename = InLinux() ? "wine" : "wowclean.exe";
                if (File.Exists("wowtbc.exe"))
                {
                    expac_mode = EXPAC_TBC;
                    exename = "wowtbc.exe";
                }
                p.StartInfo.FileName = exename;

                if (InLinux())
                    p.StartInfo.Arguments = "wowclean.exe";

                var current_path = Directory.GetCurrentDirectory();
                Trace.WriteLine($"Current folder: {current_path}");
                ClearAucData(accountname);
                SetupRealm(realm_name, realm_url);
                if (p.Start())
                {
                    HttpClientEx status_client = new HttpClientEx();
                    status_client.Timeout = TimeSpan.FromSeconds(60);
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                    status_client.DownloadString(new Uri((base_url + "scheduler/start?realm_id=" + realm_id + "&faction_id=" + faction)));
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                    //System.Threading.Thread.Sleep(waitTime);
                    IntPtr wnd;

                    // Keep trying to get main window handle
                    while (p.MainWindowHandle == IntPtr.Zero)
                        Thread.Sleep(1000);

                    wnd = p.MainWindowHandle;
                    Trace.WriteLine(getDT() + "Found process window");

                    // Wait for login screen
                    if (!pixels.WaitForPixel(wnd, PixelIndex.TitleScreen))
                    {
                        Trace.WriteLine(getDT() + "Never reached login screen");
                        SaveScreenshot(wnd);
                        p.Kill();
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                        status_client.DownloadString(new Uri((base_url + "scheduler/fail?realm_id=" + realm_id + "&faction_id=" + faction + "&reason=not+at+login+screen")));
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                        //Console.ReadKey();
                        p.WaitForExit();
                        p.Dispose();
                        Environment.Exit(2);
                        return 2;
                    }

                    // Wait a second before logging in
                    Trace.WriteLine(getDT() + "Reached login screen");
                    Thread.Sleep(1000);

                    //SetForegroundWindow(p.MainWindowHandle);
                    sendChars(wnd, accountname);
                    Thread.Sleep(100);
                    sendKeys(wnd, (int)VirtualKeyCode.TAB);
                    //InputSimulator.SimulateKeyPress(VirtualKeyCode.TAB);
                    Thread.Sleep(100);
                    sendChars(wnd, password);
                    //InputSimulator.SimulateTextEntry(password);
                    Thread.Sleep(1000);
                    sendKeys(wnd, (int)VirtualKeyCode.RETURN);
                    //InputSimulator.SimulateKeyPress(VirtualKeyCode.RETURN);
                    //System.Threading.Thread.Sleep(waitTime);
                    Thread.Sleep(2000);
                    //SetForegroundWindow(p.MainWindowHandle);

                    // By default wait 1 minute at character select
                    int charSelectWait = 60;

                    // Wait for either character select or either queue screens
                    var thisPixel = pixels.WaitForPixels(wnd, new PixelIndex[] { PixelIndex.CharacterSelect, PixelIndex.LoginInQueueEstimate, PixelIndex.LoginInQueueNoEstimate }, false, charSelectWait);
                    if (thisPixel != null && (thisPixel.Contains(PixelIndex.LoginInQueueEstimate) || thisPixel.Contains(PixelIndex.LoginInQueueNoEstimate)))
                    {
                        Console.WriteLine(getDT() + "We're in a queue, we'll wait a maximum of 15 minures for character select");
                        charSelectWait = 15 * 60;
                    }
                    if (thisPixel == null || !pixels.WaitForPixel(wnd, PixelIndex.CharacterSelect, charSelectWait))
                    {
                        Trace.WriteLine(getDT() + "Not at char select window");
                        SaveScreenshot(wnd);
                        p.Kill();
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                        status_client.DownloadString(new Uri((base_url + "scheduler/fail?realm_id=" + realm_id + "&faction_id=" + faction + "&reason=not+at+char+select+window")));
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                        //Console.ReadKey();
                        p.WaitForExit();
                        p.Dispose();
                        Environment.Exit(2);
                        return 2;
                    }
                    Trace.WriteLine(getDT() + "Passing char select screen");
                    Thread.Sleep(2000);
                    sendKeys(wnd, (int)VirtualKeyCode.RETURN);
                    //InputSimulator.SimulateKeyPress(VirtualKeyCode.RETURN);
                    //System.Threading.Thread.Sleep(waitTime);
                    //SetForegroundWindow(p.MainWindowHandle);
                    if (!pixels.WaitForPixel(wnd, PixelIndex.InGame))
                    {
                        Trace.WriteLine(getDT() + "Not in game");
                        SaveScreenshot(wnd);
                        p.Kill();
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                        status_client.DownloadString(new Uri((base_url + "scheduler/fail?realm_id=" + realm_id + "&faction_id=" + faction + "&reason=Not+in+game")));
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                        //Console.ReadKey();
                        p.WaitForExit();
                        p.Dispose();
                        Environment.Exit(2);
                        return 2;
                    }
                    Trace.WriteLine(getDT() + "In game");
                    Thread.Sleep(2000);
                    var start_url = base_url + "scheduler/start?realm_id=" + realm_id + "&faction_id=" + faction;

                    sendKeys(wnd, (int)VirtualKeyCode.RETURN);
                    Thread.Sleep(100);
                    //InputSimulator.SimulateKeyPress(VirtualKeyCode.RETURN);
                    sendKeydown(wnd, (int)VirtualKeyCode.OEM_2);
                    Thread.Sleep(100);
                    sendChars(wnd, "tar " + auctioneer);
                    //InputSimulator.SimulateTextEntry("/tar "+auctioneer);
                    sendKeys(wnd, (int)VirtualKeyCode.RETURN);
                    //InputSimulator.SimulateKeyPress(VirtualKeyCode.RETURN);
                    Thread.Sleep(500);
                    if (expac_mode == EXPAC_WOTLK)
                    {
                        sendKeys(wnd, (int)VirtualKeyCode.OEM_6);
                        //InputSimulator.SimulateKeyPress(VirtualKeyCode.OEM_6);
                    }
                    else if (expac_mode == EXPAC_TBC)
                    {
                        Rect r = new Rect();
                        GetWindowRect((IntPtr)wnd, ref r);
                        SetForegroundWindow(wnd);
                        SetCursorPosition(r.Left + ((r.Right - r.Left) / 2), r.Top + ((r.Bottom - r.Top) / 2));
                        rightclick(wnd, 0, 0);
                    }
                    //System.Threading.Thread.Sleep(3000);
                    if (!pixels.WaitForPixel(wnd, PixelIndex.AuctionHouseOpen))
                    {
                        Trace.WriteLine(getDT() + "Auction house not opened");
                        SaveScreenshot(wnd);
                        p.Kill();
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                        status_client.DownloadString(new Uri((base_url + "scheduler/fail?realm_id=" + realm_id + "&faction_id=" + faction + "&reason=auction+house+not+opened")));
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                        p.WaitForExit();
                        p.Dispose();
                        Environment.Exit(2);
                        return 2;
                    }
                    Trace.WriteLine(getDT() + "Auction house opened");
                    Thread.Sleep(5000);
                    sendKeys(wnd, (int)VirtualKeyCode.RETURN);
                    Thread.Sleep(100);
                    sendKeydown(wnd, (int)VirtualKeyCode.OEM_2);
                    Thread.Sleep(100);
                    if (expac_mode == EXPAC_TBC)
                        sendChars(wnd, "script AucAdvanced.Scan.StartScan()");
                    else
                        sendChars(wnd, "auc scan");

                    Thread.Sleep(100);
                    sendKeys(wnd, (int)VirtualKeyCode.RETURN);
                    //InputSimulator.SimulateKeyPress(VirtualKeyCode.RETURN);
                    //InputSimulator.SimulateTextEntry("/auc scan");
                    //InputSimulator.SimulateKeyPress(VirtualKeyCode.RETURN);

                    // Check if scanning started
                    var maxtries = 5;
                    while (!pixels.WaitForPixel(wnd, PixelIndex.AuctionHouseScanning, 5) && maxtries > 0)
                    {
                        // If not, keep trying to initiate scan
                        SaveScreenshot(wnd);
                        Trace.WriteLine(getDT() + "Retrying auction scan");
                        Thread.Sleep(1000);
                        sendKeys(wnd, (int)VirtualKeyCode.RETURN);
                        Thread.Sleep(100);
                        sendKeydown(wnd, (int)VirtualKeyCode.OEM_2);
                        Thread.Sleep(100);
                        if (expac_mode == EXPAC_TBC)
                            sendChars(wnd, "script AucAdvanced.Scan.StartScan()");
                        else
                            sendChars(wnd, "auc scan");

                        Thread.Sleep(100);
                        sendKeys(wnd, (int)VirtualKeyCode.RETURN);
                        maxtries--;
                    }
                    Trace.WriteLine(getDT() + "Auction scan started");

                    bool myswitch = false;
                    Random rand = new System.Random();
                    int timeout = rand.Next(240, 300);
                    int counter = 0;
                    int failcount = 0;
                    bool scanning = false;
                    int scanCompleteCount = 0;
                    int notRespondingCount = 0;
                    while (p.Responding && !p.HasExited)
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

                        if (!p.Responding)
                        {
                            if (notRespondingCount > 5)
                            {
                                Trace.WriteLine(getDT() + "Warcraft process stopped responding");
                                p.Kill();
                                p.WaitForExit();
                                p.Dispose();
                                Environment.Exit(3);
                                return 3;
                            }
                            notRespondingCount++;
                        }
                        else
                        {
                            notRespondingCount = 0;
                        }

                        // Check if scanning is in processing phase (getall scan available, play button not)
                        // I think icecrown times out here sometimes and is killed before saving
                        if (!scanning && pixels.TestPixel(wnd, PixelIndex.AuctionHouseProcessing))
                        {
                            // If we found this 3 times in a row it must be processing
                            // Can get false positives on page changes
                            if (scanCompleteCount == 3)
                            {
                                SaveScreenshot(wnd);
                                scanning = true;
                                Trace.WriteLine(getDT() + "Scan complete, now processing");
                            }
                            scanCompleteCount++;
                        }
                        else
                        {
                            scanCompleteCount = 0;
                        }

                        if (!scanning && !pixels.TestPixel(wnd, PixelIndex.InGame) && (counter % 10) == 0)
                        {
                            SaveScreenshot(wnd);
                            Trace.WriteLine(getDT() + "We are not ingame any more , mostly restart or crash");
                            if (failcount >= 3)
                            {
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                                status_client.DownloadString(new Uri((base_url + "scheduler/fail?realm_id=" + realm_id + "&faction_id=" + faction + "&reason=not+in+game+anymore+mostly+crash+or+restart")));
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
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
                        if (!scanning && !pixels.TestPixel(wnd, PixelIndex.AuctionHouseOpen) && (counter % 10) == 0)
                        {
                            //SetForegroundWindow(p.MainWindowHandle);
                            Trace.WriteLine(getDT() + "Auction House not opened, trying to reopen and continue");
                            sendKeys(wnd, (int)VirtualKeyCode.RETURN);
                            Thread.Sleep(100);
                            sendKeydown(wnd, (int)VirtualKeyCode.OEM_2);
                            Thread.Sleep(100);
                            sendChars(wnd, "tar " + auctioneer);
                            Thread.Sleep(100);
                            sendKeys(wnd, (int)VirtualKeyCode.RETURN);
                            //InputSimulator.SimulateKeyPress(VirtualKeyCode.RETURN);
                            //InputSimulator.SimulateTextEntry("/tar " + auctioneer);
                            //InputSimulator.SimulateKeyPress(VirtualKeyCode.RETURN);
                            if (expac_mode == EXPAC_WOTLK)
                            {
                                Thread.Sleep(500);
                                sendKeys(p.MainWindowHandle, (int)VirtualKeyCode.OEM_6);
                            }
                            else if (expac_mode == EXPAC_TBC)
                            {
                                Rect r = new Rect();
                                GetWindowRect((IntPtr)wnd, ref r);
                                SetForegroundWindow(wnd);
                                SetCursorPosition(r.Left + ((r.Right - r.Left) / 2), r.Top + ((r.Bottom - r.Top) / 2));
                                rightclick(wnd, 0, 0);
                            }
                            //InputSimulator.SimulateKeyPress(VirtualKeyCode.OEM_6);
                            Thread.Sleep(3000);
                            sendKeys(wnd, (int)VirtualKeyCode.RETURN);
                            Thread.Sleep(100);
                            sendKeydown(wnd, (int)VirtualKeyCode.OEM_2);
                            Thread.Sleep(100);
                            sendChars(wnd, "auc scan");
                            Thread.Sleep(100);
                            sendKeys(wnd, (int)VirtualKeyCode.RETURN);
                            /*InputSimulator.SimulateKeyPress(VirtualKeyCode.RETURN);
                            InputSimulator.SimulateTextEntry("/auc scan");
                            InputSimulator.SimulateKeyPress(VirtualKeyCode.RETURN);*/
                        }

                        Thread.Sleep(1000);
                        if (counter >= timeout)
                        {
                            if ((DateTime.Now.ToUniversalTime()-p.StartTime.ToUniversalTime()).TotalSeconds > wowTimeout * 60)
                            {
                                SaveScreenshot(wnd);
                                Trace.WriteLine(getDT() + "Client has been running too long, must be stuck. Killing it.");
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                                status_client.DownloadString(new Uri((base_url + "scheduler/fail?realm_id=" + realm_id + "&faction_id=" + faction + "&reason=timed+out+in+auction+screen")));
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                                p.Kill();
                                p.WaitForExit();
                            }

                            // Screenshot
                            sendKeys(wnd, (int)VirtualKeyCode.OEM_4);

#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                            status_client.DownloadString(new Uri((base_url + "scheduler/ping?realm_id=" + realm_id + "&faction_id=" + faction)));
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
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
                    Thread.Sleep(5000);
                    current_path = Directory.GetCurrentDirectory();
                    current_path += $"{folderSeparator}WTF{folderSeparator}Account{folderSeparator}" + accountname.ToUpper() + $"{folderSeparator}SavedVariables{folderSeparator}Auc-ScanData.lua";
                    p.Dispose();
                    if (File.Exists(current_path))
                    {
                        Trace.WriteLine(getDT() + "Uploading file");
                        HttpClientEx client1 = new HttpClientEx();
                        client1.Timeout = TimeSpan.FromMinutes(10);
                        string myfile1 = @current_path;
                        var ret = client1.UploadFile(base_url + "private-api/takefile?id=" + realm_id + "&faction=" + faction, myfile1);
                        Trace.WriteLine(ret);
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                        status_client.DownloadString(new Uri((base_url + "scheduler/end?realm_id=" + realm_id + "&faction_id=" + faction)));
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                        Environment.Exit(0);
                        return 0;
                    }
                    else
                    {
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                        status_client.DownloadString(new Uri((base_url + "scheduler/fail?realm_id=" + realm_id + "&faction_id=" + faction + "&reason=Failed+to+upload+file")));
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
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
