using Gradual.Infrastructure;
using Gradual.Models;
using Gradual.Services;

namespace Gradual.Forms;

/// <summary>
/// Floating mini-form for time tracking (Features 41–50).
/// Shows the active timer, allows manual entry, and lists today's time.
/// </summary>
public class TTF : Form
{
    private readonly TimeTrackingService _timeService;
    private readonly IReadOnlyList<ProjectRecord> _projects;

    // UI Controls
    private ComboBox _projectCombo = null!;
    private Button _startStopButton = null!;
    private Label _timerLabel = null!;
    private CheckBox _billableCheck = null!;
    private TextBox _descriptionBox = null!;
    private NumericUpDown _manualHours = null!;
    private Button _manualSaveButton = null!;
    private ListView _todayList = null!;
    private Label _totalLabel = null!;
    private System.Windows.Forms.Timer _clockTimer = null!;

    // State
    private TimeEntry? _activeEntry;
    private bool _isRunning = false;

    // Colors
    private static readonly Color BgColor    = Color.FromArgb(13, 17, 23);
    private static readonly Color CardColor  = Color.FromArgb(22, 27, 34);
    private static readonly Color AccentBlue = Color.FromArgb(88, 166, 255);
    private static readonly Color AccentGreen= Color.FromArgb(63, 185, 80);
    private static readonly Color AccentRed  = Color.FromArgb(248, 81, 73);
    private static readonly Color TextColor  = Color.FromArgb(201, 209, 217);
    private static readonly Color MutedText  = Color.FromArgb(139, 148, 158);

    public TTF(TimeTrackingService timeService, IReadOnlyList<ProjectRecord> projects)
    {
        _timeService = timeService;
        _projects = projects;
        _activeEntry = timeService.GetActiveTimer();
        _isRunning = _activeEntry != null;
        BuildUI();
        _ = RefreshTodayListAsync();
    }

    private void BuildUI()
    {
        Text = "⏱ Time Tracker";
        Size = new Size(420, 620);
        MinimumSize = new Size(380, 550);
        BackColor = BgColor;
        ForeColor = TextColor;
        Font = new Font("Segoe UI", 9.5f);
        StartPosition = FormStartPosition.Manual;
        // Stick to top-right corner
        var workArea = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1920, 1080);
        Location = new Point(workArea.Right - Width - 20, workArea.Top + 40);
        TopMost = true;
        FormBorderStyle = FormBorderStyle.SizableToolWindow;

