using System.Collections.Generic;
using Godot;
using ShotV.Combat;
using ShotV.Core;
using ShotV.Data;
using ShotV.State;
using ShotV.UI;
using ShotV.World;

namespace ShotV.Scenes;

public partial class CombatScene : Node2D, IPlayerControllerCallbacks, IEncounterCallbacks, IOverlaySceneDataProvider
{
    private PlayerAvatar _player = null!;
    private CombatRenderer _renderer = null!;
    private VfxManager _vfx = null!;
    private DamageTextManager _dmgText = null!;
    private Camera2D _camera = null!;
    private HUD? _hud;
    private Minimap? _minimap;

    private PlayerController _controller = new();
    private EncounterManager _encounter = new();
    private ExitActionManager _exitAction = new();
    private WorldMapLayout _layout = null!;

    private float _elapsed;
    private float _syncTimer;
    private float _shakeTrauma;
    private float _rewardMultiplier = 1f;
    private float _weaponSpreadBloom;
    private float _moveSpreadRatio;
    private bool _isActive;
    private bool _downHandled;
    private bool _reloadHoldActive;
    private float _reloadHoldTimer;
    private const float SyncInterval = 0.2f;

    public override void _Ready()
    {
        _player = GetNode<PlayerAvatar>("Player");
        _renderer = GetNode<CombatRenderer>("CombatRenderer");
        _vfx = GetNode<VfxManager>("VfxLayer");
        _dmgText = GetNode<DamageTextManager>("DamageTextLayer");
        _camera = GetNode<Camera2D>("Camera");
        _hud = GetNodeOrNull<HUD>("HUDLayer/HUD");
        _minimap = GetNodeOrNull<Minimap>("HUDLayer/Minimap");

        _exitAction.Completed += OnExitActionCompleted;
        _vfx.GrenadeDetonated += OnGrenadeDetonated;
    }

    public void StartEncounter(RunMapState mapState)
    {
        var currentZone = RouteManager.GetCurrentRunZone(mapState);
        if (currentZone == null) return;

        _layout = WorldLayoutBuilder.CreateCombatLayout(new CombatLayoutInput
        {
            MapId = mapState.RouteId,
            MapLabel = RouteData.GetRoute(mapState.RouteId).Label,
            Regions = mapState.Zones,
            Seed = mapState.LayoutSeed,
        });

        _player.Reset();
        _player.SetWeaponStyle(GameManager.Instance?.Store.State.Save.Session.ActiveRun?.Player.CurrentWeaponId ?? WeaponType.MachineGun);
        _player.SetPlayerPosition(_layout.PlayerSpawn.X, _layout.PlayerSpawn.Y);

        _encounter.Reset();
        _encounter.Resize(_layout.Bounds, _layout.Obstacles, _layout.Regions, _layout.SpawnAnchors);

        _renderer.Bind(_layout, _encounter, _player);
        _dmgText.Reset();

        var run = GameManager.Instance?.Store.State.Save.Session.ActiveRun;
        _controller.Reset(run?.Player.LoadoutWeaponIds, new Vector2(_layout.Bounds.Size.X / 2f, _layout.Bounds.Size.Y / 2f));
        run?.Player.EnsureWeaponStates();
        if (run != null)
            _controller.RestoreWeapon(run.Player.CurrentWeaponId, this);
        else
            _controller.NotifySilentWeaponState(this);

        if (run != null) _renderer.BindGroundLoot(run.GroundLoot);

        _vfx.Reset();
        _exitAction.Cancel();
        _camera.Position = _player.PlayerPosition;
        _elapsed = 0f;
        _syncTimer = SyncInterval;
        _shakeTrauma = 0f;
        _rewardMultiplier = currentZone.RewardMultiplier;
        _weaponSpreadBloom = 0f;
        _moveSpreadRatio = 0f;
        _isActive = true;
        _downHandled = false;
        _reloadHoldActive = false;
        _reloadHoldTimer = 0f;
        _player.SetReticleBloom(0f);

        float startHealth = run?.Player.Health ?? CombatConstants.PlayerMaxHealth;
        float startMaxHealth = run?.Player.MaxHealth ?? CombatConstants.PlayerMaxHealth;
        _player.SetLifeRatio(startHealth / Mathf.Max(1f, startMaxHealth));
        _hud?.UpdateHealth(startHealth, startMaxHealth);
        _hud?.UpdateWave(currentZone.ThreatLevel, 0);
        _hud?.UpdateEnemyStatus(0, 0);
        _hud?.UpdateQuickSlots(run?.Inventory ?? new GridInventoryState());
        _hud?.UpdateLoadout(_controller.Loadout, _controller.CurrentWeapon.Id);
        RefreshWeaponRuntime(run);
        _hud?.HideBossHealth();
        _hud?.SetPlayerDown(false);
        _hud?.ShowHint(currentZone.Description, 4f);
        if (run != null)
            SyncCurrentRegion(run);
        UpdateCombatRuntime(run);
    }

