using System;
using System.Collections.Generic;
using ShotV.Core;

namespace ShotV.Data;

public struct SpawnOrder
{
    public HostileType Type;
    public float Delay;
}

public static class WaveData
{
    private static readonly Random _rng = new();

    public static List<SpawnOrder> BuildWaveOrders(int wave)
    {
        if (wave >= 5)
        {
            return new List<SpawnOrder> { new SpawnOrder { Type = HostileType.Boss, Delay = 0.35f } };
        }

        var orders = new List<SpawnOrder>();
        int meleeCount = 2 + wave;
        int rangedCount = wave >= 2 ? 1 + (wave - 2) / 2 : 0;
        int chargerCount = wave >= 3 ? 1 + (wave - 3) / 2 : 0;

        PushOrders(orders, meleeCount, HostileType.Melee);
        PushOrders(orders, rangedCount, HostileType.Ranged);
        PushOrders(orders, chargerCount, HostileType.Charger);

        if (wave >= 4 && wave % 2 == 0)
            PushOrders(orders, 1, HostileType.Ranged);

        Shuffle(orders);
        return orders;
    }

    public static string BuildWaveHint(int wave)
    {
        if (wave >= 5) return GameText.Text("wave.boss");
        if (wave < 2) return GameText.Text("wave.melee");
        if (wave < 3) return GameText.Text("wave.ranged");
        return GameText.Text("wave.charger");
    }

    private static void PushOrders(List<SpawnOrder> target, int count, HostileType type)
    {
        for (int i = 0; i < count; i++)
        {
            target.Add(new SpawnOrder
            {
                Type = type,
                Delay = 0.28f + (float)_rng.NextDouble() * 0.18f,
            });
        }
    }

    private static void Shuffle<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = _rng.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}
