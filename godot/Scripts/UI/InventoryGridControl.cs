using System.Collections.Generic;
using Godot;
using ShotV.Core;
using ShotV.Data;
using ShotV.State;

namespace ShotV.UI;

public partial class InventoryGridControl : Control
{
    private readonly List<InventoryItemRecord> _items = new();
    private Dictionary<string, string> _badges = new();

    public int Columns { get; private set; } = 1;
    public int Rows { get; private set; } = 1;
    public float CellSize { get; private set; } = 32f;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
    }

    public void Configure(int columns, int rows, float cellSize)
    {
        Columns = Mathf.Max(1, columns);
        Rows = Mathf.Max(1, rows);
        CellSize = Mathf.Max(16f, cellSize);
        CustomMinimumSize = new Vector2(Columns * CellSize, Rows * CellSize);
        QueueRedraw();
    }

    public void SetItems(IEnumerable<InventoryItemRecord> items)
    {
        _items.Clear();
        foreach (var item in items)
            _items.Add(item.Clone());
        QueueRedraw();
    }

    public void SetBadges(Dictionary<string, string>? badges)
    {
        _badges = badges ?? new Dictionary<string, string>();
        QueueRedraw();
    }

    public bool TryGetCellAtViewport(Vector2 viewportPosition, out Vector2I cell)
    {
        var rect = GetGlobalRect();
        if (!rect.HasPoint(viewportPosition))
        {
            cell = default;
            return false;
        }

        var local = viewportPosition - rect.Position;
        int x = Mathf.FloorToInt(local.X / CellSize);
        int y = Mathf.FloorToInt(local.Y / CellSize);
        if (x < 0 || y < 0 || x >= Columns || y >= Rows)
        {
            cell = default;
            return false;
        }

        cell = new Vector2I(x, y);
        return true;
    }

    public override void _Draw()
    {
        var rect = new Rect2(Vector2.Zero, new Vector2(Columns * CellSize, Rows * CellSize));
        DrawRect(rect, new Color(1f, 1f, 1f, 0.82f));
        DrawRect(rect, new Color(Palette.Frame, 0.3f), false, 1.5f);

        const float cut = 8f;
        DrawBracket(rect.Position, rect.Position + new Vector2(cut, 0f), rect.Position + new Vector2(0f, cut));
        var topRight = new Vector2(rect.End.X, rect.Position.Y);
        DrawBracket(topRight, topRight + new Vector2(-cut, 0f), topRight + new Vector2(0f, cut));
        var bottomRight = rect.End;
        DrawBracket(bottomRight, bottomRight + new Vector2(-cut, 0f), bottomRight + new Vector2(0f, -cut));
        var bottomLeft = new Vector2(rect.Position.X, rect.End.Y);
        DrawBracket(bottomLeft, bottomLeft + new Vector2(cut, 0f), bottomLeft + new Vector2(0f, -cut));

        for (int row = 0; row < Rows; row++)
        {
            for (int column = 0; column < Columns; column++)
            {
                float x = column * CellSize;
                float y = row * CellSize;
                var cellRect = new Rect2(x + 1f, y + 1f, CellSize - 2f, CellSize - 2f);
                DrawRect(cellRect, new Color(Palette.UiActive, 0.15f));
                DrawRect(cellRect, new Color(Palette.FrameSoft, 0.1f), false, 1f);
                DrawRect(new Rect2(x + CellSize * 0.5f - 1f, y + CellSize * 0.5f - 1f, 2f, 2f), new Color(Palette.FrameSoft, 0.2f));
            }
        }

        foreach (var item in _items)
            DrawInventoryItem(item, item.X * CellSize, item.Y * CellSize, 1f, 0.26f, 0.72f);
    }

    private void DrawInventoryItem(InventoryItemRecord item, float x, float y, float alpha, float accentAlpha, float strokeAlpha)
    {
        if (!ItemData.ById.TryGetValue(item.ItemId, out var definition))
            return;

        var rect = new Rect2(x + 2f, y + 2f, item.Width * CellSize - 4f, item.Height * CellSize - 4f);
        DrawRect(rect, new Color(definition.Tint, alpha * 0.92f));
        DrawRect(rect, new Color(definition.AccentColor, strokeAlpha), false, 1.5f);
        DrawRect(new Rect2(rect.Position.X + 6f, rect.Position.Y + 6f, Mathf.Max(16f, rect.Size.X - 12f), 2f), new Color(definition.AccentColor, 0.86f));
        DrawRect(new Rect2(rect.Position + new Vector2(2f, 2f), new Vector2(rect.Size.X - 4f, Mathf.Max(10f, rect.Size.Y * 0.38f))), new Color(1f, 1f, 1f, accentAlpha));

        var font = ThemeDB.FallbackFont;
        int fontSize = rect.Size.Y >= 56f ? 12 : 11;
        string label = definition.ShortLabel;
        var labelPos = rect.Position + new Vector2(8f, 18f);
        DrawString(font, labelPos, label, HorizontalAlignment.Left, Mathf.Max(10f, rect.Size.X - 16f), fontSize, Palette.UiText);

        if (item.Quantity > 1)
        {
            string quantity = $"x{item.Quantity}";
            var size = font.GetStringSize(quantity, HorizontalAlignment.Left, -1, 10);
            DrawString(font, rect.Position + new Vector2(rect.Size.X - size.X - 6f, rect.Size.Y - 8f), quantity, HorizontalAlignment.Left, -1, 10, Palette.UiText);
        }

        if (_badges.TryGetValue(item.Id, out var badge))
        {
            var badgeRect = new Rect2(rect.Position + new Vector2(6f, rect.Size.Y - 22f), new Vector2(18f, 14f));
            DrawRect(badgeRect, new Color(definition.AccentColor, 0.92f));
            DrawRect(badgeRect, new Color(Palette.UiText, 0.14f), false, 1f);
            DrawString(font, badgeRect.Position + new Vector2(5f, 11f), badge, HorizontalAlignment.Left, -1, 10, Palette.BgInner);
        }
    }

    private void DrawBracket(Vector2 corner, Vector2 horizontal, Vector2 vertical)
    {
        DrawLine(corner, horizontal, new Color(Palette.Frame, 0.8f), 2f);
        DrawLine(corner, vertical, new Color(Palette.Frame, 0.8f), 2f);
    }
}
