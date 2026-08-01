using System.Text.Json;
using System.Text.Json.Serialization;

namespace TheGambler.Data;

/// <summary>
/// Reward table for a single container type, loaded from data/containers/*.json
/// (generated from the legacy containers/*.ts data classes). Most fields are
/// optional because different container kinds use different subsets.
/// </summary>
public record ContainerRewardData
{
    [JsonPropertyName("parent")]
    public string Parent { get; set; } = "";

    [JsonPropertyName("is_preset")]
    public bool IsPreset { get; set; }

    [JsonPropertyName("price_generate")]
    public bool PriceGenerate { get; set; }

    [JsonPropertyName("calculate_preset_prices")]
    public bool CalculatePresetPrices { get; set; }

    [JsonPropertyName("rarities")]
    public List<string> Rarities { get; set; } = new();

    [JsonPropertyName("stackable")]
    public List<bool>? Stackable { get; set; }

    [JsonPropertyName("reward_amount")]
    public List<double>? RewardAmount { get; set; }

    /// <summary>2D array of reward item tpls indexed by rarity. "NaN" means "nothing".</summary>
    [JsonPropertyName("rewards")]
    public List<List<string>>? Rewards { get; set; }

    [JsonPropertyName("reward_rolls")]
    public List<int>? RewardRolls { get; set; }

    [JsonPropertyName("rolls")]
    public int? Rolls { get; set; }

    [JsonPropertyName("guaranteed_rewards")]
    public List<string>? GuaranteedRewards { get; set; }

    [JsonPropertyName("guaranteed_stackable")]
    public List<bool>? GuaranteedStackable { get; set; }

    [JsonPropertyName("guaranteed_reward_amount")]
    public List<double>? GuaranteedRewardAmount { get; set; }

    [JsonPropertyName("guaranteed_randomness")]
    public List<bool>? GuaranteedRandomness { get; set; }

    [JsonPropertyName("skip_base_attachments")]
    public List<string>? SkipBaseAttachments { get; set; }

    [JsonPropertyName("presets_invalid_tpls")]
    public List<string>? PresetsInvalidTpls { get; set; }

    /// <summary>2D array of weapon/armor/helmet preset builds indexed by rarity.</summary>
    [JsonPropertyName("presets")]
    public List<List<PresetBuild>>? Presets { get; set; }

    /// <summary>Helmet-only: helmet tpls that cannot be paired with a headset.</summary>
    [JsonPropertyName("headset_incompatible_helmets")]
    public List<string>? HeadsetIncompatibleHelmets { get; set; }

    /// <summary>Armor-only: armor tpls that are actually rigs (skip the paired rig reward).</summary>
    [JsonPropertyName("armor_rigs")]
    public List<string>? ArmorRigs { get; set; }
}

/// <summary>A single weapon/armor/helmet preset build.</summary>
public record PresetBuild
{
    [JsonPropertyName("Id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("Name")]
    public string? Name { get; set; }

    [JsonPropertyName("Root")]
    public string Root { get; set; } = "";

    [JsonPropertyName("Items")]
    public List<PresetItem> Items { get; set; } = new();
}

/// <summary>A single item within a preset build.</summary>
public record PresetItem
{
    [JsonPropertyName("_id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("_tpl")]
    public string Tpl { get; set; } = "";

    [JsonPropertyName("slotId")]
    public string? SlotId { get; set; }

    [JsonPropertyName("parentId")]
    public string? ParentId { get; set; }

    [JsonPropertyName("upd")]
    public JsonElement? Upd { get; set; }

    /// <summary>True if this item's upd contains a Togglable component.</summary>
    public bool HasTogglable =>
        Upd is { ValueKind: JsonValueKind.Object } u && u.TryGetProperty("Togglable", out _);
}

/// <summary>
/// Ammo container data (data/containers/Ammo.json). Holds one reward table per
/// caliber plus the BSG-caliber-name to short-name mapping used during weapon
/// ammo generation.
/// </summary>
public record AmmoData
{
    [JsonPropertyName("parent")]
    public string Parent { get; set; } = "ammo";

    [JsonPropertyName("names")]
    public List<string> Names { get; set; } = new();

    /// <summary>Maps BSG caliber name (e.g. "Caliber9x18PM") to short name (e.g. "9x18").</summary>
    [JsonPropertyName("BSGCalibers")]
    public Dictionary<string, string> BsgCalibers { get; set; } = new();

    [JsonPropertyName("rarities")]
    public List<string> Rarities { get; set; } = new();

    /// <summary>Per-caliber reward tables keyed by short name (e.g. "9x18").</summary>
    [JsonPropertyName("items")]
    public Dictionary<string, ContainerRewardData> Items { get; set; } = new();
}
