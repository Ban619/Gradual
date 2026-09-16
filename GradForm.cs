using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using Gradual.Forms;
using Gradual.Infrastructure;
using Gradual.Interfaces;
using Gradual.Models;
using Gradual.Models.DTOs;
using Gradual.Services;
using Gradual.Services.GitHub;

namespace Gradual;

public partial class GradForm : Form
{
    private readonly IProjectService _service;
    private readonly AnalyticsService _analyticsService;
    private readonly ActivityLogger _activityLogger;
    private readonly TimeTrackingService _timeService;
    private readonly NotificationService _notificationService;
    private readonly List<ProjectRecord> _projects = new();
    private readonly List<ProjectRecord> _filteredProjects = new();
    private bool _darkMode = true;
    private string _currentFilter = "All";
    private string _searchText = string.Empty;
    private Guid? _selectedProjectId;

    // Search debounce — prevents a service call on every keystroke
    private readonly System.Windows.Forms.Timer _searchDebounceTimer;
    private const int SearchDebounceMs = 300;

    // Prevents double-close during async OnFormClosing
    private bool _isClosing = false;

    // Feature 15 — Undo last delete stack
    private readonly Stack<ProjectRecord> _undoDeleteStack = new();

    // Feature 26 — Active advanced filter
    private FilterCriteria? _activeFilter;

    // Feature 62 — compact layout
    private bool _compactMode = false;

    // Feature 70 — zoom
    private float _zoomLevel = 1.0f;

    // Status bar label reference (populated by Designer or BuildStatusBar)
    private Label? _statusBarLabel;
    private Label? _notifBadge;

    // Modeless form references
    private TTF? _TTF;
    private NCP? _notifCenterForm;
    private Analytics_Dashboard? _Analytics_Dashboard;
    private MRecorder? _MRecorder;

    // Secret developer overlay — toggled by F7
    private Forms.DevWidget? _devWidget;

    public GradForm()
    {
        InitializeComponent();
        
        // Get services from DI container
        _service              = ServiceContainer.GetRequiredService<IProjectService>();
        _analyticsService     = ServiceContainer.GetRequiredService<AnalyticsService>();
        _activityLogger       = ServiceContainer.GetRequiredService<ActivityLogger>();
        _timeService          = ServiceContainer.GetRequiredService<TimeTrackingService>();
        _notificationService  = ServiceContainer.GetRequiredService<NotificationService>();

        // Set up search debounce timer (fires once 300ms after last keystroke)
        _searchDebounceTimer = new System.Windows.Forms.Timer { Interval = SearchDebounceMs };
        _searchDebounceTimer.Tick += async (s, e) =>
        {
            _searchDebounceTimer.Stop();
            await ExecuteSearchAsync(_searchText);
        };
        
        UpdateTheme();
        BuildStatusBar();
        _ = LoadProjectsAsync();
        _ = RunStartupTasksAsync();

        // Wire Duty tab buttons
        dutyBtnProjects.Click += (_, _) => SwitchDutyTab(github: false);
        dutyBtnGitHub.Click   += (_, _) => { SwitchDutyTab(github: true); _ = LoadGitHubDutyAsync(); };

        // Track form opened
        _analyticsService.TrackEvent("ApplicationStarted");

        // F7 secret dev overlay — intercept before child controls
        KeyPreview = true;

        // Feature 70 — Ctrl+Wheel zoom
        this.MouseWheel += MainForm_MouseWheel;

        // Notification badge live update
        _notificationService.NotificationPosted += async (s, n) =>
        {
            if (IsHandleCreated)
                BeginInvoke(async () => await UpdateNotifBadgeAsync());
        };
    }

    // ── Feature 67 — Status bar ───────────────────────────────────────────────
    private void BuildStatusBar()
    {
        var statusBar = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 26,
            BackColor = Color.FromArgb(8, 16, 30),
            Padding = new Padding(10, 4, 10, 4)
        };

        _statusBarLabel = new Label
        {
            Text = "Ready",
            Dock = DockStyle.Left,
            AutoSize = true,
            Font = new Font("Segoe UI", 8.5f),
            ForeColor = Color.FromArgb(139, 148, 158)
        };

        _notifBadge = new Label
        {
            Text = "🔔 0",
            Dock = DockStyle.Right,
            AutoSize = true,
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
            ForeColor = Color.FromArgb(210, 153, 34),
            Cursor = Cursors.Hand,
            Padding = new Padding(0, 0, 4, 0),
            Visible = false
        };
        _notifBadge.Click += (s, e) => OpenNotificationCenter();

