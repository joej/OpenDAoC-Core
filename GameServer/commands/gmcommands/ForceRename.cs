using DOL.Database;
using DOL.GS.PacketHandler;
using DOL.GS.PacketHandler.Client.v168;

namespace DOL.GS.Commands
{
    [CmdAttribute(
        "&forcerename",
        ePrivLevel.GM,
        "Make a player choose a new name for a character at character select",
        "/forcerename <character name> - ask for a new name at the next character select",
        "/forcerename cancel <character name> - no longer ask")]
    public class ForceRenameCommandHandler : AbstractCommandHandler, ICommandHandler
    {
        public void OnCommand(GameClient client, string[] args)
        {
            bool cancel = args.Length == 3 && args[1].ToLowerInvariant() == "cancel";

            if (args.Length != 2 && !cancel)
            {
                DisplaySyntax(client);
                return;
            }

            string name = args[^1];
            DbCoreCharacter character = DOLDB<DbCoreCharacter>.SelectObject(DB.Column("Name").IsEqualTo(name));

            if (character == null)
            {
                DisplayMessage(client, $"No character is called {name}.");
                return;
            }

            if (cancel)
            {
                ForcedRename.Clear(character);
                DisplayMessage(client, $"{character.Name} will no longer be asked for a new name.");
                return;
            }

            ForcedRename.Require(character);
            AuditMgr.AddAuditEntry(client, AuditType.Character, AuditSubtype.CharacterRename, character.Name, "(rename required)");
            DisplayMessage(client, $"{character.Name} will have to choose a new name at the next character select.");
        }
    }
}
