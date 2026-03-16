using System.Collections.Generic;
using Godot;
using ShotV.Combat;
using ShotV.Core;
using ShotV.Data;
using ShotV.State;
using ShotV.UI;
using ShotV.World;

namespace ShotV.Scenes;

public partial class BaseCampScene : Node2D, IOverlaySceneDataProvider
{
    private PlayerAvatar _player = null!;
    private Camera2D _camera = null!;
    private Minimap? _minimap;
    private WorldMapLayout _layout = null!;
    private List<WorldMarker> _markers = new();
    private static readonly List<EnemyActor> EmptyEnemies = new();

    private string? _nearbyMarkerId;
    private const float InteractRange = 120f;

    public override void _Ready()
    {
        _player = GetNode<PlayerAvatar>("Player");
        _camera = GetNode<Camera2D>("Camera");
        _minimap = GetNodeOrNull<Minimap>("HUDLayer/Minimap");

        _layout = WorldLayoutBuilder.CreateBaseLayout();
        _markers = _layout.Markers;

        _player.Reset();
        _player.SetWeaponStyle(WeaponType.MachineGun);
        _player.SetPlayerPosition(_layout.PlayerSpawn.X, _layout.PlayerSpawn.Y);
        _camera.Position = _player.PlayerPosition;

        GD.Print("[BaseCampScene] Base camp loaded.");
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        HandleOverlayShortcuts();

        bool fullscreenUi = IsFullscreenUiActive();
        if (_minimap != null)
            _minimap.Visible = !fullscreenUi;

        var moveIntent = new Vector2(
            (Input.IsActionPressed("move_right") ? 1f : 0f) - (Input.IsActionPressed("move_left") ? 1f : 0f),
            (Input.IsActionPressed("move_down") ? 1f : 0f) - (Input.IsActionPressed("move_up") ? 1f : 0f));
        _player.SetMoveIntent(fullscreenUi ? 0f : moveIntent.X, fullscreenUi ? 0f : moveIntent.Y);

        var mousePos = GetGlobalMousePosition();
        var playerPos = _player.PlayerPosition;
        float aimAngle = Mathf.Atan2(mousePos.Y - playerPos.Y, mousePos.X - playerPos.X);
        _player.SetAimAngle(aimAngle);

        _player.UpdatePhysics(dt, _layout.Bounds, _layout.Obstacles);
        _camera.Position = _camera.Position.Lerp(_player.PlayerPosition, Mathf.Min(1f, 6f * dt));
        _minimap?.UpdateData(_layout.Bounds, _player.PlayerPosition, EmptyEnemies, _layout.Obstacles, _layout.Markers);

        UpdateNearbyMarker();

        if (!fullscreenUi && Input.IsActionJustPressed("interact"))
            HandleInteraction();

        QueueRedraw();
    }

