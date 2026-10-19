using SurvivalcraftRuins.Core;
using Xunit;

namespace SurvivalcraftRuins.Tests;

public class ContainerStockingTests
{
    [Fact]
    public void FirstVisitFillsTheChest()
    {
        Assert.Equal(StockAction.Fill, ContainerStocking.Decide(containerBlockPresent: true, blockEntityExists: false));
    }

    /// <summary>
    /// The reported bug: an untouched ruin chunk is regenerated on every load,
    /// so the same chest comes back through the queue each time. Its entity,
    /// with whatever the player left in it, is still there.
    /// </summary>
    [Fact]
    public void RegeneratedChestKeepsWhatThePlayerLeft()
    {
        Assert.Equal(StockAction.AlreadyStocked, ContainerStocking.Decide(containerBlockPresent: true, blockEntityExists: true));
    }

    [Fact]
    public void BrokenChestIsNotRecreated()
    {
        Assert.Equal(StockAction.Gone, ContainerStocking.Decide(containerBlockPresent: false, blockEntityExists: false));
    }
}

public class DensityTierTests
{
    [Fact]
    public void TiersRunFromOffToMaximum()
    {
        var tiers = RuinsSettings.DensityTiers;
        Assert.Equal(0f, tiers[0]);
        Assert.Equal(1f, tiers[RuinsSettings.DefaultDensityTier]);
        Assert.Equal(RuinsSettings.MaxScale, tiers[^1]);
        for (int i = 1; i < tiers.Length; i++)
        {
            Assert.True(tiers[i] > tiers[i - 1], $"tier {i} must be higher than tier {i - 1}");
        }
    }

    [Theory]
    [InlineData(0f, 0)]
    [InlineData(1f, 3)]
    [InlineData(4f, 7)]
    [InlineData(0.75f, 2)]   // old quarter-step saves snap to a neighbour
    [InlineData(2.5f, 5)]
    [InlineData(9f, 7)]
    public void SavedValuesLandOnTheNearestTier(float saved, int expected)
    {
        Assert.Equal(expected, RuinsSettings.NearestDensityTier(saved));
    }
}
