using System;
using System.IO;
using System.Security.Cryptography;
using DOL.GS.ServerProperties;
using System.Reflection;
using DOL.Logging;

namespace DOL.GS.PacketHandler
{
    /// <summary>
    /// RSA key transport for 1.130+ clients.
    ///
    /// A 1.130 client generates a random symmetric key and sends it to the server encrypted
    /// with the server's RSA public key (PKCS#1 OAEP), chunked on the wire. The server holds the
    /// matching RSA private key and recovers the symmetric key, which then keys the per-direction
    /// stream cipher. This class owns the server private key (loaded from a PEM at startup) and the
    /// OAEP decrypt primitive. Pure managed .NET: no native CryptLib168 dependency, so it runs on
    /// Linux. Decrypt/RC4 of the actual key-transport message is wired in CryptKeyRequestHandler.
    /// </summary>
    public static class Rsa1130
    {
        private static readonly Logger log = LoggerManager.Create(MethodBase.GetCurrentMethod().DeclaringType);

        private static RSA _privateKey;

        /// <summary>True when a private key is loaded and 1.130 crypto is enabled.</summary>
        public static bool Enabled => _privateKey != null && Properties.CLIENT_ENABLE_1130_CRYPTO;

        /// <summary>Modulus size in bytes (RSA block size); 0 if no key loaded.</summary>
        public static int ModulusBytes => _privateKey?.KeySize / 8 ?? 0;

        /// <summary>
        /// Init hook for GameServer.Start(). Loads the PEM private key if 1.130 crypto is on.
        /// Returns true on success (or when the feature is disabled — not an error).
        /// </summary>
        public static bool Init()
        {
            if (!Properties.CLIENT_ENABLE_1130_CRYPTO)
            {
                log.Info("1.130 client crypto disabled (client_enable_1130_crypto=false)");
                return true;
            }

            string root = GameServer.Instance?.Configuration?.RootDirectory ?? ".";
            string path = Path.Combine(root, Properties.CLIENT_RSA_PRIVATE_KEY_FILE);

            try
            {
                string pem = File.ReadAllText(path);
                var rsa = RSA.Create();
                rsa.ImportFromPem(pem);
                _privateKey = rsa;
                log.Info($"1.130 RSA private key loaded from '{path}' ({rsa.KeySize}-bit, {ModulusBytes}-byte block)");
                return true;
            }
            catch (Exception e)
            {
                log.Error($"Failed to load 1.130 RSA private key from '{path}'. " +
                          "Disable client_enable_1130_crypto or provide the key.", e);
                return false;
            }
        }

        /// <summary>
        /// RSA-OAEP decrypt one ciphertext block with the server private key.
        /// Returns the recovered plaintext, or null on failure (e.g. OAEP padding mismatch).
        /// OAEP hash/MGF must match what the client used — confirm against a real captured blob.
        /// </summary>
        public static byte[] DecryptBlock(byte[] ciphertext, RSAEncryptionPadding padding)
        {
            if (_privateKey == null)
                return null;
            try
            {
                return _privateKey.Decrypt(ciphertext, padding);
            }
            catch (CryptographicException e)
            {
                log.Warn($"RSA-OAEP decrypt failed (padding/key mismatch?): {e.Message}");
                return null;
            }
        }
    }
}