    public override void _Process(double delta)
    {
        if (!_isActive) return;
        float dt = (float)delta;
        HandleOverlayShortcuts();
        _elapsed += dt;
        _shakeTrauma = Mathf.Max(0f, _shakeTrauma - dt * 2.4f);

        var run = GameManager.Instance?.Store.State.Save.Session.ActiveRun;
        bool fullscreenUi = IsFullscreenUiActive();
        bool actionLocked = _exitAction.IsActive || fullscreenUi;
        if (_hud != null)
            _hud.Visible = !fullscreenUi;
        if (_minimap != null)
            _minimap.Visible = !fullscreenUi;

        // Gather input
        var moveIntent = new Vector2(
            (Input.IsActionPressed("move_right") ? 1f : 0f) - (Input.IsActionPressed("move_left") ? 1f : 0f),
            (Input.IsActionPressed("move_down") ? 1f : 0f) - (Input.IsActionPressed("move_up") ? 1f : 0f));
        bool dashPressed = !fullscreenUi && Input.IsActionJustPressed("dash");
        bool shootHeld = !fullscreenUi && Input.IsMouseButtonPressed(MouseButton.Left);
        var mousePos = GetGlobalMousePosition();
        bool hasPointer = true;
        bool interactPressed = !fullscreenUi && Input.IsActionJustPressed("interact");

        WeaponSlot? weaponSwitch = null;
        if (!fullscreenUi)
        {
            if (Input.IsActionJustPressed("weapon_1")) weaponSwitch = WeaponSlot.Slot1;
            else if (Input.IsActionJustPressed("weapon_2")) weaponSwitch = WeaponSlot.Slot2;
            else if (Input.IsActionJustPressed("weapon_3")) weaponSwitch = WeaponSlot.Slot3;
        }

        bool canControl = !actionLocked && run?.Status == RunStateStatus.Active && _encounter.State != EncounterState.Down;
        _moveSpreadRatio = canControl ? Mathf.Clamp(moveIntent.Length(), 0f, 1f) : 0f;

        var controllerHint = _controller.Tick(dt, run);
        if (!string.IsNullOrWhiteSpace(controllerHint))
            _hud?.ShowHint(controllerHint!, 1.6f);

        HandleReloadInput(dt, canControl, fullscreenUi, run);
        if (canControl)
            _controller.HandleInput(moveIntent, dashPressed, shootHeld, hasPointer, mousePos, weaponSwitch, _player, _layout.Bounds, this);
        else
            _player.SetMoveIntent(0, 0);

        _player.UpdatePhysics(dt, _layout.Bounds, _layout.Obstacles);
        UpdateWeaponFeel(dt);
        SyncCurrentRegion(run);
        _encounter.Update(dt, _elapsed, _player.PlayerPosition, _player.CollisionRadius, _player.IsDashing, this);
        _vfx.Tick(dt);
        _dmgText.Tick(dt);
        _exitAction.Tick(dt, _player.PlayerPosition, _layout);

        // Interact: try loot pickup first, then exit action
        if (canControl && interactPressed)
        {
            if (run != null && !TryPickupNearbyLoot(run))
                TryStartExitAction(run);
        }

        // Camera follow with shake
        var targetCam = _player.PlayerPosition;
        _camera.Position = _camera.Position.Lerp(targetCam, Mathf.Min(1f, 6f * dt));
        if (_shakeTrauma > 0.01f)
        {
            float shakeMag = _shakeTrauma * _shakeTrauma * 8f;
            _camera.Offset = new Vector2(
                (float)(GD.Randf() * 2 - 1) * shakeMag,
                (float)(GD.Randf() * 2 - 1) * shakeMag);
        }
        else
        {
            _camera.Offset = Vector2.Zero;
        }

        // HUD updates
        _hud?.UpdateDashCooldown(_player.DashCooldownRatio);
        if (_exitAction.IsActive)
            _hud?.ShowHint(_exitAction.GetProgressHint(), 0.5f);
        SyncHudState(run);
        UpdateCombatRuntime(run);

        // Minimap
        if (!fullscreenUi)
            _minimap?.UpdateData(_layout.Bounds, _player.PlayerPosition, _encounter.Enemies, _layout.Obstacles, _layout.Markers);

        if (run != null)
            _renderer.BindGroundLoot(run.GroundLoot);
        _renderer.Refresh();

        // Quick slots
        if (canControl && _encounter.State == EncounterState.Active)
            CheckQuickSlotInput();

        // Periodic sync
        _syncTimer -= dt;
        if (_syncTimer <= 0f && run != null)
        {
            run.Stats.ElapsedSeconds = _elapsed;
            GameManager.Instance?.Store.SyncActiveRun(run);
            _syncTimer = SyncInterval;
        }

        // Check encounter state transitions
        if (_encounter.State == EncounterState.Down && !_downHandled)
            OnPlayerDowned();
    }

