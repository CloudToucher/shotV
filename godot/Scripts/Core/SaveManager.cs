using System;
using System.Text.Json;
using Godot;
using ShotV.State;

namespace ShotV.Core;

public partial class SaveManager : Node
{
    private const string SavePath = "user://save_data.json";
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public SaveState Load()
    {
        if (!FileAccess.FileExists(SavePath))
        {
            GD.Print("[SaveManager] No save file found, creating initial state.");
            return SaveState.CreateInitial();
        }

        try
        {
            using var file = FileAccess.Open(SavePath, FileAccess.ModeFlags.Read);
            if (file == null)
            {
                GD.PrintErr("[SaveManager] Failed to open save file.");
                return SaveState.CreateInitial();
            }

            string json = file.GetAsText();
            var state = JsonSerializer.Deserialize<SaveState>(json, JsonOptions);
            if (state == null)
            {
                GD.PrintErr("[SaveManager] Failed to deserialize save file.");
                return SaveState.CreateInitial();
            }

            GD.Print("[SaveManager] Save loaded. Version: ", state.Version);
            return state;
        }
        catch (Exception ex)
        {
            GD.PrintErr("[SaveManager] Error loading save: ", ex.Message);
            return SaveState.CreateInitial();
        }
    }

    public void Save(SaveState state)
    {
        try
        {
            state.UpdatedAt = DateTime.UtcNow.ToString("o");
            string json = JsonSerializer.Serialize(state, JsonOptions);
            using var file = FileAccess.Open(SavePath, FileAccess.ModeFlags.Write);
            if (file == null)
            {
                GD.PrintErr("[SaveManager] Failed to open save file for writing.");
                return;
            }
            file.StoreString(json);
        }
        catch (Exception ex)
        {
            GD.PrintErr("[SaveManager] Error saving: ", ex.Message);
        }
    }

    public void DeleteSave()
    {
        if (FileAccess.FileExists(SavePath))
        {
            DirAccess.RemoveAbsolute(SavePath);
            GD.Print("[SaveManager] Save file deleted.");
        }
    }
}
