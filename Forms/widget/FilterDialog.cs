using Gradual.Infrastructure;
using Gradual.Models;

namespace Gradual.Forms;

/// <summary>
/// Advanced Filter Dialog — combines date range, tags, client, status and priority
/// into a single composable filter (Feature 26, 21–30).
/// </summary>
public class AdvancedFilterDialog : Form
{
    public FilterCriteria Result { get; private set; } = new();

    private DateTimePicker _fromDatePicker = null!;
    private DateTimePicker _toDatePicker = null!;
    private CheckBox _useDueDateCheck = null!;
    private CheckBox _fromCheck = null!;
    private CheckBox _toCheck = null!;
    private TextBox _tagsBox = null!;
    private TextBox _clientBox = null!;
    private TextBox _notesSearchBox = null!;
    private ComboBox _statusCombo = null!;
    private ComboBox _priorityCombo = null!;
    private CheckBox _overdueOnlyCheck = null!;
    private CheckBox _starredOnlyCheck = null!;
    private CheckBox _templateOnlyCheck = null!;
    private CheckBox _hasUrlCheck = null!;
    private CheckBox _hasBudgetCheck = null!;
    private CheckBox _tagAndModeCheck = null!;
    private ComboBox _presetCombo = null!;

    private static readonly Color Bg    = Color.FromArgb(18, 27, 45);
    private static readonly Color Card  = Color.FromArgb(25, 37, 60);
    private static readonly Color TextClr = Color.FromArgb(220, 230, 245);
    private static readonly Color Accent= Color.FromArgb(60, 125, 255);
    private static readonly Color Muted = Color.FromArgb(130, 150, 185);

    public AdvancedFilterDialog(FilterCriteria? existing = null)
    {
        if (existing != null) Result = existing;
        BuildUI();
        PopulateFromResult();
    }

