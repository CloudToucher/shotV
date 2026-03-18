using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using ShotV.Core;
using ShotV.Data;
using ShotV.Inventory;
using ShotV.State;

namespace ShotV.UI;

public partial class ViewportOverlay
{
    private enum HeldInventoryOrigin
    {
        None,
        BaseStash,
        DeploymentPack,
        CombatInventory,
        CombatWeaponSlot,
        GroundLoot,
    }

    private sealed class NearbyGroundLootPanelState
    {
        public List<InventoryItemRecord> Items { get; init; } = new();
        public Dictionary<string, GroundLootDrop> DropByItemId { get; init; } = new();
    }

    private static readonly Vector2[] GroundDropOffsets =
    {
        new Vector2(-18f, -8f),
        new Vector2(16f, -4f),
        new Vector2(-6f, 18f),
        new Vector2(20f, 14f),
    };
    private static readonly string[] CombatQuickSlotKeys = { "4", "5", "6", "7" };

    private const int CombatGroundColumns = 6;
    private const int CombatGroundRows = 4;

    private InventoryGridControl? _baseStashGrid;
    private InventoryGridControl? _basePackGrid;
    private InventoryGridControl? _combatGroundGrid;
    private InventoryGridControl? _combatInventoryGrid;
    private InventoryDragPreviewControl? _dragPreview;
    private readonly List<PanelContainer> _combatWeaponSlotCards = new();
    private readonly List<Label> _combatWeaponSlotLabels = new();
    private readonly List<Button> _quickSlotButtons = new();
    private PanelContainer? _combatArmorSlotCard;
    private Label? _combatArmorSlotLabel;
    private Label? _combatArmorSlotMetaLabel;
    private Label? _inventoryInstructionLabel;

    private List<InventoryItemRecord> _panelStashItems = new();
    private List<InventoryItemRecord> _panelDeploymentPackItems = new();
    private List<InventoryItemRecord> _panelRunItems = new();
    private List<GroundLootDrop> _panelGroundLoot = new();
    private List<WeaponType> _panelRunLoadoutWeaponIds = new();
    private string?[] _panelQuickSlots = new string?[GridInventoryState.RunQuickSlotCount];
    private NearbyGroundLootPanelState _nearbyGroundLoot = new();

    private string _baseSyncKey = "";
    private string _combatSyncKey = "";
    private Vector2 _panelPointer;
    private InventoryItemRecord? _heldInventoryItem;
    private InventoryItemRecord? _heldRestoreItem;
    private GroundLootDrop? _heldRestoreGroundDrop;
    private int _heldWeaponSlotIndex = -1;
    private WeaponType? _panelRunCurrentWeaponId;
    private string _panelRunArmorId = "";
    private float _panelRunArmorDurability;
    private float _panelRunArmorMaxDurability;
    private int _panelRunArmorUpgradeLevel;
    private HeldInventoryOrigin _heldInventoryOrigin;

    public override void _Input(InputEvent @event)
    {
        base._Input(@event);

        if (_panelLayer == null || !_panelLayer.Visible)
            return;

        var store = GameManager.Instance?.Store;
        if (store == null)
            return;

        var mode = ResolvePanelMode(store.State);
        if (mode is not ScenePanelMode.Locker and not ScenePanelMode.Launch and not ScenePanelMode.CombatInventory)
            return;

        if (@event is InputEventMouseMotion motion)
        {
            _panelPointer = motion.Position;
            UpdateInventoryInteractionLive(store.State);
            return;
        }

        if (@event is InputEventMouseButton button && button.ButtonIndex == MouseButton.Left)
        {
            _panelPointer = button.Position;
            if (mode == ScenePanelMode.CombatInventory && IsPointerOverQuickSlotButton(button.Position))
                return;

            bool consumed = button.Pressed
                ? HandleInventoryPointerPressed(store.State, mode, button.Position)
                : HandleInventoryPointerReleased(store.State, mode, button.Position);
            if (consumed)
                GetViewport().SetInputAsHandled();
            return;
        }

        if (@event.IsActionPressed("rotate_item"))
        {
            RotateHeldInventoryItem();
            GetViewport().SetInputAsHandled();
            return;
        }

        if (@event.IsActionPressed("sort_inventory"))
        {
            AutoArrangeInteractiveInventory(store.State, mode);
            GetViewport().SetInputAsHandled();
            return;
        }

        if (mode == ScenePanelMode.CombatInventory)
        {
            if (@event.IsActionPressed("quick_slot_1")) BindQuickSlot(0);
            else if (@event.IsActionPressed("quick_slot_2")) BindQuickSlot(1);
            else if (@event.IsActionPressed("quick_slot_3")) BindQuickSlot(2);
            else if (@event.IsActionPressed("quick_slot_4")) BindQuickSlot(3);
        }
    }

    private void BuildInventoryInteractionUi()
    {
        _dragPreview = new InventoryDragPreviewControl();
        _dragPreview.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _dragPreview.Visible = false;
        _panelLayer.AddChild(_dragPreview);
    }

    private void ResetInteractiveWidgetRefs()
    {
        _baseStashGrid = null;
        _basePackGrid = null;
        _combatGroundGrid = null;
        _combatInventoryGrid = null;
        _combatArmorSlotCard = null;
        _combatArmorSlotLabel = null;
        _combatArmorSlotMetaLabel = null;
        _inventoryInstructionLabel = null;
        _combatWeaponSlotCards.Clear();
        _combatWeaponSlotLabels.Clear();
        _quickSlotButtons.Clear();
    }

    private void ResetInteractiveDragState()
    {
        _heldInventoryItem = null;
        _heldRestoreItem = null;
        _heldRestoreGroundDrop = null;
        _heldWeaponSlotIndex = -1;
        _heldInventoryOrigin = HeldInventoryOrigin.None;
        _baseSyncKey = string.Empty;
        _combatSyncKey = string.Empty;
        _dragPreview?.SetPreview(null, Vector2.Zero, 32f, false);
    }

    private void BuildLockerInventoryContent(Control parent, InventoryState inventory)
    {
        EnsureBaseStateSynced(inventory);

        var gridRow = new HBoxContainer();
        gridRow.AddThemeConstantOverride("separation", 18);
        gridRow.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        gridRow.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        parent.AddChild(gridRow);

        var stashPane = CreateCombatInventoryPane(GameText.Text("inventory.base_stash"));
        stashPane.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        stashPane.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        gridRow.AddChild(stashPane);
        var stashBody = stashPane.GetChild<VBoxContainer>(0);
        stashBody.SizeFlagsVertical = Control.SizeFlags.ExpandFill;

        var stashScroll = new ScrollContainer
        {
            HorizontalScrollMode = ScrollContainer.ScrollMode.Auto,
            VerticalScrollMode = ScrollContainer.ScrollMode.Auto,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0f, 240f)
        };
        stashBody.AddChild(stashScroll);

        var stashCenter = new CenterContainer();
        stashCenter.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        stashCenter.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        stashScroll.AddChild(stashCenter);

        _baseStashGrid = new InventoryGridControl();
        _baseStashGrid.Configure(inventory.StashColumns, inventory.StashRows, GetInventoryCellSize());
        stashCenter.AddChild(_baseStashGrid);

