using System.Collections.Generic;
using System.Linq;
using Godot;
using ShotV.Core;
using ShotV.Data;
using ShotV.State;
using ShotV.World;

namespace ShotV.UI;

public partial class ViewportOverlay
{
    private PanelContainer _baseBanner = null!;
    private Label _baseBannerTitle = null!;
    private Label _baseBannerHint = null!;

    private Control _mapLayer = null!;
    private OverviewMapControl _overviewMap = null!;
    private PanelContainer _overviewTitleCard = null!;
    private PanelContainer _overviewMetaCard = null!;
    private PanelContainer _overviewHintCard = null!;
    private Label _overviewTitle = null!;
    private Label _overviewMeta = null!;
    private Label _overviewHint = null!;

    private Control _panelLayer = null!;
    private PanelContainer _panelFrame = null!;
    private Label _panelKicker = null!;
    private Label _panelTitle = null!;
    private Label _panelMeta = null!;
    private HBoxContainer _panelTabs = null!;
    private HBoxContainer _panelBodyRow = null!;
    private VBoxContainer _panelSummaryColumn = null!;
    private VBoxContainer _panelContentColumn = null!;
    private Label _panelFooter = null!;
    private Button _panelPrimaryButton = null!;
    private Button _panelSecondaryButton = null!;

    private string _panelContentKey = "";
    private string _panelTabKey = "";

    private void BuildFullscreenUi()
    {
        BuildBaseBanner();
        BuildMapOverlay();
        BuildPanelOverlay();
    }

    private void UpdateFullscreenLayout()
    {
        if (_panelFrame == null)
            return;

        bool compact = GetViewport().GetVisibleRect().Size.X < 1100f;
        _panelFrame.CustomMinimumSize = new Vector2(compact ? 860f : 980f, 0f);
        _panelSummaryColumn.CustomMinimumSize = new Vector2(compact ? 220f : 260f, 0f);
        _panelContentColumn.CustomMinimumSize = new Vector2(compact ? 420f : 560f, 0f);
    }

    private void UpdateFullscreenLive()
    {
        var store = GameManager.Instance?.Store;
        if (store == null)
            return;

        var state = store.State;
        var activeRun = state.Save.Session.ActiveRun;
        var selectedRoute = RouteData.GetRoute(state.Save.World.SelectedRouteId);
        var currentRoute = activeRun != null ? RouteData.GetRoute(activeRun.Map.RouteId) : selectedRoute;
        var currentZone = activeRun != null ? RouteManager.GetCurrentRunZone(activeRun.Map) : null;
        bool showSettlement = state.Mode == GameMode.Combat && activeRun?.Status == RunStateStatus.AwaitingSettlement;

        RefreshBaseBanner(state, selectedRoute, currentRoute, currentZone, showSettlement);
        RefreshOverviewMap(state, selectedRoute, currentRoute, currentZone, activeRun, showSettlement);
        UpdateInventoryInteractionLive(state);
    }

    private void UpdateFullscreenFromStore(GameState state, WorldRouteDefinition selectedRoute, WorldRouteDefinition currentRoute, RunZoneState? currentZone, RunState? activeRun, bool showSettlement)
    {
        RefreshBaseBanner(state, selectedRoute, currentRoute, currentZone, showSettlement);
        RefreshOverviewMap(state, selectedRoute, currentRoute, currentZone, activeRun, showSettlement);
        RefreshScenePanel(state, selectedRoute, currentRoute, currentZone, activeRun, showSettlement);
    }

