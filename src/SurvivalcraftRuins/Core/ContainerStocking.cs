namespace SurvivalcraftRuins.Core;

public enum StockAction
{
    /// <summary>First visit: create the block entity and roll the loot.</summary>
    Fill,

    /// <summary>Stocked on an earlier visit; leave whatever the player left in it.</summary>
    AlreadyStocked,

    /// <summary>The container block is no longer there.</summary>
    Gone,
}

/// <summary>
/// Decides what to do with a container that worldgen has just written.
///
/// The game only saves a chunk to disk once something in it has changed
/// (<c>TerrainSerializer.SaveChunk</c> requires <c>ModificationCounter &gt; 0</c>,
/// in both the plugin and the Net 2.4 runtime). A ruin the player has looted
/// but not dug into is therefore regenerated from the seed on every visit, and
/// the generation hook queues its chests all over again. Rolling loot each time
/// added a fresh set to every chest on every reload.
///
/// A ruin chest's block entity only ever comes from our first fill: neither
/// runtime creates one for a generated block (only for a placed one), and
/// breaking the chest removes it. So an entity already at the spot means the
/// chest was stocked before, and its contents now belong to the player.
/// </summary>
public static class ContainerStocking
{
    public static StockAction Decide(bool containerBlockPresent, bool blockEntityExists)
    {
        if (!containerBlockPresent)
        {
            return StockAction.Gone;
        }
        return blockEntityExists ? StockAction.AlreadyStocked : StockAction.Fill;
    }
}
