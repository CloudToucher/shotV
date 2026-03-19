using Godot;
using ShotV.Core;
using ShotV.Data;
using ShotV.State;
using ShotV.World;

namespace ShotV.UI;

public partial class ViewportOverlay : CanvasLayer
{
    private Control _root = null!;
    private PanelContainer _actionDock = null!;
    private Label _dockKicker = null!;
    private Label _dockTitle = null!;
    private Label _dockCopy = null!;
    private Label _dockHint = null!;
    private Button _primaryButton = null!;
    private Button _secondaryButton = null!;

    private Control _settlementLayer = null!;
    private PanelContainer _settlementModal = null!;
    private Label _settlementKicker = null!;
    private Label _settlementTitle = null!;
    private Label _settlementRoute = null!;
    private Label _settlementSummary = null!;
    private GridContainer _settlementGrid = null!;
    private Label _resultValue = null!;
    private Label _resultMeta = null!;
    private Label _battleValue = null!;
    private Label _battleMeta = null!;
    private Label _resourcesValue = null!;
    private Label _resourcesMeta = null!;
    private Label _lootValue = null!;
    private Label _lootMeta = null!;

    public override void _Ready()
    {
        Layer = 30;
        BuildUi();

        var store = GameManager.Instance?.Store;
        if (store != null)
            store.StateChanged += OnStateChanged;

        UpdateFromStore();
    }

    public override void _ExitTree()
    {
        var store = GameManager.Instance?.Store;
        if (store != null)
            store.StateChanged -= OnStateChanged;
    }

    public override void _Process(double delta)
    {
        if (_settlementGrid == null)
            return;

        int columns = GetViewport().GetVisibleRect().Size.X < 900f ? 1 : 2;
        if (_settlementGrid.Columns != columns)
            _settlementGrid.Columns = columns;

        UpdateFullscreenLayout();
        UpdateFullscreenLive();
    }

    private void OnStateChanged(GameState current, GameState previous)
    {
        UpdateFromStore();
    }

