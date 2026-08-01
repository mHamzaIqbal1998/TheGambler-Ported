using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Helpers;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Servers;
using TheGambler.Data;
using TheGambler.Gamble;

namespace TheGambler.Pricing;

/// <summary>
/// Recomputes mystery container prices after every mod has loaded, so the
/// values reflect the final flea/trader prices of the rewards. Ported from the
/// legacy Price.ts + traderHelpers.updateContainerPrices, run at PostSptLoad.
/// </summary>
[Injectable(TypePriority = OnLoadOrder.PostSptModLoader + 1)]
public class PriceGenerator(
    ISptLogger<PriceGenerator> logger,
    GamblerData data,
    MysteryContainerLogic logic,
    DatabaseServer databaseServer,
    ItemHelper itemHelper)
    : IOnLoad
{
    private const string RoublesTpl = "5449016a4bdc2d6f028b456f";
    private const string TraderId = "67b7b52a4767af842e0521d0"; // Gambler trader id (db/base.json _id)

    // Traders whose assort prices are consulted for cheapest sourcing.
    private static readonly string[] SourceTraderIds =
    {
        "58330581ace78e27b8b10cee", "54cb50c76803fa8b248b4571", "5c0647fdd443bc2504c2d371",
        "5a7c2eca46aef81a7ca2145d", "5935c25fb3acc3127c3d8cd9", "5ac3b934156ae10c4430e83c",
    };

    public Task OnLoad()
    {
        logger.Success("[GamblerTrader] Generating Mystery Container Prices...");

        var generated = GenerateContainerPrices();
        UpdateContainerPrices(generated);

        logger.Success("[GamblerTrader] Finished pricing. Ready to launch.");
        return Task.CompletedTask;
    }

    /// <summary>Generate a "{name}_price" -> rouble price map for every priced container.</summary>
    private Dictionary<string, double> GenerateContainerPrices()
    {
        var prices = new Dictionary<string, double>();

        foreach (var (name, container) in logic.Containers)
        {
            // Only simulate containers flagged for auto-pricing (+ all ammo)
            if (!container.PriceGenerate && !container.IsAmmo)
            {
                continue;
            }

            List<double> rarityPrices;
            if (container.IsAmmo)
            {
                var amount = (data.Config.Odd($"{name}_min") + data.Config.Odd($"{name}_max")) / 2.0;
                rarityPrices = AverageRewardPrices(container, amount);
            }
            else if (container.IsPreset)
            {
                rarityPrices = PresetPrices(container);
            }
            else
            {
                rarityPrices = AverageRewardPrices(container, 1);
            }

            prices[$"{name}_price"] = WeightByOdds(container.Odds, rarityPrices, container.ProfitPercentage);
        }

        return prices;
    }

    /// <summary>Average reward value per rarity tier for a non-preset container.</summary>
    private List<double> AverageRewardPrices(ContainerRuntime container, double amount)
    {
        var result = new List<double>();

        for (var i = 0; i < container.Rarities.Count; i++)
        {
            var pool = i < container.Rewards.Count ? container.Rewards[i] : new List<string>();
            if (pool.Count == 0)
            {
                result.Add(0);
                continue;
            }

            double sum = 0;
            foreach (var tpl in pool)
            {
                if (tpl == "NaN")
                {
                    continue;
                }

                if (tpl == RoublesTpl)
                {
                    sum += i < container.RewardAmount.Count ? container.RewardAmount[i] : 0;
                }
                else
                {
                    sum += GetItemPrice(container.Parent, tpl, amount);
                }
            }
            result.Add(sum / pool.Count);
        }

        return result;
    }

    /// <summary>
    /// Average preset value per rarity tier. Presets always come from the parent
    /// pool (weapon/armor/helmet), indexed by the container's own rarity count -
    /// this lets premium_weapon (parent "weapon") price against weapon presets.
    /// </summary>
    private List<double> PresetPrices(ContainerRuntime container)
    {
        var result = new List<double>();
        var parent = logic.Get(container.Parent);
        var presets = parent?.Presets;
        if (presets is null)
        {
            return Enumerable.Repeat(0.0, container.Rarities.Count).ToList();
        }

        var skip = parent!.SkipBaseAttachments is not null
            ? new HashSet<string>(parent.SkipBaseAttachments)
            : new HashSet<string>();

        for (var i = 0; i < container.Rarities.Count; i++)
        {
            if (i >= presets.Count || presets[i].Count == 0)
            {
                result.Add(0);
                continue;
            }

            var tier = presets[i];

            double tierSum = 0;
            foreach (var build in tier)
            {
                double buildSum = 0;
                foreach (var item in build.Items)
                {
                    if (skip.Contains(item.Tpl))
                    {
                        continue;
                    }
                    buildSum += GetItemPrice(container.Parent, item.Tpl, 1);
                }
                tierSum += Math.Floor(buildSum);
            }
            result.Add(Math.Floor(tierSum / tier.Count));
        }

        return result;
    }

    /// <summary>Order: override price -> min(trader, flea) -> handbook max. Ported from Price.getItemPrice.</summary>
    private double GetItemPrice(string parent, string tpl, double amount)
    {
        var overridePrice = data.Config.GetOverridePrice(parent, tpl);
        double price;

        if (overridePrice > 0 && data.Config.OverrideEnable)
        {
            price = overridePrice;
        }
        else
        {
            var traderPrice = TraderAssortPrice(tpl);
            var fleaPrice = itemHelper.GetDynamicItemPrice(tpl) ?? 0;

            if (traderPrice != 0 && fleaPrice == 0)
            {
                price = traderPrice;
            }
            else if (traderPrice == 0 && fleaPrice != 0)
            {
                price = fleaPrice;
            }
            else
            {
                price = Math.Min(traderPrice, fleaPrice);
            }

            if (price == 0)
            {
                price = itemHelper.GetItemMaxPrice(tpl);
            }
        }

        return price * amount;
    }

    /// <summary>Cheapest rouble price the item is sold for by the source traders, or 0.</summary>
    private double TraderAssortPrice(string tpl)
    {
        var tables = databaseServer.GetTables();
        double price = 0;

        foreach (var traderId in SourceTraderIds)
        {
            if (!tables.Traders!.TryGetValue(traderId, out var trader) || trader.Assort is null)
            {
                continue;
            }

            var assortItems = trader.Assort.Items!;
            foreach (var item in assortItems)
            {
                if (item.Template != tpl || item.SlotId != "hideout")
                {
                    continue;
                }

                var scheme = trader.Assort.BarterScheme!.GetValueOrDefault(item.Id);
                var count = scheme?[0][0].Count ?? 0;

                if (count < 10)
                {
                    // Most likely a barter, skip
                    price = 0;
                    continue;
                }

                if (traderId == "5935c25fb3acc3127c3d8cd9")
                {
                    count *= 142; // Peacekeeper sells in dollars
                }

                return count;
            }
        }

        return price;
    }

    /// <summary>Optimal container price = sum(rarity price * odds slice) * profitability.</summary>
    private static double WeightByOdds(List<double> odds, List<double> prices, double profitability)
    {
        double sum = 0;
        double tracked = 0;
        for (var i = 0; i < odds.Count && i < prices.Count; i++)
        {
            var slice = odds[i] - tracked;
            tracked = odds[i];
            sum += prices[i] * (slice / 100.0);
        }
        return Math.Floor(sum * profitability);
    }

    /// <summary>Apply generated prices to the trader assort + flea market.</summary>
    private void UpdateContainerPrices(Dictionary<string, double> generated)
    {
        var tables = databaseServer.GetTables();
        var fleaPrices = tables.Templates!.Prices!;

        if (!tables.Traders!.TryGetValue(TraderId, out var trader) || trader.Assort is null)
        {
            logger.Warning("[GamblerTrader] Trader not found during pricing update");
            return;
        }

        // Map quest_id (used as assort item id) -> logical container name
        var byQuestId = data.Info
            .Where(kv => !string.IsNullOrEmpty(kv.Value.QuestId))
            .ToDictionary(kv => kv.Value.QuestId, kv => kv.Key);

        // Update trader assort rouble prices
        foreach (var (assortItemId, scheme) in trader.Assort.BarterScheme!)
        {
            if (!byQuestId.TryGetValue(assortItemId.ToString(), out var name))
            {
                continue;
            }

            if (generated.TryGetValue($"{name}_price", out var price) && price > 0)
            {
                // Only touch rouble-priced offers (skip barters)
                if (scheme.Count > 0 && scheme[0].Count > 0 && scheme[0][0].Template == RoublesTpl)
                {
                    scheme[0][0].Count = price * data.Config.PriceMultiplier;
                }
            }
        }

        // Update flea prices for the container items themselves
        foreach (var (name, info) in data.Info)
        {
            if (!generated.TryGetValue($"{name}_price", out var price) || price <= 0)
            {
                continue;
            }

            var id = new MongoId(info.Id);
            fleaPrices[id] = price > 250000 ? price * 1.20 : price * 1.35;
        }
    }
}
