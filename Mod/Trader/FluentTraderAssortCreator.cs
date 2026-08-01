using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Services;

namespace TheGambler.Trader;

/// <summary>
/// Fluent builder for adding items to a trader's assort. Ported from the SPT
/// official trader example and the legacy fluentTraderAssortCreator.ts.
/// </summary>
[Injectable(TypePriority = OnLoadOrder.PostDBModLoader + 1)]
public class FluentTraderAssortCreator(
    DatabaseService databaseService,
    ISptLogger<FluentTraderAssortCreator> logger)
{
    private readonly List<Item> _itemsToSell = [];
    private readonly Dictionary<string, List<List<BarterScheme>>> _barterScheme = new();
    private readonly Dictionary<string, int> _loyaltyLevel = new();

    public FluentTraderAssortCreator CreateSingleAssortItem(MongoId itemTpl, MongoId? itemId = null)
    {
        var newItemToAdd = new Item
        {
            Id = itemId ?? new MongoId(),
            Template = itemTpl,
            ParentId = "hideout",
            SlotId = "hideout",
            Upd = new Upd
            {
                UnlimitedCount = false,
                StackObjectsCount = 100,
            },
        };

        _itemsToSell.Add(newItemToAdd);
        return this;
    }

    public FluentTraderAssortCreator AddStackCount(int stackCount, bool unlimited = false)
    {
        _itemsToSell[0].Upd!.StackObjectsCount = stackCount;
        if (unlimited)
        {
            _itemsToSell[0].Upd!.UnlimitedCount = true;
        }
        return this;
    }

    public FluentTraderAssortCreator AddLoyaltyLevel(int level)
    {
        _loyaltyLevel[_itemsToSell[0].Id] = level;
        return this;
    }

    public FluentTraderAssortCreator AddMoneyCost(MongoId currencyType, double amount)
    {
        _barterScheme[_itemsToSell[0].Id] =
        [
            [
                new BarterScheme
                {
                    Count = amount,
                    Template = currencyType,
                },
            ],
        ];
        return this;
    }

    public FluentTraderAssortCreator AddBarterCost(MongoId itemTpl, int count)
    {
        var sellableItemId = _itemsToSell[0].Id;

        if (!_barterScheme.TryGetValue(sellableItemId, out var scheme))
        {
            _barterScheme[sellableItemId] = [[new BarterScheme { Count = count, Template = itemTpl }]];
            return this;
        }

        var existing = scheme[0].FirstOrDefault(x => x.Template == itemTpl);
        if (existing is not null)
        {
            existing.Count += count;
        }
        else
        {
            scheme[0].Add(new BarterScheme { Count = count, Template = itemTpl });
        }
        return this;
    }

    /// <summary>Store the built assort against the given trader, then reset for reuse.</summary>
    public void Export(MongoId traderId)
    {
        var traderData = databaseService.GetTables().Traders!.GetValueOrDefault(traderId);
        if (traderData is null)
        {
            logger.Error($"[GamblerTrader] Cannot export assort, trader {traderId} not found");
            Reset();
            return;
        }

        var rootItemId = _itemsToSell[0].Id;
        if (traderData.Assort!.Items!.Exists(x => x.Id == rootItemId))
        {
            logger.Error($"[GamblerTrader] Unable to add item with key {rootItemId}, key already used");
            Reset();
            return;
        }

        traderData.Assort.Items.AddRange(_itemsToSell);
        traderData.Assort.BarterScheme![rootItemId] = _barterScheme[rootItemId];
        traderData.Assort.LoyalLevelItems![rootItemId] = _loyaltyLevel[rootItemId];

        Reset();
    }

    private void Reset()
    {
        _itemsToSell.Clear();
        _barterScheme.Clear();
        _loyaltyLevel.Clear();
    }
}