        var packPane = CreateCombatInventoryPane(GameText.Text("inventory.deployment_pack"));
        packPane.CustomMinimumSize = new Vector2(Mathf.Max(280f, inventory.DeploymentPack.Columns * GetInventoryCellSize() + 28f), 0f);
        packPane.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        gridRow.AddChild(packPane);
        var packBody = packPane.GetChild<VBoxContainer>(0);
        packBody.SizeFlagsVertical = Control.SizeFlags.ExpandFill;

        var packScroll = new ScrollContainer
        {
            HorizontalScrollMode = ScrollContainer.ScrollMode.Auto,
            VerticalScrollMode = ScrollContainer.ScrollMode.Auto,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0f, 240f)
        };
        packBody.AddChild(packScroll);

        var packCenter = new CenterContainer();
        packCenter.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        packCenter.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        packScroll.AddChild(packCenter);

        _basePackGrid = new InventoryGridControl();
        _basePackGrid.Configure(inventory.DeploymentPack.Columns, inventory.DeploymentPack.Rows, GetInventoryCellSize());
        packCenter.AddChild(_basePackGrid);

        _inventoryInstructionLabel = CreateLabel(12, Palette.UiMuted, false, 0.2f, true);
        _inventoryInstructionLabel.Visible = false;
        parent.AddChild(_inventoryInstructionLabel);
    }

    private void BuildCombatInventoryContent(RunState activeRun)
    {
        EnsureCombatStateSynced(activeRun);

        var split = new HBoxContainer();
        split.AddThemeConstantOverride("separation", 18);
        split.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        split.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        _panelContentContainer.AddChild(split);

        var leftCol = new VBoxContainer();
        leftCol.AddThemeConstantOverride("separation", 10);
        leftCol.CustomMinimumSize = new Vector2(228f, 0f);
        split.AddChild(leftCol);

        var centerCol = new VBoxContainer();
        centerCol.AddThemeConstantOverride("separation", 8);
        centerCol.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        centerCol.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        split.AddChild(centerCol);

        var rightCol = new VBoxContainer();
        rightCol.AddThemeConstantOverride("separation", 8);
        rightCol.CustomMinimumSize = new Vector2(CombatGroundColumns * GetInventoryCellSize() + 28f, 0f);
        split.AddChild(rightCol);

        var weaponSlotsCard = CreateCombatInventoryPane();
        var weaponSlotsBody = weaponSlotsCard.GetChild<VBoxContainer>(0);

        var weaponSlotsRow = new VBoxContainer();
        weaponSlotsRow.AddThemeConstantOverride("separation", 6);
        weaponSlotsBody.AddChild(weaponSlotsRow);

        for (int index = 0; index < WeaponData.MaxLoadoutSize; index++)
        {
            var slotCard = new PanelContainer
            {
                CustomMinimumSize = new Vector2(0f, 50f),
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                MouseFilter = Control.MouseFilterEnum.Ignore,
            };
            slotCard.AddThemeStyleboxOverride("panel", CreateCombatSlotStyle(Palette.Accent, false, false));
            weaponSlotsRow.AddChild(slotCard);
            _combatWeaponSlotCards.Add(slotCard);

            var slotLabel = CreateLabel(12, Palette.UiText, true, 0.2f, true);
            slotLabel.VerticalAlignment = VerticalAlignment.Center;
            slotCard.AddChild(slotLabel);
            _combatWeaponSlotLabels.Add(slotLabel);
        }

        leftCol.AddChild(weaponSlotsCard);

        var armorCard = CreateCombatInventoryPane();
        var armorBody = armorCard.GetChild<VBoxContainer>(0);

        _combatArmorSlotCard = new PanelContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        _combatArmorSlotCard.AddThemeStyleboxOverride("panel", CreateCombatSlotStyle(Palette.FrameSoft, false, false));
        armorBody.AddChild(_combatArmorSlotCard);

        var armorSlotBody = new VBoxContainer();
        armorSlotBody.AddThemeConstantOverride("separation", 4);
        _combatArmorSlotCard.AddChild(armorSlotBody);

        _combatArmorSlotLabel = CreateLabel(12, Palette.UiText, true, 0.2f, true);
        armorSlotBody.AddChild(_combatArmorSlotLabel);

        _combatArmorSlotMetaLabel = CreateLabel(11, Palette.UiMuted, false, 0.2f, true);
        armorSlotBody.AddChild(_combatArmorSlotMetaLabel);

        leftCol.AddChild(armorCard);

        var quickSlotsCard = CreateCombatInventoryPane();
        var quickSlotsBody = quickSlotsCard.GetChild<VBoxContainer>(0);

        var quickGrid = new GridContainer
        {
            Columns = 2,
        };
        quickGrid.AddThemeConstantOverride("h_separation", 6);
        quickGrid.AddThemeConstantOverride("v_separation", 6);
        quickSlotsBody.AddChild(quickGrid);

        for (int index = 0; index < GridInventoryState.RunQuickSlotCount; index++)
        {
            int slotIndex = index;
            var button = CreateCombatQuickSlotButton(index);
            button.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            button.Pressed += () => BindQuickSlot(slotIndex);
            quickGrid.AddChild(button);
            _quickSlotButtons.Add(button);
        }

        leftCol.AddChild(quickSlotsCard);

        var inventoryCard = CreateCombatInventoryPane();
        inventoryCard.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        inventoryCard.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        centerCol.AddChild(inventoryCard);
        var inventoryBody = inventoryCard.GetChild<VBoxContainer>(0);
        inventoryBody.SizeFlagsVertical = Control.SizeFlags.ExpandFill;

        var inventoryScroll = new ScrollContainer
        {
            HorizontalScrollMode = ScrollContainer.ScrollMode.Auto,
            VerticalScrollMode = ScrollContainer.ScrollMode.Auto,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0f, 312f)
        };
        inventoryBody.AddChild(inventoryScroll);

        var inventoryHost = new VBoxContainer();
        inventoryHost.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        inventoryHost.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        inventoryScroll.AddChild(inventoryHost);

        _combatInventoryGrid = new InventoryGridControl();
        _combatInventoryGrid.Configure(activeRun.Inventory.Columns, activeRun.Inventory.Rows, GetInventoryCellSize());
        inventoryHost.AddChild(_combatInventoryGrid);

        var groundCard = CreateCombatInventoryPane();
        rightCol.AddChild(groundCard);
        var groundBody = groundCard.GetChild<VBoxContainer>(0);

        _combatGroundGrid = new InventoryGridControl();
        _combatGroundGrid.Configure(CombatGroundColumns, CombatGroundRows, GetInventoryCellSize());
        groundBody.AddChild(_combatGroundGrid);
    }

    private void BuildLaunchInventoryContent(Control parent, InventoryState inventory, bool canDeploy)
    {
        EnsureBaseStateSynced(inventory);

        var packHost = CreateCombatInventoryPane();
        packHost.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        var packBody = packHost.GetChild<VBoxContainer>(0);

        var controlsRow = new HBoxContainer();
        controlsRow.AddThemeConstantOverride("separation", 10);
        packBody.AddChild(controlsRow);

        var adjustButton = CreateSmallButton(GameText.Text("inventory.adjust_supplies"), true);
        adjustButton.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        adjustButton.Pressed += () => GameManager.Instance?.Store?.OpenScenePanel(ScenePanelMode.Locker);
        controlsRow.AddChild(adjustButton);

        var deployButton = CreateActionButton(GameText.Text("fullscreen.action.deploy"), true);
        deployButton.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        deployButton.Disabled = !canDeploy;
        deployButton.Pressed += () => GameManager.Instance?.Store?.DeployCombat(true);
        controlsRow.AddChild(deployButton);
        
        var packScroll = new ScrollContainer
        {
            HorizontalScrollMode = ScrollContainer.ScrollMode.Auto,
            VerticalScrollMode = ScrollContainer.ScrollMode.Auto,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0f, 240f)
        };
        packBody.AddChild(packScroll);

        var packCenter = new CenterContainer();
        packCenter.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        packCenter.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        packScroll.AddChild(packCenter);

        _basePackGrid = new InventoryGridControl();
        _basePackGrid.Configure(inventory.DeploymentPack.Columns, inventory.DeploymentPack.Rows, GetInventoryCellSize());
        packCenter.AddChild(_basePackGrid);

        _inventoryInstructionLabel = CreateLabel(12, Palette.UiMuted, false, 0.2f, true);
        _inventoryInstructionLabel.Visible = false;
        packBody.AddChild(_inventoryInstructionLabel);

        parent.AddChild(packHost);
    }

    private void UpdateInventoryInteractionLive(GameState state)
    {
        if (_panelLayer == null || !_panelLayer.Visible)
        {
            _dragPreview?.SetPreview(null, Vector2.Zero, 32f, false);
            return;
        }

        var mode = ResolvePanelMode(state);
        if (mode == ScenePanelMode.Locker)
        {
            if (_baseStashGrid == null || _basePackGrid == null)
                return;

            _baseStashGrid.SetItems(_panelStashItems);
            _baseStashGrid.SetBadges(null);
            _basePackGrid.SetItems(_panelDeploymentPackItems);
            _basePackGrid.SetBadges(null);
            UpdateInstructionLabel(mode);
            UpdateDragPreview(mode);
            return;
        }

        if (mode == ScenePanelMode.Launch)
        {
            if (_basePackGrid == null)
                return;

            _basePackGrid.SetItems(_panelDeploymentPackItems);
            _basePackGrid.SetBadges(null);
            UpdateInstructionLabel(mode);
            UpdateDragPreview(mode);
            return;
        }

        if (mode != ScenePanelMode.CombatInventory || _combatGroundGrid == null || _combatInventoryGrid == null)
        {
            _dragPreview?.SetPreview(null, Vector2.Zero, 32f, false);
            return;
        }

        _nearbyGroundLoot = BuildNearbyGroundLootPanelState();
        _combatGroundGrid.SetItems(_nearbyGroundLoot.Items);
        _combatGroundGrid.SetBadges(null);
        _combatInventoryGrid.SetItems(_panelRunItems);
        _combatInventoryGrid.SetBadges(BuildQuickSlotBadges());
        UpdateCombatWeaponSlotCards();
        UpdateCombatArmorSlotCard();
        UpdateQuickSlotButtons();
        UpdateInstructionLabel(mode);
        UpdateDragPreview(mode);
    }

    private void EnsureBaseStateSynced(InventoryState inventory)
    {
        string stashKey = string.Join(",", inventory.StoredItems.Select(item => $"{item.Id}:{item.ItemId}:{item.Quantity}:{item.X}:{item.Y}:{item.Width}:{item.Height}:{item.Rotated}"));
        string packKey = string.Join(",", inventory.DeploymentPack.Items.Select(item => $"{item.Id}:{item.ItemId}:{item.Quantity}:{item.X}:{item.Y}:{item.Width}:{item.Height}:{item.Rotated}"));
        string key = $"{stashKey}|{packKey}";

        if (_heldInventoryItem == null && _baseSyncKey != key)
        {
            _panelStashItems = GridInventory.CloneItems(inventory.StoredItems);
            _panelDeploymentPackItems = GridInventory.CloneItems(inventory.DeploymentPack.Items);
            _baseSyncKey = key;
        }
    }

    private void EnsureCombatStateSynced(RunState activeRun)
    {
        string itemKey = string.Join(",", activeRun.Inventory.Items.Select(item => $"{item.Id}:{item.ItemId}:{item.Quantity}:{item.X}:{item.Y}:{item.Width}:{item.Height}:{item.Rotated}"));
        string lootKey = string.Join(",", activeRun.GroundLoot.Select(drop => $"{drop.Id}:{drop.Item.Id}:{drop.Item.ItemId}:{drop.X:0.00}:{drop.Y:0.00}"));
        string quickKey = string.Join(",", activeRun.Inventory.QuickSlots.Select(slot => slot ?? "-"));
        string loadoutKey = string.Join(",", activeRun.Player.LoadoutWeaponIds);
        string armorKey = $"{activeRun.Player.Armor.ArmorId}:{activeRun.Player.Armor.Durability:0.0}:{activeRun.Player.Armor.MaxDurability:0.0}:{activeRun.Player.Armor.UpgradeLevel}";
        string key = $"{itemKey}|{lootKey}|{quickKey}|{loadoutKey}|{activeRun.Player.CurrentWeaponId}|{armorKey}";

        if (_heldInventoryItem == null && _combatSyncKey != key)
        {
            _panelRunItems = GridInventory.CloneItems(activeRun.Inventory.Items);
            _panelGroundLoot = activeRun.GroundLoot.Select(drop => drop.Clone()).ToList();
            _panelRunLoadoutWeaponIds = new List<WeaponType>(activeRun.Player.LoadoutWeaponIds);
            _panelRunCurrentWeaponId = activeRun.Player.CurrentWeaponId;
            _panelRunArmorId = activeRun.Player.Armor.ArmorId;
            _panelRunArmorDurability = activeRun.Player.Armor.Durability;
            _panelRunArmorMaxDurability = activeRun.Player.Armor.MaxDurability;
            _panelRunArmorUpgradeLevel = activeRun.Player.Armor.UpgradeLevel;
            _panelQuickSlots = (string?[])activeRun.Inventory.QuickSlots.Clone();
            _combatSyncKey = key;
        }
    }

    private void UpdateCombatWeaponSlotCards()
    {
        for (int index = 0; index < _combatWeaponSlotLabels.Count; index++)
        {
            WeaponType? weaponId = index < _panelRunLoadoutWeaponIds.Count
                ? _panelRunLoadoutWeaponIds[index]
                : null;
            bool active = weaponId.HasValue && _panelRunCurrentWeaponId == weaponId.Value;
            bool occupied = weaponId.HasValue;

            string label = occupied && WeaponData.ById.TryGetValue(weaponId!.Value, out var weapon)
                ? $"{index + 1}  {weapon.Label}"
                : $"{index + 1}  {GameText.Text("common.empty")}";

            _combatWeaponSlotLabels[index].Text = label;
            _combatWeaponSlotLabels[index].AddThemeColorOverride("font_color", occupied ? Palette.UiText : new Color(Palette.UiText, 0.48f));
            _combatWeaponSlotCards[index].AddThemeStyleboxOverride("panel", CreateCombatSlotStyle(Palette.Accent, occupied, active));
        }
    }

    private void UpdateCombatArmorSlotCard()
    {
        if (_combatArmorSlotCard == null || _combatArmorSlotLabel == null || _combatArmorSlotMetaLabel == null)
            return;

        if (string.IsNullOrWhiteSpace(_panelRunArmorId) || !ArmorData.ById.TryGetValue(_panelRunArmorId, out var armor))
        {
            _combatArmorSlotLabel.Text = GameText.Text("common.empty");
            _combatArmorSlotLabel.AddThemeColorOverride("font_color", new Color(Palette.UiText, 0.48f));
            _combatArmorSlotMetaLabel.Text = string.Empty;
            _combatArmorSlotCard.AddThemeStyleboxOverride("panel", CreateCombatSlotStyle(Palette.FrameSoft, false, false));
            return;
        }

        int durability = Mathf.RoundToInt(_panelRunArmorDurability);
        int maxDurability = Mathf.Max(1, Mathf.RoundToInt(_panelRunArmorMaxDurability));
        int mitigation = Mathf.RoundToInt((armor.Mitigation + _panelRunArmorUpgradeLevel * 0.035f) * 100f);

        _combatArmorSlotLabel.Text = armor.Label;
        _combatArmorSlotLabel.AddThemeColorOverride("font_color", Palette.UiText);
        _combatArmorSlotMetaLabel.Text = $"{durability}/{maxDurability}  {mitigation}%";
        _combatArmorSlotMetaLabel.AddThemeColorOverride("font_color", new Color(Palette.UiText, 0.56f));
        _combatArmorSlotCard.AddThemeStyleboxOverride("panel", CreateCombatSlotStyle(armor.Tint, true, false));
    }

    private bool TryGetCombatWeaponSlotIndex(Vector2 viewportPosition, out int slotIndex)
    {
        for (int index = 0; index < _combatWeaponSlotCards.Count; index++)
        {
            if (_combatWeaponSlotCards[index].GetGlobalRect().HasPoint(viewportPosition))
            {
                slotIndex = index;
                return true;
            }
        }

        slotIndex = -1;
        return false;
    }

    private static InventoryItemRecord? CreateWeaponInventoryItem(WeaponType weaponId)
    {
        return GridInventory.CreateItemRecord(WeaponData.GetInventoryItemId(weaponId), 1);
    }

    private static bool TryResolveWeaponId(InventoryItemRecord? item, out WeaponType weaponId)
    {
        if (item != null && WeaponData.TryGetWeaponIdFromInventoryItem(item.ItemId, out weaponId))
            return true;

        weaponId = default;
        return false;
    }

    private void InsertWeaponIntoPanelLoadout(int slotIndex, WeaponType weaponId)
    {
        _panelRunLoadoutWeaponIds.RemoveAll(id => id == weaponId);
        int insertIndex = Mathf.Clamp(slotIndex, 0, _panelRunLoadoutWeaponIds.Count);
        if (_panelRunLoadoutWeaponIds.Count >= WeaponData.MaxLoadoutSize)
            return;

        _panelRunLoadoutWeaponIds.Insert(insertIndex, weaponId);
        if (_panelRunCurrentWeaponId == null || !_panelRunLoadoutWeaponIds.Contains(_panelRunCurrentWeaponId.Value))
            _panelRunCurrentWeaponId = weaponId;
    }

    private WeaponType? ResolveCommittedCurrentWeaponId()
    {
        if (_panelRunCurrentWeaponId.HasValue && _panelRunLoadoutWeaponIds.Contains(_panelRunCurrentWeaponId.Value))
            return _panelRunCurrentWeaponId.Value;

        return _panelRunLoadoutWeaponIds.Count > 0
            ? _panelRunLoadoutWeaponIds[0]
            : null;
    }

    private bool HandleInventoryPointerPressed(GameState state, ScenePanelMode mode, Vector2 viewportPosition)
    {
        if (_heldInventoryItem != null)
            return false;

        if (mode == ScenePanelMode.Locker)
        {
            if (_basePackGrid != null && _basePackGrid.TryGetCellAtViewport(viewportPosition, out var packCell))
            {
                var packExtraction = GridInventory.PickItemFromGridAtCell(_panelDeploymentPackItems, packCell.X, packCell.Y);
                if (packExtraction.Item == null)
                    return false;

                _heldInventoryItem = packExtraction.Item;
                _heldRestoreItem = packExtraction.Item.Clone();
                _heldInventoryOrigin = HeldInventoryOrigin.DeploymentPack;
                _panelDeploymentPackItems = packExtraction.Items;
                UpdateInventoryInteractionLive(state);
                return true;
            }

            if (_baseStashGrid == null || !_baseStashGrid.TryGetCellAtViewport(viewportPosition, out var stashCell))
                return false;

            var extraction = GridInventory.PickItemFromGridAtCell(_panelStashItems, stashCell.X, stashCell.Y);
            if (extraction.Item == null)
                return false;

            _heldInventoryItem = extraction.Item;
            _heldRestoreItem = extraction.Item.Clone();
            _heldInventoryOrigin = HeldInventoryOrigin.BaseStash;
            _panelStashItems = extraction.Items;
            UpdateInventoryInteractionLive(state);
            return true;
        }

        if (mode == ScenePanelMode.Launch)
        {
            if (_basePackGrid != null && _basePackGrid.TryGetCellAtViewport(viewportPosition, out var packCell))
            {
                var packExtraction = GridInventory.PickItemFromGridAtCell(_panelDeploymentPackItems, packCell.X, packCell.Y);
                if (packExtraction.Item == null)
                    return false;

                _heldInventoryItem = packExtraction.Item;
                _heldRestoreItem = packExtraction.Item.Clone();
                _heldInventoryOrigin = HeldInventoryOrigin.DeploymentPack;
                _panelDeploymentPackItems = packExtraction.Items;
                UpdateInventoryInteractionLive(state);
                return true;
            }
            return false;
        }

        if (mode != ScenePanelMode.CombatInventory)
            return false;

        if (TryGetCombatWeaponSlotIndex(viewportPosition, out var weaponSlotIndex))
        {
            if (weaponSlotIndex >= _panelRunLoadoutWeaponIds.Count)
                return false;

            var weaponItem = CreateWeaponInventoryItem(_panelRunLoadoutWeaponIds[weaponSlotIndex]);
            if (weaponItem == null)
                return false;

            _heldInventoryItem = weaponItem;
            _heldRestoreItem = weaponItem.Clone();
            _heldWeaponSlotIndex = weaponSlotIndex;
            _heldInventoryOrigin = HeldInventoryOrigin.CombatWeaponSlot;
            _panelRunLoadoutWeaponIds.RemoveAt(weaponSlotIndex);
            UpdateInventoryInteractionLive(state);
            return true;
        }

        if (_combatGroundGrid != null && _combatGroundGrid.TryGetCellAtViewport(viewportPosition, out var groundCell))
        {
            var targetItem = GridInventory.FindItemAtCell(_nearbyGroundLoot.Items, groundCell.X, groundCell.Y);
            if (targetItem != null && _nearbyGroundLoot.DropByItemId.TryGetValue(targetItem.Id, out var drop))
            {
                _heldInventoryItem = drop.Item.Clone();
                _heldRestoreGroundDrop = drop.Clone();
                _heldInventoryOrigin = HeldInventoryOrigin.GroundLoot;
                _panelGroundLoot = _panelGroundLoot.Where(entry => entry.Id != drop.Id).Select(entry => entry.Clone()).ToList();
                UpdateInventoryInteractionLive(state);
                return true;
            }
            return false;
        }

        if (_combatInventoryGrid == null || !_combatInventoryGrid.TryGetCellAtViewport(viewportPosition, out var inventoryCell))
            return false;

        var inventoryExtraction = GridInventory.PickItemFromGridAtCell(_panelRunItems, inventoryCell.X, inventoryCell.Y);
        if (inventoryExtraction.Item == null)
            return false;

        _heldInventoryItem = inventoryExtraction.Item;
        _heldRestoreItem = inventoryExtraction.Item.Clone();
        _heldInventoryOrigin = HeldInventoryOrigin.CombatInventory;
        _panelRunItems = inventoryExtraction.Items;
        UpdateInventoryInteractionLive(state);
        return true;
    }

    private bool HandleInventoryPointerReleased(GameState state, ScenePanelMode mode, Vector2 viewportPosition)
    {
        if (_heldInventoryItem == null)
            return false;

        if (mode == ScenePanelMode.Locker)
        {
            if (_baseStashGrid != null && _baseStashGrid.TryGetCellAtViewport(viewportPosition, out var cell))
            {
                var placement = GridInventory.StoreItemAtPosition(_baseStashGrid.Columns, _baseStashGrid.Rows, _panelStashItems, _heldInventoryItem, cell.X, cell.Y);
                if (placement.Placed)
                {
                    _panelStashItems = placement.Items;
                    CommitDeploymentInventoryState();
                    ResetInteractiveDragState();
                    return true;
                }
            }

            if (_basePackGrid != null && _basePackGrid.TryGetCellAtViewport(viewportPosition, out var packCell))
            {
                bool allowed = CanItemEnterDeploymentPack(_heldInventoryItem);
                var placement = allowed
                    ? GridInventory.StoreItemAtPosition(_basePackGrid.Columns, _basePackGrid.Rows, _panelDeploymentPackItems, _heldInventoryItem, packCell.X, packCell.Y)
                    : new PlacementResult { Placed = false, Items = GridInventory.CloneItems(_panelDeploymentPackItems) };
                if (placement.Placed)
                {
                    _panelDeploymentPackItems = placement.Items;
                    CommitDeploymentInventoryState();
                    ResetInteractiveDragState();
                    return true;
                }
            }

            RestoreHeldInventory(mode);
            return true;
        }

        if (mode == ScenePanelMode.Launch)
        {
            if (_basePackGrid != null && _basePackGrid.TryGetCellAtViewport(viewportPosition, out var packCell))
            {
                bool allowed = CanItemEnterDeploymentPack(_heldInventoryItem);
                var placement = allowed
                    ? GridInventory.StoreItemAtPosition(_basePackGrid.Columns, _basePackGrid.Rows, _panelDeploymentPackItems, _heldInventoryItem, packCell.X, packCell.Y)
                    : new PlacementResult { Placed = false, Items = GridInventory.CloneItems(_panelDeploymentPackItems) };
                if (placement.Placed)
                {
                    _panelDeploymentPackItems = placement.Items;
                    CommitDeploymentInventoryState();
                    ResetInteractiveDragState();
                    return true;
                }
            }

            RestoreHeldInventory(mode);
            return true;
        }

        if (mode != ScenePanelMode.CombatInventory)
        {
            ResetInteractiveDragState();
            return true;
        }

        if (TryGetCombatWeaponSlotIndex(viewportPosition, out var weaponSlotIndex)
            && TryResolveWeaponId(_heldInventoryItem, out var heldWeaponId))
        {
            bool canInsert = _heldInventoryOrigin == HeldInventoryOrigin.CombatWeaponSlot
                || _panelRunLoadoutWeaponIds.Count < WeaponData.MaxLoadoutSize;
            if (canInsert)
            {
                InsertWeaponIntoPanelLoadout(weaponSlotIndex, heldWeaponId);
                CommitCombatInventoryState();
                ResetInteractiveDragState();
                return true;
            }
        }

        if (_heldInventoryOrigin == HeldInventoryOrigin.CombatWeaponSlot && _panelRunLoadoutWeaponIds.Count == 0)
        {
            RestoreHeldInventory(mode);
            return true;
        }

        if (_combatInventoryGrid != null && _combatInventoryGrid.TryGetCellAtViewport(viewportPosition, out var inventoryCell))
        {
            var placement = GridInventory.StoreItemAtPosition(_combatInventoryGrid.Columns, _combatInventoryGrid.Rows, _panelRunItems, _heldInventoryItem, inventoryCell.X, inventoryCell.Y);
            if (placement.Placed)
                {
                    _panelRunItems = placement.Items;
                    CommitCombatInventoryState();
                    ResetInteractiveDragState();
                    return true;
                }
            }

        if (_combatGroundGrid != null && _combatGroundGrid.TryGetCellAtViewport(viewportPosition, out _))
        {
            ReleaseHeldInventoryToGround();
            return true;
        }

        RestoreHeldInventory(mode);
        return true;
    }

    private void RotateHeldInventoryItem()
    {
        if (_heldInventoryItem == null || _heldInventoryItem.Width == _heldInventoryItem.Height)
            return;

        _heldInventoryItem = GridInventory.RotateItem(_heldInventoryItem);
        var store = GameManager.Instance?.Store;
        if (store != null)
            UpdateInventoryInteractionLive(store.State);
    }

    private void AutoArrangeInteractiveInventory(GameState state, ScenePanelMode mode)
    {
        if (mode == ScenePanelMode.Locker || mode == ScenePanelMode.Launch)
        {
            var stashItems = new List<InventoryItemRecord>(_panelStashItems.Select(item => item.Clone()));
            var packItems = new List<InventoryItemRecord>(_panelDeploymentPackItems.Select(item => item.Clone()));

            if (_heldInventoryItem != null)
            {
                if (_heldInventoryOrigin == HeldInventoryOrigin.BaseStash)
                    stashItems.Add(_heldInventoryItem.Clone());
                else if (_heldInventoryOrigin == HeldInventoryOrigin.DeploymentPack)
                    packItems.Add(_heldInventoryItem.Clone());
            }

            _panelStashItems = GridInventory.AutoArrange(state.Save.Inventory.StashColumns, state.Save.Inventory.StashRows, stashItems);
            _panelDeploymentPackItems = GridInventory.AutoArrange(state.Save.Inventory.DeploymentPack.Columns, state.Save.Inventory.DeploymentPack.Rows, packItems);
            CommitDeploymentInventoryState();
            ResetInteractiveDragState();
            return;
        }

        if (mode != ScenePanelMode.CombatInventory || state.Save.Session.ActiveRun == null)
            return;

        var arrangedItems = new List<InventoryItemRecord>(_panelRunItems.Select(item => item.Clone()));
        if (_heldInventoryItem != null && _heldInventoryOrigin == HeldInventoryOrigin.CombatInventory)
            arrangedItems.Add(_heldInventoryItem.Clone());
        else if (_heldInventoryOrigin == HeldInventoryOrigin.CombatWeaponSlot
                 && TryResolveWeaponId(_heldInventoryItem, out var heldWeaponId))
            InsertWeaponIntoPanelLoadout(_heldWeaponSlotIndex < 0 ? _panelRunLoadoutWeaponIds.Count : _heldWeaponSlotIndex, heldWeaponId);

        _panelRunItems = GridInventory.AutoArrange(state.Save.Session.ActiveRun.Inventory.Columns, state.Save.Session.ActiveRun.Inventory.Rows, arrangedItems);
        CommitCombatInventoryState();
        ResetInteractiveDragState();
    }

    private void RestoreHeldInventory(ScenePanelMode mode)
    {
        if (_heldInventoryItem == null)
            return;

        switch (_heldInventoryOrigin)
        {
            case HeldInventoryOrigin.BaseStash:
                if (_heldRestoreItem != null && _baseStashGrid != null)
                {
                    var exact = GridInventory.StoreItemAtPosition(_baseStashGrid.Columns, _baseStashGrid.Rows, _panelStashItems, _heldRestoreItem, _heldRestoreItem.X, _heldRestoreItem.Y);
                    if (exact.Placed)
                    {
                        _panelStashItems = exact.Items;
                        CommitDeploymentInventoryState();
                        break;
                    }

                    var auto = GridInventory.PlaceItemInGrid(_baseStashGrid.Columns, _baseStashGrid.Rows, _panelStashItems, _heldRestoreItem);
                    if (auto.Placed)
                    {
                        _panelStashItems = auto.Items;
                        CommitDeploymentInventoryState();
                    }
                }
                break;

            case HeldInventoryOrigin.DeploymentPack:
                if (_heldRestoreItem != null && _basePackGrid != null)
                {
                    var exact = GridInventory.StoreItemAtPosition(_basePackGrid.Columns, _basePackGrid.Rows, _panelDeploymentPackItems, _heldRestoreItem, _heldRestoreItem.X, _heldRestoreItem.Y);
                    if (exact.Placed)
                    {
                        _panelDeploymentPackItems = exact.Items;
                        CommitDeploymentInventoryState();
                        break;
                    }

                    var auto = GridInventory.PlaceItemInGrid(_basePackGrid.Columns, _basePackGrid.Rows, _panelDeploymentPackItems, _heldRestoreItem);
                    if (auto.Placed)
                    {
                        _panelDeploymentPackItems = auto.Items;
                        CommitDeploymentInventoryState();
                    }
                }
                break;
            case HeldInventoryOrigin.CombatInventory:
                if (_heldRestoreItem != null && _combatInventoryGrid != null)
                {
                    var exact = GridInventory.StoreItemAtPosition(_combatInventoryGrid.Columns, _combatInventoryGrid.Rows, _panelRunItems, _heldRestoreItem, _heldRestoreItem.X, _heldRestoreItem.Y);
                    var fallback = exact.Placed ? exact : GridInventory.StoreItemInGrid(_combatInventoryGrid.Columns, _combatInventoryGrid.Rows, _panelRunItems, _heldRestoreItem);
                    if (fallback.Placed)
                        _panelRunItems = fallback.Items;
                }
                break;
            case HeldInventoryOrigin.CombatWeaponSlot:
                if (TryResolveWeaponId(_heldRestoreItem, out var heldWeaponId))
                    InsertWeaponIntoPanelLoadout(_heldWeaponSlotIndex < 0 ? _panelRunLoadoutWeaponIds.Count : _heldWeaponSlotIndex, heldWeaponId);
                break;
            case HeldInventoryOrigin.GroundLoot:
                if (_heldRestoreGroundDrop != null)
                    _panelGroundLoot.Add(_heldRestoreGroundDrop.Clone());
                break;
        }

        ResetInteractiveDragState();
        var store = GameManager.Instance?.Store;
        if (store != null)
            UpdateInventoryInteractionLive(store.State);
    }

    private void ReleaseHeldInventoryToGround()
    {
        if (_heldInventoryItem == null)
            return;

        if (_heldInventoryOrigin == HeldInventoryOrigin.GroundLoot && _heldRestoreGroundDrop != null)
        {
            _panelGroundLoot.Add(_heldRestoreGroundDrop.Clone());
            ResetInteractiveDragState();
            CommitCombatInventoryState();
            return;
        }

        var snapshot = FindSceneProvider()?.BuildOverlayWorldSnapshot();
        var playerPosition = snapshot?.PlayerPosition ?? Vector2.Zero;
        var offset = GroundDropOffsets[_panelGroundLoot.Count % GroundDropOffsets.Length];
        _panelGroundLoot.Add(new GroundLootDrop
        {
            Id = $"drop-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds():x}-{Guid.NewGuid().ToString()[..6]}",
            Item = _heldInventoryItem.Clone(),
            X = playerPosition.X + offset.X,
            Y = playerPosition.Y + offset.Y,
            Source = LootSource.Manual,
        });

        _panelQuickSlots = GridInventory.SanitizeQuickSlots(_panelQuickSlots, _panelRunItems.Select(item => item.Id));
        CommitCombatInventoryState();
        ResetInteractiveDragState();
    }

    private void BindQuickSlot(int slotIndex)
    {
        var store = GameManager.Instance?.Store;
        if (store == null || ResolvePanelMode(store.State) != ScenePanelMode.CombatInventory)
            return;

        if (_heldInventoryItem != null && _heldInventoryOrigin != HeldInventoryOrigin.CombatInventory)
            return;

        var target = ResolveQuickSlotTarget();
        if (target == null)
        {
            _panelQuickSlots = GridInventory.AssignQuickSlotBinding(_panelQuickSlots, slotIndex, null);
            CommitCombatInventoryState();
            return;
        }

        if (!ItemData.ById.TryGetValue(target.ItemId, out var definition) || definition.Use == null)
            return;

        _panelQuickSlots = GridInventory.AssignQuickSlotBinding(_panelQuickSlots, slotIndex, target.Id);
        CommitCombatInventoryState();
    }

    private InventoryItemRecord? ResolveQuickSlotTarget()
    {
        if (_heldInventoryItem != null && _heldInventoryOrigin == HeldInventoryOrigin.CombatInventory)
            return _heldInventoryItem;

        if (_combatInventoryGrid == null || !_combatInventoryGrid.TryGetCellAtViewport(_panelPointer, out var cell))
            return null;

        return GridInventory.FindItemAtCell(_panelRunItems, cell.X, cell.Y);
    }

    private void CommitCombatInventoryState()
    {
        _panelQuickSlots = GridInventory.SanitizeQuickSlots(_panelQuickSlots, _panelRunItems.Select(item => item.Id));
        GameManager.Instance?.Store?.UpdateActiveRunInventoryState(
            _panelRunItems,
            _panelGroundLoot,
            _panelQuickSlots,
            new List<WeaponType>(_panelRunLoadoutWeaponIds),
            ResolveCommittedCurrentWeaponId());
    }

    private void CommitDeploymentInventoryState()
    {
        GameManager.Instance?.Store?.UpdateDeploymentInventoryState(_panelStashItems, _panelDeploymentPackItems);
    }

    private NearbyGroundLootPanelState BuildNearbyGroundLootPanelState()
    {
        var snapshot = FindSceneProvider()?.BuildOverlayWorldSnapshot();
        var playerPosition = snapshot?.PlayerPosition ?? Vector2.Zero;
        var nearbyDrops = _panelGroundLoot
            .Where(drop => playerPosition.DistanceTo(new Vector2(drop.X, drop.Y)) <= 128f)
            .Select(drop => drop.Clone())
            .ToList();

        var placement = GridInventory.PlaceItemsInGrid(CombatGroundColumns, CombatGroundRows, new List<InventoryItemRecord>(), nearbyDrops.Select(drop => drop.Item).ToList());
        var visibleIds = new HashSet<string>(placement.PlacedIds);
        var dropByItemId = new Dictionary<string, GroundLootDrop>();
        foreach (var drop in nearbyDrops)
        {
            if (visibleIds.Contains(drop.Item.Id))
                dropByItemId[drop.Item.Id] = drop;
        }

        return new NearbyGroundLootPanelState
        {
            Items = placement.Items,
            DropByItemId = dropByItemId,
        };
    }

    private Dictionary<string, string> BuildQuickSlotBadges()
    {
        var badges = new Dictionary<string, string>();
        for (int index = 0; index < _panelQuickSlots.Length; index++)
        {
            string? itemId = _panelQuickSlots[index];
            if (!string.IsNullOrWhiteSpace(itemId))
                badges[itemId] = index < CombatQuickSlotKeys.Length ? CombatQuickSlotKeys[index] : (index + 1).ToString();
        }
        return badges;
    }

    private void UpdateQuickSlotButtons()
    {
        if (_quickSlotButtons.Count == 0)
            return;

        for (int index = 0; index < _quickSlotButtons.Count; index++)
        {
            string hotkey = index < CombatQuickSlotKeys.Length ? CombatQuickSlotKeys[index] : (index + 1).ToString();
            string? itemId = index < _panelQuickSlots.Length ? _panelQuickSlots[index] : null;
            InventoryItemRecord? item = null;
            if (itemId != null)
            {
                item = _panelRunItems.Find(entry => entry.Id == itemId);
                if (item == null && _heldInventoryItem?.Id == itemId && _heldInventoryOrigin == HeldInventoryOrigin.CombatInventory)
                    item = _heldInventoryItem;
            }

            string label = item != null && ItemData.ById.TryGetValue(item.ItemId, out var definition)
                ? $"{hotkey}  {definition.ShortLabel}{GameText.QuantitySuffix(item.Quantity)}"
                : hotkey;

            _quickSlotButtons[index].Text = label;
            _quickSlotButtons[index].AddThemeColorOverride("font_color", item != null ? Palette.UiText : new Color(Palette.UiText, 0.48f));
            _quickSlotButtons[index].AddThemeStyleboxOverride("normal", CreateCombatQuickSlotStyle(item != null, 0.86f));
            _quickSlotButtons[index].AddThemeStyleboxOverride("hover", CreateCombatQuickSlotStyle(item != null, 1f));
            _quickSlotButtons[index].AddThemeStyleboxOverride("pressed", CreateCombatQuickSlotStyle(item != null, 0.74f));
            _quickSlotButtons[index].Disabled = false;
        }
    }

    private void UpdateInstructionLabel(ScenePanelMode mode)
    {
        if (_inventoryInstructionLabel == null)
            return;

        if (_heldInventoryItem == null)
        {
            _inventoryInstructionLabel.Visible = false;
            _inventoryInstructionLabel.Text = string.Empty;
            return;
        }

        _inventoryInstructionLabel.Visible = true;
        string label = ItemData.ById.TryGetValue(_heldInventoryItem.ItemId, out var definition)
            ? definition.Label
            : _heldInventoryItem.ItemId;
        string heldLabel = GameText.Format("inventory.holding", label, GameText.QuantitySuffix(_heldInventoryItem.Quantity));
        if (mode == ScenePanelMode.Launch && !CanItemEnterDeploymentPack(_heldInventoryItem))
            heldLabel += GameText.Text("inventory.blocked_launch");
        _inventoryInstructionLabel.Text = heldLabel;
    }

    private void UpdateDragPreview(ScenePanelMode mode)
    {
        if (_heldInventoryItem == null)
        {
            _dragPreview?.SetPreview(null, Vector2.Zero, 32f, false);
            return;
        }

        if (_dragPreview == null)
            return;

        float cellSize = GetInventoryCellSize();

        if (mode == ScenePanelMode.Locker)
        {
            if (_baseStashGrid != null && _baseStashGrid.TryGetCellAtViewport(_panelPointer, out var cell))
            {
                var topLeft = _baseStashGrid.GetGlobalRect().Position + new Vector2(cell.X * _baseStashGrid.CellSize, cell.Y * _baseStashGrid.CellSize);
                bool valid = GridInventory.CanPlaceItemAtPosition(_baseStashGrid.Columns, _baseStashGrid.Rows, _panelStashItems, _heldInventoryItem, cell.X, cell.Y);
                _dragPreview.SetPreview(_heldInventoryItem, topLeft, _baseStashGrid.CellSize, valid);
                return;
            }

            if (_basePackGrid != null && _basePackGrid.TryGetCellAtViewport(_panelPointer, out var packCell))
            {
                var topLeft = _basePackGrid.GetGlobalRect().Position + new Vector2(packCell.X * _basePackGrid.CellSize, packCell.Y * _basePackGrid.CellSize);
                bool valid = CanItemEnterDeploymentPack(_heldInventoryItem) && GridInventory.CanPlaceItemAtPosition(_basePackGrid.Columns, _basePackGrid.Rows, _panelDeploymentPackItems, _heldInventoryItem, packCell.X, packCell.Y);
                _dragPreview.SetPreview(_heldInventoryItem, topLeft, _basePackGrid.CellSize, valid);
                return;
            }

            var fallbackTopLeftStash = _panelPointer - new Vector2(_heldInventoryItem.Width * cellSize * 0.5f, _heldInventoryItem.Height * cellSize * 0.5f);
            _dragPreview.SetPreview(_heldInventoryItem, fallbackTopLeftStash, cellSize, false);
            return;
        }

        if (mode == ScenePanelMode.Launch)
        {
            if (_basePackGrid != null && _basePackGrid.TryGetCellAtViewport(_panelPointer, out var launchPackCell))
            {
                var topLeft = _basePackGrid.GetGlobalRect().Position + new Vector2(launchPackCell.X * _basePackGrid.CellSize, launchPackCell.Y * _basePackGrid.CellSize);
                bool valid = CanItemEnterDeploymentPack(_heldInventoryItem) && GridInventory.CanPlaceItemAtPosition(_basePackGrid.Columns, _basePackGrid.Rows, _panelDeploymentPackItems, _heldInventoryItem, launchPackCell.X, launchPackCell.Y);
                _dragPreview.SetPreview(_heldInventoryItem, topLeft, _basePackGrid.CellSize, valid);
                return;
            }

            var fallbackTopLeftLaunch = _panelPointer - new Vector2(_heldInventoryItem.Width * cellSize * 0.5f, _heldInventoryItem.Height * cellSize * 0.5f);
            _dragPreview.SetPreview(_heldInventoryItem, fallbackTopLeftLaunch, cellSize, false);
            return;
        }

        if (mode == ScenePanelMode.CombatInventory)
        {
            if (TryResolveWeaponId(_heldInventoryItem, out _)
                && TryGetCombatWeaponSlotIndex(_panelPointer, out var weaponSlotIndex)
                && weaponSlotIndex >= 0
                && weaponSlotIndex < _combatWeaponSlotCards.Count)
            {
                var slotRect = _combatWeaponSlotCards[weaponSlotIndex].GetGlobalRect();
                bool valid = _heldInventoryOrigin == HeldInventoryOrigin.CombatWeaponSlot
                    || _panelRunLoadoutWeaponIds.Count < WeaponData.MaxLoadoutSize;
                _dragPreview.SetPreview(_heldInventoryItem, slotRect.Position + new Vector2(4f, 4f), cellSize, valid);
                return;
            }

            if (_combatInventoryGrid != null && _combatInventoryGrid.TryGetCellAtViewport(_panelPointer, out var packCell))
            {
                var topLeft = _combatInventoryGrid.GetGlobalRect().Position + new Vector2(packCell.X * _combatInventoryGrid.CellSize, packCell.Y * _combatInventoryGrid.CellSize);
                bool valid = GridInventory.CanPlaceItemAtPosition(_combatInventoryGrid.Columns, _combatInventoryGrid.Rows, _panelRunItems, _heldInventoryItem, packCell.X, packCell.Y);
                _dragPreview.SetPreview(_heldInventoryItem, topLeft, _combatInventoryGrid.CellSize, valid);
                return;
            }

            if (_combatGroundGrid != null && _combatGroundGrid.TryGetCellAtViewport(_panelPointer, out var groundCell))
            {
                var topLeft = _combatGroundGrid.GetGlobalRect().Position + new Vector2(groundCell.X * _combatGroundGrid.CellSize, groundCell.Y * _combatGroundGrid.CellSize);
                _dragPreview.SetPreview(_heldInventoryItem, topLeft, _combatGroundGrid.CellSize, true);
                return;
            }
        }

        var fallbackTopLeft = _panelPointer - new Vector2(_heldInventoryItem.Width * cellSize * 0.5f, _heldInventoryItem.Height * cellSize * 0.5f);
        _dragPreview.SetPreview(_heldInventoryItem, fallbackTopLeft, cellSize, false);
    }

    private PanelContainer CreateCombatInventoryPane(string? label = null)
    {
        var card = new PanelContainer
        {
            MouseFilter = Control.MouseFilterEnum.Ignore,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        card.AddThemeStyleboxOverride("panel", CreateCombatFrameStyle(
            new Color(Palette.WorldFloorDeep, 0.84f),
            new Color(Palette.Frame, 0.18f),
            12,
            12,
            12,
            12));

        var body = new VBoxContainer();
        body.AddThemeConstantOverride("separation", string.IsNullOrWhiteSpace(label) ? 0 : 8);
        card.AddChild(body);

        if (!string.IsNullOrWhiteSpace(label))
        {
            var titleLabel = CreateLabel(10, new Color(Palette.Frame, 0.72f), true, 1.2f, false);
            titleLabel.Text = label;
            body.AddChild(titleLabel);
        }

        return card;
    }

    private Button CreateCombatQuickSlotButton(int slotIndex)
    {
        var button = new Button
        {
            Text = slotIndex < CombatQuickSlotKeys.Length ? CombatQuickSlotKeys[slotIndex] : (slotIndex + 1).ToString(),
            FocusMode = Control.FocusModeEnum.None,
            Disabled = false,
            CustomMinimumSize = new Vector2(96f, 40f),
        };
        button.AddThemeFontSizeOverride("font_size", UiScale.Font(11));
        button.AddThemeStyleboxOverride("normal", CreateCombatQuickSlotStyle(false, 0.86f));
        button.AddThemeStyleboxOverride("hover", CreateCombatQuickSlotStyle(false, 1f));
        button.AddThemeStyleboxOverride("pressed", CreateCombatQuickSlotStyle(false, 0.74f));
        return button;
    }

    private static StyleBoxFlat CreateCombatFrameStyle(Color background, Color borderColor, int left, int top, int right, int bottom)
    {
        var style = new StyleBoxFlat
        {
            BgColor = background,
            BorderColor = borderColor,
            ContentMarginLeft = left,
            ContentMarginTop = top,
            ContentMarginRight = right,
            ContentMarginBottom = bottom,
            CornerRadiusTopLeft = 0,
            CornerRadiusTopRight = 0,
            CornerRadiusBottomRight = 0,
            CornerRadiusBottomLeft = 0,
        };
        style.SetBorderWidthAll(1);
        return style;
    }

    private static StyleBoxFlat CreateCombatSlotStyle(Color accent, bool occupied, bool active)
    {
        float backgroundAlpha = occupied ? (active ? 0.88f : 0.76f) : 0.54f;
        float borderAlpha = active ? 0.88f : occupied ? 0.36f : 0.18f;
        return CreateCombatFrameStyle(
            new Color(Palette.BgOuter, backgroundAlpha),
            new Color(occupied ? accent : Palette.FrameSoft, borderAlpha),
            12,
            10,
            12,
            10);
    }

    private static StyleBoxFlat CreateCombatQuickSlotStyle(bool occupied, float emphasis)
    {
        float safeEmphasis = Mathf.Clamp(emphasis, 0.4f, 1f);
        return CreateCombatFrameStyle(
            new Color(Palette.BgOuter, occupied ? 0.82f * safeEmphasis : 0.6f * safeEmphasis),
            new Color(occupied ? Palette.Accent : Palette.FrameSoft, occupied ? 0.76f * safeEmphasis : 0.28f * safeEmphasis),
            10,
            8,
            10,
            8);
    }

    private PanelContainer CreateInteractiveSection(string title, string meta)
    {
        var card = new PanelContainer
        {
            MouseFilter = Control.MouseFilterEnum.Ignore,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        card.AddThemeStyleboxOverride("panel", CreatePanelStyle(
            new Color(Palette.WorldFloorDeep, 0.74f),
            new Color(Palette.Frame, 0.24f),
            16,
            16,
            16,
            16,
            10));

        var body = new VBoxContainer();
        body.AddThemeConstantOverride("separation", 8);
        card.AddChild(body);

        var titleLabel = CreateLabel(12, new Color(Palette.Frame, 0.86f), true, 1.1f, true);
        titleLabel.Text = title;
        body.AddChild(titleLabel);

        var metaLabel = CreateLabel(12, Palette.UiMuted, false, 0.2f, true);
        metaLabel.Text = meta;
        body.AddChild(metaLabel);

        return card;
    }

    private float GetInventoryCellSize()
    {
        return GetViewport().GetVisibleRect().Size.X < 1100f ? 32f : 36f;
    }

    private static bool CanItemEnterDeploymentPack(InventoryItemRecord? item)
    {
        if (item == null || !ItemData.ById.TryGetValue(item.ItemId, out var definition))
            return false;

        return definition.Use != null || definition.Category == ItemCategory.Consumable;
    }
    
    private bool IsPointerOverQuickSlotButton(Vector2 viewportPosition)
    {
        foreach (var button in _quickSlotButtons)
        {
            if (button.GetGlobalRect().HasPoint(viewportPosition))
                return true;
        }

        return false;
    }
}