    public override void _Draw()
    {
        DrawRect(_layout.Bounds, Palette.WorldFloor);

        float gridSize = CombatConstants.GridSize;
        var lineColor = new Color(Palette.WorldLine, 0.25f);
        for (float x = _layout.Bounds.Position.X; x <= _layout.Bounds.End.X; x += gridSize)
            DrawLine(new Vector2(x, _layout.Bounds.Position.Y), new Vector2(x, _layout.Bounds.End.Y), lineColor, 1f);
        for (float y = _layout.Bounds.Position.Y; y <= _layout.Bounds.End.Y; y += gridSize)
            DrawLine(new Vector2(_layout.Bounds.Position.X, y), new Vector2(_layout.Bounds.End.X, y), lineColor, 1f);

        DrawRect(_layout.Bounds, Palette.WorldLineStrong, false, 2f);

        foreach (var obstacle in _layout.Obstacles)
        {
            var rect = new Rect2(obstacle.X, obstacle.Y, obstacle.Width, obstacle.Height);
            Color fill = obstacle.Kind switch
            {
                ObstacleKind.Cover => Palette.ObstacleCover,
                ObstacleKind.Locker => Palette.ObstacleLocker,
                ObstacleKind.Station => Palette.ObstacleStation,
                _ => Palette.ObstacleFill,
            };
            DrawRect(rect, fill);
            DrawRect(rect, new Color(Palette.ObstacleEdge, 0.5f), false, 1.5f);
        }

        foreach (var marker in _markers)
        {
            Color markerColor = marker.Kind switch
            {
                MarkerKind.Entry => Palette.Frame,
                MarkerKind.Station => Palette.ObstacleStation,
                MarkerKind.Locker => Palette.ObstacleLocker,
                _ => Palette.Frame,
            };

            var position = new Vector2(marker.X, marker.Y);
            bool isNearby = marker.Id == _nearbyMarkerId;
            float outerRadius = isNearby ? 18f : 14f;
            float innerRadius = isNearby ? 10f : 8f;

            DrawCircle(position, outerRadius, new Color(markerColor, isNearby ? 0.35f : 0.22f));
            DrawCircle(position, innerRadius, new Color(markerColor, isNearby ? 0.7f : 0.55f));
            DrawArc(position, outerRadius, 0f, Mathf.Tau, 24, new Color(markerColor, isNearby ? 0.6f : 0.4f), 1.5f);

            if (!isNearby)
                continue;

            var font = ThemeDB.FallbackFont;
            int fontSize = ThemeDB.FallbackFontSize;
            var textSize = font.GetStringSize(marker.Label, HorizontalAlignment.Center, -1, fontSize);
            DrawString(font, position + new Vector2(-textSize.X / 2f, -outerRadius - 8f), marker.Label, HorizontalAlignment.Center, -1, fontSize, Palette.UiText);
        }
    }

    public OverlayWorldSnapshot? BuildOverlayWorldSnapshot()
    {
        return new OverlayWorldSnapshot
        {
            Bounds = _layout.Bounds,
            CameraBounds = BuildCameraBounds(),
            PlayerPosition = _player.PlayerPosition,
            HighlightedMarkerId = _nearbyMarkerId ?? "launch",
            Obstacles = new List<WorldObstacle>(_layout.Obstacles),
            Markers = new List<WorldMarker>(_layout.Markers),
            EnemyPositions = new List<Vector2>(),
        };
    }

    private void UpdateNearbyMarker()
    {
        var store = GameManager.Instance?.Store;
        if (store == null)
            return;

        var playerPos = _player.PlayerPosition;
        string? closestId = null;
        string? closestLabel = null;
        MarkerKind? closestKind = null;
        float closestDist = InteractRange;

        foreach (var marker in _markers)
        {
            float distance = playerPos.DistanceTo(new Vector2(marker.X, marker.Y));
            if (distance >= closestDist)
                continue;

            closestDist = distance;
            closestId = marker.Id;
            closestLabel = marker.Label;
            closestKind = marker.Kind;
        }

        _nearbyMarkerId = closestId;

        store.UpdateSceneRuntime(runtime =>
        {
            runtime.NearbyMarkerId = closestId;
            runtime.NearbyMarkerLabel = closestLabel;
            runtime.NearbyMarkerKind = closestKind;
            runtime.PrimaryActionReady = closestId == "launch";
            runtime.PrimaryActionHint = closestId == "launch" ? "Deploy" : (closestLabel ?? "");
            runtime.NearbyLootCount = 0;
        });
    }

    private void HandleInteraction()
    {
        var store = GameManager.Instance?.Store;
        if (store == null || _nearbyMarkerId == null)
            return;

        switch (_nearbyMarkerId)
        {
            case "launch":
                store.OpenScenePanel(ScenePanelMode.Launch);
                break;
            case "command":
                store.OpenScenePanel(ScenePanelMode.Command);
                break;
            case "locker":
                store.OpenScenePanel(ScenePanelMode.Locker);
                break;
            case "workshop":
                store.OpenScenePanel(ScenePanelMode.Workshop);
                break;
        }
    }

    private void HandleOverlayShortcuts()
    {
        var store = GameManager.Instance?.Store;
        if (store == null)
            return;

        if (Input.IsActionJustPressed("toggle_map"))
            store.ToggleMapOverlay();

        if (Input.IsActionJustPressed("toggle_inventory"))
            store.ToggleScenePanel(ScenePanelMode.Overview);
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
}