        var mainLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 5,
            ColumnCount = 1,
            Padding = new Padding(14),
            BackColor = BgColor
        };
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));  // title
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 110)); // timer card
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 120)); // manual entry
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));  // today list
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));  // total

        // ── Title ─────────────────────────────────────────────────────────────
        var titleLabel = new Label
        {
            Text = "⏱ Time Tracker",
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 13, FontStyle.Bold),
            ForeColor = AccentBlue,
            TextAlign = ContentAlignment.MiddleLeft
        };
        mainLayout.Controls.Add(titleLabel, 0, 0);

        // ── Timer Card ────────────────────────────────────────────────────────
        var timerCard = new Panel { Dock = DockStyle.Fill, BackColor = CardColor, Padding = new Padding(10) };
        timerCard.Paint += (s, e) => PaintRoundedBorder(e.Graphics, timerCard, 8, Color.FromArgb(48, 54, 61));

        _projectCombo = new ComboBox
        {
            Dock = DockStyle.Top,
            DropDownStyle = ComboBoxStyle.DropDownList,
            BackColor = Color.FromArgb(30, 37, 44),
            ForeColor = TextColor,
            Height = 28,
            FlatStyle = FlatStyle.Flat,
            Margin = new Padding(0, 0, 0, 6)
        };
        foreach (var p in _projects.Where(p => !p.IsDeleted && p.Status != "Archived"))
            _projectCombo.Items.Add(p.ProjectName);
        if (_activeEntry != null && !string.IsNullOrEmpty(_activeEntry.ProjectName))
            _projectCombo.SelectedItem = _activeEntry.ProjectName;
        else if (_projectCombo.Items.Count > 0)
            _projectCombo.SelectedIndex = 0;

        _timerLabel = new Label
        {
            Text = "00:00:00",
            Font = new Font("Segoe UI", 26, FontStyle.Bold),
            ForeColor = _isRunning ? AccentGreen : MutedText,
            Dock = DockStyle.Top,
            TextAlign = ContentAlignment.MiddleCenter,
            Height = 44,
            Margin = new Padding(0, 4, 0, 4)
        };

        var timerRow = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            AutoSize = true,
            BackColor = Color.Transparent
        };

        _startStopButton = new Button
        {
            Text = _isRunning ? "⏹ Stop" : "▶ Start",
            Width = 100,
            Height = 32,
            BackColor = _isRunning ? AccentRed : AccentGreen,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 10, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        _startStopButton.FlatAppearance.BorderSize = 0;
        _startStopButton.Click += StartStop_Click;

        _billableCheck = new CheckBox
        {
            Text = "Billable",
            ForeColor = MutedText,
            Checked = true,
            AutoSize = true,
            Margin = new Padding(12, 6, 0, 0)
        };

        timerRow.Controls.Add(_startStopButton);
        timerRow.Controls.Add(_billableCheck);

        timerCard.Controls.Add(timerRow);
        timerCard.Controls.Add(_timerLabel);
        timerCard.Controls.Add(_projectCombo);
        mainLayout.Controls.Add(timerCard, 0, 1);

        // ── Manual Entry ──────────────────────────────────────────────────────
        var manualCard = new Panel { Dock = DockStyle.Fill, BackColor = CardColor, Padding = new Padding(10) };
        manualCard.Paint += (s, e) => PaintRoundedBorder(e.Graphics, manualCard, 8, Color.FromArgb(48, 54, 61));

        var manualLabel = new Label
        {
            Text = "Manual Entry",
            Dock = DockStyle.Top,
            Font = new Font("Segoe UI", 9, FontStyle.Bold),
            ForeColor = MutedText,
            Height = 20
        };

        var manualRow = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            ColumnCount = 3,
            RowCount = 1,
            AutoSize = true,
            BackColor = Color.Transparent,
            Height = 34
        };
        manualRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 70));
        manualRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        manualRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 80));

        _manualHours = new NumericUpDown
        {
            Dock = DockStyle.Fill,
            Minimum = 0.1m,
            Maximum = 24m,
            DecimalPlaces = 2,
            Increment = 0.25m,
            Value = 1m,
            BackColor = Color.FromArgb(30, 37, 44),
            ForeColor = TextColor
        };

        _descriptionBox = new TextBox
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(30, 37, 44),
            ForeColor = TextColor,
            BorderStyle = BorderStyle.FixedSingle,
            PlaceholderText = "Description...",
            Margin = new Padding(4, 0, 4, 0)
        };

        _manualSaveButton = new Button
        {
            Dock = DockStyle.Fill,
            Text = "Log",
            BackColor = AccentBlue,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        _manualSaveButton.FlatAppearance.BorderSize = 0;
        _manualSaveButton.Click += ManualSave_Click;

        manualRow.Controls.Add(_manualHours, 0, 0);
        manualRow.Controls.Add(_descriptionBox, 1, 0);
        manualRow.Controls.Add(_manualSaveButton, 2, 0);

        manualCard.Controls.Add(manualRow);
        manualCard.Controls.Add(manualLabel);
        mainLayout.Controls.Add(manualCard, 0, 2);

        // ── Today's Log ───────────────────────────────────────────────────────
        var listLabel = new Label { Text = "📋 Today's Log", Dock = DockStyle.Top, Height = 22, ForeColor = MutedText };

        _todayList = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true,
            BackColor = CardColor,
            ForeColor = TextColor,
            GridLines = false,
            BorderStyle = BorderStyle.None
        };
        _todayList.Columns.Add("Project", 130);
        _todayList.Columns.Add("Hours", 55);
        _todayList.Columns.Add("Desc", 130);
        _todayList.Columns.Add("💰", 30);

        var listContainer = new Panel { Dock = DockStyle.Fill, BackColor = BgColor };
        listContainer.Controls.Add(_todayList);
        listContainer.Controls.Add(listLabel);

        mainLayout.Controls.Add(listContainer, 0, 3);

        // ── Total ─────────────────────────────────────────────────────────────
        _totalLabel = new Label
        {
            Text = "Today total: 0.00 h",
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            ForeColor = AccentGreen,
            TextAlign = ContentAlignment.MiddleRight
        };
        mainLayout.Controls.Add(_totalLabel, 0, 4);

        Controls.Add(mainLayout);

        // ── Clock timer ───────────────────────────────────────────────────────
        _clockTimer = new System.Windows.Forms.Timer { Interval = 1000 };
        _clockTimer.Tick += (s, e) => UpdateTimerDisplay();
        if (_isRunning) _clockTimer.Start();
    }

    private async void StartStop_Click(object? sender, EventArgs e)
    {
        if (_isRunning)
        {
            // Stop
            var entry = await _timeService.StopTimer();
            _isRunning = false;
            _activeEntry = null;
            _clockTimer.Stop();
            _startStopButton.Text = "▶ Start";
            _startStopButton.BackColor = Color.FromArgb(63, 185, 80);
            _timerLabel.ForeColor = MutedText;
            _timerLabel.Text = "00:00:00";
            await RefreshTodayListAsync();
        }
        else
        {
            // Start
            var projectName = _projectCombo.SelectedItem?.ToString() ?? "Unknown";
            var project = _projects.FirstOrDefault(p => p.ProjectName == projectName);
            if (project == null) { MessageBox.Show("Select a project first.", "Time Tracker"); return; }

            _activeEntry = _timeService.StartTimer(project.Id, project.ProjectName);
            _activeEntry.IsBillable = _billableCheck.Checked;
            _isRunning = true;
            _clockTimer.Start();
            _startStopButton.Text = "⏹ Stop";
            _startStopButton.BackColor = Color.FromArgb(248, 81, 73);
            _timerLabel.ForeColor = Color.FromArgb(63, 185, 80);
        }
    }

    private async void ManualSave_Click(object? sender, EventArgs e)
    {
        var projectName = _projectCombo.SelectedItem?.ToString() ?? "";
        var project = _projects.FirstOrDefault(p => p.ProjectName == projectName);
        if (project == null) { MessageBox.Show("Select a project."); return; }

        var entry = new TimeEntry
        {
            ProjectId = project.Id,
            ProjectName = project.ProjectName,
            Date = DateTime.Today,
            Hours = _manualHours.Value,
            IsBillable = _billableCheck.Checked,
            Description = _descriptionBox.Text.Trim()
        };

        await _timeService.SaveAsync(entry);
        _descriptionBox.Clear();
        await RefreshTodayListAsync();
    }

    private async Task RefreshTodayListAsync()
    {
        var hoursByProject = await _timeService.GetTodayHoursByProjectAsync();
        var allToday = await _timeService.LoadAllAsync();
        var todayEntries = allToday.Where(e => e.Date == DateTime.Today)
            .OrderByDescending(e => e.CreatedAt).ToList();

        _todayList.Items.Clear();
        foreach (var entry in todayEntries)
        {
            var item = new ListViewItem(entry.ProjectName.Truncate(18));
            item.SubItems.Add(entry.Hours.ToString("F2"));
            item.SubItems.Add(entry.Description.Truncate(20));
            item.SubItems.Add(entry.IsBillable ? "✓" : "");
            item.Tag = entry;
            _todayList.Items.Add(item);
        }

        var total = todayEntries.Sum(e => e.Hours);
        if (_activeEntry != null)
            total += (decimal)_activeEntry.LiveDuration.TotalHours;
        _totalLabel.Text = $"Today total: {total:F2} h";
    }

    private void UpdateTimerDisplay()
    {
        if (_activeEntry?.TimerStartedAt == null) return;
        var elapsed = DateTime.Now - _activeEntry.TimerStartedAt.Value;
        _timerLabel.Text = $"{(int)elapsed.TotalHours:D2}:{elapsed.Minutes:D2}:{elapsed.Seconds:D2}";
    }

    private static void PaintRoundedBorder(Graphics g, Control ctrl, int radius, Color borderColor)
    {
        using var pen = new Pen(borderColor, 1);
        var rect = new Rectangle(0, 0, ctrl.Width - 1, ctrl.Height - 1);
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        using var path = new System.Drawing.Drawing2D.GraphicsPath();
        path.AddRoundedRectangle(rect, radius);
        g.DrawPath(pen, path);
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        _clockTimer.Stop();
        base.OnFormClosing(e);
    }
}

// Extension for GraphicsPath
file static class GraphicsPathExt
{
    public static void AddRoundedRectangle(this System.Drawing.Drawing2D.GraphicsPath path, Rectangle bounds, int radius)
    {
        int d = radius * 2;
        path.AddArc(bounds.X, bounds.Y, d, d, 180, 90);
        path.AddArc(bounds.Right - d, bounds.Y, d, d, 270, 90);
        path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
        path.AddArc(bounds.X, bounds.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
    }
}
