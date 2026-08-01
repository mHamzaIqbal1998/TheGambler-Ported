using SPTarkov.Server.Core.Models.Spt.Mod;

namespace TheGambler;

/// <summary>
/// Replaces the old package.json. Holds the metadata the SPT 4.0 mod loader
/// reads when loading this mod. Every property must be overridden; unused ones
/// may be left null.
/// </summary>
public record ModMetadata : AbstractModMetadata
{
    public override string ModGuid { get; init; } = "com.hood.thegambler";
    public override string Name { get; init; } = "GamblerTrader";
    public override string Author { get; init; } = "Hood";
    public override List<string>? Contributors { get; init; }
    public override SemanticVersioning.Version Version { get; init; } = new("1.0.0");
    public override SemanticVersioning.Range SptVersion { get; init; } = new("~4.0.0");
    public override List<string>? Incompatibilities { get; init; }
    public override Dictionary<string, SemanticVersioning.Range>? ModDependencies { get; init; }
    public override string? Url { get; init; }
    public override bool? IsBundleMod { get; init; } = false;
    public override string License { get; init; } = "MIT";
}
