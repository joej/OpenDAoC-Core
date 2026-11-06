using System;
using System.Globalization;
using System.Threading;
using DOL.Logging;

namespace DOL.GS.PacketHandler
{
    // Debug: a trace of every packet a client sends or receives, at Trace level on the logger "DOL.GS.PacketTrace".
    // Nothing is logged at Trace by default; logconfig.xml has a disabled rule that writes it to its own file.
    //
    // One tab-separated line per packet:
    //   <UTC time> <connection> <account or remote endpoint> <C>S|S>C> <TCP|UDP> <opcode> <body length> <body hex>
    // and one per connection event, with EVT as the direction, "-" as transport and opcode, 0 as length and a text.
    //
    // <connection> is "<n>/<session id>": n counts connections since the server started; session ids are reused.
    // <TCP|UDP> is the socket: S->C packets the server would send by UDP but sends over TCP show as TCP.
    // The body is the bytes after the opcode, in upper-case hex:
    //   C->S: after the 10-byte header (size, sequence, session id, parameter, 0x00, opcode), without the 2-byte checksum;
    //         taken after decryption.
    //   S->C TCP: after the 2-byte size and the opcode; taken before encryption.
    //   S->C UDP: after the 2-byte size, the 2-byte counter and the opcode; taken before encryption.
    // Packets that carry a password or key material keep their opcode and length, with MASKED in place of the body.
    // An RSA key transport, which replaces a packet, shows "-" as opcode and its length after the size field.
    public static class PacketTrace
    {
        private static readonly Logger log = LoggerManager.Create("DOL.GS.PacketTrace");
        private static int _connectionCount;

        public const string MASKED = "MASKED";
        public const string NONE = "-";

        public static int NextConnectionNumber()
        {
            return Interlocked.Increment(ref _connectionCount);
        }

        public static void Inbound(GameClient client, bool udp, GSPacketIn packet)
        {
            if (!log.IsTraceEnabled)
                return;

            ReadOnlySpan<byte> body = packet.GetBuffer().AsSpan(0, packet.DataSize);
            log.Trace(FormatLine(DateTime.UtcNow, client, "C>S", Transport(udp), $"{packet.Code:X2}", body.Length, InboundBody(client.Version, packet.Code, body)));
        }

        public static void InboundKeyTransport(GameClient client, int length)
        {
            if (log.IsTraceEnabled)
                log.Trace(FormatLine(DateTime.UtcNow, client, "C>S", Transport(false), NONE, length, MASKED));
        }

        // `frame` starts at the size field. `udpFrame`: it has the UDP layout (with the counter), whichever socket it goes out on.
        public static void Outbound(GameClient client, bool udp, ReadOnlySpan<byte> frame, bool udpFrame)
        {
            if (!log.IsTraceEnabled || !TryGetOutboundBody(frame, udpFrame, out byte code, out ReadOnlySpan<byte> body))
                return;

            log.Trace(FormatLine(DateTime.UtcNow, client, "S>C", Transport(udp), $"{code:X2}", body.Length, Convert.ToHexString(body)));
        }

        public static void Outbound(GameClient client, bool udp, ReadOnlySpan<byte> frame)
        {
            Outbound(client, udp, frame, udp);
        }

        public static void Event(GameClient client, string text)
        {
            if (log.IsTraceEnabled)
                log.Trace(FormatLine(DateTime.UtcNow, client, "EVT", NONE, NONE, 0, text.Replace('\t', ' ').Replace('\n', ' ')));
        }

        public static bool TryGetOutboundBody(ReadOnlySpan<byte> frame, bool udpFrame, out byte code, out ReadOnlySpan<byte> body)
        {
            int codeOffset = udpFrame ? 4 : 2;

            if (frame.Length <= codeOffset)
            {
                code = 0;
                body = default;
                return false;
            }

            code = frame[codeOffset];
            body = frame[(codeOffset + 1)..];
            return true;
        }

        public static string InboundBody(GameClient.eClientVersion version, int code, ReadOnlySpan<byte> body)
        {
            return IsSecretInbound(version, code, body) ? MASKED : Convert.ToHexString(body);
        }

        // The login request carries the password. A crypt key request carries an RC4 key when the client bundles one
        // (more than 7 bytes from 1.115 on; a leading 1 before).
        public static bool IsSecretInbound(GameClient.eClientVersion version, int code, ReadOnlySpan<byte> body)
        {
            return code switch
            {
                (int) eClientPackets.LoginRequest => true,
                (int) eClientPackets.CryptKeyRequest => version >= GameClient.eClientVersion.Version1115 ? body.Length > 7 : body.Length > 0 && body[0] == 1,
                _ => false
            };
        }

        public static string FormatLine(DateTime utc, string connection, string who, string direction, string transport, string code, int length, string body)
        {
            string time = utc.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);
            return string.Join('\t', time, connection, who, direction, transport, code, length.ToString(CultureInfo.InvariantCulture), body);
        }

        private static string FormatLine(DateTime utc, GameClient client, string direction, string transport, string code, int length, string body)
        {
            string who = client.Account?.Name ?? client.TraceRemoteEndPoint ?? NONE;
            return FormatLine(utc, $"{client.TraceConnectionNumber}/{client.SessionID}", who, direction, transport, code, length, body);
        }

        private static string Transport(bool udp)
        {
            return udp ? "UDP" : "TCP";
        }
    }
}
