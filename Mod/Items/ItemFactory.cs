using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Spt.Mod;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Services.Mod;
using TheGambler.Data;

namespace TheGambler.Items;

/// <summary>
/// Builds and registers the custom mystery container items in the database.
/// Ported from the legacy itemCreateHelper.ts, which created each container
/// from scratch as a loot-container-type item.
/// </summary>
[Injectable(TypePriority = OnLoadOrder.PostDBModLoader + 1)]
public class ItemFactory(ISptLogger<ItemFactory> logger, CustomItemService customItemService)
{
    // Base node the original items were parented to (loot container node).
    private const string LootContainerParent = "62f109593b54472778797866";

    // Handbook category the original items were listed under.
    private const string HandbookParentId = "5b5f6fa186f77409407a7eb7";

    /// <summary>Create every mystery container item defined in container-info.json.</summary>
    public void CreateItems(IReadOnlyDictionary<string, ContainerInfo> info)
    {
        var created = 0;
        foreach (var (_, value) in info)
        {
            var props = BuildContainerProps(value);

            var newItem = new NewItemDetails
            {
                NewItem = new TemplateItem
                {
                    Id = new MongoId(value.Id),
                    Name = value.Name,
                    Parent = LootContainerParent,
                    Type = "Item",
                    Prototype = "",
                    Properties = props,
                },
                FleaPriceRoubles = value.FleaPriceRoubles,
                HandbookPriceRoubles = value.HandbookPriceRoubles,
                HandbookParentId = HandbookParentId,
                Locales = new Dictionary<string, LocaleDetails>
                {
                    ["en"] = new LocaleDetails
                    {
                        Name = value.LocaleName,
                        ShortName = value.ShortName,
                        Description = value.Description,
                    },
                },
            };

            customItemService.CreateItem(newItem);
            created++;
        }

        logger.Info($"[GamblerTrader] Created {created} mystery container items");
    }

    /// <summary>Mirror of the _props object the legacy mod built for each container.</summary>
    private static TemplateItemProperties BuildContainerProps(ContainerInfo value)
    {
        return new TemplateItemProperties
        {
            Name = value.LocaleName,
            ShortName = value.ShortName,
            Description = value.Description,
            Width = value.Width,
            Height = value.Height,
            Weight = 2,
            BackgroundColor = "orange",
            StackMaxSize = 1,
            ItemSound = "container_plastic",
            DiscardLimit = -1,
            ExamineTime = 1,
            ExamineExperience = 100,
            ExaminedByDefault = true,
            LootExperience = 20,
            CanSellOnRagfair = false,
            CanRequireOnRagfair = false,
            InsuranceDisabled = false,
            HideEntrails = true,
            MergesWithChildren = false,
            SearchSound = "drawer_metal_looting",
            Prefab = new Prefab { Path = value.Prefab, Rcid = "" },
            UsePrefab = new Prefab { Path = "", Rcid = "" },
            Grids = BuildGrids(value.Id),
            Slots = [],
        };
    }

    /// <summary>Single 1x1 grid, matching the legacy item definition.</summary>
    private static List<Grid> BuildGrids(string ownerId)
    {
        return
        [
            new Grid
            {
                Name = "main",
                Id = "6489c03c8bc5233fdc78e789",
                Parent = ownerId,
                Prototype = "55d329c24bdc2d892f8b4567",
                Properties = new GridProperties
                {
                    CellsH = 1,
                    CellsV = 1,
                    MinCount = 1,
                    MaxCount = 99,
                    MaxWeight = 0,
                    IsSortingTable = false,
                    Filters =
                    [
                        new GridFilter
                        {
                            Filter = new HashSet<MongoId>(),
                            ExcludedFilter = new HashSet<MongoId> { new("54009119af1c881c07000029") },
                        },
                    ],
                },
            },
        ];
    }
}
