using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using SPTarkov.DI.Annotations;
using SPTarkov.Reflection.Patching;
using SPTarkov.Server.Core.Controllers;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Generators;
using SPTarkov.Server.Core.Helpers;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Eft.Inventory;
using SPTarkov.Server.Core.Models.Eft.ItemEvent;
using SPTarkov.Server.Core.Models.Spt.Mod;
using SPTarkov.Server.Core.Models.Utils;

namespace TheGambler.Gamble;

/// <summary>
/// Enables the container-opening patch during the pre-load phase. Mirrors the
/// SPT official Harmony patch example.
/// </summary>
[Injectable(TypePriority = OnLoadOrder.PreSptModLoader)]
public class OpenContainerPatchLoader(ISptLogger<OpenContainerPatchLoader> logger) : IOnLoad
{
    public Task OnLoad()
    {
        new OpenContainerPatch().Enable();
        logger.Info("[GamblerTrader] Container-opening patch enabled");
        return Task.CompletedTask;
    }
}

/// <summary>
/// Intercepts InventoryController.OpenRandomLootContainer. When the opened item
/// is one of our "gambling_" containers we generate the gambling rewards and
/// add them to the player's stash, skipping the vanilla logic. Any other
/// container falls through to the original method. Ported from the legacy
/// mod.ts newOpenRandomLoot override.
/// </summary>
public class OpenContainerPatch : AbstractPatch
{
    protected override MethodBase GetTargetMethod()
    {
        return typeof(InventoryController).GetMethod(nameof(InventoryController.OpenRandomLootContainer))!;
    }

    [PatchPrefix]
    public static bool Prefix(
        PmcData pmcData,
        OpenRandomLootContainerRequestData request,
        MongoId sessionId,
        ItemEventRouterResponse output)
    {
        var provider = ServiceLocator.ServiceProvider;
        var itemHelper = provider.GetService<ItemHelper>()!;
        var logger = provider.GetService<ISptLogger<OpenContainerPatch>>()!;

        var openedItem = pmcData.Inventory?.Items?.FirstOrDefault(x => x.Id == request.Item);
        if (openedItem is null)
        {
            return true; // let the original handle the error
        }

        var template = itemHelper.GetItem(openedItem.Template);
        if (!template.Key || template.Value?.Name is null)
        {
            return true;
        }

        var internalName = template.Value.Name;
        var isGambling = internalName.Contains("gambling_");
        var isSealed = internalName.Contains("event_container_airdrop");

        if (!isGambling && !isSealed)
        {
            // Not one of ours - defer to the vanilla sealed/random loot logic
            return true;
        }

        var inventoryHelper = provider.GetService<InventoryHelper>()!;

        List<List<Item>> rewards;
        var foundInRaid = true;

        if (isSealed)
        {
            // Our custom "Sealed Weapon Case" - give real sealed weapon case loot
            var lootGenerator = provider.GetService<LootGenerator>()!;
            var settings = inventoryHelper.GetInventoryConfig().SealedAirdropContainer;
            rewards = lootGenerator.GetSealedWeaponCaseLoot(settings);
            foundInRaid = settings.FoundInRaid;
        }
        else
        {
            var gambleService = provider.GetService<GambleService>()!;
            var result = gambleService.Open(internalName.Replace("gambling_", ""));
            rewards = result.Items;
            foundInRaid = result.FoundInRaid;
        }

        if (rewards.Count == 0)
        {
            // Nothing won - just consume the container
            inventoryHelper.RemoveItem(pmcData, request.Item, sessionId, output);
            return false;
        }

        var addRequest = new AddItemsDirectRequest
        {
            ItemsWithModsToAdd = rewards,
            FoundInRaid = foundInRaid,
            UseSortingTable = true,
        };

        if (inventoryHelper.CanPlaceItemsInInventory(sessionId, rewards))
        {
            inventoryHelper.RemoveItem(pmcData, request.Item, sessionId, output);
            inventoryHelper.AddItemsToStash(sessionId, addRequest, pmcData, output);
        }
        else
        {
            logger.Error("[GamblerTrader] Cannot open container - inventory is full!");
        }

        return false; // skip the original method
    }
}
