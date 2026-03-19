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
    private string? _selectedItemId;
    private bool _hideQuantities;

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

    public void SetSelectedItemId(string? itemId)
    {
        _selectedItemId = itemId;
        QueueRedraw();
    }

    public void SetHideQuantities(bool hide)
    {
        _hideQuantities = hide;
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
        DrawRect(rect, new Color(Palette.BgOuter, 0.78f));
        DrawRect(rect, new Color(Palette.Frame, 0.2f), false, 1f);

        const float cut = 7f;
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
                DrawRect(cellRect, new Color(Palette.WorldFloorDeep, 0.32f));
                DrawRect(cellRect, new Color(Palette.FrameSoft, 0.12f), false, 1f);
                DrawRect(new Rect2(x + CellSize * 0.5f - 1f, y + CellSize * 0.5f - 1f, 2f, 2f), new Color(Palette.Frame, 0.18f));
            }
        }

        foreach (var item in _items)
            DrawInventoryItem(item, item.X * CellSize, item.Y * CellSize, 1f, 0.26f, 0.72f);
    }

    private void DrawInventoryItem(InventoryItemRecord item, float x, float y, float alpha, float accentAlpha, float strokeAlpha)
    {
        if (!ItemData.ById.TryGetValue(item.ItemId, out var definition))
            return;

        bool selected = item.Id == _selectedItemId;
        var rect = new Rect2(x + 2f, y + 2f, item.Width * CellSize - 4f, item.Height * CellSize - 4f);
        DrawRect(rect, new Color(definition.Tint, alpha * (selected ? 0.92f : 0.78f)));
        if (selected)
            DrawRect(rect.Grow(1.5f), new Color(definition.AccentColor, 0.2f));
        DrawRect(rect, new Color(definition.AccentColor, strokeAlpha * (selected ? 1f : 0.82f)), false, selected ? 2f : 1.25f);
        DrawRect(new Rect2(rect.Position.X + 5f, rect.Position.Y + 5f, Mathf.Max(14f, rect.Size.X - 10f), 3f), new Color(definition.AccentColor, 0.74f));
        DrawRect(new Rect2(rect.Position + new Vector2(2f, 2f), new Vector2(rect.Size.X - 4f, Mathf.Max(8f, rect.Size.Y * 0.24f))), new Color(1f, 1f, 1f, accentAlpha * 0.5f));

        var font = ThemeDB.FallbackFont;
        int fontSize = UiScale.Font(rect.Size.Y >= 56f ? 11 : 10);
        string label = definition.ShortLabel;
        var labelPos = rect.Position + new Vector2(8f, 18f);
        DrawString(font, labelPos, label, HorizontalAlignment.Left, Mathf.Max(10f, rect.Size.X - 16f), fontSize, new Color(Palette.UiText, 0.84f));

        if (!_hideQuantities && item.Quantity > 1)
        {
            string quantity = $"x{item.Quantity}";
            int quantityFont = UiScale.Font(10);
            var size = font.GetStringSize(quantity, HorizontalAlignment.Left, -1, quantityFont);
            DrawString(font, rect.Position + new Vector2(rect.Size.X - size.X - 6f, rect.Size.Y - 8f), quantity, HorizontalAlignment.Left, -1, quantityFont, new Color(Palette.UiText, 0.76f));
        }

        if (_badges.TryGetValue(item.Id, out var badge))
        {
            int badgeFontSize = UiScale.Font(10);
            var badgeSize = font.GetStringSize(badge, HorizontalAlignment.Left, -1, badgeFontSize);
            float badgeWidth = Mathf.Max(18f, badgeSize.X + 10f);
            var badgeRect = new Rect2(rect.Position + new Vector2(6f, rect.Size.Y - 22f), new Vector2(badgeWidth, 14f));
            DrawRect(badgeRect, new Color(definition.AccentColor, 0.92f));
            DrawRect(badgeRect, new Color(Palette.BgOuter, 0.26f), false, 1f);
            DrawString(font, badgeRect.Position + new Vector2(5f, 11f), badge, HorizontalAlignment.Left, -1, badgeFontSize, Palette.BgInner);
        }
    }

    private void DrawBracket(Vector2 corner, Vector2 horizontal, Vector2 vertical)
    {
        DrawLine(corner, horizontal, new Color(Palette.Frame, 0.32f), 1.4f);
        DrawLine(corner, vertical, new Color(Palette.Frame, 0.32f), 1.4f);
    }
}
