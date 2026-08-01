using System.Text.Json.Serialization;
using SPTarkov.Server.Core.Models.Common;

namespace TheGambler.Data;

/// <summary>
/// Metadata for a single mystery container, loaded from data/container-info.json
/// (generated from the legacy MysteryContainerInfo.ts). Describes the custom
/// item that gets created and sold by the trader.
/// </summary>
public record ContainerInfo
{
    [JsonPropertyName("_id")]
    public string Id { get; set; } = "";

    /// <summary>Internal name, e.g. "gambling_wallet". DO NOT change - the open logic keys off this.</summary>
    [JsonPropertyName("_name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("quest_id")]
    public string QuestId { get; set; } = "";

    [JsonPropertyName("name")]
    public string LocaleName { get; set; } = "";

    [JsonPropertyName("shortName")]
    public string ShortName { get; set; } = "";

    [JsonPropertyName("desc")]
    public string Description { get; set; } = "";

    [JsonPropertyName("fleaPriceRoubles")]
    public double FleaPriceRoubles { get; set; }

    [JsonPropertyName("handbookPriceRoubles")]
    public double HandbookPriceRoubles { get; set; }

    [JsonPropertyName("prefab")]
    public string Prefab { get; set; } = "";

    [JsonPropertyName("width")]
    public int Width { get; set; } = 1;

    [JsonPropertyName("height")]
    public int Height { get; set; } = 1;

    /// <summary>Optional barter cost: itemTpl -> amount. When present the item is bought by barter instead of roubles.</summary>
    [JsonPropertyName("barter")]
    public Dictionary<string, int>? Barter { get; set; }
}
