using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Spt.Config;
using SPTarkov.Server.Core.Routers;
using SPTarkov.Server.Core.Services;
using SPTarkov.Server.Core.Utils.Cloners;

namespace TheGambler.Trader;

using Path = System.IO.Path;

/// <summary>
/// Helper for registering the Gambler trader: icon, refresh time, base data,
/// and locales. Ported from the SPT official trader example + traderHelpers.ts.
/// </summary>
[Injectable(TypePriority = OnLoadOrder.PostDBModLoader + 1)]
public class TraderRegistrationHelper(
    ICloner cloner,
    DatabaseService databaseService,
    ImageRouter imageRouter)
{
    /// <summary>Register the trader avatar image route.</summary>
    public void RegisterProfileImage(TraderBase traderBase, string modPath, string imageFileName)
    {
        var imageFilePath = Path.Combine(modPath, "res", imageFileName);
        imageRouter.AddRoute(traderBase.Avatar!.Replace(".jpg", ""), imageFilePath);
    }

    /// <summary>Add the trader's stock refresh window (in seconds) to the trader config.</summary>
    public void SetTraderUpdateTime(TraderConfig traderConfig, TraderBase traderBase, int minSeconds, int maxSeconds)
    {
        traderConfig.UpdateTime.Add(new UpdateTime
        {
            TraderId = traderBase.Id,
            Seconds = new MinMax<int>(minSeconds, maxSeconds),
        });
    }

    /// <summary>Add the trader to the database with an empty assort ready for items.</summary>
    public void AddTraderWithEmptyAssortToDb(TraderBase traderBase)
    {
        var emptyAssort = new TraderAssort
        {
            NextResupply = 0,
            Items = [],
            BarterScheme = new Dictionary<MongoId, List<List<BarterScheme>>>(),
            LoyalLevelItems = new Dictionary<MongoId, int>(),
        };

        var trader = new SPTarkov.Server.Core.Models.Eft.Common.Tables.Trader
        {
            Assort = emptyAssort,
            Base = cloner.Clone(traderBase),
            QuestAssort = new Dictionary<string, Dictionary<MongoId, MongoId>>
            {
                ["started"] = new(),
                ["success"] = new(),
                ["fail"] = new(),
            },
            Dialogue = new Dictionary<string, List<string>>(),
        };

        databaseService.GetTables().Traders!.TryAdd(traderBase.Id, trader);
    }

    /// <summary>Add trader name/location/description to all locales via a lazy-load transformer.</summary>
    public void AddTraderToLocales(TraderBase traderBase, string firstName, string description)
    {
        var locales = databaseService.GetTables().Locales!.Global;
        var id = traderBase.Id;
        var fullName = traderBase.Name;
        var nickName = traderBase.Nickname;
        var location = traderBase.Location;

        foreach (var (_, localeKvP) in locales)
        {
            localeKvP.AddTransformer(data =>
            {
                data[$"{id} FullName"] = fullName!;
                data[$"{id} FirstName"] = firstName;
                data[$"{id} Nickname"] = nickName!;
                data[$"{id} Location"] = location!;
                data[$"{id} Description"] = description;
                return data;
            });
        }
    }
}
