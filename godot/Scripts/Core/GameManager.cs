using Godot;
using ShotV.State;

namespace ShotV.Core;

public partial class GameManager : Node
{
    public static GameManager? Instance { get; private set; }

    public GameStore Store { get; private set; } = new();

    public override void _EnterTree()
    {
        Instance = this;
    }

    public override void _Ready()
    {
        var saveManager = GetNode<SaveManager>("/root/SaveManager");
        var save = saveManager.Load();
        Store.Initialize(save);
        Store.StateChanged += OnStateChanged;
        GD.Print("[GameManager] Initialized. Mode: ", Store.State.Mode);
    }

    public void Save()
    {
        var saveManager = GetNode<SaveManager>("/root/SaveManager");
        saveManager.Save(Store.State.Save);
    }

    private void OnStateChanged(GameState current, GameState previous)
    {
        if (current.Mode != previous.Mode)
        {
            GD.Print("[GameManager] Mode changed: ", previous.Mode, " -> ", current.Mode);
        }

        if (!ReferenceEquals(current.Save, previous.Save))
            Save();
    }
}
