using System;
using System.Buffers.Binary;
using DOL.GS.PacketHandler;
using DOL.GS.ServerProperties;
using NUnit.Framework;

namespace DOL.GS.Tests
{
    [TestFixture]
    public class UT_VersionRefused
    {
        // The properties keep their defaults only once loaded by the server.
        [SetUp]
        [TearDown]
        public void ResetVersionProperties()
        {
            Properties.CLIENT_VERSION_MIN = -1;
            Properties.CLIENT_VERSION_MAX = -1;
            Properties.CLIENT_VERSION_REFUSAL = true;
        }

        // The client reads [u8 flag][u32 little-endian length][string, at most 0x20 bytes][u16] and acts on a non-zero flag.
        [Test]
        public void VersionRefusedFrame_ParsesAsTheClientReadsIt()
        {
            ReadOnlySpan<byte> frame = GameClient.VersionRefusedFrame;
            ReadOnlySpan<byte> payload = frame[3..];

            Assert.That(BinaryPrimitives.ReadUInt16BigEndian(frame), Is.EqualTo(payload.Length), "length = payload size");
            Assert.That(frame[2], Is.EqualTo(0x23));
            Assert.That(payload[0], Is.Not.Zero, "flag");

            uint length = BinaryPrimitives.ReadUInt32LittleEndian(payload[1..]);

            Assert.That(length, Is.LessThanOrEqualTo(0x20u));
            Assert.That(payload.Length, Is.EqualTo(1 + 4 + (int) length + 2));
        }

        [TestCase(-1, -1, GameClient.eClientVersion.Version1130, false)]
        [TestCase(1130, -1, GameClient.eClientVersion.Version1127, true)]
        [TestCase(1127, -1, GameClient.eClientVersion.Version1127, false)]
        [TestCase(-1, 1127, GameClient.eClientVersion.Version1130, true)]
        [TestCase(-1, 1130, GameClient.eClientVersion.Version1130, false)]
        [TestCase(1127, 1127, GameClient.eClientVersion.Version1128, true)]
        public void IsRefusedBeforeVersionReply_ChecksVersionRange(int min, int max, GameClient.eClientVersion version, bool expected)
        {
            Properties.CLIENT_VERSION_MIN = min;
            Properties.CLIENT_VERSION_MAX = max;

            Assert.That(GameClient.IsRefusedBeforeVersionReply(version), Is.EqualTo(expected));
        }

        // Older clients stay with the refusal at login.
        [Test]
        public void IsRefusedBeforeVersionReply_Before1127_IsLeftToLogin()
        {
            Properties.CLIENT_VERSION_MIN = 1127;

            Assert.That(GameClient.IsRefusedBeforeVersionReply(GameClient.eClientVersion.Version1126), Is.False);
        }

        // Turned off, out-of-range clients go through to the login check, as before 0x23 was sent.
        [Test]
        public void IsRefusedBeforeVersionReply_TurnedOff_IsLeftToLogin()
        {
            Properties.CLIENT_VERSION_MIN = 1130;
            Properties.CLIENT_VERSION_REFUSAL = false;

            Assert.That(GameClient.IsRefusedBeforeVersionReply(GameClient.eClientVersion.Version1127), Is.False);
        }
    }
}
