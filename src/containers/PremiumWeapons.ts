export class PremiumWeapons {

  public parent = "weapon";
  public is_preset = true;
  public price_generate = true;
  public calculate_preset_prices = false;

  public rarities = [
    "_meta",
  ]

  // NOTE: premium_weapon draws its actual presets from the parent "weapon"
  // pool at open/price time. The legacy `rewards = ["meta"]` field was an
  // unused string placeholder and has been removed (it broke typed loading).
}
