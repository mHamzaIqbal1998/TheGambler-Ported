using System.Text.Json;
using System.Text.Json.Serialization;

namespace TheGambler.Config;

/// <summary>
/// Strongly typed view over config/config.json (converted from the legacy
/// config.jsonc). The two large sections, <c>container_config</c> and
/// <c>odds</c>, use dynamic string keys (e.g. "wallet_price", "weapon_meta"),
/// so they are loaded as dictionaries with typed accessors.
/// </summary>
public record GamblerConfig
{
    [JsonPropertyName("debug")]
    public bool Debug { get; set; }

    [JsonPropertyName("trader_update_min_time")]
    public int TraderUpdateMinTime { get; set; } = 3600;

    [JsonPropertyName("trader_update_max_time")]
    public int TraderUpdateMaxTime { get; set; } = 4000;

    [JsonPropertyName("price_multiplier")]
    public double PriceMultiplier { get; set; } = 1.0;

    [JsonPropertyName("container_config")]
    public Dictionary<string, JsonElement> ContainerConfig { get; set; } = new();

    [JsonPropertyName("odds")]
    public Dictionary<string, double> Odds { get; set; } = new();

    [JsonPropertyName("mystery_container_override_enable")]
    public bool OverrideEnable { get; set; }

    [JsonPropertyName("mystery_container_override_price")]
    public Dictionary<string, Dictionary<string, double>> OverridePrice { get; set; } = new();

    // ----- container_config typed accessors -----

    public bool GetBool(string key, bool fallback = false)
    {
        if (ContainerConfig.TryGetValue(key, out var el) && el.ValueKind is JsonValueKind.True or JsonValueKind.False)
        {
            return el.GetBoolean();
        }
        return fallback;
    }

    public double GetNumber(string key, double fallback = 0)
    {
        if (ContainerConfig.TryGetValue(key, out var el) && el.ValueKind == JsonValueKind.Number)
        {
            return el.GetDouble();
        }
        return fallback;
    }

    public int GetInt(string key, int fallback = 0) => (int)GetNumber(key, fallback);

    /// <summary>True when the key exists in container_config.</summary>
    public bool Has(string key) => ContainerConfig.ContainsKey(key);

    // ----- odds accessors -----

    public double Odd(string key, double fallback = 0)
        => Odds.TryGetValue(key, out var v) ? v : fallback;

    public bool HasOdd(string key) => Odds.ContainsKey(key);

    /// <summary>Override price for an item under a parent group (ammo/armor/weapon/helmet), or 0.</summary>
    public double GetOverridePrice(string parent, string itemTpl)
    {
        if (OverridePrice.TryGetValue(parent, out var group) && group.TryGetValue(itemTpl, out var price))
        {
            return price;
        }
        return 0;
    }
}