    private void AddShake(float amount) => _shakeTrauma = Mathf.Min(1f, _shakeTrauma + amount);

    private bool TryPickupNearbyLoot(RunState run)
    {
        var drop = LootManager.FindNearbyGroundLoot(_player.PlayerPosition, run.GroundLoot);
        if (drop == null) return false;
        var pickup = LootManager.TryPickupLoot(drop, run.Inventory, run.GroundLoot);
        if (pickup.PickedUp)
        {
            _hud?.ShowHint($"拾取 {(ItemData.ById.TryGetValue(drop.Item.ItemId, out var d) ? d.Label : drop.Item.ItemId)}", 1.5f);
            _vfx.SpawnRing(drop.X, drop.Y, 8, 32, 0.18f, Palette.MinimapMarker, 2.5f);
            return true;
        }
        _hud?.ShowHint("背包已满", 1.5f);
        return false;
    }

    private void TryStartExitAction(RunState run)
    {
        if (_encounter.State != EncounterState.Active) return;
        _exitAction.TryStart(_player.PlayerPosition, _layout, run.Map);
    }

    private void OnExitActionCompleted(ExitActionKind kind)
    {
        var store = GameManager.Instance?.Store;
        if (store == null) return;
        _syncTimer = SyncInterval;
        store.MarkRunOutcome(RunResolutionOutcome.Extracted);
        _hud?.ShowHint("撤离校验完成，等待结算确认", 3f);
        GD.Print("[CombatScene] Extraction triggered.");
    }

    private void OnGrenadeDetonated(GrenadeDetonationPayload payload)
    {
        _encounter.ApplyExplosionDamage(payload.Position.X, payload.Position.Y, payload.Radius, payload.Damage, payload.ArmorPenetration, payload.PierceCount, this);
        AddShake(0.12f);
    }

    private void CheckQuickSlotInput()
    {
        var store = GameManager.Instance?.Store;
        var run = store?.State.Save.Session.ActiveRun;
        if (run == null) return;

        for (int i = 0; i < GridInventoryState.RunQuickSlotCount; i++)
        {
            if (!Input.IsActionJustPressed($"quick_slot_{i + 1}")) continue;
            var slotId = i < run.Inventory.QuickSlots.Length ? run.Inventory.QuickSlots[i] : null;
            if (slotId == null) continue;

            var itemRecord = run.Inventory.Items.Find(it => it.Id == slotId);
            if (itemRecord == null) continue;
            if (!ItemData.ById.TryGetValue(itemRecord.ItemId, out var def)) continue;
            if (def.Use == null) continue;

            UseConsumable(run, itemRecord, def);
        }
    }

    private void UseConsumable(RunState run, InventoryItemRecord record, ItemDefinition def)
    {
        if (def.Use!.Heals > 0)
        {
            run.Player.Health = Mathf.Min(run.Player.MaxHealth, run.Player.Health + def.Use.Heals);
            _player.SetLifeRatio(run.Player.Health / run.Player.MaxHealth);
            _hud?.UpdateHealth(run.Player.Health, run.Player.MaxHealth);
            _dmgText.SpawnHealText(_player.PlayerPosition.X, _player.PlayerPosition.Y, def.Use.Heals);
        }

        if (def.Use.ExplosionDamage > 0)
        {
            _encounter.ApplyExplosionDamage(_player.PlayerPosition.X, _player.PlayerPosition.Y, def.Use.ExplosionRadius, def.Use.ExplosionDamage, this);
        }

        if (def.Use.RefreshDash)
        {
            _player.RefreshDashCharge();
        }

        record.Quantity--;
        if (record.Quantity <= 0)
            run.Inventory.Items.Remove(record);
    }

    private void OnPlayerDowned()
    {
        _downHandled = true;
        var store = GameManager.Instance?.Store;
        store?.MarkRunOutcome(RunResolutionOutcome.Down);
        _hud?.ShowHint("行动失败", 4f);
        _hud?.SetPlayerDown(true);
        GD.Print("[CombatScene] Player downed.");
    }

    // IPlayerControllerCallbacks
    public void OnWeaponChanged(WeaponDefinition weapon, WeaponSlot slot, bool silent)
    {
        var run = GameManager.Instance?.Store.State.Save.Session.ActiveRun;
        if (run != null)
        {
            run.Player.EnsureWeaponStates();
            run.Player.CurrentWeaponId = weapon.Id;
        }

        _player.SetWeaponStyle(weapon.Id);
        _weaponSpreadBloom = 0f;
        _player.SetReticleBloom(0f);
        _hud?.UpdateWeapon(weapon);
        _hud?.UpdateLoadout(_controller.Loadout, weapon.Id);
        RefreshWeaponRuntime(run);
        if (!silent) _player.TriggerWeaponSwap();
    }

