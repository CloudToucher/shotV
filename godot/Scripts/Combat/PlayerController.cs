using System.Collections.Generic;
using System.Linq;
using Godot;
using ShotV.Core;
using ShotV.Data;
using ShotV.Inventory;
using ShotV.State;

namespace ShotV.Combat;

public interface IPlayerControllerCallbacks
{
    void OnWeaponChanged(WeaponDefinition weapon, WeaponSlot slot, bool silent);
    void OnDash(Vector2 position);
    bool OnFire(WeaponDefinition weapon, Vector2 aimPoint);
}

public class PlayerController
{
    private WeaponDefinition _currentWeapon = WeaponData.GetDefaultWeapon();
    private List<WeaponDefinition> _loadout = WeaponData.DefaultLoadout.ToList();
    private WeaponSlot _currentSlot = WeaponSlot.Slot1;
    private Vector2 _lastAimPoint;
    private float _shotCooldown;
    private bool _isReloading;
    private float _reloadTimer;
    private float _reloadDuration;
    private WeaponType? _reloadWeaponId;

    public WeaponDefinition CurrentWeapon => _currentWeapon;
    public WeaponSlot CurrentSlot => _currentSlot;
    public IReadOnlyList<WeaponDefinition> Loadout => _loadout;
    public Vector2 LastAimPoint => _lastAimPoint;
    public bool IsReloading => _isReloading;
    public WeaponType? ReloadWeaponId => _isReloading ? _reloadWeaponId : null;
    public float ReloadProgress => _isReloading && _reloadDuration > 0f
        ? Mathf.Clamp(1f - _reloadTimer / _reloadDuration, 0f, 1f)
        : 0f;

    public string? Tick(float delta, RunState? runState)
    {
        _shotCooldown = Mathf.Max(0f, _shotCooldown - delta);
        if (!_isReloading)
            return null;

        _reloadTimer = Mathf.Max(0f, _reloadTimer - delta);
        if (_reloadTimer > 0f)
            return null;

        _isReloading = false;
        if (_reloadWeaponId == null || runState == null || !WeaponData.ById.TryGetValue(_reloadWeaponId.Value, out var weapon))
        {
            _reloadWeaponId = null;
            _reloadDuration = 0f;
            return null;
        }

        runState.Player.EnsureWeaponStates();
        var state = runState.Player.EnsureWeaponState(weapon.Id);
        state.MagazineCapacity = weapon.MagazineCapacity;
        var ammo = WeaponData.GetAmmo(weapon, state.AmmoTypeId);
        int missing = Mathf.Max(0, weapon.MagazineCapacity - state.Magazine);
        int loaded = missing > 0
            ? GridInventory.ConsumeItemQuantity(runState.Inventory.Items, ammo.ReserveItemId, missing)
            : 0;
        state.Magazine = Mathf.Clamp(state.Magazine + loaded, 0, weapon.MagazineCapacity);

        _reloadWeaponId = null;
        _reloadDuration = 0f;
        if (loaded <= 0)
            return $"{weapon.Label} 无 {ammo.Label} 备弹";
        if (loaded < missing)
            return $"{weapon.Label} 装入 {loaded} 发 [{ammo.Label}]";
        return $"{weapon.Label} 装填完成 [{ammo.Label}]";
    }

    public void Reset(IReadOnlyList<WeaponType>? loadoutWeaponIds = null, Vector2? viewportCenter = null)
    {
        _loadout = BuildLoadout(loadoutWeaponIds);
        _currentWeapon = _loadout[0];
        _currentSlot = WeaponSlot.Slot1;
        _shotCooldown = 0f;
        CancelReload();
        if (viewportCenter.HasValue)
            _lastAimPoint = new Vector2(viewportCenter.Value.X, viewportCenter.Value.Y - 120f);
    }

