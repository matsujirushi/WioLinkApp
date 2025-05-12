using CommandLine;
using CommandLine.Text;
using SeeedKK.WioLink;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;

class Program
{
    private const string NODE_IP = "192.168.4.1";
    private const int NODE_PORT = 1025;

    [Verb("prov", false, HelpText = "デバイスをプロビジョニングします。")]
    class ProvOptions
    {
        [Option('n', "name", Required = true, HelpText = "デバイスの名前です。")]
        public string? Name { get; set; }

        [Option('s', "server", Required = true, HelpText = "接続するWioサーバーです。")]
        public string? Server { get; set; }

        [Option('u', "user", Required = true, HelpText = "Wioサーバーにログインするユーザー名です。")]
        public string? User { get; set; }

        [Option('p', "password", Required = false, HelpText = "Wioサーバーにログインするパスワードです。")]
        public string? Password { get; set; }

        [Option('S', "wifi-ssid", Required = true, HelpText = "デバイスに設定するWi-FiのSSIDです。")]
        public string? WifiSsid { get; set; }

        [Option('P', "wifi-password", Required = false, HelpText = "デバイスに設定するWi-Fiのパスワードです。")]
        public string? WifiPassword { get; set; }
    }

    private static async Task<int> RunProvisioning(ProvOptions opts)
    {
        var addresses = await Dns.GetHostAddressesAsync(opts.Server!);
        var serverIp = addresses.First();

        var service = new WioLinkService($"https://{opts.Server}");

        // ログイン
        Console.Write("Wioサーバーにログイン... ");
        var user = await service.UserLoginAsync(opts.User!, opts.Password!);
        service.SetToken(user.token);
        //user = null;
        Console.WriteLine("成功。");

        // ノードを作成
        Console.Write("ノードを作成... ");
        var newNode = await service.CreateNodeAsync(service.TemporaryNodeName, service.BoardWioNode1_0);
        Console.WriteLine($"成功。 node_sn:{newNode.node_sn}");

        // ノードのアクセスポイントに接続
        Console.WriteLine("ノードのアクセスポイントに接続して、ENTERキーを押してください。");
        Console.ReadLine();

        // ノードを設定
        while (true)
        {
            try
            {
                Console.Write("ノードのバージョンを確認... ");
                var version = await GetNodeVersion();
                Console.WriteLine($"成功。 version:{version}");
                if (version < 1.2) Console.WriteLine("WARNING: ファームウェアがドメイン名前解決に対応していません。更新を強く推奨します。");

                Console.Write("ノードを設定中... ");
                if (version < 1.2)
                {
                    await ConfigureNode(newNode.node_sn!, newNode.node_key!, opts.WifiSsid!, opts.WifiPassword!, serverIp);
                }
                else
                {
                    await ConfigureNode(newNode.node_sn!, newNode.node_key!, opts.WifiSsid!, opts.WifiPassword!, opts.Server!);
                }
                Console.WriteLine("成功。");
                break;
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("タイムアウト。");
                if (!RetryConfirmation("再試行しますか？[Yn]")) throw new ApplicationException();
            }
        }

        // インターネットのアクセスポイントに接続
        Console.WriteLine("インターネットのアクセスポイントに接続して、ENTERキーを押してください。");
        Console.ReadLine();

        // ノードのオンラインを待機
        Console.WriteLine("ノードのオンラインを待機...");
        for (int i = 0; i < 60; i++)
        {
            var nodes2 = await service.ListAllNodesOfUserAsync();
            var node = nodes2.nodes?.Where(n => n.node_sn == newNode.node_sn).First();
            Console.Write($" {i} ");
            if (node == null) Console.WriteLine("unknown");
            else Console.WriteLine(node.online ? "online" : "offline");
            if (node?.online ?? false) break;
            await Task.Delay(1000);
        }
        Console.WriteLine("成功。");

        // ノードの名前を変更
        Console.Write("ノードの名前を変更... ");
        await service.RenameNodeAsync(newNode.node_sn!, opts.Name!);
        Console.WriteLine("成功。");

        return 0;
    }

    static async Task Main(string[] args)
    {
        await CommandLine.Parser.Default.ParseArguments<ProvOptions>(args)
            .MapResult(
            (ProvOptions opts) => RunProvisioning(opts),
            errs => Task.FromResult(1)
            );
    }

    private static async Task<double> GetNodeVersion()
    {
        using UdpClient udpClient = new UdpClient(NODE_PORT);
        using var cts = new CancellationTokenSource();
        cts.CancelAfter(4000);
        var token = cts.Token;

        var waitForOk = Task.Run(async () =>
        {
            while (true)
            {
                var result = await udpClient.ReceiveAsync(token);
                string message = Encoding.UTF8.GetString(result.Buffer);

                double version;
                if (result.RemoteEndPoint.ToString() == $"{NODE_IP}:{NODE_PORT}" && double.TryParse(message, out version)) return version;
            }
        }, token);
        await Task.Delay(1000);

        byte[] sendBytes = Encoding.ASCII.GetBytes("VERSION");
        udpClient.Send(sendBytes, sendBytes.Length, NODE_IP, NODE_PORT);

        var version = await waitForOk;

        udpClient.Close();

        return version;
    }

    private static async Task ConfigureNode(string nodeSn, string nodeKey, string wifiSsid, string wifiPassword, IPAddress serverIp)
    {
        using UdpClient udpClient = new UdpClient(NODE_PORT);
        using var cts = new CancellationTokenSource();
        cts.CancelAfter(4000);
        var token = cts.Token;

        var waitForOk = Task.Run(async () =>
        {
            while (true)
            {
                var result = await udpClient.ReceiveAsync(token);
                string message = Encoding.UTF8.GetString(result.Buffer);

                if (result.RemoteEndPoint.ToString() == $"{NODE_IP}:{NODE_PORT}" && message == "ok\r\n") return;
            }
        }, token);
        await Task.Delay(1000);

        byte[] sendBytes = Encoding.ASCII.GetBytes($"APCFG: {wifiSsid}\t{wifiPassword}\t{nodeKey}\t{nodeSn}\t{serverIp.ToString()}\t{serverIp.ToString()}\t");
        udpClient.Send(sendBytes, sendBytes.Length, NODE_IP, NODE_PORT);

        await waitForOk;

        udpClient.Close();
    }

    private static async Task ConfigureNode(string nodeSn, string nodeKey, string wifiSsid, string wifiPassword, string server)
    {
        using UdpClient udpClient = new UdpClient(NODE_PORT);
        using var cts = new CancellationTokenSource();
        cts.CancelAfter(4000);
        var token = cts.Token;

        var waitForOk = Task.Run(async () =>
        {
            while (true)
            {
                var result = await udpClient.ReceiveAsync(token);
                string message = Encoding.UTF8.GetString(result.Buffer);

                if (result.RemoteEndPoint.ToString() == $"{NODE_IP}:{NODE_PORT}" && message == "ok\r\n") return;
            }
        }, token);
        await Task.Delay(1000);

        byte[] sendBytes = Encoding.ASCII.GetBytes($"APCFG: {wifiSsid}\t{wifiPassword}\t{nodeKey}\t{nodeSn}\t{server}\t{server}\t");
        udpClient.Send(sendBytes, sendBytes.Length, NODE_IP, NODE_PORT);

        await waitForOk;

        udpClient.Close();
    }

    private static bool RetryConfirmation(string message)
    {
        string? input;
        while (true)
        {
            Console.Write(message);
            input = Console.ReadLine();

            if (input == "y" || input == "Y" || input == "") return true;
            if (input == "n" || input == "N") return false;
        }
    }
}