    public void OnDash(Vector2 position)
    {
        var run = GameManager.Instance?.Store.State.Save.Session.ActiveRun;
        if (run != null) run.Player.DashesUsed++;
        _vfx.SpawnRing(position.X, position.Y, 12, 64, 0.2f, Palette.Dash, 3f);
        _vfx.SpawnAfterimage(position, _player.AimAngle, _controller.CurrentWeapon.Id);
        AddShake(0.08f);
    }

    public bool OnFire(WeaponDefinition weapon, Vector2 aimPoint)
    {
        var store = GameManager.Instance?.Store;
        var run = store?.State.Save.Session.ActiveRun;
        if (run == null)
            return false;

        run.Player.EnsureWeaponStates();
        var weaponState = run.Player.EnsureWeaponState(weapon.Id);
        weaponState.MagazineCapacity = weapon.MagazineCapacity;
        var ammo = WeaponData.GetAmmo(weapon, weaponState.AmmoTypeId);
        if (weaponState.Magazine <= 0)
        {
            _hud?.ShowHint($"{weapon.Label} 空仓，按 R 换弹", 1.25f);
            return false;
        }

        float spreadDegrees = ResolveShotSpreadDegrees(weapon);
        weaponState.Magazine = Mathf.Max(0, weaponState.Magazine - 1);
        RefreshWeaponRuntime(run);
        ApplyShotFeel(weapon);

        switch (weapon.Id)
        {
            case WeaponType.MachineGun:
                FireMachineGun(weapon, ammo, aimPoint, spreadDegrees);
                _encounter.NotifyStimulus(_player.PlayerPosition, 360f);
                run.Player.ShotsFired++;
                return true;
            case WeaponType.Sniper:
                FireSniper(weapon, ammo, aimPoint, spreadDegrees);
                _encounter.NotifyStimulus(_player.PlayerPosition, 520f);
                run.Player.ShotsFired++;
                return true;
            case WeaponType.Grenade:
                FireGrenade(weapon, ammo, aimPoint, spreadDegrees);
                _encounter.NotifyStimulus(_player.PlayerPosition, 240f);
                run.Player.GrenadesThrown++;
                return true;
        }

        return false;
    }

    private void FireMachineGun(WeaponDefinition weapon, WeaponAmmoDefinition ammo, Vector2 aimPoint, float spreadDegrees)
    {
        var origin = _player.GetShotOrigin(38f);
        var farTarget = ResolveSpreadTarget(origin, aimPoint, weapon.Range, spreadDegrees);
        var clipped = WorldCollision.ClipSegmentToWorld(origin, farTarget, _layout.Bounds, _layout.Obstacles);
        var hits = _encounter.ResolveSegmentHits(origin, clipped, weapon.EffectWidth * 0.65f);

        int hitCapacity = ResolveHitCapacity(ammo);
        for (int index = 0; index < hits.Count && index < hitCapacity; index++)
            ApplyAmmoHit(hits[index], ammo, index);

        var trailEnd = hits.Count > hitCapacity && hitCapacity > 0
            ? new Vector2(hits[hitCapacity - 1].PointX, hits[hitCapacity - 1].PointY)
            : clipped;

        var trailColor = ResolveAmmoFeedbackColor(ammo);
        _vfx.SpawnNeedle(origin, trailEnd, weapon.EffectWidth, weapon.EffectDuration, trailColor, Palette.Accent);
        _vfx.SpawnRing(origin.X, origin.Y, 4, 14, 0.08f, trailColor, 2f);
        AddShake(0.02f);
    }

    private void FireSniper(WeaponDefinition weapon, WeaponAmmoDefinition ammo, Vector2 aimPoint, float spreadDegrees)
    {
        var origin = _player.GetShotOrigin(38f);
        var farTarget = ResolveSpreadTarget(origin, aimPoint, weapon.Range, spreadDegrees);
        var clipped = WorldCollision.ClipSegmentToWorld(origin, farTarget, _layout.Bounds, _layout.Obstacles);
        var hits = _encounter.ResolveSegmentHits(origin, clipped, weapon.EffectWidth * 0.65f);

        int hitCapacity = ResolveHitCapacity(ammo);
        for (int index = 0; index < hits.Count && index < hitCapacity; index++)
            ApplyAmmoHit(hits[index], ammo, index);

        var trailEnd = hits.Count > hitCapacity && hitCapacity > 0
            ? new Vector2(hits[hitCapacity - 1].PointX, hits[hitCapacity - 1].PointY)
            : clipped;

        var trailColor = ResolveAmmoFeedbackColor(ammo);
        _vfx.SpawnNeedle(origin, trailEnd, weapon.EffectWidth, weapon.EffectDuration, trailColor, Palette.PlayerCore);
        _vfx.SpawnRing(origin.X, origin.Y, 6, 22, 0.12f, trailColor, 3f);
        AddShake(0.06f);
    }

