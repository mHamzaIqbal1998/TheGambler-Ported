using System.Reflection;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Servers;
using SPTarkov.Server.Core.Utils;
using SPTarkov.Server.Core.Utils.Json;
using Path = System.IO.Path;

namespace Probe;

/// <summary>
/// Constructs the server's JsonUtil (with all registered converters) and tries
/// to deserialize the mod's data files, to catch binding problems without a
/// running server.
/// </summary>
public static class JsonTest
{
    public static void Run(string modRoot)
    {
        var core = typeof(DatabaseServer).Assembly;

        // Discover and instantiate all IJsonConverterRegistrator implementations
        var registratorInterface = typeof(IJsonConverterRegistrator);
        var registrators = new List<IJsonConverterRegistrator>();
        foreach (var type in core.GetTypes())
        {
            if (type.IsAbstract || type.IsInterface || !registratorInterface.IsAssignableFrom(type))
            {
                continue;
            }
            var ctor = type.GetConstructor(Type.EmptyTypes);
            if (ctor is null)
            {
                continue;
            }
            try { registrators.Add((IJsonConverterRegistrator)ctor.Invoke(null)); }
            catch { /* ignore ones needing deps */ }
        }

        Console.WriteLine($"Discovered {registrators.Count} json converter registrators");

        var jsonUtil = new JsonUtil(registrators);

        Test(jsonUtil, Path.Combine(modRoot, "db", "base.json"), typeof(TraderBase));
        Test(jsonUtil, Path.Combine(modRoot, "config", "config.json"), typeof(TheGambler.Config.GamblerConfig));
        Test(jsonUtil, Path.Combine(modRoot, "data", "container-info.json"), typeof(Dictionary<string, TheGambler.Data.ContainerInfo>));
        Test(jsonUtil, Path.Combine(modRoot, "data", "containers", "Weapons.json"), typeof(TheGambler.Data.ContainerRewardData));
        Test(jsonUtil, Path.Combine(modRoot, "data", "containers", "Ammo.json"), typeof(TheGambler.Data.AmmoData));
        Test(jsonUtil, Path.Combine(modRoot, "data", "containers", "Helmets.json"), typeof(TheGambler.Data.ContainerRewardData));
        Test(jsonUtil, Path.Combine(modRoot, "data", "containers", "PremiumWeapons.json"), typeof(TheGambler.Data.ContainerRewardData));
        Test(jsonUtil, Path.Combine(modRoot, "data", "containers", "PremiumArmors.json"), typeof(TheGambler.Data.ContainerRewardData));
    }

    private static void Test(JsonUtil jsonUtil, string file, Type type)
    {
        try
        {
            var json = File.ReadAllText(file);
            var result = jsonUtil.Deserialize(json, type);
            Console.WriteLine($"OK   {Path.GetFileName(file)} -> {type.Name}");

            switch (result)
            {
                case TraderBase tb:
                    Console.WriteLine($"       Id={tb.Id}, Name={tb.Name}, Currency={tb.Currency}, BalanceRub={tb.BalanceRub}, LoyaltyLevels={tb.LoyaltyLevels?.Count}");
                    break;
                case TheGambler.Config.GamblerConfig cfg:
                    Console.WriteLine($"       container_config={cfg.ContainerConfig.Count}, odds={cfg.Odds.Count}, overrideEnable={cfg.OverrideEnable}, ammoOverrides={cfg.OverridePrice.GetValueOrDefault("ammo")?.Count}");
                    break;
                case Dictionary<string, TheGambler.Data.ContainerInfo> info:
                    var w = info.GetValueOrDefault("wallet");
                    Console.WriteLine($"       containers={info.Count}, wallet.Id={w?.Id}, wallet.prefab={w?.Prefab}");
                    break;
                case TheGambler.Data.ContainerRewardData rd:
                    Console.WriteLine($"       parent={rd.Parent}, rarities={rd.Rarities.Count}, presets={rd.Presets?.Count}, firstPresetItems={rd.Presets?.FirstOrDefault()?.FirstOrDefault()?.Items.Count}");
                    break;
                case TheGambler.Data.AmmoData ad:
                    Console.WriteLine($"       calibers={ad.Names.Count}, bsg={ad.BsgCalibers.Count}, items={ad.Items.Count}");
                    break;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"FAIL {Path.GetFileName(file)} -> {type.Name}: {ex.Message}");
        }
    }
}
