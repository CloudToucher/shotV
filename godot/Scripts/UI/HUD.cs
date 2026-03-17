using System.Collections.Generic;
using System.Linq;
using Godot;
using ShotV.Core;
using ShotV.Data;
using ShotV.Inventory;
using ShotV.State;

namespace ShotV.UI;

public partial class HUD : Control
{
    private static readonly string[] QuickSlotKeys = { "4", "5", "6", "7" };

    private PanelContainer _healthPanel = null!;
    private Label _healthLabel = null!;
    private ProgressBar _healthBar = null!;
    private Label _dashLabel = null!;

    private PanelContainer _infoPanel = null!;
    private Label _waveLabel = null!;
    private Label _enemyLabel = null!;

    private PanelContainer _bossPanel = null!;
    private Label _bossLabel = null!;
    private ProgressBar _bossBar = null!;

    private PanelContainer _hintPanel = null!;
    private Label _hintLabel = null!;

    private PanelContainer _centerPanel = null!;
    private Label _centerTitle = null!;
    private Label _centerHint = null!;

    private PanelContainer _weaponPanel = null!;
    private readonly List<PanelContainer> _weaponCards = new();
    private readonly List<Label> _weaponLabels = new();

    private PanelContainer _quickSlotPanel = null!;
    private readonly List<PanelContainer> _quickSlotCards = new();
    private readonly List<Label> _quickSlotLabels = new();

