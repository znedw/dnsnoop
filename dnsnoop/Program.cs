using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Net.Sockets;
using System.Reactive.Linq;
using System.Reflection;
using DNS.Protocol;
using DNS.Protocol.ResourceRecords;
using OutbreakLabs.LibPacketGremlin.Abstractions;
using OutbreakLabs.LibPacketGremlin.Extensions;
using OutbreakLabs.LibPacketGremlin.PacketFactories;
using OutbreakLabs.LibPacketGremlin.Packets;
using SharpPcap;
using SharpPcap.LibPcap;
using Console = Colorful.Console;

namespace dnsnoop
{
    internal enum LogLevel
    {
        Info,
        Verbose
    }

    class Program
    {
        private static readonly string _version = (Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly())
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion!;
        
        private static LogLevel _logLevel { get; set; }


        /// <param name="port">The port number to listen on</param>
        /// <param name="logLevel">Log level (info, debug, verbose)</param>
        /// <param name="listDevices">List all capture devices</param>
        /// <param name="device">Use device at index</param>
        static void Main(int port = 53, LogLevel logLevel = LogLevel.Info, bool listDevices = false, int device = 0)
        {
            if (listDevices)
                ListDevices();
            _logLevel = logLevel;

            var selectedDevice = LibPcapLiveDeviceList.Instance.ElementAtOrDefault(device);
            if (selectedDevice == null) return;

            Console.WriteAscii("dnsnoop", Color.Red);
            Console.WriteLine(_version, Color.Cornsilk);
            Console.WriteLine("using SharpPcap v{0}", SharpPcap.Version.VersionString);
            Console.WriteLine($"Listening on device {selectedDevice.Description ?? selectedDevice.Name} for traffic on port {port}",
                Color.LawnGreen);

            var packets = Observable.FromEventPattern<PacketArrivalEventHandler, CaptureEventArgs>(
                ev => selectedDevice.OnPacketArrival += ev, ev => selectedDevice.OnPacketArrival -= ev);

            var dnsReplies = from rx in packets
                let parsed = EthernetIIFactory.Instance.ParseAs(rx.EventArgs.Packet.Data)
                let layers = parsed?.Layers() ?? Enumerable.Empty<IPacket>()
                let ipv4 = layers.OfType<IPv4>()?.FirstOrDefault()
                where (layers.OfType<UDP>().Any()
                       && layers.OfType<IPv4>().Any())
                select parsed;

            using var dnsSub = dnsReplies.Subscribe(PrintPacket);

            selectedDevice.Open();
            selectedDevice.Filter = $"udp and port {port}";
            selectedDevice.StartCapture();


            Console.WriteLine("Press enter to stop");

            Console.WriteLine("query\tname\tserver IP\tresponse");
            Console.ReadLine();
            selectedDevice.StopCapture();
            Console.WriteLine(selectedDevice.Statistics);
        }


        private static void PrintPacket(EthernetII p)
        {
            var dnsReply = p.Layers().OfType<DNSReply>().FirstOrDefault();
            var ipv4 = p.Layers().OfType<IPv4>().FirstOrDefault();
            if (dnsReply?.Questions == null || ipv4 == null) return;


            if (_logLevel == LogLevel.Verbose)
                Console.WriteLine(dnsReply);

            foreach (var q in dnsReply.Questions)
            {
                var qType = q.Type;
                var qName = q.Name;
                var serverIp = ipv4.SourceAddress;
                string response;
                if (dnsReply.AnswerRecords == null || dnsReply.AnswerRecords.Count < 1)
                {
                    response = dnsReply.ResponseCode switch
                    {
                        ResponseCode.NoError => nameof(ResponseCode.NoError).ToUpper(),
                        ResponseCode.FormatError => nameof(ResponseCode.FormatError).ToUpper(),
                        ResponseCode.ServerFailure => "SERVFAIL",
                        ResponseCode.NameError => "NXDOMAIN",
                        ResponseCode.NotImplemented => nameof(ResponseCode.NotImplemented).ToUpper(),
                        ResponseCode.Refused => nameof(ResponseCode.Refused).ToUpper(),
                        _ => "RESERVED"
                    };
                }
                else
                {
                   response = string.Join(", ", dnsReply.AnswerRecords?.Select(d =>
                    {
                        var value = d.Type switch
                        {
                            RecordType.A => ((IPAddressResourceRecord) d).IPAddress.ToString(),
                            RecordType.AAAA => ((IPAddressResourceRecord) d).IPAddress.ToString(),
                            RecordType.CNAME => ((CanonicalNameResourceRecord) d).CanonicalDomainName.ToString(),
                            RecordType.PTR => ((PointerResourceRecord) d).PointerDomainName.ToString(),
                            RecordType.MX => FormatMx((MailExchangeResourceRecord) d),
                            RecordType.NS => ((NameServerResourceRecord) d).NSDomainName.ToString(),
                            RecordType.SOA => ((StartOfAuthorityResourceRecord) d).MasterDomainName.ToString(),
                            RecordType.SRV => FormatSrv((ServiceResourceRecord) d),
                            RecordType.TXT => FormatTxt((TextResourceRecord) d),
                            RecordType.OPT => throw new NotImplementedException(),
                            RecordType.ANY => throw new NotImplementedException(),
                            RecordType.WKS => throw new NotImplementedException(),
                            _ => throw new Exception($"I don't recognize that query type, {d.Type}")
                        };
                        return $"{d.Type}: {value}";
                    }) ?? Array.Empty<string>());
                }

                Console.WriteLine($"{qType}\t{qName}\t{serverIp}\t{response}");
            }
        }

        private static string FormatTxt(TextResourceRecord txt) => $"{txt.Attribute.Key}:{txt.Attribute.Value}";

        private static string FormatSrv(ServiceResourceRecord srv) =>
            $"{srv.Priority} {srv.Weight} {srv.Port} {srv.Target}";

        private static string FormatMx(MailExchangeResourceRecord mx) => $"{mx.ExchangeDomainName} {mx.Preference}";

        public static IEnumerable<(T item, int index)> WithIndex<T>(IEnumerable<T> source) =>
            source.Select((item, index) => (item, index));

        private static void ListDevices()
        {
            foreach (var (d, i) in WithIndex(LibPcapLiveDeviceList.Instance))
            {
                var ipAddr = d.Addresses
                    .FirstOrDefault(ip => ip.Addr.ipAddress?.AddressFamily == AddressFamily.InterNetwork)?.Addr
                    ?.ipAddress.ToString() ?? "N/a";
                Console.WriteLine($"({i}). {d.Description ?? d.Name} ({ipAddr})");
            }

            Environment.Exit(0);
        }
    }
}