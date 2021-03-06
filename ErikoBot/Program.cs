﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Net;
using Telegram.Bot;
using Telegram.Bot.Args;
using Telegram.Bot.Types.Enums;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using mCopernicus.EasyChecker;
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
                tokenStr = File.ReadAllText("token.text");
            else if (!string.IsNullOrWhiteSpace(string.Join("", args)))
                tokenStr = string.Join("", args);
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

            if (message == null) return;
            if (message.Type != MessageType.Text)
                Console.WriteLine("不被支援的媒体类型。");

            Console.WriteLine($"@{e.Message.From.Username}: " + e.Message.Text);

            if (IsIP(message.Text))
                BotClient.SendTextMessageAsync(message.Chat.Id,
                    $"{message.Text} : {GeoIp(message.Text)} {GeoIsp(message.Text)}");

            if (message.Text.Replace("  ", " ").Split(' ').Length > 1)
            {
                var bgWorker = new BackgroundWorker();
                bgWorker.DoWork += (o, args) =>
                {
                    string msgStr = message.Text.Replace("  ", " ").Split(' ')[1];
                    try
                    {
                        switch (message.Text.Replace("  ", " ").Split(' ')[0])
                        {
                            case "/ip":
                                if (IsIP(msgStr))
                                    BotClient.SendTextMessageAsync(message.Chat.Id,
                                        $"{msgStr} : {GeoIp(msgStr)}");
                                else
                                {
                                    string ipAddr = HttpsDnsHostAddresses(msgStr);
                                    BotClient.SendTextMessageAsync(message.Chat.Id,
                                        $"{msgStr}({ipAddr}) : {GeoIp(ipAddr)}");
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
                                var replyPing = MPing.Ping(msgStr);

                                int packetLoss = 0;
                                foreach (var item in replyPing)
                                    if (item == 0)
                                        packetLoss++;

                                if (packetLoss == replyPing.Count)
                                    BotClient.SendTextMessageAsync(message.Chat.Id,
                                         "Packet loss : All");
                                else
                                {
                                    List<int> pingList = new List<int>();
                                    for (int i = 0; i < replyPing.Count - 1; i++)
                                        if (replyPing[i] != 0)
                                            pingList.Add(replyPing[i]);

                                    BotClient.SendTextMessageAsync(message.Chat.Id,
                                        $"{msgStr} : {pingList.Min()} / {pingList.Average():0.00} / {pingList.Max()}ms"
                                        + $"\n\rPacket loss : {packetLoss} / {replyPing.Count}");
                                }
                                break;

                            case "/tcping":
                                var ipPort = msgStr.Split(':', '：', ' ');
                                Console.WriteLine(ipPort.Count());
                                List<int> replyTcping;

                                replyTcping = ipPort.Length == 2
                                    ? MPing.Tcping(ipPort[0], Convert.ToInt32(ipPort[1]))
                                    : MPing.Tcping(ipPort[1], Convert.ToInt32(ipPort[2]));

                                int packetLossTcp = 0;
                                foreach (var item in replyTcping)
                                    if (item == 0)
                                        packetLossTcp++;

                                if (packetLossTcp == replyTcping.Count)
                                    BotClient.SendTextMessageAsync(message.Chat.Id,
                                        "Packet loss : All");
                                else
                                {
                                    List<int> tcpingList = new List<int>();
                                    for (int i = 0; i < replyTcping.Count - 1; i++)
                                        if (replyTcping[i] != 0)
                                            tcpingList.Add(replyTcping[i]);

                                    BotClient.SendTextMessageAsync(message.Chat.Id,
                                        $"{msgStr} : {tcpingList.Min()} / {tcpingList.Average():0.00} / {tcpingList.Max()}ms"
                                        + $"\n\rPacket loss : {packetLossTcp} / {replyTcping.Count}");
                                }
                                break;

                            default:
                                BotClient.SendTextMessageAsync(message.Chat.Id,
                                    @"意外的指令。");
                                break;
                        }
                    }
                    catch (Exception exception)
                    {
                        Console.WriteLine(exception);
                        BotClient.SendTextMessageAsync(message.Chat.Id,
                            @"非常抱歉，可能发生了一些意外的故障，请重试。");
                    }
                };
                bgWorker.RunWorkerAsync();
            }
            else
            {
                BotClient.SendTextMessageAsync(message.Chat.Id,
                    @"意外的指令。");
            }
        }


        public static string HttpsDnsHostAddresses(string serverIpStr, bool googleDNS = false)
        {
            string dnsStr;
            if (googleDNS)
                dnsStr = new WebClient().DownloadString(
                        $"https://dnsp.milione.cc/resolve/?name={serverIpStr}&type=A");
            
            else
                dnsStr = new WebClient().DownloadString(
                    $"https://dns.cloudflare.com/dns-query?ct=application/dns-json&name={serverIpStr}&type=A");

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
            string addr = locDataJson.AsObjectGetString("country");
            if (!string.IsNullOrWhiteSpace(locDataJson.AsObjectGetString("city")))
                addr += " " + locDataJson.AsObjectGetString("city");

            if (!string.IsNullOrWhiteSpace(locDataJson.AsObjectGetString("organization")))
                addr += " / " + locDataJson.AsObjectGetString("organization");

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