    private float _hintTimer;
    private List<WeaponDefinition> _loadout = WeaponData.Loadout.ToList();
    private WeaponType _activeWeaponId = WeaponType.MachineGun;
    private readonly Dictionary<WeaponType, WeaponHudState> _weaponRuntime = new();
    private WeaponType? _reloadWeaponId;
    private float _reloadProgress;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        SetAnchorsPreset(LayoutPreset.FullRect);
        BuildUi();
        HideBossHealth();
        SetPlayerDown(false);
    }

    public override void _Process(double delta)
    {
        if (_hintTimer <= 0f)
            return;

        _hintTimer -= (float)delta;
        if (_hintTimer <= 0f)
            _hintPanel.Visible = false;
    }

    public void UpdateHealth(float current, float max)
    {
        _healthBar.MaxValue = max;
        _healthBar.Value = Mathf.Clamp(current, 0f, max);
        _healthLabel.Text = GameText.Format("hud.health", Mathf.CeilToInt(current), Mathf.CeilToInt(max));
    }

    public void UpdateLoadout(IReadOnlyList<WeaponDefinition> loadout, WeaponType activeWeaponId)
    {
        _loadout = loadout.Count > 0 ? loadout.ToList() : WeaponData.Loadout.ToList();
        _activeWeaponId = activeWeaponId;
        RefreshWeaponStrip();
    }

    public void UpdateWeapon(WeaponDefinition weapon)
    {
        _activeWeaponId = weapon.Id;
        RefreshWeaponStrip();
    }

    public void UpdateWeaponRuntime(PlayerRunState player, GridInventoryState inventory, WeaponType? reloadWeaponId, float reloadProgress)
    {
        _weaponRuntime.Clear();
        player.EnsureWeaponStates();
        foreach (var state in player.WeaponStates)
        {
            if (!WeaponData.ById.TryGetValue(state.WeaponId, out var weapon))
                continue;

            var ammo = WeaponData.GetAmmo(weapon, state.AmmoTypeId);
            int reserve = string.IsNullOrWhiteSpace(ammo.ReserveItemId)
                ? 0
                : GridInventory.CountItemQuantity(inventory.Items, ammo.ReserveItemId);
            _weaponRuntime[state.WeaponId] = new WeaponHudState(state.Magazine, state.MagazineCapacity, reserve, ammo.Label);
        }

        _reloadWeaponId = reloadWeaponId;
        _reloadProgress = Mathf.Clamp(reloadProgress, 0f, 1f);
        RefreshWeaponStrip();
    }

    public void UpdateWave(int wave, int kills)
    {
        int displayThreat = Mathf.Max(1, wave);
        _waveLabel.Text = GameText.Format("hud.wave", displayThreat, kills);
    }

    public void UpdateEnemyStatus(int activeEnemies, int pendingSpawns)
    {
        _enemyLabel.Text = GameText.Format("hud.enemy", activeEnemies, pendingSpawns);
    }

    public void UpdateQuickSlots(GridInventoryState inventory)
    {
        for (int index = 0; index < _quickSlotLabels.Count; index++)
        {
            string key = QuickSlotKeys[index];
            string? itemId = index < inventory.QuickSlots.Length ? inventory.QuickSlots[index] : null;
            var record = itemId != null ? inventory.Items.Find(item => item.Id == itemId) : null;
            bool available = record != null;

            string labelText = GameText.Format("inventory.quick_button.empty", key);
            if (record != null)
            {
                string itemLabel = ItemData.ById.TryGetValue(record.ItemId, out var definition)
                    ? definition.ShortLabel
                    : record.ItemId;
                labelText = GameText.Format("inventory.quick_button.item", key, itemLabel, GameText.QuantitySuffix(record.Quantity));
            }

            _quickSlotLabels[index].Text = labelText;
            _quickSlotLabels[index].AddThemeColorOverride("font_color", available ? Palette.UiText : Palette.UiMuted);
            _quickSlotCards[index].AddThemeStyleboxOverride("panel", CreateCardStyle(
                available ? new Color(Palette.UiActive, 0.92f) : new Color(Palette.UiPanel, 0.86f),
                available ? new Color(Palette.Frame, 0.82f) : new Color(Palette.FrameSoft, 0.26f),
                14,
                10));
        }
    }

    public void UpdateDashCooldown(float ratio)
    {
        _dashLabel.Text = ratio > 0.01f
            ? GameText.Format("hud.dash", ratio)
            : GameText.Text("hud.dash_ready");
    }

    public void ShowHint(string text, float duration = 3f)
    {
        _hintLabel.Text = text;
        _hintPanel.Visible = !string.IsNullOrWhiteSpace(text);
        _hintTimer = duration;
    }

    public void ShowBossHealth(string label, float current, float max, int phase)
    {
        _bossPanel.Visible = true;
        _bossLabel.Text = GameText.Format("hud.boss", label, phase);
        _bossBar.MaxValue = max;
        _bossBar.Value = Mathf.Clamp(current, 0f, max);
    }

    public void HideBossHealth()
    {
        _bossPanel.Visible = false;
        _bossLabel.Text = string.Empty;
        _bossBar.Value = 0f;
    }

    public void SetPlayerDown(bool visible)
    {
        _centerPanel.Visible = visible;
    }

    private void BuildUi()
    {
        _infoPanel = CreatePanel(new Rect2(0, 0, 280, 74));
        _infoPanel.AnchorLeft = 1f;
        _infoPanel.AnchorRight = 1f;
        _infoPanel.OffsetLeft = -304f;
        _infoPanel.OffsetTop = 24f;
        _infoPanel.OffsetRight = -24f;
        _infoPanel.OffsetBottom = 98f;
        AddChild(_infoPanel);

        var infoBody = new VBoxContainer();
        infoBody.AddThemeConstantOverride("separation", 6);
        _infoPanel.AddChild(infoBody);

        _waveLabel = CreateLabel(15, Palette.UiText, true);
        infoBody.AddChild(_waveLabel);

        _enemyLabel = CreateLabel(12, Palette.UiMuted, false);
        infoBody.AddChild(_enemyLabel);

        _bossPanel = CreatePanel(new Rect2(0, 0, 440, 48));
        _bossPanel.AnchorLeft = 0.5f;
        _bossPanel.AnchorRight = 0.5f;
        _bossPanel.OffsetLeft = -220f;
        _bossPanel.OffsetTop = 96f;
        _bossPanel.OffsetRight = 220f;
        _bossPanel.OffsetBottom = 144f;
        AddChild(_bossPanel);

        var bossBody = new VBoxContainer();
        bossBody.AddThemeConstantOverride("separation", 6);
        _bossPanel.AddChild(bossBody);

        _bossLabel = CreateLabel(13, Palette.UiText, true);
        bossBody.AddChild(_bossLabel);

        _bossBar = CreateProgressBar(new Color(Palette.Danger, 0.96f));
        bossBody.AddChild(_bossBar);

        _healthPanel = CreatePanel(new Rect2(0, 0, 304, 88));
        _healthPanel.AnchorTop = 1f;
        _healthPanel.AnchorBottom = 1f;
        _healthPanel.OffsetLeft = 24f;
        _healthPanel.OffsetTop = -188f;
        _healthPanel.OffsetRight = 328f;
        _healthPanel.OffsetBottom = -100f;
        AddChild(_healthPanel);

        var healthBody = new VBoxContainer();
        healthBody.AddThemeConstantOverride("separation", 8);
        _healthPanel.AddChild(healthBody);

        _healthLabel = CreateLabel(15, Palette.UiText, true);
        healthBody.AddChild(_healthLabel);

        _healthBar = CreateProgressBar(new Color(Palette.Dash, 0.94f));
        healthBody.AddChild(_healthBar);

        _dashLabel = CreateLabel(12, Palette.UiMuted, false);
        healthBody.AddChild(_dashLabel);

        _weaponPanel = CreatePanel(new Rect2(0, 0, 464, 72));
        _weaponPanel.AnchorTop = 1f;
        _weaponPanel.AnchorBottom = 1f;
        _weaponPanel.OffsetLeft = 24f;
        _weaponPanel.OffsetTop = -88f;
        _weaponPanel.OffsetRight = 488f;
        _weaponPanel.OffsetBottom = -16f;
        AddChild(_weaponPanel);

        var weaponRow = new HBoxContainer();
        weaponRow.AddThemeConstantOverride("separation", 12);
        _weaponPanel.AddChild(weaponRow);

        for (int index = 0; index < WeaponData.Loadout.Length; index++)
        {
            var card = new PanelContainer
            {
                CustomMinimumSize = new Vector2(136f, 46f),
                MouseFilter = MouseFilterEnum.Ignore,
            };
            weaponRow.AddChild(card);
            _weaponCards.Add(card);

            var label = CreateLabel(14, Palette.UiText, true);
            label.VerticalAlignment = VerticalAlignment.Center;
            card.AddChild(label);
            _weaponLabels.Add(label);
        }

        _quickSlotPanel = CreatePanel(new Rect2(0, 0, 488, 48));
        _quickSlotPanel.AnchorLeft = 1f;
        _quickSlotPanel.AnchorRight = 1f;
        _quickSlotPanel.AnchorTop = 1f;
        _quickSlotPanel.AnchorBottom = 1f;
        _quickSlotPanel.OffsetLeft = -512f;
        _quickSlotPanel.OffsetTop = -70f;
        _quickSlotPanel.OffsetRight = -24f;
        _quickSlotPanel.OffsetBottom = -22f;
        AddChild(_quickSlotPanel);

        var quickRow = new HBoxContainer();
        quickRow.AddThemeConstantOverride("separation", 10);
        _quickSlotPanel.AddChild(quickRow);

        for (int index = 0; index < QuickSlotKeys.Length; index++)
        {
            var card = new PanelContainer
            {
                CustomMinimumSize = new Vector2(108f, 28f),
                MouseFilter = MouseFilterEnum.Ignore,
            };
            quickRow.AddChild(card);
            _quickSlotCards.Add(card);

            var label = CreateLabel(12, Palette.UiMuted, true);
            label.VerticalAlignment = VerticalAlignment.Center;
            card.AddChild(label);
            _quickSlotLabels.Add(label);
        }

        _hintPanel = CreatePanel(new Rect2(0, 0, 440, 44));
        _hintPanel.AnchorLeft = 0.5f;
        _hintPanel.AnchorRight = 0.5f;
        _hintPanel.AnchorTop = 1f;
        _hintPanel.AnchorBottom = 1f;
        _hintPanel.OffsetLeft = -220f;
        _hintPanel.OffsetTop = -88f;
        _hintPanel.OffsetRight = 220f;
        _hintPanel.OffsetBottom = -44f;
        _hintPanel.Visible = false;
        AddChild(_hintPanel);

        _hintLabel = CreateLabel(12, Palette.UiText, true);
        _hintLabel.HorizontalAlignment = HorizontalAlignment.Center;
        _hintPanel.AddChild(_hintLabel);

        _centerPanel = CreatePanel(new Rect2(0, 0, 440, 160));
        _centerPanel.AnchorLeft = 0.5f;
        _centerPanel.AnchorRight = 0.5f;
        _centerPanel.AnchorTop = 0.5f;
        _centerPanel.AnchorBottom = 0.5f;
        _centerPanel.OffsetLeft = -220f;
        _centerPanel.OffsetTop = -80f;
        _centerPanel.OffsetRight = 220f;
        _centerPanel.OffsetBottom = 80f;
        AddChild(_centerPanel);

        var centerBody = new VBoxContainer();
        centerBody.AddThemeConstantOverride("separation", 10);
        _centerPanel.AddChild(centerBody);

        _centerTitle = CreateLabel(28, Palette.UiText, true);
        _centerTitle.Text = GameText.Text("hud.center_title");
        _centerTitle.HorizontalAlignment = HorizontalAlignment.Center;
        centerBody.AddChild(_centerTitle);

        _centerHint = CreateLabel(13, Palette.UiMuted, false);
        _centerHint.Text = GameText.Text("hud.center_hint");
        _centerHint.HorizontalAlignment = HorizontalAlignment.Center;
        centerBody.AddChild(_centerHint);

        UpdateHealth(CombatConstants.PlayerMaxHealth, CombatConstants.PlayerMaxHealth);
        UpdateWave(0, 0);
        UpdateEnemyStatus(0, 0);
        UpdateDashCooldown(0f);
        RefreshWeaponStrip();
    }

    private void RefreshWeaponStrip()
    {
        for (int index = 0; index < _weaponLabels.Count; index++)
        {
            WeaponDefinition? weapon = index < _loadout.Count ? _loadout[index] : null;
            bool active = weapon != null && weapon.Id == _activeWeaponId;

            _weaponLabels[index].Text = weapon != null
                ? $"{index + 1}  {weapon.Label}"
                : $"{index + 1}  {GameText.Text("common.empty")}";
            _weaponLabels[index].AddThemeColorOverride("font_color", active ? Palette.UiText : Palette.UiMuted);
            _weaponCards[index].AddThemeStyleboxOverride("panel", CreateCardStyle(
                active ? new Color(Palette.UiActive, 0.94f) : new Color(Palette.UiPanel, 0.88f),
                active ? new Color(Palette.Frame, 0.9f) : new Color(Palette.FrameSoft, 0.28f),
                16,
                10));
        }

        ApplyWeaponRuntimeLabelOverrides();
    }

    private void ApplyWeaponRuntimeLabelOverrides()
    {
        for (int index = 0; index < _weaponLabels.Count; index++)
        {
            WeaponDefinition? weapon = index < _loadout.Count ? _loadout[index] : null;
            if (weapon == null || !_weaponRuntime.TryGetValue(weapon.Id, out var runtime))
                continue;

            string labelText = $"{index + 1}  {weapon.Label} {runtime.Magazine}/{runtime.MagazineCapacity}+{runtime.Reserve} [{runtime.AmmoLabel}]";
            if (_reloadWeaponId == weapon.Id)
                labelText += $" {GameText.Format("hud.reload", Mathf.RoundToInt(_reloadProgress * 100f))}";

            _weaponLabels[index].Text = labelText;
        }
    }

    private static PanelContainer CreatePanel(Rect2 rect)
    {
        var panel = new PanelContainer
        {
            MouseFilter = MouseFilterEnum.Ignore,
        };
        panel.AddThemeStyleboxOverride("panel", CreateCardStyle(
            new Color(Palette.UiPanel, 0.9f),
            new Color(Palette.Frame, 0.28f),
            18,
            12));
        return panel;
    }

    private static Label CreateLabel(int fontSize, Color color, bool strong)
    {
        var label = new Label
        {
            MouseFilter = MouseFilterEnum.Ignore,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        label.AddThemeFontSizeOverride("font_size", fontSize);
        label.AddThemeColorOverride("font_color", color);
        if (strong)
            label.AddThemeColorOverride("font_outline_color", new Color(0f, 0f, 0f, 0f));
        return label;
    }

    private static ProgressBar CreateProgressBar(Color fillColor)
    {
        var progress = new ProgressBar
        {
            ShowPercentage = false,
            CustomMinimumSize = new Vector2(0f, 12f),
            MouseFilter = MouseFilterEnum.Ignore,
            MaxValue = 100f,
            Value = 100f,
        };
        progress.AddThemeStyleboxOverride("background", CreateProgressStyle(new Color(Palette.UiText, 0.12f)));
        progress.AddThemeStyleboxOverride("fill", CreateProgressStyle(fillColor));
        return progress;
    }

    private static StyleBoxFlat CreateCardStyle(Color background, Color border, int horizontalPadding, int verticalPadding)
    {
        var style = new StyleBoxFlat
        {
            BgColor = background,
            BorderColor = border,
            CornerRadiusTopLeft = 0,
            CornerRadiusTopRight = 0,
            CornerRadiusBottomRight = 0,
            CornerRadiusBottomLeft = 0,
            ContentMarginLeft = horizontalPadding,
            ContentMarginTop = verticalPadding,
            ContentMarginRight = horizontalPadding,
            ContentMarginBottom = verticalPadding,
        };
        style.SetBorderWidthAll(1);
        return style;
    }

    private static StyleBoxFlat CreateProgressStyle(Color color)
    {
        var style = new StyleBoxFlat
        {
            BgColor = color,
            CornerRadiusTopLeft = 0,
            CornerRadiusTopRight = 0,
            CornerRadiusBottomRight = 0,
            CornerRadiusBottomLeft = 0,
        };
        return style;
    }

    private readonly struct WeaponHudState
    {
        public WeaponHudState(int magazine, int magazineCapacity, int reserve, string ammoLabel)
        {
            Magazine = magazine;
            MagazineCapacity = magazineCapacity;
            Reserve = reserve;
            AmmoLabel = ammoLabel;
        }

        public int Magazine { get; }
        public int MagazineCapacity { get; }
        public int Reserve { get; }
        public string AmmoLabel { get; }
    }
}
