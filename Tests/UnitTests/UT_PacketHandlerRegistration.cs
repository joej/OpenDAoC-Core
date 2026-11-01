using System;
using System.Collections.Generic;
using System.Linq;
using DOL.GS.PacketHandler;
using NUnit.Framework;

namespace DOL.GS.Tests
{
    [TestFixture]
    public class UT_PacketHandlerRegistration
    {
        private static Dictionary<int, List<Type>> HandlersByCode()
        {
            return typeof(GameServer).Assembly.GetTypes()
                .Where(t => t.IsClass && typeof(IPacketHandler).IsAssignableFrom(t) && t.Namespace != null && t.Namespace.EndsWith("v168", StringComparison.OrdinalIgnoreCase))
                .Select(t => (Type: t, Attribute: t.GetCustomAttributes(typeof(PacketHandlerAttribute), true).OfType<PacketHandlerAttribute>().FirstOrDefault()))
                .Where(x => x.Attribute != null)
                .GroupBy(x => x.Attribute.Code)
                .ToDictionary(g => g.Key, g => g.Select(x => x.Type).ToList());
        }

        [TestCase(eClientPackets.MarketExplorerClosed)]
        [TestCase(eClientPackets.MapZoneChanged)]
        [TestCase(eClientPackets.WarmapRefresh)]
        [TestCase(eClientPackets.LeaveWorld)]
        [TestCase(eClientPackets.QuitNotice)]
        public void IgnoredPacket_HasHandler(eClientPackets code)
        {
            Assert.That(HandlersByCode().ContainsKey((int) code), Is.True, $"no handler for 0x{(int) code:X2}");
        }

        [Test]
        public void EachPacketCode_HasAtMostOneHandler()
        {
            var duplicates = HandlersByCode().Where(kv => kv.Value.Count > 1)
                .Select(kv => $"0x{kv.Key:X2}: {string.Join(", ", kv.Value.Select(t => t.Name))}").ToList();
            Assert.That(duplicates, Is.Empty);
        }
    }
}
