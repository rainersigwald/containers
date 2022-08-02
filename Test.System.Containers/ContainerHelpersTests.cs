using System.Containers;

namespace Test.System.Containers
{
    [TestClass]
    public class ContainerHelpersTests
    {

        [TestMethod]
        [DataRow("https://mcr.microsoft.com/dotnet/runtime:6.0", true, "https://mcr.microsoft.com", "dotnet/runtime", "6.0")]
        [DataRow("https://mcr.microsoft.com/dotnet/runtime", true, "https://mcr.microsoft.com", "dotnet/runtime", "")]
        [DataRow("https://mcr.microsoft.com/", false, "", "", "")]
        public void TryParseFullyQualifiedContainerName(string fullyQualifiedName, bool expectedReturn, string expectedRegistry, string expectedImage, string expectedTag)
        {
            Assert.AreEqual(ContainerHelpers.TryParseFullyQualifiedContainerName(fullyQualifiedName, out string containerReg, out string containerName, out string containerTag), expectedReturn);
            Assert.AreEqual(containerReg, expectedRegistry);
            Assert.AreEqual(containerName, expectedImage);
            Assert.AreEqual(containerTag, expectedTag);
        }
    }
}