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
    private const float FurnitureUnit = CombatConstants.GridSize * 0.5f;
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
        _player.SetWeaponStyle(WeaponData.GetDefaultWeapon().Id);
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
        _player.SetAimTarget(mousePos);

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
            DrawBaseObstacle(obstacle);

        foreach (var marker in _markers)
            DrawBaseMarker(marker);
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
            runtime.PrimaryActionHint = closestId == "launch" ? GameText.Text("basecamp.deploy_current_map") : (closestLabel ?? "");
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
            case "trader":
                store.OpenScenePanel(ScenePanelMode.Shop);
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

    private void DrawBaseObstacle(WorldObstacle obstacle)
    {
        var rect = new Rect2(obstacle.X, obstacle.Y, obstacle.Width, obstacle.Height);
        Color fill = obstacle.Kind switch
        {
            ObstacleKind.Cover => Palette.ObstacleCover,
            ObstacleKind.Locker => Palette.ObstacleLocker,
            ObstacleKind.Station => Palette.ObstacleStation,
            _ => Palette.ObstacleFill,
        };
        float fillAlpha = obstacle.Kind switch
        {
            ObstacleKind.Wall => 0.82f,
            ObstacleKind.Locker => 0.56f,
            ObstacleKind.Station => 0.5f,
            _ => 0.42f,
        };

        DrawRect(rect, new Color(fill, fillAlpha));
        DrawRect(rect.Grow(-3f), new Color(1f, 1f, 1f, obstacle.Kind == ObstacleKind.Wall ? 0.04f : 0.08f));

        switch (obstacle.Kind)
        {
            case ObstacleKind.Station:
            {
                var screen = new Rect2(rect.Position + new Vector2(6f, 6f), new Vector2(Mathf.Min(rect.Size.X - 12f, rect.Size.X * 0.42f), 10f));
                if (screen.Size.X > 8f)
                    DrawRect(screen, new Color(Palette.Frame, 0.2f));
                break;
            }
            case ObstacleKind.Locker:
            {
                int handleCount = Mathf.Max(1, Mathf.RoundToInt(rect.Size.X / (FurnitureUnit * 1.5f)));
                float spacing = rect.Size.X / handleCount;
                for (int index = 0; index < handleCount; index++)
                {
                    float handleX = rect.Position.X + spacing * index + spacing * 0.5f - 1.5f;
                    DrawRect(new Rect2(handleX, rect.Position.Y + 9f, 3f, Mathf.Min(14f, rect.Size.Y - 18f)), new Color(Palette.ObstacleEdge, 0.3f));
                }
                break;
            }
            case ObstacleKind.Cover:
            {
                DrawRect(new Rect2(rect.Position + new Vector2(5f, 5f), new Vector2(Mathf.Max(8f, rect.Size.X - 10f), 6f)), new Color(1f, 1f, 1f, 0.14f));
                break;
            }
        }

        DrawRect(rect, new Color(Palette.ObstacleEdge, 0.42f), false, 1.5f);

        if (!string.IsNullOrWhiteSpace(obstacle.Label))
            DrawInlineLabel(rect.Position + new Vector2(7f, 7f), obstacle.Label!, ResolveObstacleLabelColor(obstacle.Kind), 0.72f);
    }

    private void DrawBaseMarker(WorldMarker marker)
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

        DrawCircle(position, outerRadius, new Color(markerColor, isNearby ? 0.35f : 0.18f));
        DrawCircle(position, innerRadius, new Color(markerColor, isNearby ? 0.78f : 0.48f));
        DrawArc(position, outerRadius, 0f, Mathf.Tau, 24, new Color(markerColor, isNearby ? 0.72f : 0.34f), isNearby ? 1.8f : 1.3f);

        DrawCenteredLabel(position + new Vector2(0f, -outerRadius - 14f), marker.Label, markerColor, isNearby ? 0.92f : 0.58f);
    }

    private void DrawCenteredLabel(Vector2 anchor, string text, Color tint, float alpha)
    {
        var font = ThemeDB.FallbackFont;
        int fontSize = UiScale.Font(ThemeDB.FallbackFontSize - 1);
        var textSize = font.GetStringSize(text, HorizontalAlignment.Center, -1, fontSize);
        var rect = new Rect2(
            new Vector2(anchor.X - textSize.X * 0.5f - 8f, anchor.Y - fontSize - 6f),
            new Vector2(textSize.X + 16f, fontSize + 10f));

        DrawRect(rect, new Color(1f, 1f, 1f, 0.88f * alpha));
        DrawRect(rect, new Color(tint, 0.42f * alpha), false, 1f);
        DrawString(font, new Vector2(rect.Position.X + 8f, rect.End.Y - 5f), text, HorizontalAlignment.Left, -1, fontSize, new Color(Palette.UiText, alpha));
    }

    private void DrawInlineLabel(Vector2 position, string text, Color tint, float alpha)
    {
        var font = ThemeDB.FallbackFont;
        int fontSize = UiScale.Font(ThemeDB.FallbackFontSize - 3);
        var textSize = font.GetStringSize(text, HorizontalAlignment.Left, -1, fontSize);
        var rect = new Rect2(position, new Vector2(textSize.X + 12f, fontSize + 8f));

        DrawRect(rect, new Color(1f, 1f, 1f, 0.76f * alpha));
        DrawRect(rect, new Color(tint, 0.46f * alpha), false, 1f);
        DrawString(font, new Vector2(rect.Position.X + 6f, rect.End.Y - 4f), text, HorizontalAlignment.Left, -1, fontSize, new Color(Palette.UiText, alpha));
    }

    private static Color ResolveObstacleLabelColor(ObstacleKind kind)
    {
        return kind switch
        {
            ObstacleKind.Locker => Palette.PanelWarm,
            ObstacleKind.Station => Palette.Frame,
            ObstacleKind.Cover => Palette.UiMuted,
            _ => Palette.UiMuted,
        };
    }
}
