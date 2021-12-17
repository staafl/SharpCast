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

        static void Main(string[] args)
        {
            string host = args.FirstOrDefault() ?? "192.168.0.105";
            string ip = GetMainIPv4().ToString();
            int port = 7532;
            string contentUrl = $"http://{ip}:{port}/zelda.jpg";
            const string contentType = "image/jpeg";

            bool persistent = args.Any(x => x == "/persistent");

            new Thread(() =>
            {
                HttpListener listener = new HttpListener();
                listener.Prefixes.Add($"http://{ip}:{port}/");
                listener.Start();
                while (true)
                {
                    HttpListenerContext context = listener.GetContext();
                    HttpListenerRequest request = context.Request;
                    HttpListenerResponse response = context.Response;
                    Bitmap bmp = new Bitmap(Screen.PrimaryScreen.Bounds.Width, Screen.PrimaryScreen.Bounds.Height);
                    Graphics gr = Graphics.FromImage(bmp);
                    gr.CopyFromScreen(0, 0, 0, 0, bmp.Size);
                    using (var ms = new MemoryStream())
                    {
                        bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Jpeg);
                        response.ContentLength64 = ms.Length;
                        response.ContentType = "image/jpeg";
                        System.IO.Stream output = response.OutputStream;
                        output.Write(ms.ToArray(), 0, (int)ms.Length);
                        output.Close();
                    }
                    if (!persistent)
                    {
                        break;
                    }
                }
                listener.Stop();
                Environment.Exit(0);
            }).Start();

            try
            {
                Player client = new Player(host);
                client.Connect();

                if (args.Any(x => x == "/debug"))
                {
                    client.MediaStatusChanged += (sender, status) =>
                    {
                        Console.WriteLine("New player state: " + status.PlayerState);
                    };
                }

                var rand = new Random();
                while (true)
                {
                    client.LoadPhoto(
                        new Uri(contentUrl + "/" + rand.Next()),
                        contentType,
                        new PhotoMediaMetadata
                        {
                            Title = "screenshot"
                        },
                        autoPlay: true);

                    // client.Play();
                    if (!persistent)
                    {
                        break;
                    }
                    Thread.Sleep(5000);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e + "");
            }
        }
    }
}
