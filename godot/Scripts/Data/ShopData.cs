using System.Collections.Generic;
using System.Linq;

namespace ShotV.Data;

public sealed class ShopAmmoOffer
{
    public string ItemId { get; init; } = "";
    public string GroupId { get; init; } = "";
    public int PricePerRound { get; init; }
}

public static class ShopData
{
    public const int StartingCredits = 1500;
    public static readonly int[] PurchaseQuantities = { 10, 20, 30 };

    public static readonly ShopAmmoOffer[] AmmoOffers =
    {
        new() { ItemId = "ammo-mg-ball", GroupId = "automatic", PricePerRound = 4 },
        new() { ItemId = "ammo-mg-tracer", GroupId = "automatic", PricePerRound = 5 },
        new() { ItemId = "ammo-mg-hp", GroupId = "automatic", PricePerRound = 6 },
        new() { ItemId = "ammo-mg-bonded", GroupId = "automatic", PricePerRound = 7 },
        new() { ItemId = "ammo-mg-ap", GroupId = "automatic", PricePerRound = 8 },

        new() { ItemId = "ammo-gl-frag", GroupId = "launcher", PricePerRound = 6 },
        new() { ItemId = "ammo-gl-blast", GroupId = "launcher", PricePerRound = 7 },
        new() { ItemId = "ammo-gl-breach", GroupId = "launcher", PricePerRound = 9 },
        new() { ItemId = "ammo-gl-flechette", GroupId = "launcher", PricePerRound = 9 },
        new() { ItemId = "ammo-gl-arc", GroupId = "launcher", PricePerRound = 10 },

        new() { ItemId = "ammo-sn-match", GroupId = "precision", PricePerRound = 5 },
        new() { ItemId = "ammo-sn-overmatch", GroupId = "precision", PricePerRound = 7 },
        new() { ItemId = "ammo-sn-rupture", GroupId = "precision", PricePerRound = 8 },
        new() { ItemId = "ammo-sn-sabot", GroupId = "precision", PricePerRound = 9 },
        new() { ItemId = "ammo-sn-exp", GroupId = "precision", PricePerRound = 10 },
    };

    public static readonly Dictionary<string, ShopAmmoOffer> AmmoOffersByItemId = AmmoOffers.ToDictionary(offer => offer.ItemId);

    public static bool TryGetAmmoOffer(string itemId, out ShopAmmoOffer offer)
    {
        return AmmoOffersByItemId.TryGetValue(itemId, out offer!);
    }
}
