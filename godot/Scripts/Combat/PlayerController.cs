using Godot;
using ShotV.Core;
using ShotV.Data;
using System.Collections.Generic;
using System.Linq;

namespace ShotV.Combat;

public interface IPlayerControllerCallbacks
{
    void OnWeaponChanged(WeaponDefinition weapon, WeaponSlot slot, bool silent);
    void OnDash(Vector2 position);
    void OnFire(WeaponDefinition weapon, Vector2 aimPoint);
}

public class PlayerController
{
    private WeaponDefinition _currentWeapon = WeaponData.BySlot[WeaponSlot.Slot1];
    private List<WeaponDefinition> _loadout = WeaponData.Loadout.ToList();
    private WeaponSlot _currentSlot = WeaponSlot.Slot1;
    private Vector2 _lastAimPoint;
    private float _shotCooldown;

    public WeaponDefinition CurrentWeapon => _currentWeapon;
    public WeaponSlot CurrentSlot => _currentSlot;
    public IReadOnlyList<WeaponDefinition> Loadout => _loadout;
    public Vector2 LastAimPoint => _lastAimPoint;

    public void Tick(float delta)
    {
        _shotCooldown = Mathf.Max(0f, _shotCooldown - delta);
    }

    public void Reset(IReadOnlyList<WeaponType>? loadoutWeaponIds = null, Vector2? viewportCenter = null)
    {
        _loadout = BuildLoadout(loadoutWeaponIds);
        _currentWeapon = _loadout[0];
        _currentSlot = WeaponSlot.Slot1;
        _shotCooldown = 0f;
        if (viewportCenter.HasValue)
            _lastAimPoint = new Vector2(viewportCenter.Value.X, viewportCenter.Value.Y - 120f);
    }

    public void HandleInput(
        Vector2 moveIntent, bool dashPressed, bool shootHeld, bool hasPointer,
        Vector2 pointerWorld, WeaponSlot? weaponSwitch,
        PlayerAvatar player, Rect2 arenaBounds,
        IPlayerControllerCallbacks callbacks)
    {
        if (weaponSwitch.HasValue && weaponSwitch.Value != _currentSlot)
            ApplyWeapon(weaponSwitch.Value, callbacks, false);

        var aimPoint = ResolveAimPoint(hasPointer, pointerWorld, player, arenaBounds);
        var playerPos = player.PlayerPosition;
        float aimAngle = Mathf.Atan2(aimPoint.Y - playerPos.Y, aimPoint.X - playerPos.X);

        _lastAimPoint = aimPoint;
        player.SetAimAngle(aimAngle);
        player.SetMoveIntent(moveIntent.X, moveIntent.Y);

        if (dashPressed && player.RequestDash())
            callbacks.OnDash(playerPos);

        if (shootHeld && _shotCooldown == 0f && hasPointer)
        {
            callbacks.OnFire(_currentWeapon, aimPoint);
            _shotCooldown = _currentWeapon.Cooldown;
        }
    }

    public void NotifySilentWeaponState(IPlayerControllerCallbacks callbacks)
    {
        callbacks.OnWeaponChanged(_currentWeapon, _currentSlot, true);
    }

    public void RestoreWeapon(WeaponType weaponId, IPlayerControllerCallbacks callbacks)
    {
        int index = _loadout.FindIndex(weapon => weapon.Id == weaponId);
        if (index < 0)
            index = 0;

        _currentWeapon = _loadout[index];
        _currentSlot = (WeaponSlot)(index + 1);
        _shotCooldown = 0f;
        callbacks.OnWeaponChanged(_currentWeapon, _currentSlot, true);
    }

    private Vector2 ResolveAimPoint(bool hasPointer, Vector2 pointerWorld, PlayerAvatar player, Rect2 arenaBounds)
    {
        var playerPos = player.PlayerPosition;
        if (hasPointer)
        {
            var clamped = new Vector2(
                Mathf.Clamp(pointerWorld.X, arenaBounds.Position.X, arenaBounds.End.X),
                Mathf.Clamp(pointerWorld.Y, arenaBounds.Position.Y, arenaBounds.End.Y));
            return _currentWeapon.Id == WeaponType.Grenade
                ? MathUtil.ClampToDistance(clamped, playerPos, _currentWeapon.Range)
                : clamped;
        }

        float aimAngle = player.AimAngle;
        float idleDist = _currentWeapon.Id == WeaponType.Grenade
            ? Mathf.Min(_currentWeapon.Range, 120f)
            : 120f;
        return new Vector2(
            playerPos.X + Mathf.Cos(aimAngle) * idleDist,
            playerPos.Y + Mathf.Sin(aimAngle) * idleDist);
    }

    private void ApplyWeapon(WeaponSlot slot, IPlayerControllerCallbacks callbacks, bool silent)
    {
        int index = Mathf.Clamp((int)slot - 1, 0, _loadout.Count - 1);
        _currentWeapon = _loadout[index];
        _currentSlot = slot;
        _shotCooldown = 0f;
        callbacks.OnWeaponChanged(_currentWeapon, slot, silent);
    }

    private static List<WeaponDefinition> BuildLoadout(IReadOnlyList<WeaponType>? loadoutWeaponIds)
    {
        var result = new List<WeaponDefinition>();
        var seen = new HashSet<WeaponType>();

        if (loadoutWeaponIds != null)
        {
            foreach (var weaponId in loadoutWeaponIds)
            {
                if (!seen.Add(weaponId))
                    continue;

                if (WeaponData.ById.TryGetValue(weaponId, out var definition))
                    result.Add(definition);
            }
        }

        foreach (var weapon in WeaponData.Loadout)
        {
            if (seen.Add(weapon.Id))
                result.Add(weapon);
        }

        if (result.Count == 0)
            result.AddRange(WeaponData.Loadout);

        return result.Take(WeaponData.Loadout.Length).ToList();
    }
}
