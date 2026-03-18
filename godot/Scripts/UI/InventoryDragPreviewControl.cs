using Godot;
using ShotV.Core;
using ShotV.Data;
using ShotV.State;

namespace ShotV.UI;

public partial class InventoryDragPreviewControl : Control
{
    private InventoryItemRecord? _item;
    private Vector2 _topLeft;
    private float _cellSize = 32f;
    private bool _valid;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
    }

    public void SetPreview(InventoryItemRecord? item, Vector2 topLeft, float cellSize, bool valid)
    {
        _item = item?.Clone();
        _topLeft = topLeft;
        _cellSize = Mathf.Max(16f, cellSize);
        _valid = valid;
        Visible = _item != null;
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (_item == null || !ItemData.ById.TryGetValue(_item.ItemId, out var definition))
            return;

        var rect = new Rect2(_topLeft + new Vector2(2f, 2f), new Vector2(_item.Width * _cellSize - 4f, _item.Height * _cellSize - 4f));
        float alpha = _valid ? 0.74f : 0.42f;
        float strokeAlpha = _valid ? 0.72f : 0.34f;
        DrawRect(rect, new Color(definition.Tint, alpha));
        DrawRect(rect, new Color(definition.AccentColor, strokeAlpha), false, 1.5f);
        DrawRect(new Rect2(rect.Position + new Vector2(6f, 6f), new Vector2(Mathf.Max(16f, rect.Size.X - 12f), 2f)), new Color(definition.AccentColor, 0.86f));
        DrawRect(new Rect2(rect.Position + new Vector2(2f, 2f), new Vector2(rect.Size.X - 4f, Mathf.Max(10f, rect.Size.Y * 0.38f))), new Color(1f, 1f, 1f, _valid ? 0.26f : 0.16f));

        var font = ThemeDB.FallbackFont;
        DrawString(font, rect.Position + new Vector2(8f, 18f), definition.ShortLabel, HorizontalAlignment.Left, Mathf.Max(10f, rect.Size.X - 16f), UiScale.Font(rect.Size.Y >= 56f ? 12 : 11), Palette.UiText);
    }
}