    private void BuildUI()
    {
        Text = "🔍 Advanced Filter";
        Size = new Size(560, 640);
        BackColor = Bg;
        ForeColor = TextClr;
        Font = new Font("Segoe UI", 9.5f);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;

        var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Bg };
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 2,
            Padding = new Padding(16),
            BackColor = Bg
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 190));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        // Preset
        AddSection(layout, "Quick Presets");
        _presetCombo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
        _presetCombo.Items.AddRange(["(None)", "Overdue", "Due Today", "Starred", "Templates", "Active + High Priority", "No Folder"]);
        _presetCombo.SelectedIndex = 0;
        _presetCombo.BackColor = Card;
        _presetCombo.ForeColor = TextClr;
        layout.SetColumnSpan(AddRow(layout, "Preset:", _presetCombo), 1);

        // Date range
        AddSection(layout, "Date Range");
        _fromCheck = new CheckBox { Text = "From:", ForeColor = TextClr, Checked = false, AutoSize = true };
        _fromDatePicker = new DateTimePicker { Value = DateTime.Today.AddMonths(-1), Dock = DockStyle.Fill, Format = DateTimePickerFormat.Short, CalendarForeColor = TextClr, Enabled = false };
        _fromCheck.CheckedChanged += (s, e) => _fromDatePicker.Enabled = _fromCheck.Checked;
        layout.Controls.Add(_fromCheck);
        layout.Controls.Add(_fromDatePicker);

        _toCheck = new CheckBox { Text = "To:", ForeColor = TextClr, Checked = false, AutoSize = true };
        _toDatePicker = new DateTimePicker { Value = DateTime.Today, Dock = DockStyle.Fill, Format = DateTimePickerFormat.Short, Enabled = false };
        _toCheck.CheckedChanged += (s, e) => _toDatePicker.Enabled = _toCheck.Checked;
        layout.Controls.Add(_toCheck);
        layout.Controls.Add(_toDatePicker);

        _useDueDateCheck = new CheckBox { Text = "Apply to Due Date (not Created date)", ForeColor = Muted, AutoSize = true };
        layout.Controls.Add(_useDueDateCheck);
        var dummy1 = new Label();
        layout.Controls.Add(dummy1);

        // Status / Priority
        AddSection(layout, "Status & Priority");
        _statusCombo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill, BackColor = Card, ForeColor = TextClr };
        _statusCombo.Items.AddRange(["(Any)", "Active", "On Hold", "Completed", "Archived"]);
        _statusCombo.SelectedIndex = 0;
        AddRow(layout, "Status:", _statusCombo);

        _priorityCombo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill, BackColor = Card, ForeColor = TextClr };
        _priorityCombo.Items.AddRange(["(Any)", "Low", "Normal", "High"]);
        _priorityCombo.SelectedIndex = 0;
        AddRow(layout, "Priority:", _priorityCombo);

        // Text filters
        AddSection(layout, "Text Filters");
        _clientBox = new TextBox { Dock = DockStyle.Fill, BackColor = Card, ForeColor = TextClr, BorderStyle = BorderStyle.FixedSingle, PlaceholderText = "Partial match..." };
        AddRow(layout, "Client:", _clientBox);

        _notesSearchBox = new TextBox { Dock = DockStyle.Fill, BackColor = Card, ForeColor = TextClr, BorderStyle = BorderStyle.FixedSingle, PlaceholderText = "Keywords in notes..." };
        AddRow(layout, "Notes contain:", _notesSearchBox);

        _tagsBox = new TextBox { Dock = DockStyle.Fill, BackColor = Card, ForeColor = TextClr, BorderStyle = BorderStyle.FixedSingle, PlaceholderText = "comma-separated: bug,frontend" };
        AddRow(layout, "Tags:", _tagsBox);

        _tagAndModeCheck = new CheckBox { Text = "Match ALL tags (AND mode)", ForeColor = Muted, AutoSize = true };
        layout.Controls.Add(_tagAndModeCheck);
        layout.Controls.Add(new Label());

        // Boolean flags
        AddSection(layout, "Flags");
        _overdueOnlyCheck  = new CheckBox { Text = "Overdue only",   ForeColor = TextClr, AutoSize = true };
        _starredOnlyCheck  = new CheckBox { Text = "Starred only",   ForeColor = TextClr, AutoSize = true };
        _templateOnlyCheck = new CheckBox { Text = "Templates only", ForeColor = TextClr, AutoSize = true };
        _hasUrlCheck       = new CheckBox { Text = "Has URL",        ForeColor = TextClr, AutoSize = true };
        _hasBudgetCheck    = new CheckBox { Text = "Has Budget",     ForeColor = TextClr, AutoSize = true };

        foreach (var chk in new[] { _overdueOnlyCheck, _starredOnlyCheck, _templateOnlyCheck, _hasUrlCheck, _hasBudgetCheck })
        {
            layout.Controls.Add(chk);
            layout.Controls.Add(new Label());
        }

        scroll.Controls.Add(layout);

        // ── Footer ─────────────────────────────────────────────────────────────
        var footer = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 52,
            BackColor = Card,
            Padding = new Padding(12, 8, 12, 8)
        };

        var applyBtn = new Button
        {
            Text = "✓ Apply Filter",
            Width = 130,
            Height = 34,
            Dock = DockStyle.Right,
            BackColor = Accent,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 10, FontStyle.Bold),
            Cursor = Cursors.Hand,
            Margin = new Padding(8, 0, 0, 0)
        };
        applyBtn.FlatAppearance.BorderSize = 0;
        applyBtn.Click += Apply_Click;

        var clearBtn = new Button
        {
            Text = "✕ Clear",
            Width = 80,
            Height = 34,
            Dock = DockStyle.Right,
            BackColor = Color.FromArgb(48, 54, 61),
            ForeColor = TextClr,
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand
        };
        clearBtn.FlatAppearance.BorderSize = 0;
        clearBtn.Click += (s, e) => { Result = new FilterCriteria(); PopulateFromResult(); };

        footer.Controls.Add(applyBtn);
        footer.Controls.Add(clearBtn);

        Controls.Add(footer);
        Controls.Add(scroll);
    }

    private static Label AddRow(TableLayoutPanel layout, string label, Control control)
    {
        var lbl = new Label { Text = label, ForeColor = Color.FromArgb(139, 148, 158), AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 8, 8, 8) };
        layout.Controls.Add(lbl);
        layout.Controls.Add(control);
        return lbl;
    }

    private static void AddSection(TableLayoutPanel layout, string title)
    {
        var sep = new Label
        {
            Text = title,
            Font = new Font("Segoe UI", 9, FontStyle.Bold),
            ForeColor = Color.FromArgb(88, 166, 255),
            AutoSize = true,
            Margin = new Padding(0, 14, 0, 4)
        };
        layout.Controls.Add(sep);
        layout.SetColumnSpan(sep, 2);
    }

    private void PopulateFromResult()
    {
        _fromCheck.Checked = Result.FromDate.HasValue;
        if (Result.FromDate.HasValue) _fromDatePicker.Value = Result.FromDate.Value;
        _toCheck.Checked = Result.ToDate.HasValue;
        if (Result.ToDate.HasValue) _toDatePicker.Value = Result.ToDate.Value;
        _useDueDateCheck.Checked = Result.UseDueDate;
        _tagsBox.Text = string.Join(", ", Result.Tags);
        _clientBox.Text = Result.ClientFilter;
        _notesSearchBox.Text = Result.NotesContains;
        _tagAndModeCheck.Checked = Result.TagAndMode;
        _overdueOnlyCheck.Checked = Result.OverdueOnly;
        _starredOnlyCheck.Checked = Result.StarredOnly;
        _templateOnlyCheck.Checked = Result.TemplateOnly;
        _hasUrlCheck.Checked = Result.HasUrl;
        _hasBudgetCheck.Checked = Result.HasBudget;

        _statusCombo.SelectedItem = Result.Status ?? "(Any)";
        if (_statusCombo.SelectedIndex < 0) _statusCombo.SelectedIndex = 0;
        _priorityCombo.SelectedItem = Result.Priority ?? "(Any)";
        if (_priorityCombo.SelectedIndex < 0) _priorityCombo.SelectedIndex = 0;
    }

    private void Apply_Click(object? sender, EventArgs e)
    {
        Result = new FilterCriteria
        {
            FromDate = _fromCheck.Checked ? _fromDatePicker.Value : null,
            ToDate = _toCheck.Checked ? _toDatePicker.Value : null,
            UseDueDate = _useDueDateCheck.Checked,
            Tags = _tagsBox.Text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList(),
            ClientFilter = _clientBox.Text.Trim(),
            NotesContains = _notesSearchBox.Text.Trim(),
            TagAndMode = _tagAndModeCheck.Checked,
            Status = _statusCombo.SelectedItem?.ToString() == "(Any)" ? null : _statusCombo.SelectedItem?.ToString(),
            Priority = _priorityCombo.SelectedItem?.ToString() == "(Any)" ? null : _priorityCombo.SelectedItem?.ToString(),
            OverdueOnly = _overdueOnlyCheck.Checked,
            StarredOnly = _starredOnlyCheck.Checked,
            TemplateOnly = _templateOnlyCheck.Checked,
            HasUrl = _hasUrlCheck.Checked,
            HasBudget = _hasBudgetCheck.Checked,
            Preset = _presetCombo.SelectedItem?.ToString() == "(None)" ? null : _presetCombo.SelectedItem?.ToString()
        };

        DialogResult = DialogResult.OK;
        Close();
    }
}

