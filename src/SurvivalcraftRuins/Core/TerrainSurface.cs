using System.Collections.Concurrent;
using Engine;
using Game;

namespace SurvivalcraftRuins.Core;

/// <summary>
/// The exact height of the land the game's first terrain pass lays down, for
/// any column, without generating a chunk.
///
/// <c>CalculateHeight</c> alone is off by up to a couple of blocks on plains
/// and by far more in mountains: <c>TerrainContentsGenerator24.GenerateTerrain</c>
/// turns it into a density field with 3D turbulence, samples that every 4
/// blocks across and 8 up, and interpolates. A village needs every house floor
/// to meet the street at the real ground, and the house's height has to be the
/// same from whichever chunk it is written, so this repeats that computation
/// for the column asked about, in the same order and with the same float
/// steps. Both runtimes (plugin 1.9.2 and Net 2.4) share the arithmetic; only
/// <c>MathUtils.Abs</c> vs <c>MathF.Abs</c> differ in the decompiled sources.
/// Later passes (caves, surface layers) can still nibble the odd column.
/// </summary>
public sealed class TerrainSurface24
{
    private const int GridX = 4;
    private const int GridY = 8;
    private const int Levels = 33;
    // ~140 bytes per node column; a village needs about 1,600 of them.
    private const int CacheLimit = 16_384;

    private readonly TerrainContentsGenerator24 m_generator;
    private readonly ConcurrentDictionary<long, float[]> m_nodes = new();

    public TerrainSurface24(TerrainContentsGenerator24 generator)
    {
        m_generator = generator;
    }

    /// <summary>Topmost solid cell of the column (water excluded), or 0 when there is none.</summary>
    public int GroundTop(int x, int z)
    {
        int gx = PlacementEngine.FloorDiv(x, GridX) * GridX;
        int gz = PlacementEngine.FloorDiv(z, GridX) * GridX;
        int dx = x - gx;
        int dz = z - gz;
        // Corners as GenerateTerrain's Get8 names them: v2..v9.
        float[] a = Node(gx, gz);
        float[] b = Node(gx + GridX, gz);
        float[] c = Node(gx, gz + GridX);
        float[] d = Node(gx + GridX, gz + GridX);
        Span<float> column = stackalloc float[GridY];
        for (int m = Levels - 2; m >= 0; m--)
        {
            float num15 = (b[m] - a[m]) / 4f;
            float num16 = (b[m + 1] - a[m + 1]) / 4f;
            float num17 = (d[m] - c[m]) / 4f;
            float num18 = (d[m + 1] - c[m + 1]) / 4f;
            float num19 = a[m];
            float num20 = a[m + 1];
            float num21 = c[m];
            float num22 = c[m + 1];
            for (int i = 0; i < dx; i++)
            {
                num19 += num15;
                num20 += num16;
                num21 += num17;
                num22 += num18;
            }
            float num24 = (num21 - num19) / 4f;
            float num25 = (num22 - num20) / 4f;
            float num26 = num19;
            float num27 = num20;
            for (int i = 0; i < dz; i++)
            {
                num26 += num24;
                num27 += num25;
            }
            float num29 = (num27 - num26) / 8f;
            float num30 = num26;
            for (int t = 0; t < GridY; t++)
            {
                column[t] = num30;
                num30 += num29;
            }
            for (int t = GridY - 1; t >= 0; t--)
            {
                if (column[t] >= 0f)
                {
                    return m * GridY + t;
                }
            }
        }
        return 0;
    }

    /// <summary>Density at every vertical grid level of one grid column, as GenerateTerrain fills grid3d.</summary>
    private float[] Node(int x, int z)
    {
        long key = ((long)x << 32) ^ (uint)z;
        if (m_nodes.TryGetValue(key, out var cached))
        {
            return cached;
        }
        if (m_nodes.Count > CacheLimit)
        {
            m_nodes.Clear();
        }
        var g = m_generator;
        float num7 = g.CalculateHeight(x, z);
        float v = g.CalculateMountainRangeFactor(x, z);
        float num8 = MathUtils.Lerp(g.TGMinTurbulence, 1f, TerrainContentsGenerator24.Squish(v, g.TGTurbulenceZero, 1f));
        var levels = new float[Levels];
        for (int m = 0; m < Levels; m++)
        {
            int num9 = m * GridY;
            float num10 = g.TGTurbulenceStrength * num8 * MathUtils.Saturate(num7 - (float)num9)
                * (2f * SimplexNoise.OctavedNoise(x, num9, z, g.TGTurbulenceFreq, g.TGTurbulenceOctaves, 4f, g.TGTurbulencePersistence) - 1f);
            float num11 = (float)num9 + num10;
            float num12 = num7 - num11;
            num12 += MathUtils.Max(4f * (g.TGDensityBias - (float)num9), 0f);
            levels[m] = num12;
        }
        m_nodes[key] = levels;
        return levels;
    }
}
