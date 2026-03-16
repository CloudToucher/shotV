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

    private InventoryGridControl? _lockerGrid;
    private InventoryGridControl? _combatGroundGrid;
    private InventoryGridControl? _combatInventoryGrid;
    private InventoryDragPreviewControl? _dragPreview;
    private readonly List<Button> _quickSlotButtons = new();
    private Label? _inventoryInstructionLabel;

    private List<InventoryItemRecord> _panelStashItems = new();
    private List<InventoryItemRecord> _panelRunItems = new();
    private List<GroundLootDrop> _panelGroundLoot = new();
    private string?[] _panelQuickSlots = new string?[GridInventoryState.RunQuickSlotCount];
    private NearbyGroundLootPanelState _nearbyGroundLoot = new();

    private string _lockerSyncKey = "";
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
        if (mode is not ScenePanelMode.Locker and not ScenePanelMode.CombatInventory)
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
        _lockerGrid = null;
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
        _lockerSyncKey = string.Empty;
        _combatSyncKey = string.Empty;
        _dragPreview?.SetPreview(null, Vector2.Zero, 32f, false);
    }

    private void BuildLockerInventoryContent(InventoryState inventory)
    {
        EnsureLockerStateSynced(inventory);

        var host = CreateInteractiveSection("Locker grid", "Left mouse picks and places. R rotates held items. Z auto-arranges.");
        var body = host.GetChild<VBoxContainer>(0);

        _inventoryInstructionLabel = CreateLabel(12, Palette.UiMuted, false, 0.2f, true);
        body.AddChild(_inventoryInstructionLabel);

        _lockerGrid = new InventoryGridControl();
        _lockerGrid.Configure(inventory.StashColumns, inventory.StashRows, GetInventoryCellSize());
        body.AddChild(_lockerGrid);
        _panelContentColumn.AddChild(host);
    }

    private void BuildCombatInventoryContent(RunState activeRun)
    {
        EnsureCombatStateSynced(activeRun);

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

        _combatInventoryGrid = new InventoryGridControl();
        _combatInventoryGrid.Configure(activeRun.Inventory.Columns, activeRun.Inventory.Rows, GetInventoryCellSize());
        inventoryColumn.AddChild(_combatInventoryGrid);

        var quickSlotsLabel = CreateLabel(13, Palette.UiText, true, 0.3f, true);
        quickSlotsLabel.Text = "Quick slots";
        body.AddChild(quickSlotsLabel);

        var quickRow = new HBoxContainer();
        quickRow.AddThemeConstantOverride("separation", 8);
        body.AddChild(quickRow);

        for (int index = 0; index < GridInventoryState.RunQuickSlotCount; index++)
        {
            int slotIndex = index;
            var button = CreateSmallButton($"Slot {index + 1}", true);
            button.CustomMinimumSize = new Vector2(124f, 0f);
            button.Pressed += () => BindQuickSlot(slotIndex);
            quickRow.AddChild(button);
            _quickSlotButtons.Add(button);
        }

        var bindingHint = CreateLabel(12, Palette.UiMuted, false, 0.2f, true);
        bindingHint.Text = "Click a slot or press 4 / 5 / 6 / 7 while hovering a carried consumable to bind it. Click with no target to clear.";
        body.AddChild(bindingHint);

        _panelContentColumn.AddChild(host);
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
            if (_lockerGrid == null)
                return;

            _lockerGrid.SetItems(_panelStashItems);
            _lockerGrid.SetBadges(null);
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

    private void EnsureLockerStateSynced(InventoryState inventory)
    {
        string key = string.Join(",", inventory.StoredItems.Select(item => $"{item.Id}:{item.ItemId}:{item.Quantity}:{item.X}:{item.Y}:{item.Width}:{item.Height}:{item.Rotated}"));
        if (_heldInventoryItem == null && _lockerSyncKey != key)
        {
            _panelStashItems = GridInventory.CloneItems(inventory.StoredItems);
            _lockerSyncKey = key;
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
            if (_lockerGrid == null || !_lockerGrid.TryGetCellAtViewport(viewportPosition, out var cell))
                return false;

            var extraction = GridInventory.PickItemFromGridAtCell(_panelStashItems, cell.X, cell.Y);
            if (extraction.Item == null)
                return false;

            _heldInventoryItem = extraction.Item;
            _heldRestoreItem = extraction.Item.Clone();
            _heldInventoryOrigin = HeldInventoryOrigin.BaseStash;
            _panelStashItems = extraction.Items;
            UpdateInventoryInteractionLive(state);
            return true;
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
            if (_lockerGrid != null && _lockerGrid.TryGetCellAtViewport(viewportPosition, out var cell))
            {
                var placement = GridInventory.PlaceItemAtPosition(_lockerGrid.Columns, _lockerGrid.Rows, _panelStashItems, _heldInventoryItem, cell.X, cell.Y);
                if (placement.Placed)
                {
                    _panelStashItems = placement.Items;
                    GameManager.Instance?.Store?.UpdateBaseStashItems(_panelStashItems);
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
            var placement = GridInventory.PlaceItemAtPosition(_combatInventoryGrid.Columns, _combatInventoryGrid.Rows, _panelRunItems, _heldInventoryItem, inventoryCell.X, inventoryCell.Y);
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
        if (mode == ScenePanelMode.Locker)
        {
            var items = new List<InventoryItemRecord>(_panelStashItems.Select(item => item.Clone()));
            if (_heldInventoryItem != null && _heldInventoryOrigin == HeldInventoryOrigin.BaseStash)
                items.Add(_heldInventoryItem.Clone());

            _panelStashItems = GridInventory.AutoArrange(state.Save.Inventory.StashColumns, state.Save.Inventory.StashRows, items);
            GameManager.Instance?.Store?.UpdateBaseStashItems(_panelStashItems);
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
                if (_heldRestoreItem != null && _lockerGrid != null)
                {
                    var exact = GridInventory.PlaceItemAtPosition(_lockerGrid.Columns, _lockerGrid.Rows, _panelStashItems, _heldRestoreItem, _heldRestoreItem.X, _heldRestoreItem.Y);
                    var fallback = exact.Placed ? exact : GridInventory.PlaceItemInGrid(_lockerGrid.Columns, _lockerGrid.Rows, _panelStashItems, _heldRestoreItem);
                    if (fallback.Placed)
                        _panelStashItems = fallback.Items;
                }
                break;
            case HeldInventoryOrigin.CombatInventory:
                if (_heldRestoreItem != null && _combatInventoryGrid != null)
                {
                    var exact = GridInventory.PlaceItemAtPosition(_combatInventoryGrid.Columns, _combatInventoryGrid.Rows, _panelRunItems, _heldRestoreItem, _heldRestoreItem.X, _heldRestoreItem.Y);
                    var fallback = exact.Placed ? exact : GridInventory.PlaceItemInGrid(_combatInventoryGrid.Columns, _combatInventoryGrid.Rows, _panelRunItems, _heldRestoreItem);
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
            _inventoryInstructionLabel.Text = mode == ScenePanelMode.Locker
                ? "Pick up an item with left mouse, place it on another cell, press R to rotate, and press Z to auto-arrange."
                : "Pick from ground or pack with left mouse. Drop to pack to store it, drop to ground to release it, and press R / Z for rotate or auto-arrange.";
            return;
        }

        string label = ItemData.ById.TryGetValue(_heldInventoryItem.ItemId, out var definition)
            ? definition.Label
            : _heldInventoryItem.ItemId;
        _inventoryInstructionLabel.Text = $"Holding: {label}{(_heldInventoryItem.Quantity > 1 ? $" x{_heldInventoryItem.Quantity}" : "")}";
    }

    private void UpdateDragPreview(ScenePanelMode mode)
    {
        if (_dragPreview == null)
            return;

        if (_heldInventoryItem == null)
        {
            _dragPreview.SetPreview(null, Vector2.Zero, 32f, false);
            return;
        }

        float cellSize = GetInventoryCellSize();
        if (mode == ScenePanelMode.Locker && _lockerGrid != null && _lockerGrid.TryGetCellAtViewport(_panelPointer, out var stashCell))
        {
            var topLeft = _lockerGrid.GetGlobalRect().Position + new Vector2(stashCell.X * _lockerGrid.CellSize, stashCell.Y * _lockerGrid.CellSize);
            bool valid = GridInventory.CanPlaceItemAtPosition(_lockerGrid.Columns, _lockerGrid.Rows, _panelStashItems, _heldInventoryItem, stashCell.X, stashCell.Y);
            _dragPreview.SetPreview(_heldInventoryItem, topLeft, _lockerGrid.CellSize, valid);
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
