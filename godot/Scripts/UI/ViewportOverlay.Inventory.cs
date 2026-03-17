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

    private InventoryGridControl? _baseStashGrid;
    private InventoryGridControl? _basePackGrid;
    private InventoryGridControl? _combatGroundGrid;
    private InventoryGridControl? _combatInventoryGrid;
    private InventoryDragPreviewControl? _dragPreview;
    private readonly List<Button> _quickSlotButtons = new();
    private Label? _inventoryInstructionLabel;

    private List<InventoryItemRecord> _panelStashItems = new();
    private List<InventoryItemRecord> _panelDeploymentPackItems = new();
    private List<InventoryItemRecord> _panelRunItems = new();
    private List<GroundLootDrop> _panelGroundLoot = new();
    private string?[] _panelQuickSlots = new string?[GridInventoryState.RunQuickSlotCount];
    private NearbyGroundLootPanelState _nearbyGroundLoot = new();

    private string _baseSyncKey = "";
    private string _combatSyncKey = "";
    private Vector2 _panelPointer;
    private InventoryItemRecord? _heldInventoryItem;
    private InventoryItemRecord? _heldRestoreItem;
    private GroundLootDrop? _heldRestoreGroundDrop;
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
        _inventoryInstructionLabel = null;
        _quickSlotButtons.Clear();
    }

    private void ResetInteractiveDragState()
    {
        _heldInventoryItem = null;
        _heldRestoreItem = null;
        _heldRestoreGroundDrop = null;
        _heldInventoryOrigin = HeldInventoryOrigin.None;
        _baseSyncKey = string.Empty;
        _combatSyncKey = string.Empty;
        _dragPreview?.SetPreview(null, Vector2.Zero, 32f, false);
    }

    private void BuildLockerInventoryContent(Control parent, InventoryState inventory)
    {
        EnsureBaseStateSynced(inventory);

        var host = CreateInteractiveSection("Base Storage", "Drag items between your base containers and the deployment pack.");
        host.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        var body = host.GetChild<VBoxContainer>(0);

        _inventoryInstructionLabel = CreateLabel(12, Palette.UiMuted, false, 0.2f, true);
        body.AddChild(_inventoryInstructionLabel);

        var gridRow = new HBoxContainer();
        gridRow.AddThemeConstantOverride("separation", 14);
        gridRow.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        body.AddChild(gridRow);

        var containerColumn = new VBoxContainer();
        containerColumn.AddThemeConstantOverride("separation", 8);
        containerColumn.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        gridRow.AddChild(containerColumn);

        var tabsRow = new HBoxContainer();
        tabsRow.AddThemeConstantOverride("separation", 4);
        containerColumn.AddChild(tabsRow);

        var stashTab = CreateSmallButton("Base Stash", true);
        stashTab.AddThemeStyleboxOverride("normal", CreateButtonStyle(true, 1f));
        tabsRow.AddChild(stashTab);

        var stashScroll = new ScrollContainer
        {
            HorizontalScrollMode = ScrollContainer.ScrollMode.Auto,
            VerticalScrollMode = ScrollContainer.ScrollMode.Auto,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0f, 240f)
        };
        containerColumn.AddChild(stashScroll);

        var stashCenter = new CenterContainer();
        stashCenter.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        stashCenter.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        stashScroll.AddChild(stashCenter);

        _baseStashGrid = new InventoryGridControl();
        _baseStashGrid.Configure(inventory.StashColumns, inventory.StashRows, GetInventoryCellSize());
        stashCenter.AddChild(_baseStashGrid);

        var packColumn = new VBoxContainer();
        packColumn.AddThemeConstantOverride("separation", 8);
        packColumn.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        gridRow.AddChild(packColumn);

        var packLabel = CreateLabel(13, Palette.UiText, true, 0.3f, true);
        packLabel.Text = "Deployment Pack";
        packColumn.AddChild(packLabel);

        var packScroll = new ScrollContainer
        {
            HorizontalScrollMode = ScrollContainer.ScrollMode.Auto,
            VerticalScrollMode = ScrollContainer.ScrollMode.Auto,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0f, 240f)
        };
        packColumn.AddChild(packScroll);

        var packCenter = new CenterContainer();
        packCenter.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        packCenter.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        packScroll.AddChild(packCenter);

        _basePackGrid = new InventoryGridControl();
        _basePackGrid.Configure(inventory.DeploymentPack.Columns, inventory.DeploymentPack.Rows, GetInventoryCellSize());
        packCenter.AddChild(_basePackGrid);

        var noteLabel = CreateLabel(12, Palette.UiMuted, false, 0.2f, true);
        noteLabel.Text = "Resources and salvage will automatically be placed into the base stash.";
        body.AddChild(noteLabel);

        parent.AddChild(host);
    }

    private void BuildCombatInventoryContent(RunState activeRun)
    {
        EnsureCombatStateSynced(activeRun);

        var split = new HBoxContainer();
        split.AddThemeConstantOverride("separation", 24);
        split.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        _panelContentContainer.AddChild(split);

        var leftCol = new VBoxContainer();
        leftCol.AddThemeConstantOverride("separation", 12);
        leftCol.CustomMinimumSize = new Vector2(300f, 0f);
        split.AddChild(leftCol);

        var rightCol = new VBoxContainer();
        rightCol.AddThemeConstantOverride("separation", 12);
        rightCol.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        split.AddChild(rightCol);

        var quickSlotsCard = CreateContentCard("Quick slots", "Bound Consumables", "Quick access slots available during combat.");
        var quickSlotsBody = quickSlotsCard.GetChild<VBoxContainer>(0);
        
        var quickRow = new VBoxContainer();
        quickRow.AddThemeConstantOverride("separation", 8);
        quickSlotsBody.AddChild(quickRow);

        for (int index = 0; index < GridInventoryState.RunQuickSlotCount; index++)
        {
            int slotIndex = index;
            var button = CreateSmallButton($"Slot {index + 1}", true);
            button.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            button.Pressed += () => BindQuickSlot(slotIndex);
            quickRow.AddChild(button);
            _quickSlotButtons.Add(button);
        }

        var bindingHint = CreateLabel(12, Palette.UiMuted, false, 0.2f, true);
        bindingHint.Text = "Click a slot or press 4 / 5 / 6 / 7 while hovering a carried consumable to bind it. Click with no target to clear.";
        quickSlotsBody.AddChild(bindingHint);
        
        leftCol.AddChild(quickSlotsCard);

        var host = CreateInteractiveSection("Inventory transfer", "Drag between the nearby ground grid and the carried pack. Drag to ground to drop the held item.");
        var body = host.GetChild<VBoxContainer>(0);

        _inventoryInstructionLabel = CreateLabel(12, Palette.UiMuted, false, 0.2f, true);
        body.AddChild(_inventoryInstructionLabel);

        var gridRow = new HBoxContainer();
        gridRow.AddThemeConstantOverride("separation", 14);
        body.AddChild(gridRow);

        var groundColumn = new VBoxContainer();
        groundColumn.AddThemeConstantOverride("separation", 8);
        gridRow.AddChild(groundColumn);

        var groundLabel = CreateLabel(13, Palette.UiText, true, 0.3f, true);
        groundLabel.Text = "Nearby ground loot";
        groundColumn.AddChild(groundLabel);

        _combatGroundGrid = new InventoryGridControl();
        _combatGroundGrid.Configure(6, 3, GetInventoryCellSize());
        groundColumn.AddChild(_combatGroundGrid);

        var inventoryColumn = new VBoxContainer();
        inventoryColumn.AddThemeConstantOverride("separation", 8);
        gridRow.AddChild(inventoryColumn);

        var inventoryLabel = CreateLabel(13, Palette.UiText, true, 0.3f, true);
        inventoryLabel.Text = "Carried pack";
        inventoryColumn.AddChild(inventoryLabel);

        var inventoryScroll = new ScrollContainer
        {
            HorizontalScrollMode = ScrollContainer.ScrollMode.Auto,
            VerticalScrollMode = ScrollContainer.ScrollMode.Auto,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0f, 300f)
        };
        inventoryColumn.AddChild(inventoryScroll);

        var inventoryCenter = new CenterContainer();
        inventoryCenter.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        inventoryCenter.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        inventoryScroll.AddChild(inventoryCenter);

        _combatInventoryGrid = new InventoryGridControl();
        _combatInventoryGrid.Configure(activeRun.Inventory.Columns, activeRun.Inventory.Rows, GetInventoryCellSize());
        inventoryCenter.AddChild(_combatInventoryGrid);

        rightCol.AddChild(host);
    }

    private void BuildLaunchInventoryContent(Control parent, InventoryState inventory)
    {
        EnsureBaseStateSynced(inventory);

        var packHost = CreateInteractiveSection("Deployment pack", "Only staged items are copied into the next run.");
        packHost.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        var packBody = packHost.GetChild<VBoxContainer>(0);
        
        _inventoryInstructionLabel = CreateLabel(12, Palette.UiMuted, false, 0.2f, true);
        packBody.AddChild(_inventoryInstructionLabel);
        
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

        var controlsRow = new HBoxContainer();
        controlsRow.AddThemeConstantOverride("separation", 10);
        packBody.AddChild(controlsRow);

        var adjustButton = CreateSmallButton("Adjust Supplies", true);
        adjustButton.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        adjustButton.Pressed += () => GameManager.Instance?.Store?.OpenScenePanel(ScenePanelMode.Locker);
        controlsRow.AddChild(adjustButton);

        var noteLabel = CreateLabel(12, Palette.UiMuted, false, 0.2f, true);
        noteLabel.Text = "Resources are settled directly into base stock. The deployment pack only accepts reusable consumables.";
        packBody.AddChild(noteLabel);

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
        _combatInventoryGrid.SetItems(_panelRunItems);
        _combatInventoryGrid.SetBadges(BuildQuickSlotBadges());
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
        string key = $"{itemKey}|{lootKey}|{quickKey}";

        if (_heldInventoryItem == null && _combatSyncKey != key)
        {
            _panelRunItems = GridInventory.CloneItems(activeRun.Inventory.Items);
            _panelGroundLoot = activeRun.GroundLoot.Select(drop => drop.Clone()).ToList();
            _panelQuickSlots = (string?[])activeRun.Inventory.QuickSlots.Clone();
            _combatSyncKey = key;
        }
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
        GameManager.Instance?.Store?.UpdateActiveRunInventoryState(_panelRunItems, _panelGroundLoot, _panelQuickSlots);
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

        var placement = GridInventory.PlaceItemsInGrid(6, 3, new List<InventoryItemRecord>(), nearbyDrops.Select(drop => drop.Item).ToList());
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
                badges[itemId] = (index + 1).ToString();
        }
        return badges;
    }

    private void UpdateQuickSlotButtons()
    {
        if (_quickSlotButtons.Count == 0)
            return;

        for (int index = 0; index < _quickSlotButtons.Count; index++)
        {
            string? itemId = index < _panelQuickSlots.Length ? _panelQuickSlots[index] : null;
            InventoryItemRecord? item = null;
            if (itemId != null)
            {
                item = _panelRunItems.Find(entry => entry.Id == itemId);
                if (item == null && _heldInventoryItem?.Id == itemId && _heldInventoryOrigin == HeldInventoryOrigin.CombatInventory)
                    item = _heldInventoryItem;
            }

            string label = item != null && ItemData.ById.TryGetValue(item.ItemId, out var definition)
                ? $"{index + 1}  {definition.ShortLabel}{(item.Quantity > 1 ? $" x{item.Quantity}" : "")}"
                : $"{index + 1}  Empty";

            _quickSlotButtons[index].Text = label;
            _quickSlotButtons[index].Disabled = false;
        }
    }

    private void UpdateInstructionLabel(ScenePanelMode mode)
    {
        if (_inventoryInstructionLabel == null)
            return;

        if (_heldInventoryItem == null)
        {
            _inventoryInstructionLabel.Text = mode switch
            {
                ScenePanelMode.Locker => "Pick up an item with left mouse, place it on another cell, press R to rotate, and press Z to auto-arrange.",
                ScenePanelMode.Launch => "Move consumables between stash and deployment pack with left mouse. Press R to rotate and Z to auto-arrange.",
                _ => "Pick from ground or pack with left mouse. Drop to pack to store it, drop to ground to release it, and press R / Z for rotate or auto-arrange.",
            };
            return;
        }

        string label = ItemData.ById.TryGetValue(_heldInventoryItem.ItemId, out var definition)
            ? definition.Label
            : _heldInventoryItem.ItemId;
        string heldLabel = $"Holding: {label}{(_heldInventoryItem.Quantity > 1 ? $" x{_heldInventoryItem.Quantity}" : "")}";
        if (mode == ScenePanelMode.Launch && !CanItemEnterDeploymentPack(_heldInventoryItem))
            heldLabel += " / cannot enter deployment pack";
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