    private void FireGrenade(WeaponDefinition weapon, WeaponAmmoDefinition ammo, Vector2 aimPoint, float spreadDegrees)
    {
        var origin = _player.GetShotOrigin(38f);
        var jitteredTarget = ResolveSpreadTarget(origin, aimPoint, weapon.Range, spreadDegrees, preserveDistance: true);
        var landingPoint = ResolveGrenadeLandingPoint(origin, jitteredTarget);
        float duration = Mathf.Lerp(0.22f, 0.46f, Mathf.Clamp(origin.DistanceTo(landingPoint) / Mathf.Max(1f, weapon.Range), 0f, 1f));
        _vfx.SpawnGrenade(origin, landingPoint, weapon.SplashRadius, ammo.Damage, ammo.ArmorPenetration, ammo.PierceCount, duration);
        _vfx.SpawnRing(origin.X, origin.Y, 8, 26, 0.16f, ResolveAmmoFeedbackColor(ammo), 3f);
        AddShake(0.07f);
    }

    // IEncounterCallbacks
    public void OnWaveStarted(int waveIndex, string hint)
    {
    }

    public void OnEnemySpawned(HostileType type)
    {
        var run = GameManager.Instance?.Store.State.Save.Session.ActiveRun;
        if (run != null) run.Map.HostilesRemaining++;
    }

    public void OnBossSpawned(EnemyActor enemy)
    {
        var run = GameManager.Instance?.Store.State.Save.Session.ActiveRun;
        if (run != null)
        {
            run.Map.Boss.Spawned = true;
            run.Map.Boss.Label = enemy.Definition.Label;
            run.Map.Boss.Phase = enemy.Phase;
            run.Map.Boss.Health = enemy.Health;
            run.Map.Boss.MaxHealth = enemy.Definition.MaxHealth;
        }
        _vfx.SpawnRing(enemy.X, enemy.Y, 18, 92, 0.28f, Palette.Warning, 4f);
        AddShake(0.18f);
        _hud?.ShowHint($"{enemy.Definition.Label} 出现", 3f);
    }

    public void OnBossPhaseShift(EnemyActor enemy)
    {
        var run = GameManager.Instance?.Store.State.Save.Session.ActiveRun;
        if (run != null) run.Map.Boss.Phase = enemy.Phase;
        _vfx.SpawnRing(enemy.X, enemy.Y, 24, 118, 0.34f, Palette.Danger, 4f);
        AddShake(0.32f);
        _hud?.ShowHint("主核进入强化阶段", 2.5f);
    }

    public void OnBossAttack(BossPattern pattern, EnemyActor enemy, float? targetAngle)
    {
        float radius = pattern == BossPattern.Fan ? 88f : 112f;
        _vfx.SpawnRing(enemy.X, enemy.Y, 20, radius, 0.24f, Palette.AccentSoft, 3f);
        AddShake(pattern == BossPattern.Fan ? 0.08f : 0.16f);
    }

    public void OnEnemyHit(EnemyActor enemy, float amount, float impactX, float impactY)
    {
        _vfx.SpawnRing(impactX, impactY, 6, 24, 0.12f, Palette.AccentSoft, 2f);
        _dmgText.SpawnDamageText(impactX, impactY, amount, amount >= CombatConstants.SniperDamage);
        AddShake(enemy.Type == HostileType.Boss ? 0.08f : 0.03f);

        if (enemy.Type == HostileType.Boss)
        {
            var run = GameManager.Instance?.Store.State.Save.Session.ActiveRun;
            if (run != null)
            {
                run.Map.Boss.Health = enemy.Health;
                run.Map.Boss.MaxHealth = enemy.Definition.MaxHealth;
                run.Map.Boss.Phase = enemy.Phase;
            }
        }
    }

    public void OnEnemyKilled(EnemyActor enemy)
    {
        var run = GameManager.Instance?.Store.State.Save.Session.ActiveRun;
        if (run != null)
        {
            run.Stats.Kills++;
            run.Map.HostilesRemaining = Mathf.Max(0, run.Map.HostilesRemaining - 1);
            var rewardPosition = new Vector2(enemy.X, enemy.Y);
            LootManager.ApplyKillRewards(enemy, run, ResolveRewardRegion(rewardPosition), ResolveRewardMultiplier(rewardPosition), _elapsed);
        }

        float ringRadius = enemy.Type == HostileType.Boss ? 88f : 50f;
        int particleCount = enemy.Type == HostileType.Boss ? 52 : 22;
        _vfx.SpawnRing(enemy.X, enemy.Y, 10, ringRadius, 0.22f, enemy.Definition.Colors.Glow, enemy.Type == HostileType.Boss ? 3.8f : 3f);
        _vfx.SpawnParticles(enemy.X, enemy.Y, particleCount, enemy.Definition.Colors.Glow);
        AddShake(enemy.Type == HostileType.Boss ? 0.27f : 0.1f);
        _hud?.UpdateWave(Mathf.Max(1, run?.Map.CurrentWave ?? 1), _encounter.KillCount);
    }

