using System.Runtime.CompilerServices;
using Engine;
using Game;
using SurvivalcraftRuins.Core;
using Xunit;

namespace SurvivalcraftRuins.Tests;

/// <summary>
/// The game's own terrain generator, stood up without a game project so tests
/// can run real world generation. Mirrors the TerrainContentsGenerator24
/// constructor for a default continent world: same seed-derived offsets (the
/// game's Random, drawn in the same order) and the same tuning constants.
/// </summary>
internal static class GameGenerator
{
    public static TerrainContentsGenerator24 Create(int seed)
    {
        var g = (TerrainContentsGenerator24)RuntimeHelpers.GetUninitializedObject(typeof(TerrainContentsGenerator24));
        var settings = new WorldSettings();
        var terrain = (SubsystemTerrain)RuntimeHelpers.GetUninitializedObject(typeof(SubsystemTerrain));
        terrain.Terrain = new Terrain();
        var gameInfo = (SubsystemGameInfo)RuntimeHelpers.GetUninitializedObject(typeof(SubsystemGameInfo));
        gameInfo.WorldSettings = settings;
        terrain.SubsystemGameInfo = gameInfo;
        g.m_subsystemTerrain = terrain;
        g.m_worldSettings = settings;
        g.m_seed = seed;
        g.m_islandSize = null;
        var random = new Game.Random(seed);
        g.m_oceanCorner = new Vector2(-200f, -200f);
        g.m_temperatureOffset = new Vector2(random.Float(-3000f, 3000f), random.Float(-3000f, 3000f));
        g.m_humidityOffset = new Vector2(random.Float(-3000f, 3000f), random.Float(-3000f, 3000f));
        g.m_mountainsOffset = new Vector2(random.Float(-3000f, 3000f), random.Float(-3000f, 3000f));
        g.m_riversOffset = new Vector2(random.Float(-3000f, 3000f), random.Float(-3000f, 3000f));
        g.TGBiomeScaling = 1.75f * settings.BiomeSize;
        g.TGShoreFluctuations = MathUtils.Clamp(2f * float.MaxValue, 0f, 150f);
        g.TGShoreFluctuationsScaling = MathUtils.Clamp(0.04f * float.MaxValue, 0.5f, 3f);
        g.TGOceanSlope = 0.006f;
        g.TGOceanSlopeVariation = 0.004f;
        g.TGIslandsFrequency = 0.01f;
        g.TGDensityBias = 55f;
        g.TGHeightBias = 1f;
        g.TGRiversStrength = 1f;
        g.TGMountainsStrength = 250f;
        g.TGMountainRangeFreq = 0.0006f;
        g.TGMountainsPercentage = 0.13f;
        TerrainContentsGenerator24.TGMountainsDetailFreq = 0.003f;
        TerrainContentsGenerator24.TGMountainsDetailOctaves = 4;
        TerrainContentsGenerator24.TGMountainsDetailPersistence = 0.53f;
        g.TGHillsPercentage = 0.32f;
        g.TGHillsStrength = 32f;
        g.TGHillsOctaves = 1;
        g.TGHillsFrequency = 0.014f;
        g.TGHillsPersistence = 0.5f;
        g.TGTurbulenceStrength = 55f;
        g.TGTurbulenceFreq = 0.03f;
        g.TGTurbulenceOctaves = 1;
        g.TGTurbulencePersistence = 0.5f;
        g.TGMinTurbulence = 0.04f;
        g.TGTurbulenceZero = 0.84f;
        TerrainContentsGenerator24.TGSurfaceMultiplier = 2f;
        g.TGWater = true;
        g.TGExtras = true;
        g.TGCavesAndPockets = true;
        return g;
    }

    /// <summary>
    /// A bare chunk: cells and shafts only. The real constructor also builds
    /// render geometry, which drags in the graphics stack.
    /// </summary>
    public static TerrainChunk NewChunk(int chunkX, int chunkZ)
    {
        var chunk = (TerrainChunk)RuntimeHelpers.GetUninitializedObject(typeof(TerrainChunk));
        chunk.Coords = new Point2(chunkX, chunkZ);
        chunk.Origin = new Point2(chunkX * 16, chunkZ * 16);
        chunk.Cells = new int[65536];
        chunk.Shafts = new int[256];
        return chunk;
    }

    /// <summary>Runs the game's terrain fill for one chunk exactly as passes 1 and 2 do.</summary>
    public static TerrainChunk GenerateTerrain(TerrainContentsGenerator24 g, int chunkX, int chunkZ)
    {
        var chunk = NewChunk(chunkX, chunkZ);
        g.GenerateSurfaceParameters(chunk, 0, 0, 16, 8);
        g.GenerateTerrain(chunk, 0, 0, 16, 8);
        g.GenerateSurfaceParameters(chunk, 0, 8, 16, 16);
        g.GenerateTerrain(chunk, 0, 8, 16, 16);
        return chunk;
    }
}
