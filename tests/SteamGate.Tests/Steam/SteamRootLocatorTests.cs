using SteamGate.Service;
using Xunit;

namespace SteamGate.Tests.Steam;

public sealed class SteamRootLocatorTests
{
    [Fact]
    public void Locate_GivesAFullyQualifiedPathWithoutATrailingSeparator()
    {
        var root = SteamRootLocator.Locate();

        Assert.True(Path.IsPathFullyQualified(root));
        Assert.Equal(Path.TrimEndingDirectorySeparator(root), root);
    }
}
