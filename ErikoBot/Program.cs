using System;
using System.Net;
using Telegram.Bot;
using Telegram.Bot.Args;
using Telegram.Bot.Types.Enums;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using EasyChecker;
using MojoUnity;

namespace ErikoBot
{
    static class Program
    {
        private static TelegramBotClient BotClient;

        static void Main(string[] args)
        {
            Console.WriteLine("Telegram Eriko Network Bot");
            string tokenStr;
            if (File.Exists("token.text"))
            {
                tokenStr = File.ReadAllText("token.text");
            }
            else if(!string.IsNullOrWhiteSpace(string.Join("",args)))
            {
                tokenStr = string.Join("", args);
            }
            else
            {
                Console.WriteLine("Token:");
                tokenStr = Console.ReadLine();
            }

            WebProxy webProxy = new WebProxy("127.0.0.1", 10800);
            BotClient = new TelegramBotClient(tokenStr, webProxy);

            Console.Title = "Bot:@" + BotClient.GetMeAsync().Result.Username;
            Console.WriteLine("Connected");

            BotClient.OnMessage += BotOnMessageReceived;
            BotClient.OnMessageEdited += BotOnMessageReceived;

            BotClient.StartReceiving(Array.Empty<UpdateType>());

            Console.ReadLine();
            Console.WriteLine("Exit");
            BotClient.StopReceiving();
        }

        private static void BotOnMessageReceived(object sender, MessageEventArgs e)
        {
            var message = e.Message;

            if (message == null || message.Type != MessageType.TextMessage) return;

            Console.WriteLine($"@{e.Message.From.Username}: " + e.Message.Text);

            if (message.Text.Split(' ').Length > 1)
            {
                string msgStr = message.Text.Split(' ')[1];
                switch (message.Text.Split(' ')[0])
                {
                    case "/ip":
                        if (IsIP(msgStr))
                        {
                            BotClient.SendTextMessageAsync(message.Chat.Id, $"{msgStr} : {GeoIp(msgStr)} {GeoIsp(msgStr)}");
                        }

                        break;
                    case "/dns":
                        try
                        {
                            BotClient.SendTextMessageAsync(message.Chat.Id,
                                $"DNSPOD PubDNS : {Dns.GetHostAddresses(msgStr)[0]}");

                            BotClient.SendTextMessageAsync(message.Chat.Id,
                                $"1.1.1.1 DNS : {HttpsDnsHostAddresses(msgStr)}");

                            BotClient.SendTextMessageAsync(message.Chat.Id,
                                $"Google DNS : {HttpsDnsHostAddresses(msgStr, true)}");
                        }
                        catch (Exception exception)
                        {
                            BotClient.SendTextMessageAsync(message.Chat.Id, exception.Message);
                        }

                        break;
                    case "/ping":
                        var replyPing = Ping.MPing(msgStr);

                        int packetLoss = 0;
                        foreach (var item in replyPing)
                        {
                            if (item == 0)
                            {
                                packetLoss++;
                            }
                        }

                        BotClient.SendTextMessageAsync(message.Chat.Id,
                            $"{msgStr} : {replyPing.Min()} / {replyPing.Average()} / {replyPing.Max()}ms"
                            + $"\n\rPacket loss : {packetLoss} / {replyPing.Count}");
                        break;
                    case "/tcping":
                        var ipPort = msgStr.Split(":");
                        if (ipPort.Length == 2)
                        {
                            var replyTcping = Ping.Tcping(ipPort[0], Convert.ToInt32(ipPort[1]));
                            BotClient.SendTextMessageAsync(message.Chat.Id,
                                $"{msgStr} : {replyTcping.Min()} / {replyTcping.Average()} / {replyTcping.Max()}ms");
                        }

                        break;
                }
            }
        }


        public static string HttpsDnsHostAddresses(string serverIpStr, bool googleDNS = false)
        {
            string dnsStr;
            if (googleDNS)
            {
                try
                {
                    dnsStr = new WebClient().DownloadString(
                        $"https://dns.google.com/resolve?name={serverIpStr}&type=A");
                }
                catch (Exception exception)
                {
                    Console.WriteLine("Google DNS:" + exception.Message);
                    Console.WriteLine("Try Plus1s Proxy");

                    dnsStr = new WebClient().DownloadString(
                        $"https://plus1s.site/extdomains/dns.google.com/resolve?name={serverIpStr}&type=A");
                }
            }
            else
            {
                dnsStr = new WebClient().DownloadString(
                    $"https://dns.cloudflare.com/dns-query?ct=application/dns-json&name={serverIpStr}&type=A");
            }

            JsonValue dnsAnswerJson = Json.Parse(dnsStr).AsObjectGet("Answer");
            string ipAnswerStr = dnsAnswerJson.AsArrayGet(0).AsObjectGetString("data");
            return IsIP(ipAnswerStr) ? ipAnswerStr : HttpsDnsHostAddresses(ipAnswerStr);
        }

        public static string GeoIp(string ipStr)
        {
            string locStr = new WebClient().DownloadString($"https://api.ip.sb/geoip/{ipStr}");
            JsonValue locJson = Json.Parse(locStr);
            string addr = locJson.AsObjectGetString("country_code3");
            if (!string.IsNullOrWhiteSpace(locJson.AsObjectGetString("city")))
            {
                addr += "," + locJson.AsObjectGetString("city");
            }

            addr += " / ";

            if (!string.IsNullOrWhiteSpace(locJson.AsObjectGetString("organization")))
            {
                addr += locJson.AsObjectGetString("organization");
            }

            return addr;
        }

        public static string GeoIsp(string ipStr)
        {
            string getIpStr = new WebClient().DownloadString($"http://ip.taobao.com/service/getIpInfo.php?ip={ipStr}");
            JsonValue ipJson = Json.Parse(getIpStr).AsObjectGet("data");
            return Encoding.UTF8.GetString(Encoding.Default.GetBytes(ipJson.AsObjectGetString("city") + ipJson.AsObjectGetString("isp"))).Replace("X", "");
        }

        public static bool IsIP(string ip)
        {
            return Regex.IsMatch(ip, @"^((2[0-4]\d|25[0-5]|[01]?\d\d?)\.){3}(2[0-4]\d|25[0-5]|[01]?\d\d?)$");
        }
    }
}