    public bool MatchesLoadout(IReadOnlyList<WeaponType>? loadoutWeaponIds, WeaponType currentWeaponId)
    {
        var nextLoadout = BuildLoadout(loadoutWeaponIds);
        if (_loadout.Count != nextLoadout.Count)
            return false;

        for (int index = 0; index < _loadout.Count; index++)
        {
            if (_loadout[index].Id != nextLoadout[index].Id)
                return false;
        }

        return _currentWeapon.Id == currentWeaponId;
    }

    public void SyncLoadout(IReadOnlyList<WeaponType>? loadoutWeaponIds, WeaponType currentWeaponId, IPlayerControllerCallbacks callbacks)
    {
        var nextLoadout = BuildLoadout(loadoutWeaponIds);
        if (nextLoadout.Count == 0)
            return;

        _loadout = nextLoadout;
        int nextIndex = _loadout.FindIndex(weapon => weapon.Id == currentWeaponId);
        if (nextIndex < 0)
            nextIndex = 0;

        CancelReload();
        _currentWeapon = _loadout[nextIndex];
        _currentSlot = (WeaponSlot)(nextIndex + 1);
        _shotCooldown = 0f;
        callbacks.OnWeaponChanged(_currentWeapon, _currentSlot, true);
    }

    public void HandleInput(
        Vector2 moveIntent,
        bool dashPressed,
        bool shootHeld,
        bool hasPointer,
        Vector2 pointerWorld,
        WeaponSlot? weaponSwitch,
        PlayerAvatar player,
        Rect2 arenaBounds,
        IPlayerControllerCallbacks callbacks)
    {
        if (weaponSwitch.HasValue && weaponSwitch.Value != _currentSlot && (int)weaponSwitch.Value <= _loadout.Count)
        {
            CancelReload();
            ApplyWeapon(weaponSwitch.Value, callbacks, false);
        }

        var aimPoint = ResolveAimPoint(hasPointer, pointerWorld, player, arenaBounds);
        _lastAimPoint = aimPoint;
        player.SetAimTarget(aimPoint);
        player.SetMoveIntent(moveIntent.X, moveIntent.Y);

        if (dashPressed && player.RequestDash())
            callbacks.OnDash(player.PlayerPosition);

        if (!_isReloading && shootHeld && _shotCooldown == 0f && hasPointer)
        {
            if (callbacks.OnFire(_currentWeapon, aimPoint))
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

        CancelReload();
        _currentWeapon = _loadout[index];
        _currentSlot = (WeaponSlot)(index + 1);
        _shotCooldown = 0f;
        callbacks.OnWeaponChanged(_currentWeapon, _currentSlot, true);
    }

    public string? TryStartReload(RunState? runState)
    {
        if (runState == null)
            return null;

        runState.Player.EnsureWeaponStates();
        var weaponState = runState.Player.EnsureWeaponState(_currentWeapon.Id);
        if (_isReloading && _reloadWeaponId == _currentWeapon.Id)
            return $"{_currentWeapon.Label} 正在装填";

        weaponState.MagazineCapacity = _currentWeapon.MagazineCapacity;
        if (weaponState.Magazine >= weaponState.MagazineCapacity)
            return $"{_currentWeapon.Label} 弹匣已满";

        var ammo = WeaponData.GetAmmo(_currentWeapon, weaponState.AmmoTypeId);
        int reserve = GridInventory.CountItemQuantity(runState.Inventory.Items, ammo.ReserveItemId);
        if (reserve <= 0)
            return $"{_currentWeapon.Label} 无 {ammo.Label} 备弹";

        StartReload();
        return $"{_currentWeapon.Label} 装填中 [{ammo.Label}]";
    }

    public string? TryCycleAmmoType(RunState? runState)
    {
        if (runState == null)
            return null;

        runState.Player.EnsureWeaponStates();
        var weaponState = runState.Player.EnsureWeaponState(_currentWeapon.Id);
        weaponState.MagazineCapacity = _currentWeapon.MagazineCapacity;
        if (_currentWeapon.AmmoTypes.Length <= 1)
            return $"{_currentWeapon.Label} 没有可切换弹种";

        int currentIndex = System.Array.FindIndex(_currentWeapon.AmmoTypes, ammo => ammo.Id == weaponState.AmmoTypeId);
        if (currentIndex < 0)
            currentIndex = 0;

        for (int offset = 1; offset < _currentWeapon.AmmoTypes.Length; offset++)
        {
            var candidate = _currentWeapon.AmmoTypes[(currentIndex + offset) % _currentWeapon.AmmoTypes.Length];
            int reserve = GridInventory.CountItemQuantity(runState.Inventory.Items, candidate.ReserveItemId);
            if (reserve <= 0)
                continue;

            weaponState.AmmoTypeId = candidate.Id;
            weaponState.Magazine = 0;
            StartReload();
            return $"{_currentWeapon.Label} 切换至 {candidate.Label}，开始装填";
        }

        return $"{_currentWeapon.Label} 没有可切换备弹";
    }

    public string? TrySelectAmmoType(RunState? runState, string ammoTypeId)
    {
        if (runState == null || string.IsNullOrWhiteSpace(ammoTypeId))
            return null;

        runState.Player.EnsureWeaponStates();
        var weaponState = runState.Player.EnsureWeaponState(_currentWeapon.Id);
        weaponState.MagazineCapacity = _currentWeapon.MagazineCapacity;

        var targetAmmo = _currentWeapon.AmmoTypes.FirstOrDefault(ammo => ammo.Id == ammoTypeId);
        if (targetAmmo == null || weaponState.AmmoTypeId == targetAmmo.Id)
            return null;

        int reserve = GridInventory.CountItemQuantity(runState.Inventory.Items, targetAmmo.ReserveItemId);
        if (reserve <= 0)
            return null;

        weaponState.AmmoTypeId = targetAmmo.Id;
        weaponState.Magazine = 0;
        StartReload();
        return $"{_currentWeapon.Label} 切换至 {targetAmmo.Label}，开始装填";
    }

    public void CancelReload()
    {
        _isReloading = false;
        _reloadTimer = 0f;
        _reloadDuration = 0f;
        _reloadWeaponId = null;
    }

    private void StartReload()
    {
        _isReloading = true;
        _reloadWeaponId = _currentWeapon.Id;
        _reloadDuration = _currentWeapon.ReloadDuration;
        _reloadTimer = _reloadDuration;
        _shotCooldown = Mathf.Max(_shotCooldown, 0.08f);
    }

    private Vector2 ResolveAimPoint(bool hasPointer, Vector2 pointerWorld, PlayerAvatar player, Rect2 arenaBounds)
    {
        var playerPos = player.PlayerPosition;
        if (hasPointer)
        {
            var clamped = new Vector2(
                Mathf.Clamp(pointerWorld.X, arenaBounds.Position.X, arenaBounds.End.X),
                Mathf.Clamp(pointerWorld.Y, arenaBounds.Position.Y, arenaBounds.End.Y));
            return _currentWeapon.FireMode == WeaponFireMode.Launcher
                ? MathUtil.ClampToDistance(clamped, playerPos, _currentWeapon.Range)
                : clamped;
        }

        float aimAngle = player.AimAngle;
        float idleDist = _currentWeapon.FireMode == WeaponFireMode.Launcher
            ? Mathf.Min(_currentWeapon.Range, 120f)
            : 120f;
        return new Vector2(
            playerPos.X + Mathf.Cos(aimAngle) * idleDist,
            playerPos.Y + Mathf.Sin(aimAngle) * idleDist);
    }

    private void ApplyWeapon(WeaponSlot slot, IPlayerControllerCallbacks callbacks, bool silent)
    {
        int index = Mathf.Clamp((int)slot - 1, 0, _loadout.Count - 1);
        if (index >= _loadout.Count)
            return;

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

        if (result.Count == 0)
            result.AddRange(WeaponData.DefaultLoadout);

        return result.Take(WeaponData.MaxLoadoutSize).ToList();
    }
}