    private void BuildBaseBanner()
    {
        _baseBanner = new PanelContainer
        {
            Visible = false,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        _baseBanner.AnchorLeft = 0.5f;
        _baseBanner.AnchorRight = 0.5f;
        _baseBanner.OffsetLeft = -220f;
        _baseBanner.OffsetTop = 22f;
        _baseBanner.OffsetRight = 220f;
        _baseBanner.OffsetBottom = 90f;
        _baseBanner.AddThemeStyleboxOverride("panel", CreatePanelStyle(
            new Color(1f, 1f, 1f, 0.9f),
            new Color(Palette.Frame, 0.28f),
            18,
            14,
            18,
            14,
            12));
        _root.AddChild(_baseBanner);

        var body = new VBoxContainer();
        body.AddThemeConstantOverride("separation", 6);
        _baseBanner.AddChild(body);

        _baseBannerTitle = CreateLabel(17, Palette.UiText, true, 0.4f, true);
        _baseBannerTitle.HorizontalAlignment = HorizontalAlignment.Center;
        body.AddChild(_baseBannerTitle);

        _baseBannerHint = CreateLabel(12, Palette.UiMuted, false, 0.2f, true);
        _baseBannerHint.HorizontalAlignment = HorizontalAlignment.Center;
        body.AddChild(_baseBannerHint);
    }

    private void BuildMapOverlay()
    {
        _mapLayer = new Control
        {
            Visible = false,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        _mapLayer.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _root.AddChild(_mapLayer);

        _overviewMap = new OverviewMapControl();
        _overviewMap.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _mapLayer.AddChild(_overviewMap);

        _overviewTitleCard = CreateMapCard(24f, 18f, 360f, 78f);
        _mapLayer.AddChild(_overviewTitleCard);
        _overviewTitle = CreateLabel(18, Palette.UiText, true, 0.5f, true);
        _overviewTitleCard.AddChild(_overviewTitle);

        _overviewMetaCard = CreateMapCard(0f, 18f, 308f, 92f);
        _overviewMetaCard.AnchorLeft = 1f;
        _overviewMetaCard.AnchorRight = 1f;
        _overviewMetaCard.OffsetLeft = -332f;
        _overviewMetaCard.OffsetRight = -24f;
        _mapLayer.AddChild(_overviewMetaCard);
        _overviewMeta = CreateLabel(12, Palette.UiMuted, false, 0.2f, true);
        _overviewMetaCard.AddChild(_overviewMeta);

        _overviewHintCard = CreateMapCard(24f, 0f, 500f, 100f);
        _overviewHintCard.AnchorTop = 1f;
        _overviewHintCard.AnchorBottom = 1f;
        _overviewHintCard.OffsetTop = -132f;
        _overviewHintCard.OffsetBottom = -32f;
        _mapLayer.AddChild(_overviewHintCard);
        _overviewHint = CreateLabel(12, Palette.UiText, false, 0.2f, true);
        _overviewHintCard.AddChild(_overviewHint);
    }

    private void BuildPanelOverlay()
    {
        _panelLayer = new Control
        {
            Visible = false,
            MouseFilter = Control.MouseFilterEnum.Stop,
        };
        _panelLayer.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _root.AddChild(_panelLayer);

        var backdrop = new ColorRect
        {
            Color = new Color(Palette.BgOuter, 0.88f),
            MouseFilter = Control.MouseFilterEnum.Stop,
        };
        backdrop.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _panelLayer.AddChild(backdrop);

        var host = new CenterContainer
        {
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        host.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _panelLayer.AddChild(host);

        _panelFrame = new PanelContainer
        {
            CustomMinimumSize = new Vector2(980f, 0f),
            MouseFilter = Control.MouseFilterEnum.Stop,
        };
        _panelFrame.AddThemeStyleboxOverride("panel", CreatePanelStyle(
            new Color(1f, 1f, 1f, 0.96f),
            new Color(Palette.Frame, 0.42f),
            24,
            22,
            24,
            20,
            18));
        host.AddChild(_panelFrame);

        var body = new VBoxContainer();
        body.AddThemeConstantOverride("separation", 14);
        _panelFrame.AddChild(body);

        var accentLine = new ColorRect
        {
            Color = new Color(Palette.PanelWarm, 0.92f),
            CustomMinimumSize = new Vector2(56f, 2f),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        body.AddChild(accentLine);

        _panelKicker = CreateLabel(11, new Color(Palette.Frame, 0.92f), true, 1.6f);
        _panelKicker.Text = "OPERATIONS PANEL";
        body.AddChild(_panelKicker);

        _panelTitle = CreateLabel(24, Palette.UiText, true, 0.6f, true);
        body.AddChild(_panelTitle);

        _panelMeta = CreateLabel(13, Palette.UiMuted, false, 0.2f, true);
        body.AddChild(_panelMeta);

        _panelTabs = new HBoxContainer();
        _panelTabs.AddThemeConstantOverride("separation", 8);
        body.AddChild(_panelTabs);

        _panelBodyRow = new HBoxContainer();
        _panelBodyRow.AddThemeConstantOverride("separation", 14);
        body.AddChild(_panelBodyRow);

        _panelSummaryColumn = new VBoxContainer();
        _panelSummaryColumn.AddThemeConstantOverride("separation", 10);
        _panelBodyRow.AddChild(_panelSummaryColumn);

        var scroll = new ScrollContainer
        {
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            VerticalScrollMode = ScrollContainer.ScrollMode.Auto,
            MouseFilter = Control.MouseFilterEnum.Stop,
            CustomMinimumSize = new Vector2(560f, 420f),
        };
        scroll.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        scroll.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        _panelBodyRow.AddChild(scroll);

        _panelContentColumn = new VBoxContainer();
        _panelContentColumn.AddThemeConstantOverride("separation", 10);
        _panelContentColumn.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        scroll.AddChild(_panelContentColumn);

        var footerRow = new HBoxContainer();
        footerRow.AddThemeConstantOverride("separation", 10);
        body.AddChild(footerRow);

        _panelFooter = CreateLabel(12, Palette.UiMuted, false, 0.2f, true);
        _panelFooter.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        _panelFooter.Text = "Tab closes the panel. M opens the full map.";
        footerRow.AddChild(_panelFooter);

        _panelSecondaryButton = CreateActionButton("Close", false);
        _panelSecondaryButton.Pressed += OnPanelSecondaryPressed;
        footerRow.AddChild(_panelSecondaryButton);

        _panelPrimaryButton = CreateActionButton("Apply", true);
        _panelPrimaryButton.Pressed += OnPanelPrimaryPressed;
        footerRow.AddChild(_panelPrimaryButton);

        BuildInventoryInteractionUi();
    }

    private void RefreshBaseBanner(GameState state, WorldRouteDefinition selectedRoute, WorldRouteDefinition currentRoute, RunZoneState? currentZone, bool showSettlement)
    {
        bool visible = state.Mode == GameMode.Base && !showSettlement && !state.Runtime.MapOverlayOpen && !state.Runtime.PanelOpen;
        _baseBanner.Visible = visible;
        if (!visible)
            return;

        _baseBannerTitle.Text = $"BASE CAMP / {selectedRoute.Label}";
        _baseBannerHint.Text = !string.IsNullOrWhiteSpace(state.Runtime.PrimaryActionHint)
            ? state.Runtime.PrimaryActionHint
            : selectedRoute.Summary;
    }

    private void RefreshOverviewMap(GameState state, WorldRouteDefinition selectedRoute, WorldRouteDefinition currentRoute, RunZoneState? currentZone, RunState? activeRun, bool showSettlement)
    {
        bool visible = state.Runtime.MapOverlayOpen && !showSettlement;
        _mapLayer.Visible = visible;
        if (!visible)
        {
            _overviewMap.SetSnapshot(null);
            return;
        }

        var snapshot = FindSceneProvider()?.BuildOverlayWorldSnapshot();
        _overviewMap.SetSnapshot(snapshot);

        string focusLabel = ResolveFocusedMarkerLabel(snapshot);
        if (state.Mode == GameMode.Base)
        {
            _overviewTitle.Text = $"BASE OVERVIEW / {selectedRoute.Label}";
            _overviewMeta.Text = $"Focus: {focusLabel}\nLegend: player / camera / markers";
            _overviewHint.Text = "Review station positions, route selection, and launch lane before deployment. Press M to close.";
            return;
        }

        string zoneLabel = currentZone?.Label ?? "Current Zone";
        int enemyCount = snapshot?.EnemyPositions.Count ?? 0;
        _overviewTitle.Text = $"{currentRoute.Label} / {zoneLabel}";
        _overviewMeta.Text = $"Focus: {focusLabel}\nEnemies: {enemyCount} / Nearby loot: {state.Runtime.NearbyLootCount}";
        _overviewHint.Text = BuildSceneHint(state);
    }

    private PanelContainer CreateMapCard(float left, float top, float width, float height)
    {
        var card = new PanelContainer
        {
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        card.OffsetLeft = left;
        card.OffsetTop = top;
        card.OffsetRight = left + width;
        card.OffsetBottom = top + height;
        card.AddThemeStyleboxOverride("panel", CreatePanelStyle(
            new Color(1f, 1f, 1f, 0.95f),
            new Color(Palette.Frame, 0.34f),
            16,
            14,
            16,
            14,
            12));
        return card;
    }

    private IOverlaySceneDataProvider? FindSceneProvider()
    {
        if (GetParent() is not Node parent)
            return null;

        foreach (var child in parent.GetChildren())
        {
            if (child == this)
                continue;

            if (child is IOverlaySceneDataProvider provider)
                return provider;
        }

        return null;
    }

    private static string ResolveFocusedMarkerLabel(OverlayWorldSnapshot? snapshot)
    {
        if (snapshot == null)
            return "No focus";

        var marker = snapshot.Markers.FirstOrDefault(entry => entry.Id == snapshot.HighlightedMarkerId);
        return marker?.Label ?? "No focus";
    }

    private void RefreshScenePanel(GameState state, WorldRouteDefinition selectedRoute, WorldRouteDefinition currentRoute, RunZoneState? currentZone, RunState? activeRun, bool showSettlement)
    {
        bool visible = state.Runtime.PanelOpen && !showSettlement;
        _panelLayer.Visible = visible;
        if (!visible)
        {
            ResetInteractiveDragState();
            ResetInteractiveWidgetRefs();
            _panelContentKey = string.Empty;
            _panelTabKey = string.Empty;
            return;
        }

        var mode = ResolvePanelMode(state);
        RebuildPanelTabsIfNeeded(state.Mode, mode);
        UpdatePanelHeader(state, selectedRoute, currentRoute, currentZone, activeRun, mode);
        RebuildPanelContentIfNeeded(state, selectedRoute, currentRoute, currentZone, activeRun, mode);
        UpdatePanelActions(state, activeRun, mode);
    }

    private void UpdatePanelHeader(GameState state, WorldRouteDefinition selectedRoute, WorldRouteDefinition currentRoute, RunZoneState? currentZone, RunState? activeRun, ScenePanelMode mode)
    {
        _panelTitle.Text = mode switch
        {
            ScenePanelMode.Locker => "Locker / Stash",
            ScenePanelMode.Workshop => "Workshop / Loadout",
            ScenePanelMode.Command => "Command / Routes",
            ScenePanelMode.Launch => "Launch / Deploy",
            ScenePanelMode.CombatInventory => "Combat Pack / Loot",
            _ => state.Mode == GameMode.Base ? "Base Overview" : "Mission Overview",
        };

        _panelMeta.Text = mode switch
        {
            ScenePanelMode.CombatInventory when activeRun != null => $"{currentRoute.Label} / {currentZone?.Label ?? activeRun.Map.CurrentZoneId}",
            ScenePanelMode.Command => $"Selected route: {selectedRoute.Label}",
            ScenePanelMode.Workshop => "Loadout order is mirrored to weapon slots 1 / 2 / 3.",
            ScenePanelMode.Locker => $"Stored items: {state.Save.Inventory.StoredItems.Count}",
            ScenePanelMode.Launch => $"Deployment target: {selectedRoute.Label}",
            _ => state.Mode == GameMode.Base ? selectedRoute.Summary : BuildSceneHint(state),
        };
    }

    private void RebuildPanelTabsIfNeeded(GameMode gameMode, ScenePanelMode mode)
    {
        string key = $"{gameMode}:{mode}";
        if (_panelTabKey == key)
            return;

        ClearChildren(_panelTabs);
        foreach (var (tabMode, label) in GetTabs(gameMode))
        {
            var button = CreateTabButton(label, tabMode == mode);
            button.Pressed += () => GameManager.Instance?.Store?.OpenScenePanel(tabMode);
            _panelTabs.AddChild(button);
        }

        _panelTabKey = key;
    }

    private void RebuildPanelContentIfNeeded(GameState state, WorldRouteDefinition selectedRoute, WorldRouteDefinition currentRoute, RunZoneState? currentZone, RunState? activeRun, ScenePanelMode mode)
    {
        string key = BuildPanelContentKey(state, mode);
        if (_panelContentKey == key)
            return;

        ResetInteractiveWidgetRefs();
        ClearChildren(_panelSummaryColumn);
        ClearChildren(_panelContentColumn);

        BuildSummaryCards(state, selectedRoute, currentRoute, currentZone, activeRun);
        BuildContentCards(state, selectedRoute, currentRoute, currentZone, activeRun, mode);

        _panelContentKey = key;
    }

    private void BuildSummaryCards(GameState state, WorldRouteDefinition selectedRoute, WorldRouteDefinition currentRoute, RunZoneState? currentZone, RunState? activeRun)
    {
        if (state.Mode == GameMode.Base)
        {
            AddSummaryCard("Resources", FormatBaseResources(state.Save.Base.Resources));
            AddSummaryCard("Route", $"{selectedRoute.Label}\nZones: {selectedRoute.Zones.Length}");
            AddSummaryCard("Loadout", FormatLoadout(state.Save.Inventory.EquippedWeaponIds));
            AddSummaryCard("Stash", $"Items: {state.Save.Inventory.StoredItems.Count}\nDiscovered zones: {state.Save.World.DiscoveredZones.Count}");
            return;
        }

        if (activeRun == null)
            return;

        AddSummaryCard("Resources", FormatResourceBundle(activeRun.Resources, "No carried resources"));
        AddSummaryCard("Zone", $"{currentRoute.Label}\n{currentZone?.Label ?? activeRun.Map.CurrentZoneId}");
        AddSummaryCard("Loadout", FormatLoadout(activeRun.Player.LoadoutWeaponIds));
        AddSummaryCard("Runtime", $"Kills: {activeRun.Stats.Kills}\nNearby loot: {state.Runtime.NearbyLootCount}");
    }

    private void BuildContentCards(GameState state, WorldRouteDefinition selectedRoute, WorldRouteDefinition currentRoute, RunZoneState? currentZone, RunState? activeRun, ScenePanelMode mode)
    {
        switch (mode)
        {
            case ScenePanelMode.Locker:
                BuildLockerPanel(state.Save.Inventory);
                break;
            case ScenePanelMode.Workshop:
                BuildWorkshopPanel(state.Save.Inventory);
                break;
            case ScenePanelMode.Command:
                BuildCommandPanel(state.Save.World.SelectedRouteId);
                break;
            case ScenePanelMode.Launch:
                BuildLaunchPanel(state, selectedRoute);
                break;
            case ScenePanelMode.CombatInventory:
                if (activeRun != null)
                    BuildCombatInventoryPanel(activeRun, currentRoute, currentZone);
                break;
            default:
                BuildOverviewPanel(state, selectedRoute, currentRoute, currentZone, activeRun);
                break;
        }
    }

    private void BuildOverviewPanel(GameState state, WorldRouteDefinition selectedRoute, WorldRouteDefinition currentRoute, RunZoneState? currentZone, RunState? activeRun)
    {
        if (state.Mode == GameMode.Base)
        {
            AddContentCard("Deployment target", selectedRoute.Label, selectedRoute.Summary);
            AddContentCard("Current stash", $"Stored items: {state.Save.Inventory.StoredItems.Count}", "Open Locker for item details and auto-arrange.");
            AddContentCard("Workshop", FormatLoadout(state.Save.Inventory.EquippedWeaponIds), "Open Workshop to reorder the loadout used by weapon slots.");
            if (state.Save.Session.LastExtraction != null)
                AddContentCard("Last extraction", FormatOutcome(state.Save.Session.LastExtraction.Outcome), state.Save.Session.LastExtraction.SummaryLabel);
            return;
        }

        if (activeRun == null)
            return;

        AddContentCard("AO", $"{currentRoute.Label} / {currentZone?.Label ?? activeRun.Map.CurrentZoneId}", currentZone?.Description ?? "Mission is active.");
        AddContentCard("Pack", $"Items carried: {activeRun.Inventory.Items.Count}", $"Quick slots: {FormatQuickSlots(activeRun.Inventory)}");
        AddContentCard("Loot", $"Ground loot: {activeRun.GroundLoot.Count}", $"Nearby pickups: {state.Runtime.NearbyLootCount}");
    }

    private void BuildLockerPanel(InventoryState inventory)
    {
        BuildLockerInventoryContent(inventory);
    }

    private void BuildWorkshopPanel(InventoryState inventory)
    {
        for (int index = 0; index < inventory.EquippedWeaponIds.Count; index++)
        {
            int weaponIndex = index;
            var weaponId = inventory.EquippedWeaponIds[index];
            var weapon = WeaponData.ById[weaponId];
            var card = CreateContentCard("Weapon slot", $"{index + 1}. {weapon.Label}", weapon.Hint);
            var body = card.GetChild<VBoxContainer>(0);

            var actionRow = new HBoxContainer();
            actionRow.AddThemeConstantOverride("separation", 8);
            body.AddChild(actionRow);

            var upButton = CreateSmallButton("Move Up", weaponIndex > 0);
            upButton.Pressed += () => GameManager.Instance?.Store?.MoveEquippedWeapon(weaponIndex, weaponIndex - 1);
            actionRow.AddChild(upButton);

            var downButton = CreateSmallButton("Move Down", weaponIndex < inventory.EquippedWeaponIds.Count - 1);
            downButton.Pressed += () => GameManager.Instance?.Store?.MoveEquippedWeapon(weaponIndex, weaponIndex + 1);
            actionRow.AddChild(downButton);

            _panelContentColumn.AddChild(card);
        }
    }

    private void BuildCommandPanel(string selectedRouteId)
    {
        foreach (var route in RouteData.Routes)
        {
            var card = CreateContentCard("Route", route.Label, route.Summary);
            var body = card.GetChild<VBoxContainer>(0);

            var zonesLabel = CreateLabel(12, Palette.UiMuted, false, 0.2f, true);
            zonesLabel.Text = string.Join(" / ", route.Zones.Select(zone => zone.Label));
            body.AddChild(zonesLabel);

            var button = CreateSmallButton(route.Id == selectedRouteId ? "Selected" : "Select Route", route.Id != selectedRouteId);
            button.Pressed += () => GameManager.Instance?.Store?.SelectWorldRoute(route.Id);
            body.AddChild(button);

            _panelContentColumn.AddChild(card);
        }
    }

    private void BuildLaunchPanel(GameState state, WorldRouteDefinition selectedRoute)
    {
        AddContentCard("Selected route", selectedRoute.Label, selectedRoute.Summary);
        AddContentCard("Loadout", FormatLoadout(state.Save.Inventory.EquippedWeaponIds), "This order is mirrored to combat weapon slots.");
        AddContentCard("Deploy state", state.Runtime.PrimaryActionReady ? "Launch gate ready" : "Move to launch gate", "Primary action on the right-side dock still works.");
    }

    private void BuildCombatInventoryPanel(RunState activeRun, WorldRouteDefinition currentRoute, RunZoneState? currentZone)
    {
        AddContentCard("Route", $"{currentRoute.Label} / {currentZone?.Label ?? activeRun.Map.CurrentZoneId}", currentZone?.Description ?? "Combat is active.");
        BuildCombatInventoryContent(activeRun);
    }

    private void UpdatePanelActions(GameState state, RunState? activeRun, ScenePanelMode mode)
    {
        _panelSecondaryButton.Visible = true;
        _panelSecondaryButton.Text = "Close";
        _panelSecondaryButton.Disabled = false;

        switch (mode)
        {
            case ScenePanelMode.Locker:
                _panelPrimaryButton.Text = "Auto Arrange";
                _panelPrimaryButton.Disabled = false;
                break;
            case ScenePanelMode.Workshop:
                _panelPrimaryButton.Text = "Rotate Loadout";
                _panelPrimaryButton.Disabled = state.Save.Inventory.EquippedWeaponIds.Count < 2;
                break;
            case ScenePanelMode.Command:
                _panelPrimaryButton.Text = "Next Route";
                _panelPrimaryButton.Disabled = false;
                break;
            case ScenePanelMode.Launch:
                _panelPrimaryButton.Text = "Deploy";
                _panelPrimaryButton.Disabled = !state.Runtime.PrimaryActionReady;
                break;
            case ScenePanelMode.CombatInventory:
                _panelPrimaryButton.Text = "Sort Pack";
                _panelPrimaryButton.Disabled = activeRun == null || activeRun.Inventory.Items.Count < 2;
                break;
            default:
                _panelPrimaryButton.Text = state.Mode == GameMode.Base ? "Deploy" : "Sort Pack";
                _panelPrimaryButton.Disabled = state.Mode == GameMode.Base ? !state.Runtime.PrimaryActionReady : activeRun == null || activeRun.Inventory.Items.Count < 2;
                break;
        }
    }

    private void OnPanelPrimaryPressed()
    {
        var store = GameManager.Instance?.Store;
        if (store == null)
            return;

        var state = store.State;
        var mode = ResolvePanelMode(state);

        switch (mode)
        {
            case ScenePanelMode.Locker:
                store.AutoArrangeBaseStash();
                break;
            case ScenePanelMode.Workshop:
                if (state.Save.Inventory.EquippedWeaponIds.Count > 1)
                    store.MoveEquippedWeapon(0, state.Save.Inventory.EquippedWeaponIds.Count - 1);
                break;
            case ScenePanelMode.Command:
                store.SelectNextWorldRoute();
                break;
            case ScenePanelMode.Launch:
                if (state.Runtime.PrimaryActionReady)
                    store.DeployCombat();
                break;
            case ScenePanelMode.CombatInventory:
                store.AutoArrangeActiveRunInventory();
                break;
            default:
                if (state.Mode == GameMode.Base)
                {
                    if (state.Runtime.PrimaryActionReady)
                        store.DeployCombat();
                }
                else
                {
                    store.AutoArrangeActiveRunInventory();
                }
                break;
        }

        _panelContentKey = string.Empty;
    }

    private void OnPanelSecondaryPressed()
    {
        GameManager.Instance?.Store?.CloseScenePanel();
    }

    private void AddSummaryCard(string title, string value)
    {
        _panelSummaryColumn.AddChild(CreateContentCard(title, value, string.Empty));
    }

    private void AddContentCard(string title, string value, string meta)
    {
        _panelContentColumn.AddChild(CreateContentCard(title, value, meta));
    }

    private PanelContainer CreateContentCard(string title, string value, string meta)
    {
        var card = new PanelContainer
        {
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        card.AddThemeStyleboxOverride("panel", CreatePanelStyle(
            new Color(Palette.WorldFloorDeep, 0.7f),
            new Color(Palette.Frame, 0.22f),
            16,
            14,
            16,
            14,
            10));

        var body = new VBoxContainer();
        body.AddThemeConstantOverride("separation", 6);
        card.AddChild(body);

        var titleLabel = CreateLabel(11, new Color(Palette.Frame, 0.86f), true, 1.2f, true);
        titleLabel.Text = title;
        body.AddChild(titleLabel);

        var valueLabel = CreateLabel(16, Palette.UiText, true, 0.3f, true);
        valueLabel.Text = value;
        body.AddChild(valueLabel);

        if (!string.IsNullOrWhiteSpace(meta))
        {
            var metaLabel = CreateLabel(12, Palette.UiMuted, false, 0.2f, true);
            metaLabel.Text = meta;
            body.AddChild(metaLabel);
        }

        return card;
    }

    private Button CreateTabButton(string text, bool active)
    {
        var button = new Button
        {
            Text = text,
            CustomMinimumSize = new Vector2(112f, 0f),
            FocusMode = Control.FocusModeEnum.None,
        };
        button.AddThemeFontSizeOverride("font_size", 12);
        button.AddThemeColorOverride("font_color", Palette.UiText);
        button.AddThemeStyleboxOverride("normal", CreateButtonStyle(active, active ? 1f : 0.84f));
        button.AddThemeStyleboxOverride("hover", CreateButtonStyle(true, 1f));
        button.AddThemeStyleboxOverride("pressed", CreateButtonStyle(true, 0.82f));
        return button;
    }

    private Button CreateSmallButton(string text, bool enabled)
    {
        var button = new Button
        {
            Text = text,
            FocusMode = Control.FocusModeEnum.None,
            Disabled = !enabled,
            CustomMinimumSize = new Vector2(96f, 0f),
        };
        button.AddThemeFontSizeOverride("font_size", 11);
        button.AddThemeColorOverride("font_color", Palette.UiText);
        button.AddThemeStyleboxOverride("normal", CreateButtonStyle(enabled, enabled ? 0.86f : 0.44f));
        button.AddThemeStyleboxOverride("hover", CreateButtonStyle(true, 1f));
        button.AddThemeStyleboxOverride("pressed", CreateButtonStyle(true, 0.82f));
        button.AddThemeStyleboxOverride("disabled", CreateButtonStyle(false, 0.38f));
        return button;
    }

    private static ScenePanelMode ResolvePanelMode(GameState state)
    {
        if (state.Runtime.PanelMode != ScenePanelMode.None)
            return state.Runtime.PanelMode;

        return state.Mode == GameMode.Base ? ScenePanelMode.Overview : ScenePanelMode.CombatInventory;
    }

    private static IEnumerable<(ScenePanelMode mode, string label)> GetTabs(GameMode gameMode)
    {
        if (gameMode == GameMode.Base)
        {
            yield return (ScenePanelMode.Overview, "Overview");
            yield return (ScenePanelMode.Locker, "Locker");
            yield return (ScenePanelMode.Workshop, "Workshop");
            yield return (ScenePanelMode.Command, "Command");
            yield return (ScenePanelMode.Launch, "Launch");
            yield break;
        }

        yield return (ScenePanelMode.CombatInventory, "Inventory");
    }

    private static string BuildPanelContentKey(GameState state, ScenePanelMode mode)
    {
        var activeRun = state.Save.Session.ActiveRun;
        string inventoryKey = activeRun == null
            ? string.Join(",", state.Save.Inventory.StoredItems.Select(item => $"{item.ItemId}:{item.Quantity}:{item.X}:{item.Y}:{item.Rotated}"))
            : string.Join(",", activeRun.Inventory.Items.Select(item => $"{item.ItemId}:{item.Quantity}:{item.X}:{item.Y}:{item.Rotated}"));
        string loadoutKey = string.Join(",", activeRun?.Player.LoadoutWeaponIds ?? state.Save.Inventory.EquippedWeaponIds);
        string lootKey = activeRun == null ? string.Empty : string.Join(",", activeRun.GroundLoot.Select(drop => $"{drop.Item.ItemId}:{drop.X:0}:{drop.Y:0}"));
        return $"{state.Mode}:{mode}:{state.Save.World.SelectedRouteId}:{inventoryKey}:{loadoutKey}:{lootKey}:{state.Runtime.NearbyLootCount}:{activeRun?.Stats.Kills ?? 0}";
    }

    private static string FormatLoadout(IReadOnlyList<WeaponType> loadout)
    {
        if (loadout.Count == 0)
            return "No weapons equipped";

        var labels = new List<string>(loadout.Count);
        foreach (var weaponId in loadout)
        {
            labels.Add(WeaponData.ById.TryGetValue(weaponId, out var weapon) ? weapon.Label : weaponId.ToString());
        }

        return string.Join(" / ", labels);
    }

    private static string FormatQuickSlots(GridInventoryState inventory)
    {
        var labels = new List<string>(GridInventoryState.RunQuickSlotCount);
        for (int index = 0; index < GridInventoryState.RunQuickSlotCount; index++)
        {
            string? slotId = index < inventory.QuickSlots.Length ? inventory.QuickSlots[index] : null;
            var record = slotId != null ? inventory.Items.Find(item => item.Id == slotId) : null;
            if (record == null)
            {
                labels.Add($"{index + 1}: empty");
                continue;
            }

            string itemLabel = ItemData.ById.TryGetValue(record.ItemId, out var definition)
                ? definition.ShortLabel
                : record.ItemId;
            labels.Add($"{index + 1}: {itemLabel}");
        }

        return string.Join(" / ", labels);
    }

    private static string FormatBaseResources(BaseResources resources)
    {
        int total = resources.Salvage + resources.Alloy + resources.Research;
        if (total <= 0)
            return "No base stock";

        return $"Scrap {resources.Salvage} / Alloy {resources.Alloy} / Research {resources.Research}";
    }

    private static void ClearChildren(Node node)
    {
        foreach (Node child in node.GetChildren())
        {
            node.RemoveChild(child);
            child.QueueFree();
        }
    }
}