/// <summary>
/// Holds all composable filter criteria for the advanced filter dialog.
/// </summary>
public class FilterCriteria
{
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public bool UseDueDate { get; set; }
    public List<string> Tags { get; set; } = new();
    public bool TagAndMode { get; set; }
    public string? Status { get; set; }
    public string? Priority { get; set; }
    public string ClientFilter { get; set; } = "";
    public string NotesContains { get; set; } = "";
    public bool OverdueOnly { get; set; }
    public bool StarredOnly { get; set; }
    public bool TemplateOnly { get; set; }
    public bool HasUrl { get; set; }
    public bool HasBudget { get; set; }
    public string? Preset { get; set; }

    public bool IsEmpty =>
        !FromDate.HasValue && !ToDate.HasValue && !Tags.Any() &&
        string.IsNullOrEmpty(ClientFilter) && string.IsNullOrEmpty(NotesContains) &&
        string.IsNullOrEmpty(Status) && string.IsNullOrEmpty(Priority) &&
        !OverdueOnly && !StarredOnly && !TemplateOnly && !HasUrl && !HasBudget &&
        string.IsNullOrEmpty(Preset);

    /// <summary>
    /// Applies all criteria to a project list.
    /// </summary>
    public IEnumerable<ProjectRecord> Apply(IEnumerable<ProjectRecord> projects)
    {
        var q = projects.Where(p => !p.IsDeleted);

        if (!string.IsNullOrEmpty(Status))
            q = q.Where(p => p.Status.Equals(Status, StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrEmpty(Priority))
            q = q.Where(p => p.Priority.Equals(Priority, StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrEmpty(ClientFilter))
            q = q.Where(p => p.Client.ContainsI(ClientFilter));

        if (!string.IsNullOrEmpty(NotesContains))
            q = q.Where(p => p.Notes.ContainsI(NotesContains));

        if (Tags.Any())
        {
            var lowerTags = Tags.Select(t => t.ToLowerInvariant()).ToList();
            q = TagAndMode
                ? q.Where(p => lowerTags.All(t => p.Tags.Contains(t)))
                : q.Where(p => p.Tags.Any(t => lowerTags.Contains(t)));
        }

        if (FromDate.HasValue)
        {
            q = UseDueDate
                ? q.Where(p => p.DueDate.HasValue && p.DueDate.Value >= FromDate.Value)
                : q.Where(p => p.CreatedAt >= FromDate.Value);
        }

        if (ToDate.HasValue)
        {
            q = UseDueDate
                ? q.Where(p => p.DueDate.HasValue && p.DueDate.Value <= ToDate.Value)
                : q.Where(p => p.CreatedAt <= ToDate.Value);
        }

        if (OverdueOnly)  q = q.Where(p => p.IsOverdue);
        if (StarredOnly)  q = q.Where(p => p.IsStarred);
        if (TemplateOnly) q = q.Where(p => p.IsTemplate);
        if (HasUrl)       q = q.Where(p => !string.IsNullOrWhiteSpace(p.ExternalUrl));
        if (HasBudget)    q = q.Where(p => p.Budget.HasValue && p.Budget.Value > 0);

        // Preset overrides everything else
        if (!string.IsNullOrEmpty(Preset))
        {
            q = Preset.ToLowerInvariant() switch
            {
                "overdue"              => projects.Where(p => !p.IsDeleted && p.IsOverdue),
                "due today"            => projects.Where(p => !p.IsDeleted && p.IsDueToday),
                "starred"              => projects.Where(p => !p.IsDeleted && p.IsStarred),
                "templates"            => projects.Where(p => !p.IsDeleted && p.IsTemplate),
                "active + high priority" => projects.Where(p => !p.IsDeleted && p.Status == "Active" && p.Priority == "High"),
                "no folder"            => projects.Where(p => !p.IsDeleted && string.IsNullOrWhiteSpace(p.FolderPath)),
                _                      => q
            };
        }

        return q.OrderByDescending(p => p.UpdatedAt);
    }
}
