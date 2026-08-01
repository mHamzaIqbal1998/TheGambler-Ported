# The Gambler Trader

A trader for SPT that sells "mystery containers" you can gamble on: open them to
win weapons, armor, ammo, keys, whole loadouts, coin flips and more. Prices are
balanced automatically from live flea/trader prices.

Originally a TypeScript mod for SPT 3.11 (by Hood). This repository is the
**SPT 4.0.13 C# port**.

Original hub page: https://hub.sp-tarkov.com/files/file/2013-the-gambler-trader/

## Requirements

- [.NET 9 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/9.0)
- SPT `4.0.x` (built and tested against `4.0.13`)

## Building

```
dotnet build -c Release
```

The build produces a ready-to-ship mod folder at:

```
bin/Release/TheGambler/
```

It contains the compiled `Hood-TheGambler.dll` plus the `config/`, `data/`,
`db/` and `res/` folders.

## Installing

Copy the `bin/Release/TheGambler/` folder into your SPT install:

```
<SPT>/user/mods/TheGambler/
```

Then start the server. The Gambler trader appears in-game (unlocked by default).

> Server mods only load with the server closed. Close SPT before adding/updating.

## Configuration

`config/config.json` controls everything: which containers the trader sells,
prices, stock, gambling odds, found-in-raid status, and the automatic price
balancer's profit margins and override prices.

- `*_manual_pricing: false` means the price is auto-generated from reward values.
- `*_manual_pricing: true` uses the `*_price` value as-is.
- `price_multiplier` scales all rouble prices at once.

## Project layout

```
Mod/                        C# source (the only code compiled into the mod)
  ModMetadata.cs            Mod manifest (replaces the old package.json)
  GamblerMod.cs             Entry point: registers trader, items, assorts
  Config/GamblerConfig.cs   Typed view over config.json
  Data/                     Data models + loader for the JSON below
  Items/ItemFactory.cs      Creates the custom mystery-container items
  Trader/                   Trader registration + fluent assort builder
  Gamble/                   Container-opening logic + odds + Harmony patch
  Pricing/PriceGenerator.cs Automatic price balancer (runs post-load)

config/config.json          Runtime config (converted from legacy config.jsonc)
data/container-info.json    Container metadata (id, prefab, price, barter)
data/containers/*.json      Reward tables + weapon/armor/helmet preset builds
db/base.json                Trader base definition
res/thegambler.jpg          Trader avatar

src/                        Legacy 3.11 TypeScript sources (reference only)
tools/                      Dev-only scripts (data conversion + API probe)
```

## How the data was ported

The legacy mod stored ~130k lines of reward/preset data in TypeScript classes
under `src/containers/`. Rather than hand-porting that data, the scripts in
`tools/` convert it to JSON that the C# mod loads at runtime:

- `tools/convert-containers.mjs` - container reward tables -> `data/containers/*.json`
- `tools/convert-config.mjs` - `config.jsonc` -> `config.json` and container metadata -> `data/container-info.json`

Re-run them (with Node 22.18+/24) if you change the legacy `src/` data:

```
node tools/convert-containers.mjs
node tools/convert-config.mjs
```

## License

MIT
