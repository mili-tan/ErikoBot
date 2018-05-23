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
        private static readonly WebProxy MWebProxy = new WebProxy("127.0.0.1", 10800);

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

            BotClient = new TelegramBotClient(tokenStr, MWebProxy);

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

            if (IsIP(message.Text))
            {
                BotClient.SendTextMessageAsync(message.Chat.Id,
                    $"{message.Text} : {GeoIp(message.Text)} {GeoIsp(message.Text)}");
            }

            if (message.Text.Split(' ').Length > 1)
            {
                string msgStr = message.Text.Split(' ')[1];
                try
                {
                    switch (message.Text.Split(' ')[0])
                    {
                        case "/ip":
                            if (IsIP(msgStr))
                            {
                                BotClient.SendTextMessageAsync(message.Chat.Id,
                                    $"{msgStr} : {GeoIp(msgStr)} {GeoIsp(msgStr)}");
                            }
                            else
                            {
                                string ipAddr = HttpsDnsHostAddresses(msgStr);
                                BotClient.SendTextMessageAsync(message.Chat.Id,
                                    $"{msgStr}({ipAddr}) : {GeoIp(ipAddr)} {GeoIsp(ipAddr)}");
                            }

                            break;
                        case "/ipv6":
                            BotClient.SendTextMessageAsync(message.Chat.Id,
                                $"{msgStr} : {GeoIp(msgStr)} / {GeoIpZXv6(msgStr)}");
                            break;
                        case "/dns":
                            try
                            {
                                BotClient.SendTextMessageAsync(message.Chat.Id,
                                    $"DNSPOD PubDNS : {HttpDnsPodHostAddresses(msgStr)}");

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
                catch (Exception exception)
                {
                    Console.WriteLine(exception);
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

        public static string HttpDnsPodHostAddresses(string serverIpStr)
        {

            string dnsStr = new WebClient().DownloadString(
                $"http://119.29.29.29/d?dn={serverIpStr}");
            dnsStr = dnsStr.Split(';')[0];
            return dnsStr;
        }

        public static string GeoIp(string ipStr)
        {
            WebClient webClient = new WebClient
            {
                Proxy = MWebProxy,
                Headers =
                {
                    ["User-Agent"] =
                        "Mozilla/5.0 (Windows NT 6.1; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/64.0.2767.0 Safari/537.36"
                }
            };

            string locStr = webClient.DownloadString($"https://api.ip.sb/geoip/{ipStr}");
            JsonValue locDataJson = Json.Parse(locStr);
            string addr = locDataJson.AsObjectGetString("country_code3");
            if (!string.IsNullOrWhiteSpace(locDataJson.AsObjectGetString("city")))
            {
                addr += "," + locDataJson.AsObjectGetString("city");
            }

            if (!string.IsNullOrWhiteSpace(locDataJson.AsObjectGetString("organization")))
            {
                addr += " / " + locDataJson.AsObjectGetString("organization");
            }

            return addr;
        }

        public static string GeoIpZXv6(string ipStr)
        {
            WebClient webClient = new WebClient
            {
                Proxy = MWebProxy,
                Headers =
                {
                    ["User-Agent"] =
                        "Mozilla/5.0 (Windows NT 6.1; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/64.0.2767.0 Safari/537.36"
                }
            };

            string locStr = webClient.DownloadString($"http://ip.zxinc.org/api.php?type=json&ip={ipStr}");
            JsonValue locDataJson = Json.Parse(locStr).AsObjectGet("data");
            string addrLocationr = locDataJson.AsObjectGetString("location");
            return addrLocationr;
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
