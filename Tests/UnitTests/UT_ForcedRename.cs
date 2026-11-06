using DOL.GS.PacketHandler.Client.v168;
using NUnit.Framework;

namespace DOL.GS.Tests
{
    [TestFixture]
    public class UT_ForcedRename
    {
        [TestCase("Plooker", "Plooker")]
        [TestCase("plooker", "Plooker")]
        [TestCase("Abc", "Abc")]
        [TestCase("Abcdefghijklmnopqrst", "Abcdefghijklmnopqrst")]
        [TestCase("McDonald", "McDonald")]
        public void NormalizeName_ValidName_ReturnsCapitalized(string name, string expected)
        {
            Assert.That(ForcedRename.NormalizeName(name), Is.EqualTo(expected));
        }

        [TestCase("Ab")]
        [TestCase("Abcdefghijklmnopqrstu")]
        [TestCase("Ploo ker")]
        [TestCase("Plooker1")]
        [TestCase("Plooker*")]
        [TestCase("Plo-oker")]
        [TestCase("")]
        [TestCase(null)]
        public void NormalizeName_InvalidName_ReturnsNull(string name)
        {
            Assert.That(ForcedRename.NormalizeName(name), Is.Null);
        }

        [TestCase(0, 100)]
        [TestCase(9, 109)]
        [TestCase(10, 200)]
        [TestCase(15, 205)]
        [TestCase(29, 309)]
        public void AccountSlot_MapsClientSlot(int clientSlot, int expected)
        {
            Assert.That(ForcedRename.AccountSlot(clientSlot), Is.EqualTo(expected));
        }

        [Test]
        public void OverviewName_RenameRequired_IsNotAllLetters()
        {
            string name = ForcedRename.OverviewName("Plooker", true);

            Assert.That(name, Does.StartWith("Plooker"));
            Assert.That(name, Does.Not.Match("^[A-Za-z]+$"));
            Assert.That(ForcedRename.OverviewName("Plooker", false), Is.EqualTo("Plooker"));
        }
    }
}
