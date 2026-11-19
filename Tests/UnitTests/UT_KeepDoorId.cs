using DOL.GS.Keeps;
using DOL.Logging;
using NUnit.Framework;

namespace DOL.GS.Tests
{
    [TestFixture]
    public class UT_KeepDoorId
    {
        [OneTimeSetUp]
        public void SetUp()
        {
            LoggerManager.InitializeWithExplicitLibrary(null, LogLibrary.None);
        }

        // The client builds a keep door's ID as 700000000 + (KeepID & 0xFF) * 100000 + (KeepID >> 8) * 10000 + component * 100 + door.
        [TestCase(106, 2, 1, 710600201)]   // keep
        [TestCase(362, 1, 2, 710610102)]   // tower of keep 106 (0x16A)
        [TestCase(381, 3, 12, 712510312)]  // battleground portal keep (0x17D), two-digit door number
        [TestCase(255, 0, 0, 725500000)]
        [TestCase(2559, 99, 99, 725599999)] // largest ID that fits (0x9FF)
        public void GetKeepDoorId_MatchesClientFormat(int keepId, int componentId, int doorIndex, int expected)
        {
            Assert.That(GameKeepDoor.GetKeepDoorId(keepId, componentId, doorIndex), Is.EqualTo(expected));
        }
    }
}
