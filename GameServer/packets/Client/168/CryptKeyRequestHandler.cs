using System.Reflection;
using DOL.GS.ServerProperties;
using DOL.Logging;

namespace DOL.GS.PacketHandler.Client.v168
{
	[PacketHandlerAttribute(PacketHandlerType.TCP, eClientPackets.CryptKeyRequest, "Handles crypt key requests", eClientStatus.None)]
	public class CryptKeyRequestHandler : PacketHandler
	{
		private static readonly Logger log = LoggerManager.Create(MethodBase.GetCurrentMethod().DeclaringType);

		protected override void HandlePacketInternal(GameClient client, GSPacketIn packet)
		{
			// for 1.115c+ The First client packet Changes.
			if (client.Version < GameClient.eClientVersion.Version1115)
			{
				int rc4 = packet.ReadByte();
				byte clientType = (byte)packet.ReadByte();
				client.ClientType = (GameClient.eClientType)(clientType & 0x0F);
				client.ClientAddons = (GameClient.eClientAddons)(clientType & 0xF0);
				client.MajorBuild = (byte)packet.ReadByte();
				client.MinorBuild = (byte)packet.ReadByte();
				client.MinorRev = packet.ReadString(1);
				if (rc4 == 1)
				{
					packet.Read(client.PacketProcessor.Encoding.SBox, 0, 256);
					client.PacketProcessor.Encoding.EncryptionState = eEncryptionState.PseudoRC4Encrypted;
				}
				else
				{
					//Send the crypt key to the client
					client.Out.SendVersionAndCryptKey();
				}
			}
			else
			{
				// if the DataSize is above 7 then a key is bundled with this request.
				if (packet.DataSize > 7)
				{
					// 1.130+ clients bundle an RSA-encrypted key-transport message here (the client
					// generated a random symmetric key and encrypted it under the server's RSA public
					// key). Only the size is logged: the payload carries the session key.
					if (Rsa1130.Enabled)
					{
						if (log.IsInfoEnabled)
							log.Info($"[1130-crypto] key-transport request from {client.TcpEndpointAddress}, " +
							         $"client version {client.Version}, payload {packet.DataSize} bytes");
						return;
					}

					if (Properties.CLIENT_ENABLE_ENCRYPTION_RC4)
					{
						var length = packet.ReadIntLowEndian();
						var key = new byte[length];
						packet.Read(key, 0, (int) length);
						client.PacketProcessor.Encoding.SBox = key;
						client.PacketProcessor.Encoding.EncryptionState = eEncryptionState.PseudoRC4Encrypted;
					}
					return;
				}

				// register client type
				byte clientType = (byte)packet.ReadByte();
				client.ClientType = (GameClient.eClientType)(clientType & 0x0F);
				client.ClientAddons = (GameClient.eClientAddons)(clientType & 0xF0);
				// the next 4 bytes are the game.dll version but not in string form
				// ie: 01 01 19 61 = 1.125a
				// this version is handled elsewhere before being sent here.
				packet.Skip(3); // skip the numbers in the version
				client.MinorRev = packet.ReadString(1); // get the minor revision letter // 1125d support
				packet.Skip(2); // build


				//Send the crypt key to the client
				client.Out.SendVersionAndCryptKey();

				// 1.130 clients encrypt from here: after we reply 0x22, the client's next packet is the
				// RSA key-transport. Arm RSA state so GameClient decrypts it (OpenDAoC's own RSAEncrypted
				// assignment lives in a disabled #if block).
				if (client.Uses1130Crypto)
					client.PacketProcessor.Encoding.EncryptionState = eEncryptionState.RSAEncrypted;
			}
		}
	}
}