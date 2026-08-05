using System.Reflection;

namespace SurvivalcraftRuins.Core;

/// <summary>
/// The same assembly runs on two runtimes: SCAPI (single-player plugin, no
/// netcode types) and SurvivalcraftNet 2.4 (co-op, Game.NetWork.CommonLib).
/// CommonLib is probed via reflection so neither runtime needs a hard
/// reference; on SCAPI the process is never a network client.
/// </summary>
public static class RuntimeCompat
{
    private static Func<bool>? s_isClientProbe;

    public static bool IsClient
    {
        get
        {
            s_isClientProbe ??= BuildProbe();
            return s_isClientProbe();
        }
    }

    private static Func<bool> BuildProbe()
    {
        try
        {
            var type = Type.GetType("Game.NetWork.CommonLib, Survivalcraft");
            var property = type?.GetProperty("WorkType", BindingFlags.Public | BindingFlags.Static);
            if (property != null)
            {
                return () =>
                {
                    try
                    {
                        return property.GetValue(null)?.ToString() == "Client";
                    }
                    catch
                    {
                        return false;
                    }
                };
            }
        }
        catch
        {
            // Fall through: runtime without netcode.
        }
        return static () => false;
    }
}