        statusBar.Controls.Add(_statusBarLabel);
        statusBar.Controls.Add(_notifBadge);
        Controls.Add(statusBar);
    }

    private async Task UpdateNotifBadgeAsync()
    {
        var count = await _notificationService.GetUnreadCountAsync();
        if (_notifBadge == null) return;
        _notifBadge.Text = $"🔔 {count}";
        _notifBadge.Visible = count > 0;
        _notifBadge.ForeColor = count > 0 ? Color.FromArgb(248, 81, 73) : Color.FromArgb(139, 148, 158);
    }

    // ── Startup tasks ─────────────────────────────────────────────────────────
    private async Task RunStartupTasksAsync()
    {
        // Feature 59 — daily digest / due date scan
        await Task.Delay(1500); // wait for form to be fully shown
        if (!IsHandleCreated) return;

        var summary = await _notificationService.ScanDueDatesAsync(_projects, warningDays: 2);
        BeginInvoke(() =>
        {
            if (_statusBarLabel != null)
                _statusBarLabel.Text = summary;
        });
        await UpdateNotifBadgeAsync();

        // Feature 10 — process recurring projects
        await _service.ProcessRecurringProjectsAsync();

        // Feature 100 — What's New dialog (shown once per version upgrade)
        BeginInvoke(() =>
        {
            WhatsNewDialog.ShowIfNew();
        });
    }


    // ── Feature 70 — Ctrl+Wheel zoom ─────────────────────────────────────────
    private void MainForm_MouseWheel(object? sender, MouseEventArgs e)
    {
        if (ModifierKeys != Keys.Control) return;
        _zoomLevel = e.Delta > 0
            ? Math.Min(2.0f, _zoomLevel + 0.1f)
            : Math.Max(0.7f, _zoomLevel - 0.1f);
        var newSize = (int)(9.5f * _zoomLevel);
        if (formTable != null) formTable.Font = new Font("Segoe UI", newSize);
        if (_statusBarLabel != null) _statusBarLabel.Text = $"Zoom: {(int)(_zoomLevel * 100)}%";
    }

    // ── Feature 55 — Sound alerts helper ─────────────────────────────────────
    private static void PlayAlertSound() => System.Media.SystemSounds.Asterisk.Play();

    // ── Open modeless helpers ────────────────────────────────────────────────
    private void OpenTimeTracker()
    {
        if (_TTF == null || _TTF.IsDisposed)
            _TTF = new TTF(_timeService, _projects.AsReadOnly());
        _TTF.Show();
        _TTF.BringToFront();
    }

    private void OpenNotificationCenter()
    {
        if (_notifCenterForm == null || _notifCenterForm.IsDisposed)
            _notifCenterForm = new NCP(_notificationService);
        _notifCenterForm.Show();
        _notifCenterForm.BringToFront();
        _ = _notifCenterForm.RefreshAsync();
    }

    private void OpenAnalyticsDashboard()
    {
        if (_Analytics_Dashboard == null || _Analytics_Dashboard.IsDisposed)
            _Analytics_Dashboard = new Analytics_Dashboard(_service);
        _Analytics_Dashboard.Show();
        _Analytics_Dashboard.BringToFront();
    }

    // Toggle the dev widget on F7 / handle all keyboard shortcuts (Feature 63)
    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        switch (keyData)
        {
            // F7 — dev widget
            case Keys.F7:
            {
                if (_devWidget == null)
                {
                    var config  = ServiceContainer.GetRequiredService<Services.ConfigurationService>();
                    var dataDir = System.IO.Path.Combine(AppContext.BaseDirectory, config.Settings.ApplicationSettings.DataDirectory);
                    System.IO.Directory.CreateDirectory(dataDir);
                    _devWidget = new Forms.DevWidget(dataDir);
                }
                if (_devWidget.Visible) _devWidget.Hide();
                else _devWidget.Show(this);
                return true;
            }

            // F1 — keyboard shortcut overlay (Feature 63)
            case Keys.F1:
                using (var overlay = new KeyboardShortcutOverlay())
                    overlay.ShowDialog(this);
                return true;

            // F5 — refresh
            case Keys.F5:
                _ = LoadProjectsAsync();
                return true;

            // Ctrl+N — new project
            case Keys.Control | Keys.N:
                clearFormButton_Click(this, EventArgs.Empty);
                projectNameTextBox?.Focus();
                return true;

            // Ctrl+S — save
            case Keys.Control | Keys.S:
                saveProjectButton_Click(this, EventArgs.Empty);
                return true;

            // Ctrl+F — focus search (Feature 30)
            case Keys.Control | Keys.F:
                searchTextBox?.Focus();
                searchTextBox?.SelectAll();
                return true;

            // Ctrl+Shift+F — advanced filter (Feature 26)
            case Keys.Control | Keys.Shift | Keys.F:
                OpenAdvancedFilter();
                return true;

            // Ctrl+Z — undo last delete (Feature 15)
            case Keys.Control | Keys.Z:
                _ = UndoLastDeleteAsync();
                return true;

            // Ctrl+D — duplicate/clone (Feature 12)
            case Keys.Control | Keys.D:
                _ = CloneSelectedProjectAsync();
                return true;

            // Ctrl+M — mark complete (Feature 3)
            case Keys.Control | Keys.M:
                _ = MarkSelectedCompleteAsync();
                return true;

            // Ctrl+T — time tracker (Feature 41)
            case Keys.Control | Keys.T:
                OpenTimeTracker();
                return true;

            // Ctrl+Shift+N — notification center (Feature 53)
            case Keys.Control | Keys.Shift | Keys.N:
                OpenNotificationCenter();
                return true;

            // Ctrl+Shift+H — system health (Feature 94)
            case Keys.Control | Keys.Shift | Keys.H:
                using (var hf = new SystemHealthForm())
                    hf.ShowDialog(this);
                return true;

            // Ctrl+Shift+R — recycle bin (Feature 17)
            case Keys.Control | Keys.Shift | Keys.R:
                using (var rb = new RecycleBinForm(_service))
                    rb.ShowDialog(this);
                return true;

            // Ctrl+Shift+S — settings (Feature 96)
            case Keys.Control | Keys.Shift | Keys.S:
            {
                var cfg = ServiceContainer.GetRequiredService<Services.ConfigurationService>();
                using var sf = new SettingsForm(cfg);
                sf.ShowDialog(this);
                return true;
            }

            // Ctrl+Shift+B — backup (Feature 91)
            case Keys.Control | Keys.Shift | Keys.B:
                _ = CreateBackupAsync();
                return true;

            // Ctrl+E — export to Excel (Feature 33)
            case Keys.Control | Keys.E:
                exportButton_Click(this, EventArgs.Empty);
                return true;

            // Ctrl+Shift+E — export to CSV (Feature 31)
            case Keys.Control | Keys.Shift | Keys.E:
                _ = ExportToCsvAsync();
                return true;

            // Ctrl+P — print (Feature 38)
            case Keys.Control | Keys.P:
                PrintProjectList();
                return true;

            // Ctrl+A — analytics dashboard (Feature 71-80)
            case Keys.Control | Keys.A:
                OpenAnalyticsDashboard();
                return true;

            // Ctrl+Shift+W — What's New dialog (Feature 100)
            case Keys.Control | Keys.Shift | Keys.W:
                using (var wn = new WhatsNewDialog())
                    wn.ShowDialog(this);
                return true;

            // Ctrl+Shift+A — Audit log (Feature 95)
            case Keys.Control | Keys.Shift | Keys.A:
            {
                var cfg = ServiceContainer.GetRequiredService<Services.ConfigurationService>();
                var dataDir = System.IO.Path.Combine(AppContext.BaseDirectory,
                    cfg.Settings.ApplicationSettings.DataDirectory);
                System.IO.Directory.CreateDirectory(dataDir);
                using var al = new AuditLog_Viewer(dataDir);
                al.ShowDialog(this);
                return true;
            }

            // Ctrl+Shift+M — macro recorder (Feature 99)
            case Keys.Control | Keys.Shift | Keys.M:
            {
                if (_MRecorder == null || _MRecorder.IsDisposed)
                {
                    var cfg2 = ServiceContainer.GetRequiredService<Services.ConfigurationService>();
                    var dd2  = System.IO.Path.Combine(AppContext.BaseDirectory,
                        cfg2.Settings.ApplicationSettings.DataDirectory);
                    System.IO.Directory.CreateDirectory(dd2);
                    _MRecorder = new MRecorder(dd2);
                }
                _MRecorder.Show();
                _MRecorder.BringToFront();
                return true;
            }

            // Ctrl+Shift+C — script runner (Feature 97)
            case Keys.Control | Keys.Shift | Keys.C:
            {
                var cfg3 = ServiceContainer.GetRequiredService<Services.ConfigurationService>();
                var dd3  = System.IO.Path.Combine(AppContext.BaseDirectory,
                    cfg3.Settings.ApplicationSettings.DataDirectory);
                using var sr = new ScriptRunnerForm(dd3, _projects);
                sr.ShowDialog(this);
                return true;
            }

            // ── Navigation ──────────────────────────────────────────────────────────
            case Keys.Control | Keys.D1: navButton_Click(GetNavBtn("Duty"), EventArgs.Empty);   return true;
            case Keys.Control | Keys.D2: navButton_Click(GetNavBtn("Board"), EventArgs.Empty);  return true;
            case Keys.Control | Keys.D3: navButton_Click(GetNavBtn("Repo"), EventArgs.Empty);   return true;
            case Keys.Control | Keys.D4: navButton_Click(GetNavBtn("Setup"), EventArgs.Empty);  return true;
        }
        return base.ProcessCmdKey(ref msg, keyData);
    }

    private Button? GetNavBtn(string label) =>
        navFlow?.Controls.OfType<Button>().FirstOrDefault(b => b.Text.Contains(label, StringComparison.OrdinalIgnoreCase));

    // ── Feature 15 — Undo delete ─────────────────────────────────────────────
    private async Task UndoLastDeleteAsync()
    {
        if (!_undoDeleteStack.TryPop(out var project)) return;
        var result = await _service.RestoreProjectAsync(project.Id);
        if (result.Success)
        {
            await LoadProjectsAsync();
            if (_statusBarLabel != null) _statusBarLabel.Text = $"Restored: {project.ProjectName}";
        }
    }

    // ── Feature 12 — Clone ────────────────────────────────────────────────────
    private async Task CloneSelectedProjectAsync()
    {
        if (!_selectedProjectId.HasValue) return;
        var result = await _service.CloneProjectAsync(_selectedProjectId.Value);
        if (result.Success)
        {
            await LoadProjectsAsync();
            if (_statusBarLabel != null) _statusBarLabel.Text = result.Message ?? "Cloned.";
        }
    }

    // ── Feature 3 — Mark complete ─────────────────────────────────────────────
    private async Task MarkSelectedCompleteAsync()
    {
        if (!_selectedProjectId.HasValue) return;
        var result = await _service.SetCompletionAsync(_selectedProjectId.Value, 100);
        if (result.Success)
        {
            await LoadProjectsAsync();
            if (_statusBarLabel != null) _statusBarLabel.Text = "Marked 100% complete.";
        }
    }

    // ── Feature 26 — Advanced filter ─────────────────────────────────────────
    private void OpenAdvancedFilter()
    {
        using var dlg = new AdvancedFilterDialog(_activeFilter);
        if (dlg.ShowDialog(this) == DialogResult.OK)
        {
            _activeFilter = dlg.Result.IsEmpty ? null : dlg.Result;
            ApplyAdvancedFilter();
        }
    }

    private void ApplyAdvancedFilter()
    {
        _filteredProjects.Clear();
        var source = _activeFilter != null
            ? _activeFilter.Apply(_projects)
            : _projects.AsEnumerable();

        if (_currentFilter != "All")
            source = source.Where(p => p.Status == _currentFilter);

        if (!string.IsNullOrWhiteSpace(_searchText))
            source = source.Where(p =>
                p.ProjectName.ContainsI(_searchText) || p.Client.ContainsI(_searchText) ||
                p.Notes.ContainsI(_searchText) || p.Tags.Any(t => t.ContainsI(_searchText)));

        _filteredProjects.AddRange(source.OrderByDescending(p => p.UpdatedAt));
        PopulateListView();
        UpdateSummary();

        if (_statusBarLabel != null)
            _statusBarLabel.Text = _activeFilter != null && !_activeFilter.IsEmpty
                ? $"🔍 Filter active — {_filteredProjects.Count} results"
                : $"{_filteredProjects.Count} projects";
    }

    // ── Feature 38 — Print ───────────────────────────────────────────────────
    private void PrintProjectList()
    {
        var pd = new System.Drawing.Printing.PrintDocument();
        pd.PrintPage += (s, e) =>
        {
            if (e.Graphics == null) return;
            float y = 40;
            e.Graphics.DrawString("Gradual — Project List", new Font("Segoe UI", 14, FontStyle.Bold),
                Brushes.Black, 40, 20);
            foreach (var p in _filteredProjects.Take(50))
            {
                var line = $"{p.ProjectName,-30} {p.Client,-20} {p.Status,-12} {p.Priority,-10}";
                if (p.DueDate.HasValue) line += $" Due:{p.DueDate:MM/dd/yy}";
                e.Graphics.DrawString(line, new Font("Courier New", 9), Brushes.Black, 40, y);
                y += 18;
                if (y > (e.PageBounds.Height - 60)) { e.HasMorePages = true; return; }
            }
        };
        using var preview = new System.Windows.Forms.PrintPreviewDialog { Document = pd };
        preview.ShowDialog(this);
    }

    // ── Feature 91 — Backup ──────────────────────────────────────────────────
    private async Task CreateBackupAsync()
    {
        var result = await _service.CreateBackupAsync();
        MessageBox.Show(result.Message ?? "Done", "Backup",
            MessageBoxButtons.OK,
            result.Success ? MessageBoxIcon.Information : MessageBoxIcon.Error);
    }

    // ── Feature 31 — CSV export ──────────────────────────────────────────────
    private async Task ExportToCsvAsync()
    {
        using var dlg = new SaveFileDialog
        {
            Filter = "CSV Files|*.csv",
            FileName = $"projects_{DateTime.Today:yyyy-MM-dd}.csv"
        };
        if (dlg.ShowDialog() != DialogResult.OK) return;
        var result = await _service.ExportToCsvAsync(_filteredProjects, dlg.FileName);
        MessageBox.Show(result.Message ?? "Done", "Export CSV",
            MessageBoxButtons.OK,
            result.Success ? MessageBoxIcon.Information : MessageBoxIcon.Error);
    }

    protected override async void OnFormClosing(FormClosingEventArgs e)
    {
        // Cancel the first close, await the async save, then re-close.
        // This ensures analytics are flushed before the window disappears.
        if (!_isClosing)
        {
            e.Cancel = true;
            _isClosing = true;
            _searchDebounceTimer.Dispose();

            try
            {
                await _analyticsService.SaveSessionAsync();
            }
            finally
            {
                base.OnFormClosing(e);
                Application.Exit();
            }
        }
    }

    private async Task LoadProjectsAsync()
    {
        try
        {
            var result = await _service.GetAllProjectsAsync();
            
            if (result.Success && result.Data != null)
            {
                _projects.Clear();
                _projects.AddRange(result.Data);
                RefreshList();
                UpdateSummary();
            }
            else
            {
                MessageBox.Show(
                    result.Message ?? "Failed to load projects",
                    "Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"An unexpected error occurred while loading projects:\n{ex.Message}",
                "Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private void RefreshList()
    {
        // Route through advanced filter if active
        ApplyAdvancedFilter();
    }


    private void PopulateListView()
    {
        projectListView.Items.Clear();

        foreach (var proj in _filteredProjects)
        {
            // ── Star prefix on project name (Feature 9) ──────────────────────
            var nameDisplay = proj.IsStarred ? $"⭐ {proj.ProjectName}" : proj.ProjectName;

            // ── Overdue / due-today suffix (Feature 1/52) ────────────────────
            if (proj.IsOverdue)   nameDisplay += " 🔴";
            else if (proj.IsDueToday) nameDisplay += " 📅";

            // ── Completion bar in name (Feature 3) ───────────────────────────
            if (proj.CompletionPercent > 0 && proj.CompletionPercent < 100)
                nameDisplay += $" [{proj.CompletionPercent}%]";
            else if (proj.CompletionPercent >= 100)
                nameDisplay += " ✓";

            var item = new ListViewItem(nameDisplay);
            item.SubItems.Add(proj.Client);
            item.SubItems.Add(proj.Status + (proj.Tags.Any() ? $" · {string.Join(",", proj.Tags.Take(2))}" : ""));
            item.SubItems.Add(proj.Priority);

            // ── Folder or due date (Feature 1) ───────────────────────────────
            var folderDisplay = string.IsNullOrEmpty(proj.FolderPath)
                ? (proj.DueDate.HasValue ? $"📅 Due: {proj.DueDate:MM/dd/yy}" : "")
                : proj.FolderPath;
            item.SubItems.Add(folderDisplay);
            item.SubItems.Add(proj.UpdatedAt.ToString("MM/dd/yyyy"));
            item.Tag = proj;

            // ── Feature 5 — Row color coding ─────────────────────────────────
            if (!string.IsNullOrWhiteSpace(proj.ColorHex))
            {
                try
                {
                    var color = ColorTranslator.FromHtml(proj.ColorHex);
                    item.BackColor = Color.FromArgb(30, color); // subtle tint
                    item.ForeColor = color;
                }
                catch { /* invalid hex — skip */ }
            }
            else if (proj.IsOverdue)
            {
                item.ForeColor = Color.FromArgb(248, 81, 73);
            }
            else if (proj.IsDueToday)
            {
                item.ForeColor = Color.FromArgb(210, 153, 34);
            }

            projectListView.Items.Add(item);
        }
    }

    private void UpdateSummary()
    {
        var active    = _projects.Count(p => p.Status == "Active");
        var overdue   = _projects.Count(p => p.IsOverdue);
        var starred   = _projects.Count(p => p.IsStarred);
        var total     = _projects.Count;
        var summary   = $"{total} total · {active} active · {overdue} overdue · {starred} starred · showing {_filteredProjects.Count}";
        if (_statusBarLabel != null) _statusBarLabel.Text = summary;
    }

    private async void saveProjectButton_Click(object? sender, EventArgs e)
    {
        try
        {
            // Create DTO from form inputs
            var dto = new ProjectDto
            {
                ProjectName = projectNameTextBox.Text,
                Client      = clientTextBox.Text,
                Status      = statusComboBox.Text,
                Priority    = priorityComboBox.Text,
                FolderPath  = folderTextBox.Text,
                Notes       = notesTextBox.Text,
                AttachmentPaths = attachmentListBox.Items.Cast<string>().ToList(),
                ProjectPhases   = projectPhasesTextBox.Text
                    .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(p => p.Trim()).Where(p => !string.IsNullOrWhiteSpace(p)).ToList(),

                // ── Group A new fields ─────────────────────────────────────────
                // Feature 1 — Due date: read from dueDatePicker if it exists
                DueDate = GetDueDateFromForm(),

                // Feature 4 — Tags: comma-separated text box
                Tags = GetTagsFromForm(),

                // Feature 9 — Star: preserve existing star status on update
                IsStarred = _selectedProjectId.HasValue
                    ? (_projects.FirstOrDefault(p => p.Id == _selectedProjectId)?.IsStarred ?? false)
                    : false,
            };

            ServiceResult<ProjectRecord> result;

            if (_selectedProjectId.HasValue)
            {
                // Update existing project
                result = await _service.UpdateProjectAsync(_selectedProjectId.Value, dto);
            }
            else
            {
                // Create new project
                result = await _service.CreateProjectAsync(dto);
            }

            if (result.Success)
            {
                // Log the activity
                var action = _selectedProjectId.HasValue ? "Updated" : "Created";
                var detail = _selectedProjectId.HasValue
                    ? $"Status → {dto.Status}, Priority → {dto.Priority}"
                    : $"Status: {dto.Status}";
                _ = _activityLogger.LogAsync(new Models.ActivityEntry
                {
                    Action      = action,
                    ProjectName = dto.ProjectName,
                    Client      = dto.Client,
                    Detail      = detail
                });

                MessageBox.Show(
                    result.Message ?? "Project saved successfully",
                    "Success",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);

                await LoadProjectsAsync();
                ClearForm();
                RefreshBoardIfVisible();
            }
            else
            {
                var errorMessage = result.Message;
                if (result.Errors != null && result.Errors.Any())
                {
                    errorMessage += "\n\nValidation Errors:\n" + string.Join("\n", result.Errors);
                }

                MessageBox.Show(
                    errorMessage,
                    "Validation Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"An unexpected error occurred while saving the project:\n{ex.Message}",
                "Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private async void deleteProjectButton_Click(object? sender, EventArgs e)
    {
        if (!_selectedProjectId.HasValue)
        {
            MessageBox.Show(
                "Please select a project to delete.",
                "No Selection",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        var confirm = MessageBox.Show(
            "Send this project to the Recycle Bin? (Ctrl+Z to undo)",
            "Confirm Delete",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning);

        if (confirm != DialogResult.Yes)
            return;

        try
        {
            // Feature 15: capture before deletion so we can undo
            var toDelete = _projects.FirstOrDefault(p => p.Id == _selectedProjectId);

            var result = await _service.DeleteProjectAsync(_selectedProjectId.Value);

            if (result.Success)
            {
                // Push to undo stack (Feature 15)
                if (toDelete != null) _undoDeleteStack.Push(toDelete);

                // Log the deletion
                _ = _activityLogger.LogAsync(new Models.ActivityEntry
                {
                    Action      = "Deleted",
                    ProjectName = toDelete?.ProjectName ?? "Unknown",
                    Client      = toDelete?.Client ?? "",
                    Detail      = "Sent to Recycle Bin (Ctrl+Z to undo)"
                });

                if (_statusBarLabel != null)
                    _statusBarLabel.Text = $"Deleted ‘{toDelete?.ProjectName}’ — press Ctrl+Z to undo";

                await LoadProjectsAsync();
                ClearForm();
                RefreshBoardIfVisible();
            }
            else
            {
                MessageBox.Show(
                    result.Message ?? "Failed to delete project",
                    "Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"An unexpected error occurred while deleting the project:\n{ex.Message}",
                "Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private void ClearForm()
    {
        _selectedProjectId = null;
        projectNameTextBox.Clear();
        clientTextBox.Clear();
        statusComboBox.SelectedIndex = 0;
        priorityComboBox.SelectedIndex = 1;
        projectPhasesTextBox.Clear();
        folderTextBox.Clear();
        notesTextBox.Clear();
        attachmentListBox.Items.Clear();
        UpdateAttachmentBadge();
    }

    private void clearFormButton_Click(object? sender, EventArgs e)
    {
        ClearForm();
    }

    private void projectListView_SelectedIndexChanged(object? sender, EventArgs e)
    {
        if (projectListView.SelectedItems.Count == 0)
            return;

        var selectedItem = projectListView.SelectedItems[0];
        var project = selectedItem.Tag as ProjectRecord;

        if (project == null)
            return;

        _selectedProjectId = project.Id;
        projectNameTextBox.Text = project.ProjectName;
        clientTextBox.Text = project.Client;
        statusComboBox.Text = project.Status;
        priorityComboBox.Text = project.Priority;
        projectPhasesTextBox.Text = string.Join(", ", project.ProjectPhases);
        folderTextBox.Text = project.FolderPath;
        notesTextBox.Text = project.Notes;

        attachmentListBox.Items.Clear();
        foreach (var path in project.AttachmentPaths)
            attachmentListBox.Items.Add(path);
        UpdateAttachmentBadge();

        // Feature 4 — Populate tags text box if it exists
        if (tagsTextBox != null)
            tagsTextBox.Text = string.Join(", ", project.Tags);

        // Feature 1 — Populate due date picker if it exists
        if (dueDatePicker != null && project.DueDate.HasValue)
        {
            dueDateEnableCheck!.Checked = true;
            dueDatePicker.Value = project.DueDate.Value;
        }
        else if (dueDatePicker != null)
        {
            dueDateEnableCheck!.Checked = false;
        }

        // Update status bar with project info
        if (_statusBarLabel != null)
        {
            var statusInfo = $"Selected: {project.ProjectName}";
            if (project.DueDate.HasValue) statusInfo += $" | Due: {project.DueDate:MM/dd/yy}";
            if (project.CompletionPercent > 0) statusInfo += $" | {project.CompletionPercent}% complete";
            if (project.IsStarred) statusInfo += " | ⭐ Starred";
            _statusBarLabel.Text = statusInfo;
        }
    }

    private void filterButton_Click(object? sender, EventArgs e)
    {
        if (sender is not Button button)
            return;

        _currentFilter = button.Text;
        
        _analyticsService.TrackFilter("Status", _currentFilter);

        foreach (Button btn in statusFilterPanel!.Controls.OfType<Button>())
        {
            btn.BackColor = btn == button
                ? Color.FromArgb(32, 54, 87)
                : Color.FromArgb(18, 32, 56);
        }

        RefreshList();
        UpdateSummary();
    }

    private void sidebarStatusFilter_Click(object? sender, EventArgs e)
    {
        if (sender is not Button button || button.Tag is not string status)
            return;

        _currentFilter = status;
        RefreshList();
        UpdateSummary();

        // Update sidebar button styles
        var parent = button.Parent;
        if (parent == null) return;

        foreach (var ctrl in parent.Controls.OfType<Button>())
        {
            if (ctrl.Tag is string)
            {
                ctrl.BackColor = ctrl == button
                    ? Color.FromArgb(52, 76, 112)
                    : Color.FromArgb(42, 66, 102);
            }
        }
    }

    private async void sidebarPriorityFilter_Click(object? sender, EventArgs e)
    {
        if (sender is not Button button || button.Tag is not string priority)
            return;

        try
        {
            var result = await _service.GetProjectsByPriorityAsync(priority);

            if (result.Success && result.Data != null)
            {
                _projects.Clear();
                _projects.AddRange(result.Data);
                _currentFilter = "All";
                RefreshList();
                UpdateSummary();

                // Update sidebar button styles
                var parent = button.Parent;
                if (parent != null)
                {
                    foreach (var ctrl in parent.Controls.OfType<Button>())
                    {
                        if (ctrl.Tag is string)
                        {
                            ctrl.BackColor = ctrl == button
                                ? Color.FromArgb(52, 76, 112)
                                : Color.FromArgb(42, 66, 102);
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Error filtering by priority:\n{ex.Message}",
                "Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private void biDashboardBtn_Click(object? sender, EventArgs e)
    {
        try
        {
            var dashboard = new Gradual.Forms.DashboardForm();
            dashboard.Show(this);   // modeless — stays open alongside the main window
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Could not open Business Intelligence Dashboard:\n{ex.Message}",
                "Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private void searchTextBox_TextChanged(object? sender, EventArgs e)
    {
        _searchText = searchTextBox.Text;

        // Debounce: reset the timer on each keystroke so the service is
        // called only once, 300ms after the user stops typing.
        _searchDebounceTimer.Stop();
        _searchDebounceTimer.Start();
    }

    private async Task ExecuteSearchAsync(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            RefreshList();
            return;
        }

        try
        {
            var result = await _service.SearchProjectsAsync(text);

            if (result.Success && result.Data != null)
            {
                _filteredProjects.Clear();
                _filteredProjects.AddRange(result.Data);
                PopulateListView();
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Error searching projects:\n{ex.Message}",
                "Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private void browseFolderButton_Click(object? sender, EventArgs e)
    {
        using var dialog = new FolderBrowserDialog();
        dialog.Description = "Select project working folder";

        if (!string.IsNullOrWhiteSpace(folderTextBox.Text) && Directory.Exists(folderTextBox.Text))
        {
            dialog.SelectedPath = folderTextBox.Text;
        }

        if (dialog.ShowDialog() == DialogResult.OK)
        {
            folderTextBox.Text = dialog.SelectedPath;
        }
    }

    private void openFolderButton_Click(object? sender, EventArgs e)
    {
        if (projectListView.SelectedItems.Count == 0)
        {
            MessageBox.Show(
                "Please select a project first.",
                "No Selection",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        var project = projectListView.SelectedItems[0].Tag as ProjectRecord;
        if (project == null || string.IsNullOrWhiteSpace(project.FolderPath))
        {
            MessageBox.Show(
                "No folder path is set for this project.",
                "No Folder",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        // Canonicalise and validate the path before passing to the OS shell
        string safePath;
        try
        {
            safePath = Path.GetFullPath(project.FolderPath);
        }
        catch
        {
            MessageBox.Show(
                "The folder path is invalid.",
                "Invalid Path",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }

        if (!Directory.Exists(safePath))
        {
            MessageBox.Show(
                "The project folder no longer exists.",
                "Folder Not Found",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = safePath,
                UseShellExecute = true,
                Verb = "open"
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Failed to open folder:\n{ex.Message}",
                "Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private async void exportButton_Click(object? sender, EventArgs e)
    {
        if (_filteredProjects.Count == 0)
        {
            MessageBox.Show(
                "No projects to export.",
                "Empty List",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        using var dialog = new SaveFileDialog
        {
            Filter = "Excel Files|*.xlsx",
            Title = "Export Projects to Excel",
            FileName = $"Gradual_Projects_{DateTime.Now:yyyyMMdd}.xlsx"
        };

        if (dialog.ShowDialog() != DialogResult.OK)
            return;

        try
        {
            var result = await _service.ExportToExcelAsync(_filteredProjects, dialog.FileName);

            if (result.Success)
            {
                var open = MessageBox.Show(
                    $"{result.Message}\n\nDo you want to open the file?",
                    "Export Successful",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Information);

                if (open == DialogResult.Yes)
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = dialog.FileName,
                        UseShellExecute = true
                    });
                }
            }
            else
            {
                MessageBox.Show(
                    result.Message ?? "Export failed",
                    "Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"An error occurred during export:\n{ex.Message}",
                "Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private void browseAttachmentButton_Click(object? sender, EventArgs e)
    {
        using var dialog = new OpenFileDialog
        {
            Multiselect = true,
            Title = "Select files to attach"
        };

        if (dialog.ShowDialog() == DialogResult.OK)
        {
            foreach (var filePath in dialog.FileNames)
            {
                if (!attachmentListBox.Items.Contains(filePath))
                {
                    attachmentListBox.Items.Add(filePath);
                }
            }
            UpdateAttachmentBadge();
        }
    }

    private void openAttachmentButton_Click(object? sender, EventArgs e)
    {
        if (attachmentListBox.SelectedItem is not string filePath)
        {
            MessageBox.Show(
                "Please select an attachment to open.",
                "No Selection",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        OpenAttachment(filePath);
    }

    private void removeAttachmentButton_Click(object? sender, EventArgs e)
    {
        if (attachmentListBox.SelectedIndex >= 0)
        {
            attachmentListBox.Items.RemoveAt(attachmentListBox.SelectedIndex);
            UpdateAttachmentBadge();
        }
    }

    private void attachmentListBox_DoubleClick(object? sender, EventArgs e)
    {
        if (attachmentListBox.SelectedItem is string filePath)
        {
            OpenAttachment(filePath);
        }
    }

    private void OpenAttachment(string filePath)
    {
        // Canonicalise the path before passing it to the OS shell
        string safePath;
        try
        {
            safePath = Path.GetFullPath(filePath);
        }
        catch
        {
            MessageBox.Show(
                "The attachment path is invalid.",
                "Invalid Path",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }

        if (!File.Exists(safePath))
        {
            MessageBox.Show(
                "The file no longer exists.",
                "File Not Found",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = safePath,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Failed to open file:\n{ex.Message}",
                "Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private void attachmentOpenToolStripMenuItem_Click(object? sender, EventArgs e)
    {
        if (attachmentListBox.SelectedItem is string filePath)
        {
            OpenAttachment(filePath);
        }
    }

    private void attachmentRemoveToolStripMenuItem_Click(object? sender, EventArgs e)
    {
        if (attachmentListBox.SelectedIndex >= 0)
        {
            attachmentListBox.Items.RemoveAt(attachmentListBox.SelectedIndex);
            UpdateAttachmentBadge();
        }
    }

    private void attachmentListBox_DragEnter(object? sender, DragEventArgs e)
    {
        if (e.Data?.GetDataPresent(DataFormats.FileDrop) == true)
        {
            e.Effect = DragDropEffects.Copy;
        }
    }

    private void attachmentListBox_DragDrop(object? sender, DragEventArgs e)
    {
        if (e.Data?.GetData(DataFormats.FileDrop) is string[] files)
        {
            foreach (var file in files)
            {
                if (File.Exists(file) && !attachmentListBox.Items.Contains(file))
                {
                    attachmentListBox.Items.Add(file);
                }
            }
            UpdateAttachmentBadge();
        }
    }

    private void attachmentListBox_DrawItem(object? sender, DrawItemEventArgs e)
    {
        if (e.Index < 0 || e.Index >= attachmentListBox.Items.Count)
            return;

        e.DrawBackground();

        var filePath = attachmentListBox.Items[e.Index].ToString() ?? string.Empty;
        var fileName = Path.GetFileName(filePath);
        var fileExt = Path.GetExtension(filePath).ToUpperInvariant();

        var icon = fileExt switch
        {
            ".PDF" => "📄",
            ".DOCX" or ".DOC" => "📝",
            ".XLSX" or ".XLS" => "📊",
            ".PNG" or ".JPG" or ".JPEG" or ".GIF" => "🖼",
            ".ZIP" or ".RAR" => "📦",
            _ => "📎"
        };

        var displayText = $"{icon} {fileName}";
        using var brush = new SolidBrush(e.ForeColor);
        e.Graphics.DrawString(displayText, e.Font ?? attachmentListBox.Font, brush, e.Bounds.X + 2, e.Bounds.Y + 2);

        e.DrawFocusRectangle();
    }

    private void attachmentListBox_MeasureItem(object? sender, MeasureItemEventArgs e)
    {
        e.ItemHeight = 22;
    }

    private void attachmentListBox_MouseMove(object? sender, MouseEventArgs e)
    {
        var index = attachmentListBox.IndexFromPoint(e.Location);
        if (index >= 0 && index < attachmentListBox.Items.Count)
        {
            var fullPath = attachmentListBox.Items[index].ToString() ?? string.Empty;
            attachmentToolTip!.SetToolTip(attachmentListBox, fullPath);
        }
    }

    private void attachmentListBox_SelectedIndexChanged(object? sender, EventArgs e)
    {
        var hasSelection = attachmentListBox.SelectedIndex >= 0;
        openAttachmentButton!.Enabled = hasSelection;
        removeAttachmentButton!.Enabled = hasSelection;
    }

    private void UpdateAttachmentBadge()
    {
        var count = attachmentListBox.Items.Count;
        attachmentCountBadge!.Text = count == 1 ? "1 file" : $"{count} files";
    }

    private void themeButton_Click(object? sender, EventArgs e)
    {
        _darkMode = !_darkMode;
        UpdateTheme();
        _analyticsService.TrackEvent("ThemeToggled", new Dictionary<string, object>
        {
            { "Theme", _darkMode ? "Dark" : "Light" }
        });
    }

    private void UpdateTheme()
    {
        if (_darkMode)
        {
            themeButton!.Text = "🌙 Theme";
            BackColor = Color.FromArgb(18, 27, 45);
            leftPanel!.BackColor = Color.FromArgb(245, 247, 250);
            formTable!.BackColor = Color.White;
        }
        else
        {
            themeButton!.Text = "☀️ Theme";
            BackColor = Color.FromArgb(240, 245, 250);
            leftPanel!.BackColor = Color.FromArgb(255, 255, 255);
            formTable!.BackColor = Color.FromArgb(250, 252, 255);
        }
    }

    private void navButton_Click(object? sender, EventArgs e)
    {
        if (sender is not Button button)
            return;

        // Active highlight on clicked nav button
        foreach (var btn in navFlow!.Controls.OfType<Button>())
        {
            btn.BackColor = btn == button
                ? Color.FromArgb(20, 32, 60)
                : Color.FromArgb(11, 23, 40);
            btn.ForeColor = btn == button
                ? Color.FromArgb(94, 129, 255)
                : Color.FromArgb(160, 185, 220);
        }

        var label = button.Text;

        if (label.Contains("Board"))
        {
            ShowPanel(board: true);
            RefreshBoardIfVisible();
        }
        else if (label.Contains("Repository"))
        {
            ShowPanel(repo: true);
            RefreshRepoIfVisible();
        }
        else if (label.Contains("Setup"))
        {
            ShowPanel(setup: true);
        }
        else
        {
            // Duty — project view
            ShowPanel();
        }
    }

    /// <summary>
    /// Shows exactly one of the four root panels; hides the others.
    /// No arguments = show project view (dutyPanel).
    /// </summary>
    private void ShowPanel(bool board = false, bool repo = false, bool setup = false)
    {
        dutyPanel!.Visible       = !board && !repo && !setup;
        boardPanel!.Visible      = board;
        repositoryPanel!.Visible = repo;
        setupPanel!.Visible      = setup;

        // When returning from Setup, refresh GitHub credentials
        if (!setup)
        {
            try
            {
                var ghService = ServiceContainer.GetService<IGitHubService>() as GitHubService;
                ghService?.RefreshCredentials();
            }
            catch { /* service not available — ignore */ }
        }
    }

    // ── Duty tab switching ────────────────────────────────────────────────

    private bool _dutyShowingGitHub = false;
    private CancellationTokenSource? _githubDutyCts;

    private void SwitchDutyTab(bool github)
    {
        _dutyShowingGitHub = github;

        contentLayout!.Visible = !github;
        githubListView!.Visible = github;

        // Active tab styling
        var accent  = Color.FromArgb(94, 129, 255);
        var muted   = Color.FromArgb(120, 150, 190);
        dutyBtnProjects!.ForeColor = github ? muted  : accent;
        dutyBtnProjects.BackColor  = github ? Color.Transparent : Color.FromArgb(20, 32, 60);
        dutyBtnProjects.Font       = new Font("Segoe UI", 9.5F, github ? FontStyle.Regular : FontStyle.Bold);
        dutyBtnGitHub!.ForeColor   = github ? accent : muted;
        dutyBtnGitHub.BackColor    = github ? Color.FromArgb(20, 32, 60) : Color.Transparent;
        dutyBtnGitHub.Font         = new Font("Segoe UI", 9.5F, github ? FontStyle.Bold : FontStyle.Regular);
    }

    private async Task LoadGitHubDutyAsync()
    {
        // Cancel any previous in-flight fetch
        _githubDutyCts?.Cancel();
        _githubDutyCts = new CancellationTokenSource();
        var ct = _githubDutyCts.Token;

        githubListView!.Items.Clear();
        githubListView.Items.Add(new ListViewItem(
            new[] { "Loading GitHub repositories…", "", "", "", "", "" }));

        try
        {
            var svc = ServiceContainer.GetService<IGitHubService>();
            if (svc == null)
            {
                githubListView.Items.Clear();
                githubListView.Items.Add(new ListViewItem(
                    new[] { "Not connected — go to Setup → GitHub", "", "", "", "", "" }));
                return;
            }

            var (ok, msg) = await svc.TestConnectionAsync();
            if (!ok || ct.IsCancellationRequested)
            {
                githubListView.Items.Clear();
                githubListView.Items.Add(new ListViewItem(
                    new[] { $"GitHub error: {msg}", "", "", "", "", "" }));
                return;
            }

            var repos = await svc.GetUserRepositoriesAsync(count: 200);
            if (ct.IsCancellationRequested) return;

            repos = repos.OrderByDescending(r => r.PushedAt ?? r.UpdatedAt).ToList();

            githubListView.Items.Clear();
            foreach (var repo in repos)
            {
                if (ct.IsCancellationRequested) break;

                // ── Project: repository title
                var project = repo.Name;

                // ── Client: contributor count (stars as proxy; real count via API is rate-limited)
                var client = repo.Forks > 0
                    ? $"{repo.Forks} fork{(repo.Forks == 1 ? "" : "s")}"
                    : "No forks";

                // ── Status: based on push activity
                var pushed  = repo.PushedAt ?? repo.UpdatedAt;
                var ageDays = (DateTime.UtcNow - pushed.ToUniversalTime()).TotalDays;
                var status = ageDays < 30  ? "Active"
                           : ageDays < 180 ? "On Hold"
                           : ageDays < 730 ? "Completed"
                           : "Inactive";

                // ── Priority: based on star count + recent activity
                var priority = repo.Stars > 50 || ageDays < 7  ? "High"
                             : repo.Stars > 10 || ageDays < 60 ? "Normal"
                             : "Low";

                // ── Folder: primary language + topics/files indicator
                var folder = !string.IsNullOrEmpty(repo.Language)
                    ? $"{repo.Language}  ·  ★ {repo.Stars}  ·  ⚠ {repo.OpenIssues} issues"
                    : $"★ {repo.Stars}  ·  ⚠ {repo.OpenIssues} issues";

                // ── Updated: last push / commit date
                var updated = pushed.ToLocalTime().ToString("MM/dd/yyyy");

                var item = new ListViewItem(project);
                item.SubItems.Add(client);
                item.SubItems.Add(status);
                item.SubItems.Add(priority);
                item.SubItems.Add(folder);
                item.SubItems.Add(updated);
                item.Tag = repo;
                githubListView.Items.Add(item);
            }
        }
        catch (OperationCanceledException) { /* tab switched away */ }
        catch (Exception ex)
        {
            if (!ct.IsCancellationRequested)
            {
                githubListView.Items.Clear();
                githubListView.Items.Add(new ListViewItem(
                    new[] { $"Error: {ex.Message}", "", "", "", "", "" }));
            }
        }
    }

    private void RefreshBoardIfVisible()
    {
        if (boardPanel?.Visible == true)
            _ = boardPanel.RefreshAsync(_projects, _activityLogger);
    }

    private void RefreshRepoIfVisible()
    {
        if (repositoryPanel?.Visible != true) return;

        // Safely resolve GitHub services — they may not be registered if no PAT is configured
        Services.GitHub.IGitHubService? gitHubService = null;
        Repositories.GitHub.IG_ReposStore? repoStore = null;

        try { gitHubService = Infrastructure.ServiceContainer.GetService<Services.GitHub.IGitHubService>(); }
        catch { /* Not configured — panel will show empty state */ }

        try { repoStore = Infrastructure.ServiceContainer.GetService<Repositories.GitHub.IG_ReposStore>(); }
        catch { /* Not configured — panel will show empty state */ }

        // Remove the placeholder panel from the layout
        var old = repositoryPanel;
        rootLayout!.Controls.Remove(old);

        // Create a new instance wired up with the (possibly null) services
        repositoryPanel = new Forms.RepositoryPanel(gitHubService, repoStore)
        {
            Dock    = DockStyle.Fill,
            Visible = true
        };
        rootLayout.Controls.Add(repositoryPanel, 1, 1);

        _ = repositoryPanel.RefreshAsync();
    }

    // ── Column weight helper ──────────────────────────────────────────────────
    // Distributes ListView client width proportionally.
    // weights[] entries correspond 1-to-1 with column indices.
    // Folder (index 4, weight 50) always gets the largest share.
    private static void ApplyListViewWeights(ListView lv, int[] weights)
    {
        if (lv.Columns.Count != weights.Length) return;

        var sb       = SystemInformation.VerticalScrollBarWidth;   // always reserve space
        var total    = lv.ClientSize.Width - sb - 2;   // 2px border allowance
        if (total <= 0) return;

        int weightSum = 0;
        foreach (var w in weights) weightSum += w;

        // Minimum widths per column (px) — ensures nothing clips
        int[] mins = { 130, 90, 80, 72, 160, 110 };

        // First pass: assign proportional widths
        var widths = new int[weights.Length];
        int assigned = 0;
        for (int i = 0; i < weights.Length; i++)
        {
            widths[i] = Math.Max(mins[i], total * weights[i] / weightSum);
            assigned += widths[i];
        }

        // Second pass: give any leftover pixels to the Folder column (index 4)
        var leftover = total - assigned;
        widths[4] = Math.Max(mins[4], widths[4] + leftover);

        lv.BeginUpdate();
        for (int i = 0; i < lv.Columns.Count; i++)
            lv.Columns[i].Width = widths[i];
        lv.EndUpdate();
    }

    // Feature helpers \u2014 due date, tags, context menu, and new right-click actions
    // \u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500

    // Nullable references to new designer controls — safe even if not yet in Designer.cs
    private DateTimePicker? dueDatePicker => TryGetControl<DateTimePicker>("dueDatePicker");
    private CheckBox? dueDateEnableCheck => TryGetControl<CheckBox>("dueDateEnableCheck");
    private TextBox? tagsTextBox => TryGetControl<TextBox>("tagsTextBox");

    private T? TryGetControl<T>(string name) where T : Control
    {
        // Walk the full control tree to find by name, returning null if missing
        return FindControlByName(Controls, name) as T;
    }

    private static Control? FindControlByName(Control.ControlCollection controls, string name)
    {
        foreach (Control c in controls)
        {
            if (c.Name == name) return c;
            var found = FindControlByName(c.Controls, name);
            if (found != null) return found;
        }
        return null;
    }

    // Feature 1 \u2014 reads the optional due date picker
    private DateTime? GetDueDateFromForm()
    {
        if (dueDateEnableCheck?.Checked == true && dueDatePicker != null)
            return dueDatePicker.Value.Date;
        return null;
    }

    // Feature 4 \u2014 reads the optional tags text box
    private List<string> GetTagsFromForm()
    {
        if (tagsTextBox == null || string.IsNullOrWhiteSpace(tagsTextBox.Text))
            return new();
        return tagsTextBox.Text
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(t => t.ToLowerInvariant())
            .Distinct()
            .ToList();
    }

    // ── Right-click context menu on projectListView (Features 12,13,14,16,17,9)
    private ContextMenuStrip? _listContextMenu;

    private void EnsureContextMenu()
    {
        if (_listContextMenu != null) return;
        _listContextMenu = new ContextMenuStrip { BackColor = Color.FromArgb(22, 27, 34) };

        AddCtxItem("\u2b50 Toggle Starred",         async () => await ToggleStarredAsync());
        AddCtxItem("\ud83d\udcc4 Clone / Duplicate",       async () => await CloneSelectedProjectAsync());
        AddCtxItem("\u2705 Mark 100% Complete",     async () => await MarkSelectedCompleteAsync());
        _listContextMenu.Items.Add(new ToolStripSeparator());
        AddCtxItem("\ud83d\udce6 Archive Project",       async () => await ArchiveSelectedAsync());
        AddCtxItem("\u26a1 Advanced Filter\u2026",     () => { OpenAdvancedFilter(); return Task.CompletedTask; });
        AddCtxItem("\u23f1 Track Time\u2026",           () => { OpenTimeTracker(); return Task.CompletedTask; });
        _listContextMenu.Items.Add(new ToolStripSeparator());
        AddCtxItem("\ud83d\uddd1 Delete (Recycle Bin)", async () => await ContextDeleteAsync());
        AddCtxItem("\ud83d\udd04 Open Recycle Bin\u2026",  () => { using var rb = new RecycleBinForm(_service); rb.ShowDialog(this); return Task.CompletedTask; });

        projectListView.ContextMenuStrip = _listContextMenu;
    }

    private void AddCtxItem(string text, Func<Task> action)
    {
        var item = new ToolStripMenuItem(text)
        {
            ForeColor = Color.FromArgb(201, 209, 217),
            BackColor = Color.FromArgb(22, 27, 34)
        };
        item.Click += async (s, e) => await action();
        _listContextMenu!.Items.Add(item);
    }

    private async Task ToggleStarredAsync()
    {
        if (!_selectedProjectId.HasValue) return;
        var proj = _projects.FirstOrDefault(p => p.Id == _selectedProjectId);
        if (proj == null) return;
        var dto = new ProjectDto { IsStarred = !proj.IsStarred };
        await _service.UpdateProjectAsync(proj.Id, dto);
        await LoadProjectsAsync();
    }

    private async Task ArchiveSelectedAsync()
    {
        if (!_selectedProjectId.HasValue) return;
        var result = await _service.ArchiveProjectAsync(_selectedProjectId.Value);
        if (result.Success) { await LoadProjectsAsync(); ClearForm(); }
    }

    private async Task ContextDeleteAsync()
    {
        if (!_selectedProjectId.HasValue) return;
        var toDelete = _projects.FirstOrDefault(p => p.Id == _selectedProjectId);
        var result = await _service.DeleteProjectAsync(_selectedProjectId.Value);
        if (result.Success)
        {
            if (toDelete != null) _undoDeleteStack.Push(toDelete);
            await LoadProjectsAsync();
            ClearForm();
            if (_statusBarLabel != null)
                _statusBarLabel.Text = $"Deleted ‘{toDelete?.ProjectName}’ — Ctrl+Z to undo";
        }
    }

    // ── Feature 64/68 state ───────────────────────────────────────────────────
    private ListViewDragDropReorder? _dragDrop;
    private BreadcrumbBar? _breadcrumb;

    // Ensure context menu, drag-drop, and breadcrumb are set up on first load
    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        EnsureContextMenu();

        // Feature 64 — ListView drag-drop reorder
        _dragDrop = new ListViewDragDropReorder(projectListView);
        _dragDrop.ReorderRequested += (s, args) =>
        {
            if (_statusBarLabel != null)
                _statusBarLabel.Text = $"Reordered: row {args.from + 1} → {args.to + 1}  (display order only)";
        };

        // Feature 68 — Breadcrumb bar
        _breadcrumb = new BreadcrumbBar();
        _breadcrumb.SetRoot("🏠 Projects", () => _ = LoadProjectsAsync());
        _breadcrumb.Navigated += (s, crumb) =>
        {
            if (_statusBarLabel != null) _statusBarLabel.Text = $"→ {crumb}";
        };

        // Insert breadcrumb below the top nav bar, above the list
        // Find the panel that contains projectListView and add breadcrumb to top
        var host = projectListView.Parent;
        if (host != null)
        {
            _breadcrumb.BringToFront();
            host.Controls.Add(_breadcrumb);
            host.Controls.SetChildIndex(_breadcrumb, 0);
        }
    }

    /// <summary>
    /// Updates the breadcrumb when the user selects a project (drills into detail).
    /// Called from projectListView_SelectedIndexChanged.
    /// </summary>
    private void UpdateBreadcrumb(string projectName)
    {
        if (_breadcrumb == null) return;
        if (_breadcrumb.CurrentCrumb != projectName)
            _breadcrumb.Navigate(projectName);
    }
}
