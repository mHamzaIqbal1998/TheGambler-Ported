using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Spt.Config;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Servers;
using TheGambler.Data;
using TheGambler.Items;
using TheGambler.Trader;

namespace TheGambler;

using Path = System.IO.Path;

/// <summary>
/// Main entry point. Runs after the database has loaded and wires up the
/// Gambler trader: registers the trader, creates the custom mystery container
/// items, and adds them to the trader's assort. Container pricing and the
/// container-opening logic are handled by their own services.
/// </summary>
[Injectable(TypePriority = OnLoadOrder.PostDBModLoader + 1)]
public class GamblerMod(
    ISptLogger<GamblerMod> logger,
    GamblerData data,
    ConfigServer configServer,
    TraderRegistrationHelper traderHelper,
    ItemFactory itemFactory,
    FluentTraderAssortCreator assortCreator,
    Gamble.MysteryContainerLogic containerLogic)
    : IOnLoad
{
    private const string RoublesTpl = "5449016a4bdc2d6f028b456f";
    private const string TraderImage = "thegambler.jpg";

    public Task OnLoad()
    {
        logger.Info("[GamblerTrader] Loading...");

        // Load config + all container data files, then precompute container odds
        data.Load();
        containerLogic.Build(data);

        var traderConfig = configServer.GetConfig<TraderConfig>();
        var ragfairConfig = configServer.GetConfig<RagfairConfig>();

        // Load the trader base (db/base.json)
        var traderBase = LoadTraderBase();

        // Register image, refresh time, flea visibility
        traderHelper.RegisterProfileImage(traderBase, data.ModPath, TraderImage);
        traderHelper.SetTraderUpdateTime(traderConfig, traderBase, data.Config.TraderUpdateMinTime, data.Config.TraderUpdateMaxTime);
        ragfairConfig.Traders![traderBase.Id] = true;

        // Create custom mystery container items
        itemFactory.CreateItems(data.Info);

        // Add trader (empty assort) then fill it with the containers
        traderHelper.AddTraderWithEmptyAssortToDb(traderBase);
        AddContainersToTrader(traderBase.Id);

        // Locales
        traderHelper.AddTraderToLocales(traderBase, "Gambler",
            "Welcome Traveler! May I indulge you in purchasing some mystery boxes?");

        logger.Success("[GamblerTrader] Loaded");
        return Task.CompletedTask;
    }

    private TraderBase LoadTraderBase()
    {
        // Reuse ModHelper via data path; load base.json into the TraderBase model.
        var path = Path.Combine(data.ModPath, "db");
        return data.LoadTraderBase(path, "base.json");
    }

    /// <summary>
    /// Adds each enabled container to the trader assort. Ported from
    /// traderHelpers.addSingleItemsToTrader.
    /// </summary>
    private void AddContainersToTrader(MongoId traderId)
    {
        var config = data.Config;
        var added = 0;

        foreach (var (key, value) in data.Info)
        {
            // Only add containers explicitly enabled in config
            if (!config.GetBool($"{key}_enable"))
            {
                continue;
            }

            // Ammo containers: skip when the "all ammo" master switch is off
            var isAmmo = char.IsDigit(key[0]) || key[0] == '.';
            if (isAmmo && !config.GetBool("all_ammo_enable"))
            {
                continue;
            }

            var itemId = string.IsNullOrEmpty(value.QuestId) ? (MongoId?)null : new MongoId(value.QuestId);
            assortCreator.CreateSingleAssortItem(new MongoId(value.Id), itemId);

            if (value.Barter is { Count: > 0 })
            {
                foreach (var (barterTpl, amount) in value.Barter)
                {
                    assortCreator.AddBarterCost(new MongoId(barterTpl), amount);
                }
            }
            else
            {
                var price = config.GetNumber($"{key}_price") * config.PriceMultiplier;
                assortCreator.AddMoneyCost(new MongoId(RoublesTpl), price);
            }

            var unlimited = config.GetBool($"{key}_unlimited_stock");
            var stock = unlimited ? 999999 : config.GetInt($"{key}_stock");
            assortCreator.AddStackCount(stock, unlimited);
            assortCreator.AddLoyaltyLevel(1);
            assortCreator.Export(traderId);
            added++;
        }

        logger.Info($"[GamblerTrader] Added {added} containers to trader assort");
    }
}
