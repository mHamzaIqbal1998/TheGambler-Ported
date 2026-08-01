using System.Reflection;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Helpers;
using SPTarkov.Server.Core.Models.Utils;
using TheGambler.Config;

namespace TheGambler.Data;

using Path = System.IO.Path;

/// <summary>
/// Loads and holds all of the mod's static data: the config, container metadata
/// and every container reward table. Registered as a singleton so both the
/// load orchestrator and the container-opening patch can share it.
/// </summary>
[Injectable(InjectionType.Singleton)]
public class GamblerData(ISptLogger<GamblerData> logger, ModHelper modHelper)
{
    /// <summary>Maps the logical container name (used everywhere in the logic) to its data file.</summary>
    private static readonly Dictionary<string, string> ContainerFiles = new()
    {
        ["wallet"] = "Wallet.json",
        ["100k"] = "CoinFlip100k.json",
        ["500k"] = "CoinFlip500k.json",
        ["1mil"] = "CoinFlip1Mil.json",
        ["keycard"] = "Keycard.json",
        ["key"] = "keys.json",
        ["stim"] = "Stims.json",
        ["medical"] = "Medical.json",
        ["food"] = "Foods.json",
        ["melee"] = "Melees.json",
        ["backpack"] = "Backpacks.json",
        ["rig"] = "Rigs.json",
        ["helmet"] = "Helmets.json",
        ["headset"] = "Headsets.json",
        ["weapon"] = "Weapons.json",
        ["premium_weapon"] = "PremiumWeapons.json",
        ["armor"] = "Armors.json",
        ["premium_armor"] = "PremiumArmors.json",
        ["roubles"] = "FlipRouble.json",
        ["bitcoin"] = "FlipBitcoin.json",
        ["gpcoin"] = "FlipGPCoin.json",
        ["loadout"] = "Loadouts.json",
        ["premium_loadout"] = "Loadouts.json",
        ["loadout_grenade"] = "LoadoutGrenade.json",
        ["loadout_facecovers"] = "LoadoutFacecovers.json",
        ["loadout_food"] = "LoadoutFood.json",
        ["loadout_drink"] = "LoadoutDrink.json",
        ["loadout_light_bleed"] = "LoadoutLightBleed.json",
        ["loadout_heavy_bleed"] = "LoadoutHeavyBleed.json",
        ["loadout_stim"] = "LoadoutStim.json",
        ["loadout_splint"] = "LoadoutSplint.json",
        ["loadout_healing"] = "LoadoutHealing.json",
    };

    private bool _loaded;

    public GamblerConfig Config { get; private set; } = new();

    /// <summary>Container metadata (item id, prefab, prices, barter) keyed by logical name.</summary>
    public Dictionary<string, ContainerInfo> Info { get; private set; } = new();

    /// <summary>Reward tables keyed by logical container name (e.g. "wallet", "weapon").</summary>
    public Dictionary<string, ContainerRewardData> Rewards { get; private set; } = new();

    /// <summary>Ammo data (calibers, per-caliber reward tables).</summary>
    public AmmoData Ammo { get; private set; } = new();

    /// <summary>Absolute path to this mod's folder.</summary>
    public string ModPath { get; private set; } = "";

    public void Load()
    {
        if (_loaded)
        {
            return;
        }

        ModPath = modHelper.GetAbsolutePathToModFolder(Assembly.GetExecutingAssembly());

        Config = modHelper.GetJsonDataFromFile<GamblerConfig>(Path.Combine(ModPath, "config"), "config.json");
        Info = modHelper.GetJsonDataFromFile<Dictionary<string, ContainerInfo>>(Path.Combine(ModPath, "data"), "container-info.json");

        var containersDir = Path.Combine(ModPath, "data", "containers");
        Ammo = modHelper.GetJsonDataFromFile<AmmoData>(containersDir, "Ammo.json");

        foreach (var (name, file) in ContainerFiles)
        {
            try
            {
                Rewards[name] = modHelper.GetJsonDataFromFile<ContainerRewardData>(containersDir, file);
            }
            catch (Exception ex)
            {
                logger.Error($"[GamblerTrader] Failed to load container data '{file}' for '{name}': {ex.Message}");
            }
        }

        _loaded = true;
        logger.Info($"[GamblerTrader] Loaded {Info.Count} container infos, {Rewards.Count} reward tables, {Ammo.Names.Count} ammo calibers");
    }

    /// <summary>Load the trader base json (db/base.json) into the SPT TraderBase model.</summary>
    public SPTarkov.Server.Core.Models.Eft.Common.Tables.TraderBase LoadTraderBase(string path, string fileName)
        => modHelper.GetJsonDataFromFile<SPTarkov.Server.Core.Models.Eft.Common.Tables.TraderBase>(path, fileName);
}
