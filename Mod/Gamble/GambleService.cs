using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Helpers;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Utils;
using TheGambler.Data;

namespace TheGambler.Gamble;

/// <summary>Result of opening a gambling container.</summary>
public class GambleResult
{
    public List<List<Item>> Items { get; } = new();
    public bool FoundInRaid { get; set; } = true;
}

/// <summary>
/// Generates the rewards for a gambling container. Ported from the legacy
/// Gamble.ts. A fresh <see cref="GambleRun"/> is created per container opening
/// so all the per-open mutable state is isolated.
/// </summary>
[Injectable(InjectionType.Singleton)]
public class GambleService(
    ISptLogger<GambleService> logger,
    MysteryContainerLogic logic,
    GamblerData data,
    ItemHelper itemHelper)
{
    /// <summary>Open a container by its logical name (e.g. "weapon", "9x18", "loadout").</summary>
    public GambleResult Open(string name)
    {
        var run = new GambleRun(logger, logic, data, itemHelper);
        run.NewGamble(name);
        return run.Result;
    }
}

/// <summary>Per-open reward generation state machine.</summary>
internal class GambleRun(
    ISptLogger<GambleService> logger,
    MysteryContainerLogic logic,
    GamblerData data,
    ItemHelper itemHelper)
{
    private const string RoublesTpl = "5449016a4bdc2d6f028b456f";

    public readonly GambleResult Result = new();
    private readonly Random _rng = new();

    // Carried state across recursive loadout generation (mirrors Gamble.ts fields)
    private string? _currentId;
    private string? _currentCaliber;
    private string? _currentMagazine;
    private int _currentMagazineMaxAmmo;
    private string? _currentWeaponType;
    private bool _currentHeadsetCompatible = true;

    private double Roll() => _rng.NextDouble() * 100.0;
    private double RollBetween(double min, double max) => min + _rng.NextDouble() * (max - min);

    public void NewGamble(string name, double? roll = null)
    {
        var r = roll ?? Roll();

        switch (name)
        {
            case "weapon":
            case "average_weapon":
            case "default_weapon":
            case "premium_weapon":
            case "helmet":
            case "armor":
            case "premium_armor":
                OpenPreset(name, r);
                break;

            case "loadout":
                OpenLoadoutContainer(name, r);
                break;

            case "premium_loadout":
                OpenLoadoutContainer(name, 10.0);
                break;

            default:
                if (logic.Exists(name))
                {
                    OpenReward(name, r);
                }
                else
                {
                    logger.Error($"[GamblerTrader] Mystery Container '{name}' doesn't exist! Contact author.");
                }
                break;
        }

        // Found-in-raid status
        Result.FoundInRaid = logic.IsAmmo(name)
            ? data.Config.GetBool("all_ammo_rewards_found_in_raid_status")
            : data.Config.GetBool($"{name}_rewards_found_in_raid_status");
    }

    private void OpenReward(string name, double roll, string id = "NaN", bool stackable = false, double? rewardAmount = null)
    {
        var container = logic.Get(name);
        if (container is null)
        {
            return;
        }

        var odds = container.Odds;
        var rewardRolls = container.RewardRolls;

        // Fixed-roll pools (e.g. medical): draw N times from each rarity pool
        if (id == "NaN" && rewardRolls is not null)
        {
            for (var i = 0; i < container.Rewards.Count; i++)
            {
                var rolls = i < rewardRolls.Count ? rewardRolls[i] : 0;
                for (var j = 0; j < rolls; j++)
                {
                    var item = logic.GetReward(name, i);
                    if (item == "NaN")
                    {
                        continue;
                    }
                    _currentId = item;
                    Push(item);
                }
            }
            return;
        }

        // Odds-based single reward selection
        if (id == "NaN")
        {
            for (var i = 0; i < odds.Count; i++)
            {
                if (roll <= odds[i])
                {
                    id = logic.GetReward(name, i);
                    _currentId = id;
                    rewardAmount ??= container.RewardAmount[i];
                    if (!stackable)
                    {
                        stackable = container.Stackable[i];
                    }
                    break;
                }
            }
        }

        if (id != "NaN")
        {
            rewardAmount ??= logic.GetRandomAmount(name);
            if (!stackable)
            {
                for (var i = 0; i < rewardAmount; i++)
                {
                    Push(id);
                }
            }
            else
            {
                Push(id, (int)rewardAmount.Value);
            }
        }
    }

    private void OpenPreset(string name, double roll)
    {
        var container = logic.Get(name);
        if (container is null)
        {
            return;
        }

        var parentName = container.Parent; // "weapon" / "armor" / "helmet"
        var parent = logic.Get(parentName);
        if (parent?.Presets is null)
        {
            logger.Error($"[GamblerTrader] Preset parent '{parentName}' has no presets");
            return;
        }

        var odds = container.Odds;
        var creator = new PresetItemCreator(itemHelper);
        List<Item> preset = new();

        for (var i = 0; i < odds.Count; i++)
        {
            if (roll <= odds[i])
            {
                if (i >= parent.Presets.Count || parent.Presets[i].Count == 0)
                {
                    break;
                }

                var rarity = i < parent.Rarities.Count ? parent.Rarities[i] : null;
                var build = parent.Presets[i][_rng.Next(parent.Presets[i].Count)];
                preset = creator.BuildPreset(build, parentName == "weapon" ? rarity : null);
                _currentId = preset.Count > 0 ? preset[0].Template.ToString() : null;

                if (name is "weapon" or "premium_weapon")
                {
                    _currentCaliber = creator.Caliber;
                    _currentMagazine = creator.Magazine;
                    _currentWeaponType = creator.WeaponType;
                    _currentMagazineMaxAmmo = creator.MagazineMaxAmmo;
                }

                if (name == "helmet")
                {
                    _currentHeadsetCompatible = creator.HeadsetCompatible;
                }

                break;
            }
        }

        if (preset.Count != 0)
        {
            Result.Items.Add(preset);
        }
    }

    /// <summary>
    /// Generates a full loadout by recursively gambling each guaranteed reward
    /// container and pairing weapons with magazines + ammo. Ported from
    /// Gamble.ts openLoadoutContainer().
    /// </summary>
    private void OpenLoadoutContainer(string name, double roll)
    {
        var container = logic.Get(name);
        if (container?.GuaranteedRewards is null)
        {
            return;
        }

        var rewards = container.GuaranteedRewards;
        var randomness = container.GuaranteedRandomness;

        if (roll > 90)
        {
            roll = RollBetween(44.3, 74.3); // scav tier
        }
        else if (roll > 82)
        {
            roll = RollBetween(19.3, 44.3); // decent tier
        }

        for (var i = 0; i < rewards.Count; i++)
        {
            var current = rewards[i];

            if (logic.Exists(current))
            {
                // Reward is itself a container -> recurse with a tier-adjusted roll
                switch (_currentWeaponType)
                {
                    case "_meme": NewGamble(current, RollBetween(5, 65)); break;
                    case "_meta": NewGamble(current, RollBetween(5, 25)); break;
                    case "_decent": NewGamble(current, RollBetween(12, 50)); break;
                    case "_scav": NewGamble(current, RollBetween(35, 70)); break;
                    default:
                        if (randomness is not null && i < randomness.Count && randomness[i])
                        {
                            NewGamble(current);
                        }
                        else
                        {
                            NewGamble(current, roll);
                        }
                        break;
                }

                // Helmet: skip the paired headset reward when incompatible
                if (current == "helmet")
                {
                    var helmet = logic.Get("helmet");
                    if (!_currentHeadsetCompatible ||
                        (_currentId is not null && helmet?.HeadsetIncompatibleHelmets?.Contains(_currentId) == true))
                    {
                        i++;
                    }
                }

                // Headset: special-cased headset that is always given
                if (current == "headset" && _currentId == "5c06c6a80db834001b735491")
                {
                    OpenReward("headset", roll, "5c06c6a80db834001b735491", false, 1);
                    continue;
                }

                // Armor: skip the paired rig reward when the "armor" is actually a rig
                if (current == "armor")
                {
                    var armor = logic.Get("armor");
                    if (_currentId is not null && armor?.ArmorRigs?.Contains(_currentId) == true)
                    {
                        i++;
                    }
                }

                // Weapon: generate magazines + ammo to match
                if (current == "weapon")
                {
                    GenerateWeaponAmmo(roll);
                }
            }
            else
            {
                // Reward is a plain item tpl
                var rewardAmount = i < container.GuaranteedRewardAmount?.Count ? (int)container.GuaranteedRewardAmount[i] : 1;
                var stackable = i < container.GuaranteedStackable?.Count && container.GuaranteedStackable[i];

                if (!stackable)
                {
                    for (var k = 0; k < rewardAmount; k++)
                    {
                        Push(current);
                    }
                }
                else
                {
                    Push(current, rewardAmount);
                }
            }
        }
    }

    /// <summary>Push magazines and ammo stacks for the last generated weapon.</summary>
    private void GenerateWeaponAmmo(double roll)
    {
        var currentCaliber = _currentCaliber;
        var currentMagazine = _currentMagazine;
        var currentMagazineMaxAmmo = _currentMagazineMaxAmmo;
        var currentWeaponType = _currentWeaponType;
        const int magazineCount = 3;

        var badMagazines = new HashSet<string>
        {
            "633ec6ee025b096d320a3b15", // RSh-12 cylinder
            "5ae0973a5acfc4001562206c", // Mosin magazine
            "587df3a12459772c28142567", // SKS internal magazine
        };

        if (currentCaliber is null)
        {
            return;
        }

        var caliber = logic.ResolveCaliber(currentCaliber);
        if (caliber is null)
        {
            return;
        }

        // Push two spare magazines for calibers that use them
        if (caliber is not ("20x70" or "23x75" or "12/70" or ".357")
            && currentMagazine is not null && !badMagazines.Contains(currentMagazine))
        {
            OpenReward(caliber, roll, currentMagazine, false, 1);
            OpenReward(caliber, roll, currentMagazine, false, 1);
        }

        // Ammo rarity range depends on weapon tier
        double min, max;
        switch (currentWeaponType)
        {
            case "_meme": min = 5; max = 30; break;
            case "_decent": min = 10; max = 45; break;
            case "_meta": min = 2; max = 26; break;
            default: min = 10; max = 90; break;
        }

        if (caliber is "20x70" or "23x75" or "12/70")
        {
            currentMagazineMaxAmmo = 10;
        }

        var tempRoll = RollBetween(min, max);
        string? ammoId = null;
        for (var i = 0; i < magazineCount; i++)
        {
            if (ammoId is null)
            {
                OpenReward(caliber, tempRoll, "NaN", true, currentMagazineMaxAmmo);
                ammoId = _currentId;
            }
            else
            {
                OpenReward(caliber, tempRoll, ammoId, true, currentMagazineMaxAmmo);
            }
        }
    }

    private void Push(string tpl, int count = 1)
    {
        Result.Items.Add(new List<Item>
        {
            new()
            {
                Id = new MongoId(),
                Template = new MongoId(tpl),
                ParentId = "hideout",
                SlotId = "hideout",
                Upd = new Upd { StackObjectsCount = count },
            },
        });
    }
}
