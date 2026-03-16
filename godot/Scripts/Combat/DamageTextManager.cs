using System.Collections.Generic;
using Godot;
using ShotV.Core;

namespace ShotV.Combat;

public class DamageTextEntry
{
    public float X { get; set; }
    public float Y { get; set; }
    public string Text { get; set; } = "";
    public Color DrawColor { get; set; }
    public float Age { get; set; }
    public float Duration { get; set; }
    public float VelocityY { get; set; }
    public float Scale { get; set; } = 1f;
    public bool IsCritical { get; set; }
}

public partial class DamageTextManager : Node2D
{
    private readonly List<DamageTextEntry> _entries = new();

    public void SpawnDamageText(float x, float y, float amount, bool critical = false)
    {
        string text = critical ? $"{amount:F0}!" : $"{amount:F0}";
        float jitterX = (float)(GD.Randf() * 24 - 12);
        _entries.Add(new DamageTextEntry
        {
            X = x + jitterX,
            Y = y - 18f,
            Text = text,
            DrawColor = critical ? Palette.Accent : Palette.UiText,
            Age = 0f,
            Duration = critical ? 0.72f : 0.55f,
            VelocityY = -(68f + (float)GD.Randf() * 28f),
            Scale = critical ? 1.35f : 1f,
            IsCritical = critical,
        });
    }

    public void SpawnHealText(float x, float y, float amount)
    {
        _entries.Add(new DamageTextEntry
        {
            X = x + (float)(GD.Randf() * 16 - 8),
            Y = y - 14f,
            Text = $"+{amount:F0}",
            DrawColor = Palette.Dash,
            Age = 0f,
            Duration = 0.6f,
            VelocityY = -58f,
            Scale = 1.1f,
        });
    }

    public void SpawnStatusText(float x, float y, string text, Color color)
    {
        _entries.Add(new DamageTextEntry
        {
            X = x,
            Y = y - 22f,
            Text = text,
            DrawColor = color,
            Age = 0f,
            Duration = 0.8f,
            VelocityY = -42f,
            Scale = 0.9f,
        });
    }

    public void Reset() => _entries.Clear();

    public void Tick(float delta)
    {
        for (int i = _entries.Count - 1; i >= 0; i--)
        {
            var e = _entries[i];
            e.Age += delta;
            if (e.Age >= e.Duration) { _entries.RemoveAt(i); continue; }
            e.Y += e.VelocityY * delta;
            e.VelocityY *= 0.94f;
        }
        QueueRedraw();
    }

    public override void _Draw()
    {
        var font = ThemeDB.FallbackFont;
        if (font == null) return;

        foreach (var e in _entries)
        {
            float t = e.Age / e.Duration;
            float alpha = t < 0.15f ? t / 0.15f : 1f - MathUtil.EaseOutCubic(Mathf.Max(0f, (t - 0.55f) / 0.45f));
            float scale = e.Scale * (1f + (1f - t) * 0.15f);
            int fontSize = Mathf.RoundToInt(14f * scale);
            var pos = new Vector2(e.X, e.Y);

            // Shadow
            DrawString(font, pos + new Vector2(1, 1), e.Text, HorizontalAlignment.Center, -1, fontSize, new Color(0, 0, 0, alpha * 0.2f));
            // Text
            DrawString(font, pos, e.Text, HorizontalAlignment.Center, -1, fontSize, new Color(e.DrawColor, alpha));
        }
    }
}
