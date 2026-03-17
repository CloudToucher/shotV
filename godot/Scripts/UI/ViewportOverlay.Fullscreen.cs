using System.Collections.Generic;
using System.Linq;
using Godot;
using ShotV.Core;
using ShotV.Data;
using ShotV.Inventory;
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
    private Control _panelContentContainer = null!;
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
        float viewportHeight = GetViewport().GetVisibleRect().Size.Y;
        _panelFrame.CustomMinimumSize = new Vector2(compact ? 900f : 1100f, Mathf.Min(600f, viewportHeight - 80f));
    }

    private void UpdateFullscreenLive()
    {
        var store = GameManager.Instance?.Store;
        if (store == null)
            return;

        var state = store.State;
        var activeRun = state.Save.Session.ActiveRun;
        var selectedRoute = RouteData.GetMap(state.Save.World.SelectedRouteId);
        var currentRoute = activeRun != null ? RouteData.GetMap(activeRun.Map.RouteId) : selectedRoute;
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
            0));
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
            CustomMinimumSize = new Vector2(1100f, 600f),
            MouseFilter = Control.MouseFilterEnum.Stop,
        };
        _panelFrame.AddThemeStyleboxOverride("panel", CreatePanelStyle(
            new Color(1f, 1f, 1f, 0.96f),
            new Color(Palette.Frame, 0.6f),
            24,
            24,
            24,
            24,
            0));
        
        var marginHost = new MarginContainer();
        marginHost.AddThemeConstantOverride("margin_top", 40);
        marginHost.AddThemeConstantOverride("margin_bottom", 40);
        marginHost.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _panelLayer.AddChild(marginHost);
        
        var centerHost = new CenterContainer
        {
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        centerHost.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        centerHost.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        marginHost.AddChild(centerHost);

        centerHost.AddChild(_panelFrame);

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

        _panelContentContainer = new VBoxContainer();
        _panelContentContainer.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        _panelContentContainer.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        body.AddChild(_panelContentContainer);

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
            : BuildMapSummary(selectedRoute);
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
            _overviewHint.Text = "Review base stations, map intel, and the launch gate before deployment. Press M to close.";
            return;
        }

        string zoneLabel = currentZone?.Label ?? "Current Region";
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
            0));
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
            ScenePanelMode.Command => "Command / Map Intel",
            ScenePanelMode.Launch => "Launch / Deploy",
            ScenePanelMode.CombatInventory => "Combat Pack / Loot",
            _ => state.Mode == GameMode.Base ? "Base Overview" : "Mission Overview",
        };

        _panelMeta.Text = mode switch
        {
            ScenePanelMode.CombatInventory when activeRun != null => $"{currentRoute.Label} / {currentZone?.Label ?? activeRun.Map.CurrentZoneId}",
            ScenePanelMode.Command => $"Selected map: {selectedRoute.Label}",
            ScenePanelMode.Workshop => "Loadout order is mirrored to weapon slots 1 / 2 / 3. Fabricated consumables are stored in the stash.",
            ScenePanelMode.Locker => $"Stored items: {state.Save.Inventory.StoredItems.Count}",
            ScenePanelMode.Launch => $"Deployment target: {selectedRoute.Label} / staged items: {state.Save.Inventory.DeploymentPack.Items.Count}",
            _ => state.Mode == GameMode.Base ? BuildMapSummary(selectedRoute) : BuildSceneHint(state),
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
        ClearChildren(_panelContentContainer);

        BuildContentCards(state, selectedRoute, currentRoute, currentZone, activeRun, mode);

        _panelContentKey = key;
    }

    private void BuildContentCards(GameState state, WorldRouteDefinition selectedRoute, WorldRouteDefinition currentRoute, RunZoneState? currentZone, RunState? activeRun, ScenePanelMode mode)
    {
        switch (mode)
        {
            case ScenePanelMode.Locker:
                BuildLockerPanel(state.Save.Inventory);
                break;
            case ScenePanelMode.Workshop:
                BuildWorkshopPanel(state);
                break;
            case ScenePanelMode.Command:
                BuildCommandPanel(state.Save.World.SelectedRouteId);
                break;
            case ScenePanelMode.Launch:
                BuildLaunchPanel(state, selectedRoute, state.Runtime.PrimaryActionReady);
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

        if (state.Mode == GameMode.Base)
        {
            AddCardTo(leftCol, "Resources", FormatBaseResources(state.Save.Base.Resources), string.Empty);
            AddCardTo(leftCol, "Map", $"{selectedRoute.Label}\nRegions: {selectedRoute.Zones.Length}", string.Empty);
            AddCardTo(leftCol, "Stash", $"Items: {state.Save.Inventory.StoredItems.Count}\nKnown regions: {state.Save.World.DiscoveredZones.Count}", string.Empty);

            AddCardTo(rightCol, "Selected map", selectedRoute.Label, BuildMapSummary(selectedRoute));
            AddCardTo(rightCol, "Current stash", $"Stored items: {state.Save.Inventory.StoredItems.Count}", "Open Locker for item details and auto-arrange.");
            AddCardTo(rightCol, "Workshop", FormatLoadout(state.Save.Inventory.EquippedWeaponIds), "Open Workshop to reorder the loadout used by weapon slots.");
            if (state.Save.Session.LastExtraction != null)
                AddCardTo(rightCol, "Last extraction", FormatOutcome(state.Save.Session.LastExtraction.Outcome), state.Save.Session.LastExtraction.SummaryLabel);
            return;
        }

        if (activeRun == null)
            return;

        AddCardTo(leftCol, "Resources", FormatResourceBundle(activeRun.Resources, "No carried resources"), string.Empty);
        AddCardTo(leftCol, "Region", $"{currentRoute.Label}\n{currentZone?.Label ?? activeRun.Map.CurrentZoneId}", string.Empty);
        AddCardTo(leftCol, "Loadout", FormatLoadout(activeRun.Player.LoadoutWeaponIds), string.Empty);
        AddCardTo(leftCol, "Runtime", $"Kills: {activeRun.Stats.Kills}\nNearby loot: {state.Runtime.NearbyLootCount}", string.Empty);

        AddCardTo(rightCol, "AO", $"{currentRoute.Label} / {currentZone?.Label ?? activeRun.Map.CurrentZoneId}", currentZone?.Description ?? "Open-world combat is active.");
        AddCardTo(rightCol, "Pack", $"Items carried: {activeRun.Inventory.Items.Count}", $"Quick slots: {FormatQuickSlots(activeRun.Inventory)}");
        AddCardTo(rightCol, "Loot", $"Ground loot: {activeRun.GroundLoot.Count}", $"Nearby pickups: {state.Runtime.NearbyLootCount}");
    }

    private void BuildLockerPanel(InventoryState inventory)
    {
        var split = new HBoxContainer();
        split.AddThemeConstantOverride("separation", 24);
        split.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        _panelContentContainer.AddChild(split);

        var infoCol = new VBoxContainer();
        infoCol.AddThemeConstantOverride("separation", 12);
        infoCol.CustomMinimumSize = new Vector2(300f, 0f);
        split.AddChild(infoCol);

        var contentCol = new VBoxContainer();
        contentCol.AddThemeConstantOverride("separation", 12);
        contentCol.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        split.AddChild(contentCol);

        AddCardTo(infoCol, "Stash Information", $"Stored items: {inventory.StoredItems.Count}", "The base locker is used to store components, salvage, and fabrication materials.");
        
        BuildLockerInventoryContent(contentCol, inventory);
    }

    private void BuildWorkshopPanel(GameState state)
    {
        var inventory = state.Save.Inventory;

        var split = new HBoxContainer();
        split.AddThemeConstantOverride("separation", 24);
        split.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        _panelContentContainer.AddChild(split);

        var weaponsCol = new VBoxContainer();
        weaponsCol.AddThemeConstantOverride("separation", 12);
        weaponsCol.CustomMinimumSize = new Vector2(340f, 0f);
        split.AddChild(weaponsCol);

        var header = CreateLabel(14, Palette.UiText, true, 0.4f, false);
        header.Text = "Loadout Configuration";
        weaponsCol.AddChild(header);

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

            weaponsCol.AddChild(card);
        }

        var fabCol = new VBoxContainer();
        fabCol.AddThemeConstantOverride("separation", 12);
        fabCol.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        split.AddChild(fabCol);

        var fabHeader = CreateLabel(14, Palette.UiText, true, 0.4f, false);
        fabHeader.Text = "Fabrication";
        fabCol.AddChild(fabHeader);

        var fabScroll = new ScrollContainer
        {
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            VerticalScrollMode = ScrollContainer.ScrollMode.Auto,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };
        fabCol.AddChild(fabScroll);

        var fabList = new VBoxContainer();
        fabList.AddThemeConstantOverride("separation", 10);
        fabList.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        fabScroll.AddChild(fabList);

        foreach (var definition in ItemData.Catalog.Where(item => item.CraftCost != null))
        {
            var card = CreateContentCard("Blueprint", definition.Label, definition.Description);
            var body = card.GetChild<VBoxContainer>(0);

            var costLabel = CreateLabel(12, Palette.UiMuted, false, 0.2f, true);
            costLabel.Text = $"Cost: {FormatCompactResourceBundle(definition.CraftCost!)}";
            body.AddChild(costLabel);

            int stashCount = CountStoredQuantity(inventory.StoredItems, definition.Id);
            var stockLabel = CreateLabel(12, Palette.UiMuted, false, 0.2f, true);
            stockLabel.Text = $"In stash: {stashCount}";
            body.AddChild(stockLabel);

            bool canCraft = CanCraftWorkshopItem(state, definition);
            var statusLabel = CreateLabel(12, canCraft ? Palette.Accent : Palette.UiMuted, false, 0.2f, true);
            statusLabel.Text = canCraft
                ? "Ready to fabricate into the base stash."
                : ResolveWorkshopCraftBlocker(state, definition);
            body.AddChild(statusLabel);

            var craftButton = CreateSmallButton(canCraft ? "Fabricate" : "Unavailable", canCraft);
            craftButton.Pressed += () => GameManager.Instance?.Store?.CraftWorkshopItem(definition.Id);
            body.AddChild(craftButton);

            fabList.AddChild(card);
        }
    }

    private void BuildCommandPanel(string selectedRouteId)
    {
        var scroll = new ScrollContainer
        {
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            VerticalScrollMode = ScrollContainer.ScrollMode.Auto,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        _panelContentContainer.AddChild(scroll);

        var list = new VBoxContainer();
        list.AddThemeConstantOverride("separation", 12);
        list.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        scroll.AddChild(list);

        foreach (var route in RouteData.Maps)
        {
            int extractionCount = route.Zones.Count(zone => zone.AllowsExtraction);
            int highestThreat = route.Zones.Length > 0 ? route.Zones.Max(zone => zone.ThreatLevel) : 0;
            var card = CreateContentCard("Map", route.Label, BuildMapSummary(route));
            var body = card.GetChild<VBoxContainer>(0);

            var pressureLabel = CreateLabel(12, Palette.UiMuted, false, 0.2f, true);
            pressureLabel.Text = $"Regions {route.Zones.Length} / Highest threat {highestThreat} / Extraction points {extractionCount}";
            body.AddChild(pressureLabel);

            var supplyLabel = CreateLabel(12, Palette.UiMuted, false, 0.2f, true);
            supplyLabel.Text = BuildMapSupplyAdvice(route);
            body.AddChild(supplyLabel);

            foreach (var zone in route.Zones)
            {
                var regionLabel = CreateLabel(12, Palette.UiText, false, 0.1f, true);
                regionLabel.Text = BuildRegionIntelLine(zone);
                body.AddChild(regionLabel);
            }

            var button = CreateSmallButton(route.Id == selectedRouteId ? "Selected" : "Select Map", route.Id != selectedRouteId);
            button.Pressed += () => GameManager.Instance?.Store?.SelectWorldMap(route.Id);
            body.AddChild(button);

            list.AddChild(card);
        }
    }

    private void BuildLaunchPanel(GameState state, WorldRouteDefinition selectedRoute, bool deploymentReady)
    {
        var inventory = state.Save.Inventory;
        var readiness = GameManager.Instance?.Store?.EvaluateDeploymentReadiness() ?? new DeploymentReadinessResult
        {
            CanDeploy = true,
            StatusLabel = "Ready",
            Detail = "Deployment pack is acceptable for the selected map.",
            CapacityCells = inventory.DeploymentPack.Columns * inventory.DeploymentPack.Rows,
        };

        var topScroll = new ScrollContainer
        {
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            VerticalScrollMode = ScrollContainer.ScrollMode.Auto,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.Fill,
            CustomMinimumSize = new Vector2(0, 130f)
        };
        _panelContentContainer.AddChild(topScroll);

        var topList = new VBoxContainer();
        topList.AddThemeConstantOverride("separation", 12);
        topList.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        topScroll.AddChild(topList);

        var infoGrid = new GridContainer
        {
            Columns = 2,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        infoGrid.AddThemeConstantOverride("h_separation", 12);
        infoGrid.AddThemeConstantOverride("v_separation", 12);
        topList.AddChild(infoGrid);

        AddCardTo(infoGrid, "Selected map", selectedRoute.Label, BuildMapSummary(selectedRoute));
        AddCardTo(infoGrid, "Regional pressure", $"Highest threat {readiness.HighestThreat} / Regions {selectedRoute.Zones.Length}", readiness.HighestThreat >= 3
            ? "High-risk map: medical support is mandatory before deployment."
            : "Lower-risk map: empty deployment packs are still allowed, but not recommended.");
        AddCardTo(infoGrid, "Deployment status", readiness.StatusLabel, readiness.Detail);
        AddCardTo(infoGrid, "Pack summary",
            $"Units {readiness.StagedUnits} / Cells {readiness.OccupiedCells}/{Mathf.Max(1, readiness.CapacityCells)}",
            $"Heal {readiness.HealingUnits} / Mobility {readiness.MobilityUnits} / Utility {readiness.UtilityUnits}");

        var packLabel = CreateLabel(14, Palette.UiText, true, 0.4f, false);
        packLabel.Text = "Deployment Pack";
        _panelContentContainer.AddChild(packLabel);

        BuildLaunchInventoryContent(_panelContentContainer, inventory);
    }

    private void BuildCombatInventoryPanel(RunState activeRun, WorldRouteDefinition currentRoute, RunZoneState? currentZone)
    {
        AddContentCard("Region", $"{currentRoute.Label} / {currentZone?.Label ?? activeRun.Map.CurrentZoneId}", currentZone?.Description ?? "Combat is active.");
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
                _panelPrimaryButton.Text = "Cycle Map";
                _panelPrimaryButton.Disabled = false;
                break;
            case ScenePanelMode.Launch:
                _panelPrimaryButton.Text = "Deploy";
                _panelPrimaryButton.Disabled = !state.Runtime.PrimaryActionReady
                    || !(GameManager.Instance?.Store?.EvaluateDeploymentReadiness().CanDeploy ?? true);
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
                store.SelectNextWorldMap();
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

    private void AddCardTo(Control parent, string title, string value, string meta)
    {
        parent.AddChild(CreateContentCard(title, value, meta));
    }

    private void AddContentCard(string title, string value, string meta)
    {
        _panelContentContainer.AddChild(CreateContentCard(title, value, meta));
    }

    private PanelContainer CreateContentCard(string title, string value, string meta)
    {
        var card = new PanelContainer
        {
            MouseFilter = Control.MouseFilterEnum.Ignore,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        card.AddThemeStyleboxOverride("panel", CreatePanelStyle(
            new Color(Palette.WorldFloorDeep, 0.7f),
            new Color(Palette.Frame, 0.22f),
            16,
            14,
            16,
            14,
            0));

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
            yield return (ScenePanelMode.Command, "Intel");
            yield return (ScenePanelMode.Launch, "Launch");
            yield break;
        }

        yield return (ScenePanelMode.CombatInventory, "Inventory");
    }

    private static string BuildPanelContentKey(GameState state, ScenePanelMode mode)
    {
        var activeRun = state.Save.Session.ActiveRun;
        string inventoryKey = activeRun == null
            ? $"{string.Join(",", state.Save.Inventory.StoredItems.Select(item => $"{item.ItemId}:{item.Quantity}:{item.X}:{item.Y}:{item.Rotated}"))}|{string.Join(",", state.Save.Inventory.DeploymentPack.Items.Select(item => $"{item.ItemId}:{item.Quantity}:{item.X}:{item.Y}:{item.Rotated}"))}"
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

    private static string FormatCompactResourceBundle(ResourceBundle bundle)
    {
        var parts = new List<string>(3);
        if (bundle.Salvage > 0)
            parts.Add($"Scrap {bundle.Salvage}");
        if (bundle.Alloy > 0)
            parts.Add($"Alloy {bundle.Alloy}");
        if (bundle.Research > 0)
            parts.Add($"Research {bundle.Research}");
        return parts.Count > 0 ? string.Join(" / ", parts) : "No cost";
    }

    private static string BuildRegionIntelLine(WorldRouteZoneDefinition zone)
    {
        string extraction = zone.AllowsExtraction ? "Extractable" : "No extraction";
        return $"T{zone.ThreatLevel} / {DescribeZoneKind(zone.Kind)} / {zone.Label} / {DescribeRegionPressure(zone)} / {DescribeRegionLootBias(zone)} / {extraction}";
    }

    private static string BuildMapSupplyAdvice(WorldRouteDefinition route)
    {
        int highestThreat = route.Zones.Length > 0 ? route.Zones.Max(zone => zone.ThreatLevel) : 0;
        return highestThreat switch
        {
            >= 3 => "Suggested pack: at least one medical item, one mobility tool, and one area-control consumable.",
            2 => "Suggested pack: bring one healing item and one flexible utility consumable before entering deeper regions.",
            _ => "Suggested pack: light supplies are acceptable, but staging a fallback heal is still safer.",
        };
    }

    private static string BuildMapSummary(WorldRouteDefinition route)
    {
        int highestThreat = route.Zones.Length > 0 ? route.Zones.Max(zone => zone.ThreatLevel) : 0;
        int extractionCount = route.Zones.Count(zone => zone.AllowsExtraction);
        return $"Open map / Regions {route.Zones.Length} / Highest threat {highestThreat} / Extractions {extractionCount}";
    }

    private static string DescribeZoneKind(WorldZoneKind kind)
    {
        return kind switch
        {
            WorldZoneKind.Perimeter => "Perimeter",
            WorldZoneKind.HighRisk => "High Risk",
            WorldZoneKind.HighValue => "High Value",
            WorldZoneKind.Extraction => "Extraction",
            _ => "Open Region",
        };
    }

    private static string DescribeRegionPressure(WorldRouteZoneDefinition zone)
    {
        return zone.Kind switch
        {
            WorldZoneKind.HighRisk when zone.ThreatLevel >= 3 => "dense chargers",
            WorldZoneKind.HighRisk => "dense mixed hostiles",
            WorldZoneKind.HighValue => "ranged watchers",
            WorldZoneKind.Extraction => "contested exit",
            _ => "light patrols",
        };
    }

    private static string DescribeRegionLootBias(WorldRouteZoneDefinition zone)
    {
        return zone.Kind switch
        {
            WorldZoneKind.HighRisk => "alloy-heavy recovery",
            WorldZoneKind.HighValue => "research-heavy recovery",
            WorldZoneKind.Extraction => "utility cache chance",
            _ => "salvage and meds",
        };
    }

    private static int CountStoredQuantity(IEnumerable<InventoryItemRecord> items, string itemId)
    {
        return items.Where(item => item.ItemId == itemId).Sum(item => item.Quantity);
    }

    private static bool CanCraftWorkshopItem(GameState state, ItemDefinition definition)
    {
        if (definition.CraftCost == null)
            return false;

        var stock = state.Save.Base.Resources;
        if (stock.Salvage < definition.CraftCost.Salvage
            || stock.Alloy < definition.CraftCost.Alloy
            || stock.Research < definition.CraftCost.Research)
            return false;

        var preview = GridInventory.CreateItemRecord(definition.Id, 1);
        if (preview == null)
            return false;

        return GridInventory.PlaceItemInGrid(
            state.Save.Inventory.StashColumns,
            state.Save.Inventory.StashRows,
            state.Save.Inventory.StoredItems,
            preview).Placed;
    }

    private static string ResolveWorkshopCraftBlocker(GameState state, ItemDefinition definition)
    {
        if (definition.CraftCost == null)
            return "No fabrication recipe.";

        var stock = state.Save.Base.Resources;
        if (stock.Salvage < definition.CraftCost.Salvage
            || stock.Alloy < definition.CraftCost.Alloy
            || stock.Research < definition.CraftCost.Research)
            return "Insufficient base resources.";

        var preview = GridInventory.CreateItemRecord(definition.Id, 1);
        if (preview == null)
            return "Recipe data is invalid.";

        bool hasSpace = GridInventory.PlaceItemInGrid(
            state.Save.Inventory.StashColumns,
            state.Save.Inventory.StashRows,
            state.Save.Inventory.StoredItems,
            preview).Placed;

        return hasSpace ? "Unavailable." : "Base stash is full.";
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