    public void OnPlayerDamaged(float amount, float sourceX, float sourceY)
    {
        var run = GameManager.Instance?.Store.State.Save.Session.ActiveRun;
        if (run == null) return;
        if (_player.IsDashing) return;

        run.Player.Health = Mathf.Max(0f, run.Player.Health - amount);
        run.Player.DamageTaken += amount;
        _player.SetLifeRatio(run.Player.Health / run.Player.MaxHealth);
        _player.FlashDamage(1f);
        _hud?.UpdateHealth(run.Player.Health, run.Player.MaxHealth);
        _vfx.SpawnRing(_player.PlayerPosition.X, _player.PlayerPosition.Y, 8, 36, 0.16f, Palette.Danger, 2.5f);
        AddShake(0.06f);

        if (run.Player.Health <= 0f)
            _encounter.MarkPlayerDown();
    }

    public void OnBossDefeated(EnemyActor enemy)
    {
        var run = GameManager.Instance?.Store.State.Save.Session.ActiveRun;
        if (run != null)
        {
            run.Map.Boss.Defeated = true;
            run.Map.Boss.Health = 0;
            run.Stats.BossDefeated = true;
        }
        _vfx.SpawnRing(enemy.X, enemy.Y, 24, 144, 0.42f, Palette.Accent, 4f);
        _vfx.SpawnParticles(enemy.X, enemy.Y, 40, Palette.AccentSoft);
        AddShake(0.4f);
        _hud?.ShowHint($"{enemy.Definition.Label} 已被消灭", 4f);
    }

    private void SyncHudState(RunState? run)
    {
        if (run == null)
            return;

        _hud?.UpdateWave(Mathf.Max(1, run.Map.CurrentWave), _encounter.KillCount);
        _hud?.UpdateEnemyStatus(_encounter.Enemies.Count, _encounter.PendingSpawnCount);
        _hud?.UpdateQuickSlots(run.Inventory);
        _hud?.UpdateLoadout(_controller.Loadout, _controller.CurrentWeapon.Id);
        RefreshWeaponRuntime(run);
        _hud?.HideBossHealth();
        _hud?.SetPlayerDown(_encounter.State == EncounterState.Down);
    }

    private void UpdateCombatRuntime(RunState? run)
    {
        var store = GameManager.Instance?.Store;
        if (store == null || run == null)
            return;

        string? nearbyMarkerId = null;
        string? nearbyMarkerLabel = null;
        MarkerKind? nearbyMarkerKind = null;
        bool primaryReady = false;
        string hint;

        if (run.Status == RunStateStatus.AwaitingSettlement)
        {
            nearbyMarkerId = "settlement";
            nearbyMarkerLabel = "结算确认";
            nearbyMarkerKind = MarkerKind.Extraction;
            primaryReady = true;
            hint = run.PendingOutcome == RunResolutionOutcome.Down
                ? "战术链路中断，确认结算后返回基地。"
                : "行动已完成，确认结算后返回基地。";
        }
        else
        {
            var exitMarker = FindNearbyExitMarker();
            bool canExtract = RouteManager.CanExtractFromRunMap(run.Map);
            var currentZone = RouteManager.GetCurrentRunZone(run.Map);

            if (exitMarker != null)
            {
                nearbyMarkerId = exitMarker.Value.id;
                nearbyMarkerLabel = exitMarker.Value.label;
                nearbyMarkerKind = MarkerKind.Extraction;
            }

            primaryReady = !_exitAction.IsActive && exitMarker != null && canExtract;

            if (_exitAction.IsActive)
            {
                hint = _exitAction.GetProgressHint();
            }
            else if (exitMarker == null)
            {
                hint = currentZone != null
                    ? $"{currentZone.Label} / 威胁 {currentZone.ThreatLevel} / 保持机动并留意巡游敌群。"
                    : "在开放区域内探索并保持机动。";
            }
            else if (canExtract)
            {
                hint = "执行撤离并进入结算。";
            }
            else
            {
                hint = "清空当前区域后才能操作出口。";
            }
        }

        store.UpdateSceneRuntime(rt =>
        {
            rt.PrimaryActionReady = primaryReady;
            rt.PrimaryActionHint = hint;
            rt.NearbyMarkerId = nearbyMarkerId;
            rt.NearbyMarkerLabel = nearbyMarkerLabel;
            rt.NearbyMarkerKind = nearbyMarkerKind;
            rt.NearbyLootCount = CountNearbyLoot(run);
        });
    }

    public OverlayWorldSnapshot? BuildOverlayWorldSnapshot()
    {
        if (!_isActive)
            return null;

        var run = GameManager.Instance?.Store?.State.Save.Session.ActiveRun;
        var enemies = new List<Vector2>(_encounter.Enemies.Count);
        foreach (var enemy in _encounter.Enemies)
            enemies.Add(new Vector2(enemy.X, enemy.Y));

        return new OverlayWorldSnapshot
        {
            Bounds = _layout.Bounds,
            CameraBounds = BuildCameraBounds(),
            PlayerPosition = _player.PlayerPosition,
            HighlightedMarkerId = FindHighlightedMarkerId(run),
            Obstacles = new List<WorldObstacle>(_layout.Obstacles),
            Markers = new List<WorldMarker>(_layout.Markers),
            EnemyPositions = enemies,
        };
    }

