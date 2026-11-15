using System.Reflection;
using Engine;
using Game;

namespace SurvivalcraftRuins.Core;

/// <summary>Answers "was this chunk already generated and saved?" without loading it.</summary>
public interface IChunkProbe
{
    bool Exists(int chunkX, int chunkZ);
}

/// <summary>
/// Asks the game's own chunk file whether a chunk is already on disk. Reached by
/// reflection because the two runtimes this assembly serves disagree about the
/// serializer: SCAPI keeps a RegionFileStorage on TerrainSerializer23, while
/// SurvivalcraftNet's client-side serializer has no storage at all. A probe that
/// cannot answer is simply absent, and the caller falls back to trusting the
/// placement table.
/// </summary>
public sealed class SerializerChunkProbe : IChunkProbe
{
    private readonly object m_storage;
    private readonly MethodInfo m_exists;
    private readonly Terrain m_terrain;
    private bool m_faulted;

    private SerializerChunkProbe(object storage, MethodInfo exists, Terrain terrain)
    {
        m_storage = storage;
        m_exists = exists;
        m_terrain = terrain;
    }

    public static IChunkProbe? TryCreate(SubsystemTerrain terrain)
    {
        try
        {
            object? serializer = terrain
                .GetType()
                .GetProperty("TerrainSerializer", BindingFlags.Public | BindingFlags.Instance)
                ?.GetValue(terrain);
            if (serializer == null)
            {
                return null;
            }
            object? storage = serializer
                .GetType()
                .GetField("m_storage", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                ?.GetValue(serializer);
            if (storage == null)
            {
                return null;
            }
            var exists = storage.GetType().GetMethod(
                "Exists", BindingFlags.Public | BindingFlags.Instance, null, new[] { typeof(Point2) }, null);
            return exists == null ? null : new SerializerChunkProbe(storage, exists, terrain.Terrain);
        }
        catch (Exception exception)
        {
            Log.Warning($"[Ruins] Chunk probe unavailable: {exception.Message}");
            return null;
        }
    }

    public bool Exists(int chunkX, int chunkZ)
    {
        if (m_faulted)
        {
            return false;
        }
        try
        {
            // The game only writes a chunk to disk once something in it has
            // changed; until then it lives in memory and is regenerated next
            // time. One generated this session is land all the same.
            var loaded = m_terrain.GetChunkAtCoords(chunkX, chunkZ);
            if (loaded != null && loaded.ThreadState > TerrainChunkState.InvalidContents4)
            {
                return true;
            }
            return m_exists.Invoke(m_storage, new object[] { new Point2(chunkX, chunkZ) }) is true;
        }
        catch (Exception exception)
        {
            m_faulted = true;
            Log.Warning($"[Ruins] Chunk probe failed, falling back to the placement table: {exception.Message}");
            return false;
        }
    }
}
