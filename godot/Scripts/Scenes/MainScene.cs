using Godot;
using ShotV.Core;
using ShotV.State;

namespace ShotV.Scenes;

public partial class MainScene : Node
{
    private Node? _currentScene;

    private readonly PackedScene _combatScenePacked = GD.Load<PackedScene>("res://Scenes/Combat.tscn");
    private readonly PackedScene _baseCampScenePacked = GD.Load<PackedScene>("res://Scenes/BaseCamp.tscn");

    public override void _Ready()
    {
        CallDeferred(nameof(DeferredStart));
    }

    private void DeferredStart()
    {
        var store = GameManager.Instance?.Store;
        if (store == null)
        {
            GD.PrintErr("[MainScene] GameManager not found.");
            return;
        }

        store.StateChanged += OnStateChanged;
        TransitionToMode(store.State.Mode);
    }

    private void OnStateChanged(GameState current, GameState previous)
    {
        if (current.Mode != previous.Mode)
            TransitionToMode(current.Mode);
    }

    private void TransitionToMode(GameMode mode)
    {
        GD.Print("[MainScene] Transitioning to: ", mode);

        if (_currentScene != null)
        {
            _currentScene.QueueFree();
            _currentScene = null;
        }

        switch (mode)
        {
            case GameMode.Base:
                _currentScene = _baseCampScenePacked.Instantiate();
                AddChild(_currentScene);
                break;

            case GameMode.Combat:
                _currentScene = _combatScenePacked.Instantiate();
                AddChild(_currentScene);
                // Start the encounter from active run state
                var run = GameManager.Instance?.Store.State.Save.Session.ActiveRun;
                if (run != null && _currentScene is CombatScene combatScene)
                    combatScene.StartEncounter(run.Map);
                break;
        }
    }
}