    private void HandleOverlayShortcuts()
    {
        var store = GameManager.Instance?.Store;
        if (store == null)
            return;

        if (Input.IsActionJustPressed("toggle_map"))
            store.ToggleMapOverlay();

        if (Input.IsActionJustPressed("toggle_inventory"))
            store.ToggleScenePanel(ScenePanelMode.CombatInventory);
    }

    private bool IsFullscreenUiActive()
    {
        var runtime = GameManager.Instance?.Store?.State.Runtime;
        return runtime != null && (runtime.MapOverlayOpen || runtime.PanelOpen);
    }

    private Rect2 BuildCameraBounds()
    {
        var viewportSize = GetViewportRect().Size;
        var worldSize = new Vector2(viewportSize.X * _camera.Zoom.X, viewportSize.Y * _camera.Zoom.Y);
        var rect = new Rect2(_camera.Position - worldSize * 0.5f, worldSize);
        return rect.Intersection(_layout.Bounds);
    }

    private int CountNearbyLoot(RunState run)
    {
        int count = 0;
        foreach (var drop in run.GroundLoot)
        {
            if (_player.PlayerPosition.DistanceTo(new Vector2(drop.X, drop.Y)) <= 64f)
                count++;
        }

        return count;
    }

    private string? FindHighlightedMarkerId(RunState? run)
    {
        var exitMarker = FindNearbyExitMarker();
        if (exitMarker != null)
            return exitMarker.Value.id;

        if (run != null)
            return $"region-{run.Map.CurrentZoneId}";

        return _layout.Markers.Count > 0 ? _layout.Markers[0].Id : null;
    }

    private (string id, string label)? FindNearbyExitMarker()
    {
        foreach (var marker in _layout.Markers)
        {
            if (marker.Kind != MarkerKind.Extraction)
                continue;

            if (_player.PlayerPosition.DistanceTo(new Vector2(marker.X, marker.Y)) <= 120f)
                return (marker.Id, marker.Label);
        }

        return null;
    }

    private RunZoneState? SyncCurrentRegion(RunState? run)
    {
        if (run == null)
            return null;

        var zone = RouteManager.ResolveZoneAtPosition(run.Map, _layout, _player.PlayerPosition);
        if (zone == null)
            return null;

        if (run.Map.CurrentZoneId != zone.Id)
            RouteManager.SetCurrentRunZone(run.Map, zone.Id);

        run.Map.CurrentWave = zone.ThreatLevel;
        run.Map.HighestWave = Mathf.Max(run.Map.HighestWave, zone.ThreatLevel);
        run.Stats.HighestWave = Mathf.Max(run.Stats.HighestWave, zone.ThreatLevel);
        _rewardMultiplier = zone.RewardMultiplier;
        return zone;
    }

    private float ResolveRewardMultiplier(Vector2 position)
    {
        var region = _layout.GetRegionAtPosition(position);
        return region?.RewardMultiplier ?? _rewardMultiplier;
    }

    private WorldRegion? ResolveRewardRegion(Vector2 position)
    {
        return _layout.GetRegionAtPosition(position);
    }

    private Vector2 ResolveGrenadeLandingPoint(Vector2 origin, Vector2 aimPoint)
    {
        var clampedTarget = new Vector2(
            Mathf.Clamp(aimPoint.X, _layout.Bounds.Position.X, _layout.Bounds.End.X),
            Mathf.Clamp(aimPoint.Y, _layout.Bounds.Position.Y, _layout.Bounds.End.Y));
        return WorldCollision.ClipSegmentToWorld(origin, clampedTarget, _layout.Bounds, _layout.Obstacles, 8f);
    }

    private void HandleReloadInput(float delta, bool canControl, bool fullscreenUi, RunState? run)
    {
        bool reloadPressed = !fullscreenUi && Input.IsActionJustPressed("reload_weapon");
        bool reloadHeld = !fullscreenUi && Input.IsActionPressed("reload_weapon");

        if (!canControl)
        {
            _reloadHoldActive = false;
            _reloadHoldTimer = 0f;
            return;
        }

        if (reloadPressed)
        {
            _reloadHoldActive = true;
            _reloadHoldTimer = 0f;
        }

        if (!_reloadHoldActive)
            return;

        if (reloadHeld)
        {
            _reloadHoldTimer += delta;
            return;
        }

        string? hint = _reloadHoldTimer >= CombatConstants.AmmoSwitchHoldSeconds
            ? _controller.TryCycleAmmoType(run)
            : _controller.TryStartReload(run);
        if (!string.IsNullOrWhiteSpace(hint))
            _hud?.ShowHint(hint!, 1.5f);

        RefreshWeaponRuntime(run);
        _reloadHoldActive = false;
        _reloadHoldTimer = 0f;
    }

