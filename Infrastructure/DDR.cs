namespace Gradual.Infrastructure;

/// <summary>
/// Feature 64 — Drag-and-drop reorder support for ListView.
/// Attach to any ListView; stores the custom display order in memory and fires
/// ReorderRequested when the user drops an item.
/// </summary>
public sealed class ListViewDragDropReorder : IDisposable
{
    private readonly ListView _listView;
    private ListViewItem? _dragItem;
    private bool _isDragging;

    /// <summary>Fires when user drops an item. Args = (fromIndex, toIndex).</summary>
    public event EventHandler<(int from, int to)>? ReorderRequested;

    public ListViewDragDropReorder(ListView listView)
    {
        _listView = listView;
        _listView.AllowDrop   = true;
        _listView.ItemDrag    += ListView_ItemDrag;
        _listView.DragEnter   += ListView_DragEnter;
        _listView.DragOver    += ListView_DragOver;
        _listView.DragDrop    += ListView_DragDrop;
        _listView.DragLeave   += (s, e) => { _isDragging = false; _listView.Invalidate(); };
    }

    private void ListView_ItemDrag(object? sender, ItemDragEventArgs e)
    {
        if (e.Item is not ListViewItem item) return;
        _dragItem = item;
        _isDragging = true;
        _listView.DoDragDrop(item, DragDropEffects.Move);
    }

    private void ListView_DragEnter(object? sender, DragEventArgs e)
    {
        e.Effect = e.Data?.GetDataPresent(typeof(ListViewItem)) == true
            ? DragDropEffects.Move : DragDropEffects.None;
    }

    private void ListView_DragOver(object? sender, DragEventArgs e)
    {
        if (!_isDragging) return;
        e.Effect = DragDropEffects.Move;

        // Visual feedback: highlight target row
        var pt = _listView.PointToClient(new Point(e.X, e.Y));
        var target = _listView.GetItemAt(pt.X, pt.Y);
        if (target != null && target != _dragItem)
        {
            _listView.BeginUpdate();
            foreach (ListViewItem item in _listView.Items)
                item.BackColor = Color.Transparent;
            target.BackColor = Color.FromArgb(40, 88, 166, 255); // blue tint
            _listView.EndUpdate();
        }
    }

    private void ListView_DragDrop(object? sender, DragEventArgs e)
    {
        _isDragging = false;
        if (_dragItem == null) return;

        var pt = _listView.PointToClient(new Point(e.X, e.Y));
        var targetItem = _listView.GetItemAt(pt.X, pt.Y);

        if (targetItem == null || targetItem == _dragItem)
        {
            // Clear highlights
            foreach (ListViewItem item in _listView.Items)
                item.BackColor = Color.Transparent;
            return;
        }

        int fromIdx = _dragItem.Index;
        int toIdx   = targetItem.Index;

        // Move visually
        _listView.BeginUpdate();
        _listView.Items.RemoveAt(fromIdx);
        _listView.Items.Insert(toIdx, _dragItem);
        foreach (ListViewItem item in _listView.Items)
            item.BackColor = Color.Transparent;
        _listView.EndUpdate();

        _dragItem.Selected = true;
        _listView.EnsureVisible(toIdx);
        _dragItem = null;

        ReorderRequested?.Invoke(this, (fromIdx, toIdx));
    }

    public void Dispose()
    {
        _listView.ItemDrag  -= ListView_ItemDrag;
        _listView.DragEnter -= ListView_DragEnter;
        _listView.DragOver  -= ListView_DragOver;
        _listView.DragDrop  -= ListView_DragDrop;
    }
}
