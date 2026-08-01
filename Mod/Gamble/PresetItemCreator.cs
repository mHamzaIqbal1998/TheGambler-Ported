using SPTarkov.Server.Core.Helpers;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using TheGambler.Data;

namespace TheGambler.Gamble;

/// <summary>
/// Builds a concrete item + attachments list from a preset build, assigning
/// fresh unique ids so duplicate rewards never collide in the player's stash.
/// Ported from the legacy itemCreator.ts generateItem().
/// </summary>
public class PresetItemCreator(ItemHelper itemHelper)
{
    // A helmet template that already includes a built-in headset.
    private const string HelmetWithHeadsetTpl = "5a16b9fffcdbcb0176308b34";

    public bool HeadsetCompatible { get; private set; } = true;
    public string? Caliber { get; private set; }
    public string? Magazine { get; private set; }
    public int MagazineMaxAmmo { get; private set; }
    public string? WeaponType { get; private set; }

    /// <summary>Build a preset. <paramref name="weaponRarity"/> is stored as WeaponType for weapons.</summary>
    public List<Item> BuildPreset(PresetBuild build, string? weaponRarity = null)
    {
        HeadsetCompatible = true;
        if (weaponRarity is not null)
        {
            WeaponType = weaponRarity;
        }

        var items = new List<Item>();
        var buildItems = build.Items;
        if (buildItems.Count == 0)
        {
            return items;
        }

        // Pre-map every old id to a fresh MongoId (root included). This avoids
        // id collisions and ordering issues with parent references.
        var idMap = new Dictionary<string, MongoId>();
        foreach (var bi in buildItems)
        {
            if (!idMap.ContainsKey(bi.Id))
            {
                idMap[bi.Id] = new MongoId();
            }
        }

        var rootOldId = buildItems[0].Id;

        for (var i = 0; i < buildItems.Count; i++)
        {
            var bi = buildItems[i];

            if (bi.Tpl == HelmetWithHeadsetTpl)
            {
                HeadsetCompatible = false;
            }

            var newItem = new Item
            {
                Id = idMap[bi.Id],
                Template = new MongoId(bi.Tpl),
            };

            if (i == 0)
            {
                // Root item: no parent/slot
                items.Add(newItem);
                continue;
            }

            newItem.SlotId = bi.SlotId;
            newItem.ParentId = bi.ParentId is not null && idMap.TryGetValue(bi.ParentId, out var mappedParent)
                ? mappedParent.ToString()
                : idMap[rootOldId].ToString();

            if (bi.HasTogglable)
            {
                newItem.Upd = new Upd { Togglable = new UpdTogglable { On = true } };
            }

            // Track magazine + capacity for later ammo generation
            if (bi.SlotId == "mod_magazine")
            {
                Magazine = bi.Tpl;
                var mag = itemHelper.GetItem(bi.Tpl);
                var cartridge = mag.Value?.Properties?.Cartridges?.FirstOrDefault();
                MagazineMaxAmmo = (int)(cartridge?.MaxCount ?? 0);
            }

            items.Add(newItem);
        }

        // Weapon caliber comes from the root item's properties
        var rootTemplate = itemHelper.GetItem(items[0].Template);
        Caliber = rootTemplate.Value?.Properties?.AmmoCaliber;

        return items;
    }
}
