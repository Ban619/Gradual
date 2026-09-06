using Gradual.Infrastructure;
using Gradual.Interfaces;
using Gradual.Models;

namespace Gradual.Forms;

/// <summary>
/// Recycle Bin form — lists soft-deleted projects and allows restore or permanent purge (Features 16–18).
/// </summary>
public class RecycleBinForm : Form
{
    private readonly IProjectService _service;
    private List<ProjectRecord> _deletedProjects = new();

    private ListView _list = null!;
    private Button _restoreBtn = null!;
    private Button _purgeAllBtn = null!;
    private Label _countLabel = null!;

    private static readonly Color Bg    = Color.FromArgb(18, 27, 45);
    private static readonly Color Panel = Color.FromArgb(25, 37, 60);
    private static readonly Color TextClr = Color.FromArgb(220, 230, 245);

    public RecycleBinForm(IProjectService service)
    {
        _service = service;
        BuildUI();
        _ = LoadAsync();
    }

    private void BuildUI()
    {
        Text = "🗑 Recycle Bin";
        Size = new Size(860, 540);
        BackColor = Bg;
        ForeColor = TextClr;
        Font = new Font("Segoe UI", 9.5f);
        StartPosition = FormStartPosition.CenterScreen;

        var toolbar = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 46,
            BackColor = Panel,
            FlowDirection = FlowDirection.LeftToRight,
            Padding = new Padding(10, 6, 0, 6),
            WrapContents = false
        };

        var title = new Label
        {
            Text = "🗑 Recycle Bin",
            Font = new Font("Segoe UI", 13, FontStyle.Bold),
            ForeColor = Color.FromArgb(248, 81, 73),
            AutoSize = true,
            Margin = new Padding(0, 0, 20, 0)
        };

        _restoreBtn = new Button
        {
            Text = "♻ Restore Selected",
            Height = 30,
            AutoSize = true,
            BackColor = Color.FromArgb(63, 185, 80),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand,
            Enabled = false,
            Margin = new Padding(0, 0, 8, 0)
        };
        _restoreBtn.FlatAppearance.BorderSize = 0;
        _restoreBtn.Click += RestoreSelected_Click;

        _purgeAllBtn = new Button
        {
            Text = "🔥 Empty Bin",
            Height = 30,
            AutoSize = true,
            BackColor = Color.FromArgb(248, 81, 73),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand
        };
        _purgeAllBtn.FlatAppearance.BorderSize = 0;
        _purgeAllBtn.Click += PurgeAll_Click;

        _countLabel = new Label
        {
            Text = "0 items",
            ForeColor = Color.FromArgb(139, 148, 158),
            AutoSize = true,
            Margin = new Padding(16, 0, 0, 0)
        };

        toolbar.Controls.Add(title);
        toolbar.Controls.Add(_restoreBtn);
        toolbar.Controls.Add(_purgeAllBtn);
        toolbar.Controls.Add(_countLabel);

        _list = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true,
            BackColor = Panel,
            ForeColor = TextClr,
            GridLines = false,
            BorderStyle = BorderStyle.None,
            MultiSelect = true
        };
        _list.Columns.Add("Project Name", 200);
        _list.Columns.Add("Client", 130);
        _list.Columns.Add("Status (before)", 110);
        _list.Columns.Add("Priority", 90);
        _list.Columns.Add("Deleted At", 140);
        _list.SelectedIndexChanged += (s, e) =>
            _restoreBtn.Enabled = _list.SelectedItems.Count > 0;

        Controls.Add(_list);
        Controls.Add(toolbar);
    }

    private async Task LoadAsync()
    {
        var result = await _service.GetDeletedProjectsAsync();
        _deletedProjects = result.Data ?? new();

        _list.Items.Clear();
        foreach (var p in _deletedProjects)
        {
            var item = new ListViewItem(p.ProjectName);
            item.SubItems.Add(p.Client);
            item.SubItems.Add(p.Status);
            item.SubItems.Add(p.Priority);
            item.SubItems.Add(p.DeletedAt?.ToString("MM/dd/yyyy HH:mm") ?? "");
            item.Tag = p;
            _list.Items.Add(item);
        }

        _countLabel.Text = $"{_deletedProjects.Count} item(s) in recycle bin";
        _purgeAllBtn.Enabled = _deletedProjects.Any();
    }

    private async void RestoreSelected_Click(object? sender, EventArgs e)
    {
        var selected = _list.SelectedItems.Cast<ListViewItem>()
            .Select(i => (ProjectRecord)i.Tag!).ToList();

        if (!selected.Any()) return;

        foreach (var p in selected)
            await _service.RestoreProjectAsync(p.Id);

        MessageBox.Show($"Restored {selected.Count} project(s).", "Recycle Bin",
            MessageBoxButtons.OK, MessageBoxIcon.Information);
        await LoadAsync();
    }

    private async void PurgeAll_Click(object? sender, EventArgs e)
    {
        if (!_deletedProjects.Any()) return;

        var confirm = MessageBox.Show(
            $"Permanently delete all {_deletedProjects.Count} project(s)? This cannot be undone.",
            "Empty Recycle Bin",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning);

        if (confirm != DialogResult.Yes) return;

        var result = await _service.PurgeDeletedProjectsAsync();
        MessageBox.Show(result.Message ?? "Done.", "Recycle Bin",
            MessageBoxButtons.OK, MessageBoxIcon.Information);
        await LoadAsync();
    }
}
