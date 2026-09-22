using System.Linq;
using NUnit.Framework;

namespace WakeUp.Tests;

[TestFixture]
public sealed class PreparationTerrainPolicyTests
{
    [Test]
    public void SeparatePollutionDoesNotRefuseOrdinaryBase()
    {
        var result = PreparationTerrainPolicy.Evaluate("Terrain/Straw", "Terrain/StrawPolluted", "", false, false, false, 1, true);
        Assert.That(result.Single(r => r.Path == "Terrain/Straw").Allowed, Is.True);
        Assert.That(result.Single(r => r.Path == "Terrain/StrawPolluted").Allowed, Is.False);
    }
    [Test]
    public void SharedBaseRefusalMatchesNativeNullFallbackButNotEmptyPath()
    {
        var fallback = PreparationTerrainPolicy.Evaluate("Terrain/Base", null, "Terrain/Overlay", false, false, false, 1, true);
        var empty = PreparationTerrainPolicy.Evaluate("Terrain/Base", "", "Terrain/Overlay", false, false, false, 1, true);
        Assert.That(fallback.Single(r => r.Path == "Terrain/Base").Allowed, Is.False);
        Assert.That(empty.Single(r => r.Path == "Terrain/Base").Allowed, Is.True);
    }
    [Test]
    public void InactivePollutionCannotRefuseBaseButUnknownBaseShaderDoes()
    {
        var inactive = PreparationTerrainPolicy.Evaluate("Terrain/Base", null, "Terrain/Overlay", false, false, false, 1, false);
        Assert.That(inactive.Single().Allowed, Is.True);
        var custom = PreparationTerrainPolicy.Evaluate("Terrain/Base", null, "Terrain/Overlay", false, true, false, 1, false);
        Assert.That(custom.All(r => !r.Allowed), Is.True);
    }
}
