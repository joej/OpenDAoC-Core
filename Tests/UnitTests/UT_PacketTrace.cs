using System;
using DOL.GS.PacketHandler;
using DOL.Logging;
using NUnit.Framework;

namespace DOL.GS.Tests
{
    [TestFixture]
    public class UT_PacketTrace
    {
        private static readonly byte[] PAYLOAD = [0x01, 0x02, 0xFE];

        [OneTimeSetUp]
        public void InitializeLogging()
        {
            LoggerManager.InitializeWithExplicitLibrary(null, LogLibrary.None);
        }

        [Test]
        public void FormatLine_IsEightTabSeparatedFields()
        {
            DateTime utc = new(2026, 11, 6, 21, 5, 9, 42, DateTimeKind.Utc);
            string line = PacketTrace.FormatLine(utc, "3/12", "account", "C>S", "TCP", "A9", 3, "0102FE");

            Assert.That(line, Is.EqualTo("2026-11-06T21:05:09.042Z\t3/12\taccount\tC>S\tTCP\tA9\t3\t0102FE"));
        }

        [Test]
        public void TryGetOutboundBody_TcpPacket_IsWhatFollowsTheCode()
        {
            GSTCPPacketOut packet = new GSTCPPacketOut().Init(0x2A);
            packet.Write(PAYLOAD, 0, PAYLOAD.Length);
            packet.WritePacketLength();

            Assert.That(PacketTrace.TryGetOutboundBody(packet.GetBuffer().AsSpan(0, (int) packet.Length), false, out byte code, out ReadOnlySpan<byte> body), Is.True);
            Assert.That(code, Is.EqualTo(0x2A));
            Assert.That(body.ToArray(), Is.EqualTo(PAYLOAD));
        }

        // The same frame whether it goes out by UDP or, converted, over TCP.
        [Test]
        public void TryGetOutboundBody_UdpPacket_SkipsTheCounter()
        {
            GSUDPPacketOut packet = new GSUDPPacketOut().Init(0xA9);
            packet.Write(PAYLOAD, 0, PAYLOAD.Length);
            packet.WritePacketLength();

            Assert.That(PacketTrace.TryGetOutboundBody(packet.GetBuffer().AsSpan(0, (int) packet.Length), true, out byte code, out ReadOnlySpan<byte> body), Is.True);
            Assert.That(code, Is.EqualTo(0xA9));
            Assert.That(body.ToArray(), Is.EqualTo(PAYLOAD));
        }

        [Test]
        public void TryGetOutboundBody_VersionRefusedFrame()
        {
            Assert.That(PacketTrace.TryGetOutboundBody(GameClient.VersionRefusedFrame, false, out byte code, out ReadOnlySpan<byte> body), Is.True);
            Assert.That(code, Is.EqualTo(0x23));
            Assert.That(body.Length, Is.EqualTo(7));
        }

        [Test]
        public void TryGetOutboundBody_TooShort_IsSkipped()
        {
            Assert.That(PacketTrace.TryGetOutboundBody([0x00, 0x00], false, out _, out _), Is.False);
        }

        // The body starts after the 10-byte header and leaves out the checksum.
        [Test]
        public void InboundBody_IsTheLoadedPacketData()
        {
            byte[] frame = [0x00, 0x03, 0x00, 0x01, 0x00, 0x05, 0x00, 0x00, 0x00, 0xA9, .. PAYLOAD, 0xCC, 0xDD];
            GSPacketIn packet = new GSPacketIn().Init();
            packet.Load(frame, 0, frame.Length);

            string body = PacketTrace.InboundBody(GameClient.eClientVersion.Version1127, packet.Code, packet.GetBuffer().AsSpan(0, packet.DataSize));

            Assert.That(body, Is.EqualTo("0102FE"));
        }

        [TestCase(GameClient.eClientVersion.Version1127, 0xA7, 40, true)]   // login request: password
        [TestCase(GameClient.eClientVersion.Version1130, 0xA7, 0, true)]
        [TestCase(GameClient.eClientVersion.Version1127, 0xF4, 7, false)]   // version only
        [TestCase(GameClient.eClientVersion.Version1127, 0xF4, 8, true)]    // bundled RC4 key
        [TestCase(GameClient.eClientVersion.Version1110, 0xF4, 260, true)]  // leading 1: RC4 key follows
        [TestCase(GameClient.eClientVersion.Version1127, 0xA9, 40, false)]
        public void IsSecretInbound(GameClient.eClientVersion version, int code, int length, bool expected)
        {
            byte[] body = new byte[length];

            if (length > 0)
                body[0] = 1;

            Assert.That(PacketTrace.IsSecretInbound(version, code, body), Is.EqualTo(expected));
            Assert.That(PacketTrace.InboundBody(version, code, body) == PacketTrace.MASKED, Is.EqualTo(expected));
        }

        [Test]
        public void IsSecretInbound_OldClientWithoutKey_IsNotMasked()
        {
            Assert.That(PacketTrace.IsSecretInbound(GameClient.eClientVersion.Version1110, 0xF4, [0x00, 0x01, 0x01]), Is.False);
        }
    }
}
