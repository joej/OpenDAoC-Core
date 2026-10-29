using System;
using DOL.GS.PacketHandler;
using DOL.Logging;
using NUnit.Framework;

namespace DOL.GS.Tests
{
    [TestFixture]
    public class UT_PacketEncoding168
    {
        private const int OFFSET = 7; // A UDP datagram can carry several frames; this one isn't the first.
        private const int PAYLOAD_SIZE = 23;
        private const ushort COUNTER = 0x1234;
        private const byte CODE = 0xA9;

        // `PacketEncoding168` logs every packet it encrypts.
        [OneTimeSetUp]
        public void InitializeLogging()
        {
            LoggerManager.InitializeWithExplicitLibrary(null, LogLibrary.None);
        }

        [Test]
        public void EncryptPacket_UdpFrame_ClientDecryptRestoresCounterCodeAndPayload()
        {
            byte[] sbox = CreateSBox();
            byte[] expected = CreateDatagram();
            byte[] buf = (byte[]) expected.Clone();

            CreateEncoding(sbox).EncryptPacket(buf, OFFSET, true);

            Assert.That(buf[OFFSET], Is.EqualTo(expected[OFFSET]), "length stays in clear");
            Assert.That(buf[OFFSET + 1], Is.EqualTo(expected[OFFSET + 1]), "length stays in clear");
            Assert.That(buf.AsSpan(OFFSET + 2, PAYLOAD_SIZE + 3).SequenceEqual(expected.AsSpan(OFFSET + 2, PAYLOAD_SIZE + 3)), Is.False);
            Assert.That(buf.AsSpan(0, OFFSET).SequenceEqual(expected.AsSpan(0, OFFSET)), Is.True, "bytes before the frame are untouched");

            ClientDecryptUdpFrame(sbox, buf, OFFSET);

            Assert.That(buf, Is.EqualTo(expected));
        }

        [Test]
        public void ClientDecrypt_UnencryptedUdpFrame_GarblesCodeAndPayload()
        {
            byte[] sbox = CreateSBox();
            byte[] expected = CreateDatagram();
            byte[] buf = (byte[]) expected.Clone();

            ClientDecryptUdpFrame(sbox, buf, OFFSET);

            Assert.That(buf, Is.Not.EqualTo(expected));
        }

        [Test]
        public void Rsa1130AppliesTo_OnlyVersion1130WithCryptoEnabled()
        {
            foreach (GameClient.eClientVersion version in Enum.GetValues<GameClient.eClientVersion>())
            {
                Assert.That(Rsa1130.AppliesTo(false, version), Is.False, $"{version}, crypto disabled");
                Assert.That(Rsa1130.AppliesTo(true, version), Is.EqualTo(version is GameClient.eClientVersion.Version1130), $"{version}, crypto enabled");
            }
        }

        // No RSA key is loaded here, so `Rsa1130.Enabled` is false, as on a server with 1.130 crypto turned off.
        [TestCase(GameClient.eClientVersion.Version1127)]
        [TestCase(GameClient.eClientVersion.Version1130)]
        public void Uses1130Crypto_CryptoDisabled_IsFalse(GameClient.eClientVersion version)
        {
            GameClient client = new(null) { Version = version };

            Assert.That(Rsa1130.Enabled, Is.False);
            Assert.That(client.Uses1130Crypto, Is.False);
        }

        private static PacketEncoding168 CreateEncoding(byte[] sbox)
        {
            return new PacketEncoding168
            {
                SBox = (byte[]) sbox.Clone(),
                EncryptionState = eEncryptionState.PseudoRC4Encrypted
            };
        }

        private static byte[] CreateSBox()
        {
            byte[] sbox = new byte[256];

            for (int i = 0; i < sbox.Length; i++)
                sbox[i] = (byte) i;

            Random random = new(1130);

            for (int i = sbox.Length - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                (sbox[i], sbox[j]) = (sbox[j], sbox[i]);
            }

            return sbox;
        }

        // Same layout as `GSUDPPacketOut` after `PacketProcessor` writes the counter: [size][counter][code][payload].
        private static byte[] CreateDatagram()
        {
            byte[] buf = new byte[OFFSET + 5 + PAYLOAD_SIZE];

            for (int i = 0; i < OFFSET; i++)
                buf[i] = 0xEE;

            buf[OFFSET] = PAYLOAD_SIZE >> 8;
            buf[OFFSET + 1] = PAYLOAD_SIZE & 0xFF;
            buf[OFFSET + 2] = COUNTER >> 8;
            buf[OFFSET + 3] = COUNTER & 0xFF;
            buf[OFFSET + 4] = CODE;

            for (int i = 0; i < PAYLOAD_SIZE; i++)
                buf[OFFSET + 5 + i] = (byte) (i * 7 + 1);

            return buf;
        }

        // The 1.130 client's UDP receive: decrypts `size + 3` bytes (counter, code, payload) after the size,
        // with a fresh copy of the S-box, second half first.
        private static void ClientDecryptUdpFrame(byte[] sbox, byte[] buf, int offset)
        {
            byte[] s = (byte[]) sbox.Clone();
            int start = offset + 2;
            int count = ((buf[offset] << 8) | buf[offset + 1]) + 3;
            int half = count >> 1;
            byte i = 0;
            byte j = 0;

            for (int k = half; k < count; k++)
                DecryptByte(s, buf, start + k, ref i, ref j);

            for (int k = 0; k < half; k++)
                DecryptByte(s, buf, start + k, ref i, ref j);

            static void DecryptByte(byte[] s, byte[] buf, int index, ref byte i, ref byte j)
            {
                i++;
                byte tmp = s[i];
                j += tmp;
                s[i] = s[j];
                s[j] = tmp;
                buf[index] ^= s[(byte) (s[i] + tmp)];
                j += buf[index];
            }
        }
    }
}
