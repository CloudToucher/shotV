using System;
using ShotV.Core;

namespace ShotV.State;

public class GameSettingsState
{
    public bool DeveloperMode { get; set; } = true;
    public string Locale { get; set; } = GameText.ChineseLocale;
}

public class SaveState
{
    public int Version { get; set; } = 8;
    public string CreatedAt { get; set; } = "";
    public string UpdatedAt { get; set; } = "";
    public BaseState Base { get; set; } = new();
    public InventoryState Inventory { get; set; } = new();
    public WorldState World { get; set; } = new();
    public SessionState Session { get; set; } = new();
    public GameSettingsState Settings { get; set; } = new();

    public SaveState Clone() => new()
    {
        Version = Version,
        CreatedAt = CreatedAt,
        UpdatedAt = UpdatedAt,
        Base = Base.Clone(),
        Inventory = Inventory.Clone(),
        World = World.Clone(),
        Session = Session.Clone(),
        Settings = new GameSettingsState
        {
            DeveloperMode = Settings.DeveloperMode,
            Locale = string.IsNullOrWhiteSpace(Settings.Locale) ? GameText.ChineseLocale : Settings.Locale,
        },
    };

    public static SaveState CreateInitial()
    {
        var now = DateTime.UtcNow.ToString("o");
        return new SaveState
        {
            Version = 8,
            CreatedAt = now,
            UpdatedAt = now,
            Base = new BaseState(),
            Inventory = new InventoryState(),
            World = new WorldState(),
            Session = new SessionState(),
            Settings = new GameSettingsState(),
        };
    }
}
