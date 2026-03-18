using Godot;
using ShotV.Core;
using ShotV.State;
using ShotV.World;

namespace ShotV.Combat;

public enum ExitActionKind { Extract }
public enum ExitActionPhase { Charging, Opening }

public class ExitActionState
{
    public ExitActionKind Kind { get; set; }
    public ExitActionPhase Phase { get; set; }
    public string MarkerId { get; set; } = "";
    public string MarkerLabel { get; set; } = "";
    public float Elapsed { get; set; }
    public float Duration { get; set; }
}

public class ExitActionManager
{
    private const float ExtractChannelSeconds = 1.1f;
    private const float GateOpenSeconds = 0.42f;
    private const float MarkerRange = 120f;

    private ExitActionState? _state;
    public ExitActionState? Current => _state;
    public bool IsActive => _state != null;
    public float GateOpenProgress => GetGateOpenProgress();

    public delegate void ExitActionCompleted(ExitActionKind kind);
    public event ExitActionCompleted? Completed;

    public bool TryStart(
        Vector2 playerPos,
        WorldMapLayout layout,
        RunMapState map)
    {
        var exitMarker = FindExitMarker(playerPos, layout);
        if (exitMarker == null) return false;
        if (_state != null) return false;
        if (!RouteManager.CanExtractFromRunMap(map)) return false;

        _state = new ExitActionState
        {
            Kind = ExitActionKind.Extract,
            Phase = ExitActionPhase.Charging,
            MarkerId = exitMarker.Value.id,
            MarkerLabel = exitMarker.Value.label,
            Elapsed = 0f,
            Duration = ExtractChannelSeconds,
        };
        return true;
    }

    public void Cancel()
    {
        _state = null;
    }

    public void Tick(float delta, Vector2 playerPos, WorldMapLayout layout)
    {
        if (_state == null) return;

        var exitMarker = FindExitMarker(playerPos, layout);
        if (exitMarker == null || exitMarker.Value.id != _state.MarkerId)
        {
            Cancel();
            return;
        }

        _state.Elapsed += delta;
        if (_state.Elapsed < _state.Duration) return;

        if (_state.Phase == ExitActionPhase.Charging)
        {
            _state.Phase = ExitActionPhase.Opening;
            _state.Elapsed = 0f;
            _state.Duration = GateOpenSeconds;
            return;
        }

        // Opening phase complete
        var kind = _state.Kind;
        _state = null;
        Completed?.Invoke(kind);
    }

    public string GetProgressHint()
    {
        if (_state == null) return "";
        float progress = Mathf.Min(1f, _state.Elapsed / _state.Duration);
        int pct = Mathf.RoundToInt(progress * 100f);
        if (_state.Phase == ExitActionPhase.Charging)
            return GameText.Format("exit.progress.extract", pct);
        return GameText.Format("exit.progress.opening", pct);
    }

    private float GetGateOpenProgress()
    {
        if (_state == null) return 0f;
        if (_state.Phase == ExitActionPhase.Opening)
            return Mathf.Min(1f, _state.Elapsed / _state.Duration);
        return 0f;
    }

    private static (string id, string label)? FindExitMarker(Vector2 playerPos, WorldMapLayout layout)
    {
        (string id, string label)? closest = null;
        float closestDistance = MarkerRange;
        foreach (var marker in layout.Markers)
        {
            if (marker.Kind != MarkerKind.Extraction) continue;
            float distance = playerPos.DistanceTo(new Vector2(marker.X, marker.Y));
            if (distance > closestDistance) continue;

            closestDistance = distance;
            closest = (marker.Id, marker.Label);
        }
        return closest;
    }
}