    private void RefreshWeaponRuntime(RunState? run)
    {
        if (run == null)
            return;

        run.Player.EnsureWeaponStates();
        _hud?.UpdateWeaponRuntime(run.Player, run.Inventory, _controller.ReloadWeaponId, _controller.ReloadProgress);
    }

    private void UpdateWeaponFeel(float delta)
    {
        var weapon = _controller.CurrentWeapon;
        _weaponSpreadBloom = Mathf.MoveToward(_weaponSpreadBloom, 0f, weapon.SpreadRecoveryPerSecond * delta);
        _player.SetReticleBloom(ResolveSpreadRatio(weapon));
    }

    private void ApplyShotFeel(WeaponDefinition weapon)
    {
        _weaponSpreadBloom = Mathf.Min(weapon.MaxSpreadDegrees, _weaponSpreadBloom + weapon.SpreadPerShotDegrees);
        _player.TriggerShot(weapon.RecoilKick, weapon.RecoilTwistDegrees);
        _player.SetReticleBloom(ResolveSpreadRatio(weapon));
    }

    private void ApplyAmmoHit(SegmentHit hit, WeaponAmmoDefinition ammo, int hitIndex)
    {
        float armorScale = ResolveArmorScale(ammo, hit.Enemy.Definition);
        float damage = ammo.Damage * armorScale;
        _encounter.DamageEnemy(hit.Enemy, damage, hit.PointX, hit.PointY, this);

        var feedbackColor = ResolveAmmoFeedbackColor(ammo);
        _vfx.SpawnParticles(hit.PointX, hit.PointY, 4 + Mathf.Min(3, ammo.PierceCount), feedbackColor);
        _player.TriggerHitConfirm(armorScale >= 1f ? 0.7f : 0.42f);

        int armorGap = Mathf.Max(0, hit.Enemy.Definition.ArmorLevel - ammo.ArmorPenetration);
        if (hit.Enemy.Definition.ArmorLevel > 0)
        {
            string? status = armorGap switch
            {
                0 => "PEN",
                >= 2 => "ARMOR",
                _ => null,
            };

            if (!string.IsNullOrWhiteSpace(status))
            {
                Color statusColor = armorGap == 0 ? Palette.Accent : Palette.Warning;
                _dmgText.SpawnStatusText(hit.PointX, hit.PointY, status!, statusColor);
            }
        }

        if (hitIndex == 1 && ammo.PierceCount > 0)
            _dmgText.SpawnStatusText(hit.PointX, hit.PointY - 12f, "PIERCE", feedbackColor);
    }

    private float ResolveShotSpreadDegrees(WeaponDefinition weapon)
    {
        float moveSpread = weapon.MoveSpreadDegrees * (_player.IsDashing ? 1f : _moveSpreadRatio);
        return Mathf.Clamp(weapon.BaseSpreadDegrees + _weaponSpreadBloom + moveSpread, 0f, weapon.MaxSpreadDegrees);
    }

    private float ResolveSpreadRatio(WeaponDefinition weapon)
    {
        return weapon.MaxSpreadDegrees <= 0.001f
            ? 0f
            : Mathf.Clamp(ResolveShotSpreadDegrees(weapon) / weapon.MaxSpreadDegrees, 0f, 1f);
    }

    private Vector2 ResolveSpreadTarget(Vector2 origin, Vector2 aimPoint, float range, float spreadDegrees, bool preserveDistance = false)
    {
        float aimAngle = Mathf.Atan2(aimPoint.Y - origin.Y, aimPoint.X - origin.X);
        float spreadOffset = Mathf.DegToRad((GD.Randf() - GD.Randf()) * spreadDegrees);
        float targetDistance = preserveDistance
            ? Mathf.Min(origin.DistanceTo(aimPoint), range)
            : range;

        return origin + new Vector2(Mathf.Cos(aimAngle + spreadOffset), Mathf.Sin(aimAngle + spreadOffset)) * targetDistance;
    }

    private static Color ResolveAmmoFeedbackColor(WeaponAmmoDefinition ammo)
    {
        return ammo.Id switch
        {
            "ap" or "sabot" or "breach" => Palette.Frame,
            "hp" or "exp" => Palette.Danger,
            "arc" => Palette.Dash,
            _ => Palette.Accent,
        };
    }

    private static int ResolveHitCapacity(WeaponAmmoDefinition ammo)
    {
        return Mathf.Max(1, ammo.PierceCount + 1);
    }

    private static float ResolveArmorScale(WeaponAmmoDefinition ammo, HostileDefinition target)
    {
        int gap = Mathf.Max(0, target.ArmorLevel - ammo.ArmorPenetration);
        return gap switch
        {
            0 => 1f,
            1 => 0.78f,
            2 => 0.58f,
            3 => 0.4f,
            _ => 0.28f,
        };
    }
}
