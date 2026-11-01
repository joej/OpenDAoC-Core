using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using DOL.Database;
using DOL.Logging;

namespace DOL.GS.PacketHandler.Client.v168
{
    /// <summary>
    /// Forced renames at character select. A GM flags a character (a character custom param); the character overview
    /// then sends its name with a marker that isn't a letter, which makes the client open its rename window when the
    /// player tries to play that character. The client sends the new name with CharacterRenameRequest.
    /// </summary>
    public static class ForcedRename
    {
        public const string KeyName = "RenameRequired";
        // Appended to a flagged character's name in the overview. Any character that isn't a letter makes the client open
        // its rename window when the player presses Play. '*' isn't valid in a Windows file name, which matters for the
        // order of the replies in CharacterRenameRequestHandler.
        public const char NameMarker = '*';
        public const int MinNameLength = 3;
        public const int MaxNameLength = 20;

        private static readonly Regex _nameFormat = new($"^[A-Z][a-zA-Z]{{{MinNameLength - 1},{MaxNameLength - 1}}}$");

        public static string OverviewName(string name, bool renameRequired)
        {
            return renameRequired ? name + NameMarker : name;
        }

        /// <summary>
        /// Returns the name with its first letter capitalized, or null if it isn't 3-20 letters A-Z / a-z.
        /// Doesn't check that the name is free or allowed.
        /// </summary>
        public static string NormalizeName(string name)
        {
            if (string.IsNullOrEmpty(name))
                return null;

            name = char.ToUpperInvariant(name[0]) + name[1..];
            return _nameFormat.IsMatch(name) ? name : null;
        }

        /// <summary>
        /// Maps the slot the client sends (0-9 Albion, 10-19 Midgard, 20-29 Hibernia) to an account slot.
        /// </summary>
        public static int AccountSlot(int clientSlot)
        {
            return (clientSlot / 10 + 1) * 100 + clientSlot % 10;
        }

        public static bool IsRequired(DbCoreCharacter character)
        {
            return Select(character.ObjectId) != null;
        }

        public static async Task<HashSet<string>> SelectRequiredAsync(IEnumerable<DbCoreCharacter> characters)
        {
            List<string> ids = characters.Select(c => c.ObjectId).ToList();

            if (ids.Count == 0)
                return [];

            var flags = await DOLDB<DbCoreCharacterXCustomParam>.SelectObjectsAsync(DB.Column("DOLCharactersObjectId").IsIn(ids).And(DB.Column("KeyName").IsEqualTo(KeyName)));
            return flags.Select(f => f.DOLCharactersObjectId).ToHashSet();
        }

        public static void Require(DbCoreCharacter character)
        {
            if (Select(character.ObjectId) == null)
                GameServer.Database.AddObject(new DbCoreCharacterXCustomParam(character.ObjectId, KeyName, "1"));
        }

        public static void Clear(DbCoreCharacter character)
        {
            DbCoreCharacterXCustomParam param = Select(character.ObjectId);

            if (param != null)
                GameServer.Database.DeleteObject(param);
        }

        private static DbCoreCharacterXCustomParam Select(string characterId)
        {
            return DOLDB<DbCoreCharacterXCustomParam>.SelectObject(DB.Column("DOLCharactersObjectId").IsEqualTo(characterId).And(DB.Column("KeyName").IsEqualTo(KeyName)));
        }
    }

    // Sent from the rename window at character select: the slot (big-endian), then the new name in 24 bytes.
    [PacketHandlerAttribute(PacketHandlerType.TCP, eClientPackets.CharacterRenameRequest, "Character rename at character select", eClientStatus.LoggedIn)]
    public class CharacterRenameRequestHandler : PacketHandler
    {
        private static readonly Logger Log = LoggerManager.Create(MethodBase.GetCurrentMethod().DeclaringType);

        protected override async void HandlePacketInternal(GameClient client, GSPacketIn packet)
        {
            int clientSlot = packet.ReadShort();
            string requestedName = packet.ReadString(24);

            if (client.ClientState is not GameClient.eClientState.CharScreen)
                return;

            int accountSlot = ForcedRename.AccountSlot(clientSlot);
            DbCoreCharacter character = client.Account.Characters?.FirstOrDefault(c => c.AccountSlot == accountSlot);

            if (character == null || !ForcedRename.IsRequired(character))
            {
                Refuse(client, accountSlot, requestedName, $"no character in slot {clientSlot} needs a new name");
                return;
            }

            string newName = ForcedRename.NormalizeName(requestedName);
            string refusal = null;

            if (newName == null)
                refusal = "not 3-20 letters";
            else if (GameServer.Instance.PlayerManager.InvalidNames[newName])
                refusal = "in the invalid names list";
            else if (DOLDB<DbCoreCharacter>.SelectObject(DB.Column("Name").IsEqualTo(newName)) != null)
                refusal = "already taken";

            if (refusal != null)
            {
                Refuse(client, accountSlot, requestedName, refusal);
                return;
            }

            string oldName = character.Name;
            character.Name = newName;
            GameServer.Database.SaveObject(character);
            ForcedRename.Clear(character);
            AuditMgr.AddAuditEntry(client, AuditType.Character, AuditSubtype.CharacterRename, oldName, newName);

            if (Log.IsInfoEnabled)
                Log.Info($"Character renamed at character select: {oldName} -> {newName} (account={client.Account.Name})");

            // The updated overview MUST be sent before the reply. The client enters the world as soon as the reply arrives,
            // and keeps the name from the last overview for the session, including for its per-character files. With the
            // marker still in the name ('*' isn't valid in a Windows file name), the client crashes on its next world exit.
            eRealm realm = (eRealm) (accountSlot / 100);

            if (client.Out is PacketLib1126 packetLib)
                await packetLib.SendCharacterOverviewAsync(realm);
            else
                client.Out.SendCharacterOverview(realm);

            client.Out.SendCharacterRenameReply(true);
        }

        // The client has already put the refused name in its character list, so send the list again to restore the marker.
        private static void Refuse(GameClient client, int accountSlot, string requestedName, string reason)
        {
            if (Log.IsInfoEnabled)
                Log.Info($"Rename to \"{requestedName}\" refused: {reason} (account={client.Account.Name})");

            client.Out.SendCharacterRenameReply(false);

            if (accountSlot / 100 is >= (int) eRealm._FirstPlayerRealm and <= (int) eRealm._LastPlayerRealm)
                client.Out.SendCharacterOverview((eRealm) (accountSlot / 100));
        }
    }
}
