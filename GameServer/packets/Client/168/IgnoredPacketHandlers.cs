using System.Reflection;
using DOL.Logging;

namespace DOL.GS.PacketHandler.Client.v168
{
    // Packets the client sends that need no action from the server. They are handled so that they don't
    // fall through as unknown packet codes; each one is logged at debug level only.

    // Sent when the market explorer window closes. The 4 bytes are not meaningful.
    [PacketHandlerAttribute(PacketHandlerType.TCP, eClientPackets.MarketExplorerClosed, "Market explorer window closed", eClientStatus.PlayerInGame)]
    public class MarketExplorerClosedHandler : PacketHandler
    {
        private static readonly Logger Log = LoggerManager.Create(MethodBase.GetCurrentMethod().DeclaringType);

        protected override void HandlePacketInternal(GameClient client, GSPacketIn packet)
        {
            if (Log.IsDebugEnabled)
                Log.Debug($"Market explorer closed (player={client.Player?.Name})");
        }
    }

    // Sent when the map window switches to the player's new zone, followed by WarmapRefresh. One zero byte.
    [PacketHandlerAttribute(PacketHandlerType.TCP, eClientPackets.MapZoneChanged, "Map window zone changed", eClientStatus.PlayerInGame)]
    public class MapZoneChangedHandler : PacketHandler
    {
        private static readonly Logger Log = LoggerManager.Create(MethodBase.GetCurrentMethod().DeclaringType);

        protected override void HandlePacketInternal(GameClient client, GSPacketIn packet)
        {
            if (Log.IsDebugEnabled)
                Log.Debug($"Map window zone changed (player={client.Player?.Name})");
        }
    }

    // Sent after MapZoneChanged and after each warmap button press. One zero byte. The expected reply, if any, is unknown.
    [PacketHandlerAttribute(PacketHandlerType.TCP, eClientPackets.WarmapRefresh, "Warmap refresh", eClientStatus.PlayerInGame)]
    public class WarmapRefreshHandler : PacketHandler
    {
        private static readonly Logger Log = LoggerManager.Create(MethodBase.GetCurrentMethod().DeclaringType);

        protected override void HandlePacketInternal(GameClient client, GSPacketIn packet)
        {
            if (Log.IsDebugEnabled)
                Log.Debug($"Warmap refresh (player={client.Player?.Name})");
        }
    }

    // Sent when the client leaves the world or the session ends. The byte is not meaningful.
    [PacketHandlerAttribute(PacketHandlerType.TCP, eClientPackets.LeaveWorld, "Client leaving the world", eClientStatus.LoggedIn)]
    public class LeaveWorldHandler : PacketHandler
    {
        private static readonly Logger Log = LoggerManager.Create(MethodBase.GetCurrentMethod().DeclaringType);

        protected override void HandlePacketInternal(GameClient client, GSPacketIn packet)
        {
            if (Log.IsDebugEnabled)
                Log.Debug($"Client leaving the world (account={client.Account?.Name})");
        }
    }

    // Sent about a second before the client closes the connection when it quits. One byte, 0 or 1.
    [PacketHandlerAttribute(PacketHandlerType.TCP, eClientPackets.QuitNotice, "Client quitting", eClientStatus.LoggedIn)]
    public class QuitNoticeHandler : PacketHandler
    {
        private static readonly Logger Log = LoggerManager.Create(MethodBase.GetCurrentMethod().DeclaringType);

        protected override void HandlePacketInternal(GameClient client, GSPacketIn packet)
        {
            if (Log.IsDebugEnabled)
                Log.Debug($"Client quitting (account={client.Account?.Name}, flag={packet.ReadByte()})");
        }
    }
}
