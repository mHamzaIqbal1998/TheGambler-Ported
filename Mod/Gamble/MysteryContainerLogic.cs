using SPTarkov.DI.Annotations;
using TheGambler.Config;
using TheGambler.Data;

namespace TheGambler.Gamble;

/// <summary>
/// Runtime, precomputed view of a single container's reward configuration:
/// cumulative odds, reward pools, amounts, and preset builds. Mirrors the
/// Container helper class from the legacy MysteryContainer.ts.
/// </summary>
public class ContainerRuntime
{
    public string Name = "";
    public string Parent = "";
    public List<string> Rarities = new();

    /// <summary>Cumulative odds thresholds (0-100) per rarity index.</summary>
    public List<double> Odds = new();

    /// <summary>Reward tpl pools per rarity index. "NaN" means nothing.</summary>
    public List<List<string>> Rewards = new();

    public List<bool> Stackable = new();
    public List<double> RewardAmount = new();
    public List<int>? RewardRolls;

    public int Min = 1;
    public int Max = 1;
    public double ProfitPercentage;

    public bool IsAmmo;
    public bool IsPreset;
    public bool PriceGenerate;

    public List<List<PresetBuild>>? Presets;
    public List<string>? SkipBaseAttachments;

    public int Rolls = 1;
    public List<string>? GuaranteedRewards;
    public List<bool>? GuaranteedStackable;
    public List<double>? GuaranteedRewardAmount;
    public List<bool>? GuaranteedRandomness;

    // Helmet/armor specific
    public List<string>? HeadsetIncompatibleHelmets;
    public List<string>? ArmorRigs;
}

/// <summary>
/// Builds and serves the runtime container definitions used by the gambling
/// logic. Ported from MysteryContainer.ts.
/// </summary>
[Injectable(InjectionType.Singleton)]
public class MysteryContainerLogic
{
    /// <summary>Container names that use price override groups + are stackable.</summary>
    private static readonly HashSet<string> OverrideNames = new() { "ammo", "armor", "weapon", "helmet" };

    private readonly Dictionary<string, ContainerRuntime> _containers = new();
    private GamblerData _data = null!;
    private readonly Random _rng = new();

    public IReadOnlyDictionary<string, ContainerRuntime> Containers => _containers;

    public void Build(GamblerData data)
    {
        _data = data;
        _containers.Clear();

        // Standard containers
        foreach (var (name, reward) in data.Rewards)
        {
            _containers[name] = BuildContainer(name, reward, data.Config, isAmmo: false);
        }

        // Ammo calibers (each caliber is its own container)
        foreach (var caliber in data.Ammo.Names)
        {
            if (data.Ammo.Items.TryGetValue(caliber, out var ammoReward))
            {
                _containers[caliber] = BuildContainer(caliber, ammoReward, data.Config, isAmmo: true);
            }
        }
    }

    private ContainerRuntime BuildContainer(string name, ContainerRewardData reward, GamblerConfig config, bool isAmmo)
    {
        var c = new ContainerRuntime
        {
            Name = name,
            Parent = reward.Parent,
            Rarities = reward.Rarities,
            IsPreset = reward.IsPreset,
            PriceGenerate = reward.PriceGenerate,
            Presets = reward.Presets,
            SkipBaseAttachments = reward.SkipBaseAttachments,
            RewardRolls = reward.RewardRolls,
            Rolls = reward.Rolls ?? 1,
            GuaranteedRewards = reward.GuaranteedRewards,
            GuaranteedStackable = reward.GuaranteedStackable,
            GuaranteedRewardAmount = reward.GuaranteedRewardAmount,
            GuaranteedRandomness = reward.GuaranteedRandomness,
            HeadsetIncompatibleHelmets = reward.HeadsetIncompatibleHelmets,
            ArmorRigs = reward.ArmorRigs,
            Min = (int)config.Odd($"{name}_min", 1),
            Max = (int)config.Odd($"{name}_max", 1),
            ProfitPercentage = config.Odd($"{name}_profit_percentage"),
        };

        // Cumulative odds + reward pools per rarity
        double running = 0;
        for (var j = 0; j < reward.Rarities.Count; j++)
        {
            running += config.Odd($"{name}{reward.Rarities[j]}");
            c.Odds.Add(running);
            c.Rewards.Add(reward.Rewards is not null && j < reward.Rewards.Count
                ? new List<string>(reward.Rewards[j])
                : new List<string>());
        }

        // Overrides + stackable / reward amount defaults (mirror applyOverrides)
        if (isAmmo)
        {
            c.IsAmmo = true;
            c.Stackable = reward.Stackable ?? Fill(reward.Rarities.Count, true);
            // GetRandomAmount instead of using a zero amount.
            c.RewardAmount = reward.RewardAmount ?? new List<double>();
        }
        else
        {
            c.RewardAmount = reward.RewardAmount ?? Fill1(reward.Rarities.Count);
            c.Stackable = reward.Stackable ?? Fill(reward.Rarities.Count, false);
        }

        return c;
    }

    private static List<bool> Fill(int n, bool v) => Enumerable.Repeat(v, n).ToList();
    private static List<double> Fill1(int n) => Enumerable.Repeat(1.0, n).ToList();

    // ----- accessors used by GambleService (mirror MysteryContainer.ts getters) -----

    public ContainerRuntime? Get(string name) => _containers.GetValueOrDefault(name);

    public bool Exists(string name) => _containers.ContainsKey(name);

    public bool IsAmmo(string name) => _containers.TryGetValue(name, out var c) && c.IsAmmo;

    public bool IsPreset(string name) => _containers.TryGetValue(name, out var c) && c.IsPreset;

    /// <summary>Random tpl from the reward pool at a rarity index.</summary>
    public string GetReward(string name, int rarityIndex)
    {
        var pool = _containers[name].Rewards[rarityIndex];
        return pool[_rng.Next(pool.Count)];
    }

    public int GetRandomAmount(string name)
    {
        var c = _containers[name];
        return _rng.Next(c.Min, c.Max + 1);
    }

    /// <summary>BSG caliber name (e.g. "Caliber9x18PM") -> short caliber (e.g. "9x18").</summary>
    public string? ResolveCaliber(string bsgCaliber)
        => _data.Ammo.BsgCalibers.GetValueOrDefault(bsgCaliber);
}
