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
        float alpha = _valid ? 0.62f : 0.32f;
        float strokeAlpha = _valid ? 0.72f : 0.26f;
        DrawRect(rect, new Color(definition.Tint, alpha));
        DrawRect(rect, new Color(definition.AccentColor, strokeAlpha), false, 1.25f);
        DrawRect(new Rect2(rect.Position + new Vector2(5f, 5f), new Vector2(Mathf.Max(14f, rect.Size.X - 10f), 3f)), new Color(definition.AccentColor, 0.78f));
        DrawRect(new Rect2(rect.Position + new Vector2(2f, 2f), new Vector2(rect.Size.X - 4f, Mathf.Max(8f, rect.Size.Y * 0.24f))), new Color(1f, 1f, 1f, _valid ? 0.18f : 0.08f));

        var font = ThemeDB.FallbackFont;
        DrawString(font, rect.Position + new Vector2(8f, 18f), definition.ShortLabel, HorizontalAlignment.Left, Mathf.Max(10f, rect.Size.X - 16f), UiScale.Font(rect.Size.Y >= 56f ? 11 : 10), new Color(Palette.UiText, 0.88f));
    }
}
