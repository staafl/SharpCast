namespace SharpCastConsole {
    using System;
    using SharpCast;
    using System.Net;
    using System.Threading;
    using System.IO;
    using System.Drawing;
    using System.Windows.Forms;
    using System.Net.Sockets;
    using System.Net.NetworkInformation;
    using System.Linq;

    class Program {

        public static bool IsIPv4(IPAddress ipa) => ipa.AddressFamily == AddressFamily.InterNetwork;

        public static IPAddress GetMainIPv4() => NetworkInterface.GetAllNetworkInterfaces()
        .Select((ni) => ni.GetIPProperties())
        .Where((ip) => ip.GatewayAddresses.Where((ga) => IsIPv4(ga.Address)).Count() > 0)
        .FirstOrDefault()?.UnicastAddresses?
        .Where((ua) => IsIPv4(ua.Address))?.FirstOrDefault()?.Address;

        static string GetArg(string[] args, string name)
        {
            return args.FirstOrDefault(x => x.StartsWith("/" + name + "="))?.Split(new[] { '=' }, 2).Skip(1).FirstOrDefault();
        }

        static void Main(string[] args)
        {
            string host = args.FirstOrDefault(x => !x.StartsWith("/")) ?? "192.168.0.105";

            if (args.Any(x => x == "/pause"))
            {
                Player client1 = new Player(host);
                client1.Connect();
                client1.Pause();
                Environment.Exit(0);
                return;
            }
            if (args.Any(x => x == "/play"))
            {
                Player client1 = new Player(host);
                client1.Connect();
                client1.Play();
                Environment.Exit(0);
                return;
            }

            string ip = GetArg(args, "ip") ?? GetMainIPv4().ToString();
            int port = int.Parse(GetArg(args, "port") ?? "7532");
            int timeout = int.Parse(GetArg(args, "timeout") ?? "0");
            if (timeout > 0)
            {
                new Thread(() =>
                {
                    Thread.Sleep(timeout * 60 * 1000);
                    Environment.Exit(0);
                })
                { IsBackground = true }.Start();
            }
            int interval = int.Parse(GetArg(args, "interval") ?? "295");
            string contentUrl = $"http://{ip}:{port}/zelda.jpg";
            const string contentType = "image/jpeg";

            bool persistent = args.Any(x => x == "/persistent");
            bool single = args.Any(x => x == "/single");
            bool debug = args.Any(x => x == "/debug");

            byte[] bmpArray = GetScreenshot();
            if (true)
            {
                new Thread(() =>
                {
                    HttpListener listener = new HttpListener();
                    listener.Prefixes.Add($"http://{ip}:{port}/");
                    listener.Start();
                    while (true)
                    {
                        try
                        {
                            HttpListenerContext context = listener.GetContext();
                            HttpListenerRequest request = context.Request;
                            if (debug)
                            {
                                Console.WriteLine("Got request");
                            }
                            HttpListenerResponse response = context.Response;
                            if (!single || bmpArray == null)
                            {
                                bmpArray = GetScreenshot();
                            }

                            response.ContentLength64 = bmpArray.Length;
                            response.ContentType = "image/jpeg";
                            Stream output = response.OutputStream;
                            output.Write(bmpArray, 0, (int)bmpArray.Length);
                            output.Close();

                            if (!persistent)
                            {
                                break;
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine("Listener exception");
                            Console.WriteLine(ex + "");
                        }
                    }
                    listener.Stop();
                    Environment.Exit(0);
                })
                { IsBackground = true }.Start();
            }

            Player client = new Player(host);
            client.Connect();
            client.LaunchApp("233637DE"); // CC1AD845 ?
            if (debug)
            {
                client.MediaStatusChanged += (sender, status) =>
                {
                    Console.WriteLine("New player state: " + status.PlayerState);
                };
            }

            bool first = true;

            var rand = new Random();
            while (true)
            {
                try
                {
                    client.LoadPhoto(
                        new Uri(contentUrl + "/" + rand.Next()),
                        contentType,
                        new PhotoMediaMetadata
                        {
                            Title = "screenshot"
                        },
                        autoPlay: true,
                        timeout: 30*1000);

                    if (!persistent)
                    {
                        break;
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine("Chromecast exception");
                    Console.WriteLine(e + "");
                    if (e.Message == "No running application")
                    {
                        Console.Beep();
                        Console.Beep();
                        Environment.Exit(0);
                        return;
                    }
                }
                for (int x = interval; x > 0; )
                {
                    Thread.Sleep(Math.Min(10, x) * 1000);
                    x -= Math.Min(10, x);
                    if (client.GetRunningApp()?.ApplicationId != "CC1AD845")
                    {
                        if (first)
                        {
                            x += 10;
                        }
                        else
                        {
                            Console.Beep();
                            Console.Beep();
                            Environment.Exit(0);
                            return;
                        }
                    }
                    else
                    {
                        first = false;
                    }
                    Console.WriteLine(client.GetRunningApp()?.ApplicationId);
                }
                first = false;
            }
        }

        private static byte[] GetScreenshot()
        {
            byte[] bmpArray;
            using (var bmp = new Bitmap(Screen.PrimaryScreen.Bounds.Width, Screen.PrimaryScreen.Bounds.Height))
            {
                using (Graphics gr = Graphics.FromImage(bmp))
                {
                    gr.CopyFromScreen(0, 0, 0, 0, bmp.Size);
                    using (var ms = new MemoryStream())
                    {
                        bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Jpeg);
                        bmpArray = ms.ToArray();
                    }
                }
            }

            return bmpArray;
        }
    }
}