    private void BuildUi()
    {
        _root = new Control
        {
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        _root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(_root);

        BuildActionDock();
        BuildSettlementModal();
        BuildFullscreenUi();
    }

    private void BuildActionDock()
    {
        _actionDock = new PanelContainer();
        _actionDock.AnchorLeft = 1f;
        _actionDock.AnchorRight = 1f;
        _actionDock.OffsetLeft = -330f;
        _actionDock.OffsetTop = 112f;
        _actionDock.OffsetRight = -18f;
        _actionDock.CustomMinimumSize = new Vector2(312f, 0f);
        _actionDock.MouseFilter = Control.MouseFilterEnum.Stop;
        _actionDock.AddThemeStyleboxOverride("panel", CreatePanelStyle(
            new Color(1f, 1f, 1f, 0.95f),
            new Color(Palette.Frame, 0.42f),
            18,
            18,
            18,
            16,
            0));
        _root.AddChild(_actionDock);

        var dockBody = new VBoxContainer();
        dockBody.AddThemeConstantOverride("separation", 10);
        _actionDock.AddChild(dockBody);

        var accentLine = new ColorRect
        {
            Color = new Color(Palette.Frame, 0.92f),
            CustomMinimumSize = new Vector2(40f, 2f),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        dockBody.AddChild(accentLine);

        _dockKicker = CreateLabel(11, new Color(Palette.Frame, 0.92f), true, 1.8f);
        _dockKicker.Text = GameText.Text("overlay.dock.kicker.tactical");
        dockBody.AddChild(_dockKicker);

        _dockTitle = CreateLabel(22, Palette.UiText, true, 0.6f);
        dockBody.AddChild(_dockTitle);

        _dockCopy = CreateLabel(13, Palette.UiMuted, false, 0.2f, true);
        dockBody.AddChild(_dockCopy);

        _dockHint = CreateLabel(13, Palette.Accent, false, 0.2f, true);
        dockBody.AddChild(_dockHint);

        var actionRow = new HBoxContainer();
        actionRow.AddThemeConstantOverride("separation", 8);
        dockBody.AddChild(actionRow);

        _primaryButton = CreateActionButton(GameText.Text("overlay.button.deploy"), true);
        _primaryButton.Pressed += OnPrimaryActionPressed;
        actionRow.AddChild(_primaryButton);

        _secondaryButton = CreateActionButton(GameText.Text("overlay.button.extract_now"), false);
        _secondaryButton.Pressed += OnSecondaryActionPressed;
        actionRow.AddChild(_secondaryButton);
    }

    private void BuildSettlementModal()
    {
        _settlementLayer = new Control
        {
            Visible = false,
            MouseFilter = Control.MouseFilterEnum.Stop,
        };
        _settlementLayer.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _root.AddChild(_settlementLayer);

        var backdrop = new ColorRect
        {
            Color = new Color(0.91f, 0.95f, 0.97f, 0.62f),
            MouseFilter = Control.MouseFilterEnum.Stop,
        };
        backdrop.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _settlementLayer.AddChild(backdrop);

        var modalHost = new CenterContainer
        {
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        modalHost.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _settlementLayer.AddChild(modalHost);

        _settlementModal = new PanelContainer
        {
            CustomMinimumSize = new Vector2(720f, 0f),
            MouseFilter = Control.MouseFilterEnum.Stop,
        };
        _settlementModal.AddThemeStyleboxOverride("panel", CreatePanelStyle(
            new Color(1f, 1f, 1f, 0.96f),
            new Color(Palette.Frame, 0.46f),
            24,
            24,
            24,
            22,
            0));
        modalHost.AddChild(_settlementModal);

        var modalBody = new VBoxContainer();
        modalBody.AddThemeConstantOverride("separation", 12);
        _settlementModal.AddChild(modalBody);

        var accentLine = new ColorRect
        {
            Color = new Color(Palette.Frame, 0.94f),
            CustomMinimumSize = new Vector2(56f, 2f),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        modalBody.AddChild(accentLine);

        _settlementKicker = CreateLabel(11, new Color(Palette.Frame, 0.92f), true, 2f);
        _settlementKicker.Text = GameText.Text("overlay.settlement.kicker");
        modalBody.AddChild(_settlementKicker);

        _settlementTitle = CreateLabel(26, Palette.UiText, true, 0.6f);
        modalBody.AddChild(_settlementTitle);

        _settlementRoute = CreateLabel(13, Palette.UiMuted, false, 0.2f, true);
        modalBody.AddChild(_settlementRoute);

        _settlementSummary = CreateLabel(13, Palette.Accent, false, 0.2f, true);
        modalBody.AddChild(_settlementSummary);

        _settlementGrid = new GridContainer
        {
            Columns = 2,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        _settlementGrid.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        _settlementGrid.AddThemeConstantOverride("h_separation", 12);
        _settlementGrid.AddThemeConstantOverride("v_separation", 12);
        modalBody.AddChild(_settlementGrid);

        (_resultValue, _resultMeta) = AddSettlementCard(_settlementGrid, GameText.Text("overlay.card.result"));
        (_battleValue, _battleMeta) = AddSettlementCard(_settlementGrid, GameText.Text("overlay.card.battle"));
        (_resourcesValue, _resourcesMeta) = AddSettlementCard(_settlementGrid, GameText.Text("overlay.card.resources"), true);
        (_lootValue, _lootMeta) = AddSettlementCard(_settlementGrid, GameText.Text("overlay.card.loot"), true);

        var footnote = CreateLabel(12, Palette.UiMuted, false, 0.2f, true);
        footnote.Text = GameText.Text("overlay.footnote.settlement");
        modalBody.AddChild(footnote);

        var actionRow = new HBoxContainer();
        actionRow.Alignment = BoxContainer.AlignmentMode.End;
        modalBody.AddChild(actionRow);

        var confirmButton = CreateActionButton(GameText.Text("overlay.button.confirm_settlement"), true);
        confirmButton.CustomMinimumSize = new Vector2(240f, 0f);
        confirmButton.Pressed += OnPrimaryActionPressed;
        actionRow.AddChild(confirmButton);
    }

    private void UpdateFromStore()
    {
        var store = GameManager.Instance?.Store;
        if (store == null)
            return;

        var state = store.State;
        var activeRun = state.Save.Session.ActiveRun;
        var selectedRoute = RouteData.GetMap(state.Save.World.SelectedRouteId);
        var currentRoute = activeRun != null ? RouteData.GetMap(activeRun.Map.RouteId) : selectedRoute;
        var currentZone = activeRun != null ? RouteManager.GetCurrentRunZone(activeRun.Map) : null;
        var deploymentReadiness = state.Mode == GameMode.Base ? store.EvaluateDeploymentReadiness() : null;
        bool showSettlement = state.Mode == GameMode.Combat && activeRun?.Status == RunStateStatus.AwaitingSettlement;
        bool showDock = !showSettlement && !state.Runtime.MapOverlayOpen && !state.Runtime.PanelOpen && (state.Mode == GameMode.Base || activeRun != null);
        bool canExtract = activeRun != null && activeRun.Status == RunStateStatus.Active
            && RouteManager.CanExtractFromRunMap(activeRun.Map);

        UpdateFullscreenFromStore(state, selectedRoute, currentRoute, currentZone, activeRun, showSettlement);

        _actionDock.Visible = showDock;
        _settlementLayer.Visible = showSettlement;

        if (showDock)
        {
            _dockKicker.Text = state.Mode == GameMode.Base
                ? GameText.Text("overlay.dock.kicker.deploy")
                : GameText.Text("overlay.dock.kicker.tactical");
            _dockTitle.Text = BuildHeadline(state.Mode, currentRoute, currentZone);
            _dockCopy.Text = BuildSubline(state.Mode, selectedRoute, activeRun, currentZone);
            _dockHint.Text = state.Mode == GameMode.Base && state.Runtime.PrimaryActionReady && deploymentReadiness != null
                ? deploymentReadiness.Detail
                : BuildSceneHint(state);
            _primaryButton.Text = state.Mode == GameMode.Base && deploymentReadiness is { CanDeploy: false }
                ? GameText.Text("overlay.primary.not_ready")
                : BuildPrimaryActionLabel(state.Mode, activeRun);
            _primaryButton.Disabled = state.Mode == GameMode.Base
                ? !state.Runtime.PrimaryActionReady || !(deploymentReadiness?.CanDeploy ?? true)
                : IsPrimaryActionDisabled(state, activeRun, canExtract);
            _secondaryButton.Visible = state.Mode == GameMode.Combat && canExtract;
            _secondaryButton.Disabled = !canExtract;
        }

        if (!showSettlement)
            return;

        var preview = store.PreviewActiveRunSettlement();
        if (preview == null || activeRun == null)
        {
            _settlementLayer.Visible = false;
            return;
        }

        _settlementTitle.Text = preview.Outcome == RunResolutionOutcome.Down
            ? GameText.Text("overlay.settlement.title.down")
            : GameText.Text("overlay.settlement.title.return");
        _settlementRoute.Text = currentZone != null ? $"{currentRoute.Label} / {currentZone.Label}" : currentRoute.Label;
        _settlementSummary.Text = preview.SummaryLabel;
        _resultValue.Text = FormatOutcome(preview.Outcome);
        _resultMeta.Text = GameText.Format("overlay.settlement.time", FormatDuration(preview.DurationSeconds));
        _battleValue.Text = GameText.Format("overlay.settlement.kills", preview.Kills);
        _battleMeta.Text = GameText.Format("overlay.settlement.wave", preview.HighestWave);
        _resourcesValue.Text = FormatResourceBundle(preview.ResourcesRecovered, "无资源回收");
        _resourcesMeta.Text = GameText.Format("overlay.settlement.loss", FormatResourceBundle(preview.ResourcesLost, "无资源损失"));
        _lootValue.Text = FormatLootEntries(preview.LootRecovered, "无回收物资");
        _lootMeta.Text = GameText.Format("overlay.settlement.lost", FormatLootEntries(preview.LootLost, "无遗失物资"));
    }

    private void OnPrimaryActionPressed()
    {
        var store = GameManager.Instance?.Store;
        if (store == null)
            return;

        var state = store.State;
        var activeRun = state.Save.Session.ActiveRun;

        if (state.Mode == GameMode.Base)
        {
            store.DeployCombat();
            return;
        }

        if (activeRun == null)
            return;

        if (activeRun.Status == RunStateStatus.AwaitingSettlement)
        {
            store.ResolveActiveRunToBase(force: true);
            return;
        }

        bool canExtract = RouteManager.CanExtractFromRunMap(activeRun.Map);

        if (canExtract)
            store.MarkRunOutcome(RunResolutionOutcome.Extracted);
    }

    private void OnSecondaryActionPressed()
    {
        var store = GameManager.Instance?.Store;
        var activeRun = store?.State.Save.Session.ActiveRun;

        if (store == null || activeRun == null || activeRun.Status != RunStateStatus.Active)
            return;

        if (RouteManager.CanExtractFromRunMap(activeRun.Map))
            store.MarkRunOutcome(RunResolutionOutcome.Extracted);
    }

    private static string BuildHeadline(GameMode mode, WorldRouteDefinition route, RunZoneState? currentZone)
    {
        if (mode == GameMode.Base)
            return GameText.Text("overlay.headline.base");

        return currentZone != null ? $"{route.Label} / {currentZone.Label}" : GameText.Text("overlay.headline.combat_default");
    }

    private static string BuildSubline(GameMode mode, WorldRouteDefinition selectedRoute, RunState? activeRun, RunZoneState? currentZone)
    {
        if (mode == GameMode.Base)
            return $"{selectedRoute.Label} / {BuildMapSummary(selectedRoute)}";

        if (activeRun == null)
            return GameText.Text("overlay.subline.connecting");

        if (activeRun.Status == RunStateStatus.AwaitingSettlement)
            return GameText.Format("overlay.subline.result", FormatOutcome(activeRun.PendingOutcome ?? RunResolutionOutcome.Down));

        if (currentZone != null)
            return GameText.Format("overlay.subline.current_zone", currentZone.Label, currentZone.ThreatLevel, currentZone.Description);

        return GameText.Text("overlay.subline.open_area");
    }

    private static string BuildSceneHint(GameState state)
    {
        if (!string.IsNullOrWhiteSpace(state.Runtime.PrimaryActionHint))
            return state.Runtime.PrimaryActionHint;

        return state.Mode == GameMode.Base
            ? GameText.Text("overlay.hint.base")
            : GameText.Text("overlay.hint.combat");
    }

    private static string BuildPrimaryActionLabel(GameMode mode, RunState? activeRun)
    {
        if (mode == GameMode.Base)
            return GameText.Text("overlay.primary.deploy");

        if (activeRun?.Status == RunStateStatus.AwaitingSettlement)
            return GameText.Text("overlay.primary.confirm_return");

        return GameText.Text("overlay.primary.extract");
    }

    private static bool IsPrimaryActionDisabled(GameState state, RunState? activeRun, bool canExtract)
    {
        if (state.Mode == GameMode.Base)
            return !state.Runtime.PrimaryActionReady;

        if (activeRun == null)
            return true;

        if (activeRun.Status == RunStateStatus.AwaitingSettlement)
            return false;

        return !canExtract || !state.Runtime.PrimaryActionReady;
    }

    private static string FormatOutcome(RunResolutionOutcome outcome)
    {
        return outcome switch
        {
            RunResolutionOutcome.BossClear => GameText.Text("overlay.outcome.boss_clear"),
            RunResolutionOutcome.Down => GameText.Text("overlay.outcome.down"),
            _ => GameText.Text("overlay.outcome.extracted"),
        };
    }

    private static string FormatDuration(int totalSeconds)
    {
        int seconds = Mathf.Max(0, totalSeconds);
        int minutes = seconds / 60;
        int restSeconds = seconds % 60;

        if (minutes <= 0)
            return GameText.Format("overlay.duration.seconds", restSeconds);

        return GameText.Format("overlay.duration.minutes", minutes, restSeconds);
    }

    private static string FormatResourceBundle(ResourceBundle bundle, string emptyLabel)
    {
        int total = bundle.Salvage + bundle.Alloy + bundle.Research;
        if (total <= 0)
            return emptyLabel;

        return GameText.Format("overlay.resource_bundle", bundle.Salvage, bundle.Alloy, bundle.Research);
    }

    private static string FormatLootEntries(System.Collections.Generic.IReadOnlyList<LootEntry> entries, string emptyLabel)
    {
        if (entries.Count == 0)
            return emptyLabel;

        int maxShown = Mathf.Min(entries.Count, 4);
        var labels = new string[maxShown];
        for (int index = 0; index < maxShown; index++)
            labels[index] = entries[index].Label;

        return entries.Count > 4
            ? GameText.Format("overlay.loot_more", string.Join(" / ", labels), entries.Count)
            : string.Join(" / ", labels);
    }

    private static Label CreateLabel(int fontSize, Color color, bool bold, float letterSpacing, bool wrap = false)
    {
        var label = new Label
        {
            AutowrapMode = wrap ? TextServer.AutowrapMode.WordSmart : TextServer.AutowrapMode.Off,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        label.AddThemeColorOverride("font_color", color);
        label.AddThemeFontSizeOverride("font_size", UiScale.Font(fontSize));
        if (bold)
            label.AddThemeConstantOverride("outline_size", 0);
        if (letterSpacing != 0f)
            label.AddThemeConstantOverride("outline_size", 0);
        return label;
    }

    private static Button CreateActionButton(string text, bool primary)
    {
        var button = new Button
        {
            Text = text,
            CustomMinimumSize = new Vector2(primary ? 180f : 116f, 0f),
            FocusMode = Control.FocusModeEnum.None,
        };
        button.AddThemeFontSizeOverride("font_size", UiScale.Font(12));
        button.AddThemeColorOverride("font_color", Palette.UiText);
        button.AddThemeStyleboxOverride("normal", CreateButtonStyle(primary, 0.92f));
        button.AddThemeStyleboxOverride("hover", CreateButtonStyle(primary, 1f));
        button.AddThemeStyleboxOverride("pressed", CreateButtonStyle(primary, 0.82f));
        button.AddThemeStyleboxOverride("disabled", CreateButtonStyle(false, 0.38f));
        return button;
    }

    private static StyleBoxFlat CreatePanelStyle(Color background, Color borderColor, int left, int top, int right, int bottom, int radius)
    {
        var style = new StyleBoxFlat
        {
            BgColor = background,
            BorderColor = borderColor,
            ShadowColor = new Color(0.08f, 0.2f, 0.27f, 0.14f),
            ShadowSize = 10,
            CornerRadiusTopLeft = 0,
            CornerRadiusTopRight = 0,
            CornerRadiusBottomRight = 0,
            CornerRadiusBottomLeft = 0,
            ContentMarginLeft = left,
            ContentMarginTop = top,
            ContentMarginRight = right,
            ContentMarginBottom = bottom,
        };
        style.SetBorderWidthAll(1);
        return style;
    }

    private static StyleBoxFlat CreateButtonStyle(bool primary, float alpha)
    {
        var baseColor = primary
            ? new Color(Palette.Frame, 0.16f * alpha)
            : new Color(1f, 1f, 1f, 0.92f * alpha);
        var borderColor = primary
            ? new Color(Palette.Frame, 0.64f * alpha)
            : new Color(Palette.Frame, 0.34f * alpha);

        var style = new StyleBoxFlat
        {
            BgColor = baseColor,
            BorderColor = borderColor,
            ContentMarginLeft = 14,
            ContentMarginTop = 8,
            ContentMarginRight = 14,
            ContentMarginBottom = 8,
            CornerRadiusTopLeft = 0,
            CornerRadiusTopRight = 0,
            CornerRadiusBottomRight = 0,
            CornerRadiusBottomLeft = 0,
        };
        style.SetBorderWidthAll(1);
        return style;
    }

    private static (Label value, Label meta) AddSettlementCard(GridContainer parent, string title, bool compactValue = false)
    {
        var card = new PanelContainer
        {
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        card.AddThemeStyleboxOverride("panel", CreatePanelStyle(
            new Color(Palette.WorldFloorDeep, 0.76f),
            new Color(Palette.Frame, 0.22f),
            16,
            16,
            16,
            16,
            0));
        parent.AddChild(card);

        var body = new VBoxContainer();
        body.AddThemeConstantOverride("separation", 8);
        card.AddChild(body);

        var label = CreateLabel(11, new Color(Palette.Frame, 0.88f), true, 1.8f);
        label.Text = title;
        body.AddChild(label);

        var value = CreateLabel(compactValue ? 14 : 18, Palette.UiText, true, 0.3f, true);
        body.AddChild(value);

        var meta = CreateLabel(12, Palette.UiMuted, false, 0.2f, true);
        body.AddChild(meta);

        return (value, meta);
    }
}
