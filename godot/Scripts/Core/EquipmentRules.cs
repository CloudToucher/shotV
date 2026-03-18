using Godot;
using ShotV.Data;
using ShotV.State;

namespace ShotV.Core;

public static class EquipmentRules
{
    public const int MaxWeaponUpgradeLevel = 3;
    public const int MaxArmorUpgradeLevel = 3;

    public static float GetDurabilityRatio(float current, float max)
    {
        return max <= 0.001f ? 0f : Mathf.Clamp(current / max, 0f, 1f);
    }

    public static float GetWeaponDamageMultiplier(PlayerRunState.PlayerWeaponState state)
    {
        float durabilityRatio = GetDurabilityRatio(state.Durability, state.MaxDurability);
        float wearScale = Mathf.Lerp(0.72f, 1f, durabilityRatio);
        float upgradeScale = 1f + state.UpgradeLevel * 0.06f;
        return wearScale * upgradeScale;
    }

    public static float GetWeaponSpreadMultiplier(PlayerRunState.PlayerWeaponState state)
    {
        float durabilityRatio = GetDurabilityRatio(state.Durability, state.MaxDurability);
        float wearPenalty = 1f + (1f - durabilityRatio) * 0.65f;
        float upgradeOffset = 1f - state.UpgradeLevel * 0.05f;
        return Mathf.Clamp(wearPenalty * upgradeOffset, 0.72f, 1.65f);
    }

    public static int GetWeaponArmorPenetrationBonus(PlayerRunState.PlayerWeaponState state)
    {
        return state.UpgradeLevel >= 2 ? 1 : 0;
    }

    public static float GetWeaponDurabilityLoss(WeaponDefinition weapon, WeaponAmmoDefinition ammo)
    {
        float loss = weapon.DurabilityLossPerShot;
        if (weapon.FireMode == WeaponFireMode.Launcher)
            loss *= 1.28f;
        if (ammo.ArmorPenetration >= 3)
            loss *= 1.14f;
        if (ammo.PierceCount >= 3)
            loss *= 1.08f;
        return loss;
    }

    public static ResourceBundle GetWeaponRepairCost(WeaponBenchState state)
    {
        if (!WeaponData.ById.TryGetValue(state.WeaponId, out var weapon))
            return ResourceBundle.Zero();

        return BuildRepairCost(weapon.RepairUnitCost, state.Durability, state.MaxDurability);
    }

    public static ResourceBundle GetWeaponUpgradeCost(WeaponDefinition weapon, int nextLevel)
    {
        return ScaleCost(weapon.UpgradeBaseCost, Mathf.Max(1, nextLevel));
    }

    public static float GetArmorMitigation(PlayerRunState.PlayerArmorState? armor)
    {
        if (armor == null || string.IsNullOrWhiteSpace(armor.ArmorId))
            return 0f;
        if (!ArmorData.ById.TryGetValue(armor.ArmorId, out var definition))
            return 0f;

        float durabilityRatio = GetDurabilityRatio(armor.Durability, armor.MaxDurability);
        float baseMitigation = definition.Mitigation + armor.UpgradeLevel * 0.035f;
        return Mathf.Clamp(baseMitigation * (0.45f + durabilityRatio * 0.55f), 0f, 0.72f);
    }

    public static float GetArmorMaxHealthBonus(PlayerRunState.PlayerArmorState? armor)
    {
        if (armor == null || string.IsNullOrWhiteSpace(armor.ArmorId))
            return 0f;
        if (!ArmorData.ById.TryGetValue(armor.ArmorId, out var definition))
            return 0f;

        return definition.MaxHealthBonus + armor.UpgradeLevel * 6f;
    }

    public static float GetArmorDurabilityLoss(PlayerRunState.PlayerArmorState armor, float incomingDamage, float absorbedDamage)
    {
        float loss = absorbedDamage * 0.42f + incomingDamage * 0.08f;
        float upgradeScale = Mathf.Max(0.72f, 1f - armor.UpgradeLevel * 0.06f);
        return Mathf.Max(0.35f, loss * upgradeScale);
    }

    public static ResourceBundle GetArmorRepairCost(ArmorBenchState state)
    {
        if (!ArmorData.ById.TryGetValue(state.ArmorId, out var armor))
            return ResourceBundle.Zero();

        return BuildRepairCost(armor.RepairUnitCost, state.Durability, state.MaxDurability);
    }

    public static ResourceBundle GetArmorUpgradeCost(ArmorDefinition armor, int nextLevel)
    {
        return ScaleCost(armor.UpgradeBaseCost, Mathf.Max(1, nextLevel));
    }

    private static ResourceBundle BuildRepairCost(ResourceBundle unitCost, float currentDurability, float maxDurability)
    {
        float missing = Mathf.Max(0f, maxDurability - currentDurability);
        if (missing <= 0.001f)
            return ResourceBundle.Zero();

        int repairUnits = Mathf.Max(1, Mathf.CeilToInt(missing / 20f));
        return ScaleCost(unitCost, repairUnits);
    }

    private static ResourceBundle ScaleCost(ResourceBundle cost, int factor)
    {
        return new ResourceBundle
        {
            Salvage = cost.Salvage * factor,
            Alloy = cost.Alloy * factor,
            Research = cost.Research * factor,
        };
    }
}
