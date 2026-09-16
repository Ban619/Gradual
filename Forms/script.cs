using Gradual.Models;

namespace Gradual.Forms;

/// <summary>
/// Feature 97 — Script / Plugin runner.
/// Lets advanced users write PowerShell or simple C# expressions against
/// the live project list without recompiling the app.
/// PowerShell execution is done via System.Diagnostics.Process.
/// </summary>
public class ScriptRunnerForm : Form
{
    private static readonly Color BgColor    = Color.FromArgb(13, 17, 23);
    private static readonly Color CardColor  = Color.FromArgb(22, 27, 34);
    private static readonly Color AccentBlue = Color.FromArgb(88, 166, 255);
    private static readonly Color TextClr    = Color.FromArgb(201, 209, 217);
    private static readonly Color MutedClr   = Color.FromArgb(139, 148, 158);
    private static readonly Color AccentGreen= Color.FromArgb(63, 185, 80);
    private static readonly Color AccentRed  = Color.FromArgb(248, 81, 73);

    private readonly string _scriptDir;
    private readonly IReadOnlyList<ProjectRecord> _projects;

    private RichTextBox _editor = null!;
    private RichTextBox _output = null!;
    private ComboBox _scriptCombo = null!;
    private Label _statusLabel = null!;

    // Built-in script templates
    private static readonly Dictionary<string, string> Templates = new()
    {
        ["(new script)"] = "# PowerShell script\n# Variable $projects is available as JSON in $env:PROJ_JSON\n\nWrite-Output \"Hello from Gradual script runner!\"",
        ["List overdue projects"] =
            "$data = $env:PROJ_JSON | ConvertFrom-Json\n" +
            "$data | Where-Object { $_.IsOverdue -eq $true } | " +
            "Select-Object ProjectName, Client, DueDate | Format-Table",
        ["Count by status"] =
            "$data = $env:PROJ_JSON | ConvertFrom-Json\n" +
            "$data | Group-Object Status | Select-Object Name, Count | Sort-Object Count -Descending",
        ["Top clients by project count"] =
            "$data = $env:PROJ_JSON | ConvertFrom-Json\n" +
            "$data | Group-Object Client | Sort-Object Count -Descending | Select-Object -First 10 | Format-Table Name, Count",
        ["Overdue hours report"] =
            "$data = $env:PROJ_JSON | ConvertFrom-Json\n" +
            "$total = ($data | Where-Object { $_.IsOverdue } | Measure-Object EstimatedHours -Sum).Sum\n" +
            "Write-Output \"Total overdue estimated hours: $total\"",
        ["Projects due this week"] =
            "$data = $env:PROJ_JSON | ConvertFrom-Json\n" +
            "$end = (Get-Date).AddDays(7)\n" +
            "$data | Where-Object { $_.DueDate -and [DateTime]$_.DueDate -le $end } | " +
            "Select-Object ProjectName, DueDate | Format-Table",
    };

    public ScriptRunnerForm(string dataDirectory, IReadOnlyList<ProjectRecord> projects)
    {
        _scriptDir = Path.Combine(dataDirectory, "scripts");
        _projects  = projects;
        Directory.CreateDirectory(_scriptDir);
        BuildUI();
    }

    private void BuildUI()
    {
        Text = "⚡ Script Runner";
        Size = new Size(900, 680);
        MinimumSize = new Size(700, 500);
        BackColor = BgColor;
        ForeColor = TextClr;
        Font = new Font("Segoe UI", 9.5f);
        StartPosition = FormStartPosition.CenterScreen;

        var mainSplit = new SplitContainer
        {
            Dock        = DockStyle.Fill,
            Orientation = Orientation.Horizontal,
            SplitterDistance = 380,
            BackColor   = BgColor,
            BorderStyle = BorderStyle.None
        };

        // ── TOP: editor panel ─────────────────────────────────────────────────
        var topPanel = new Panel { Dock = DockStyle.Fill, BackColor = BgColor };

        // Toolbar
        var toolbar = new FlowLayoutPanel
        {
            Dock = DockStyle.Top, Height = 44,
            BackColor = Color.FromArgb(8, 12, 18),
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false, Padding = new Padding(8, 6, 8, 6)
        };

        var templateLbl = new Label
        {
            Text = "Template:", AutoSize = true,
            ForeColor = MutedClr, Margin = new Padding(0, 6, 6, 0)
        };
        _scriptCombo = new ComboBox
        {
            Width = 240, Height = 28,
            BackColor = CardColor, ForeColor = TextClr,
            FlatStyle = FlatStyle.Flat,
            DropDownStyle = ComboBoxStyle.DropDownList,
            Margin = new Padding(0, 2, 12, 0)
        };
        foreach (var t in Templates.Keys) _scriptCombo.Items.Add(t);
        _scriptCombo.SelectedIndex = 0;
        _scriptCombo.SelectedIndexChanged += TemplateCombo_Changed;

        var runBtn  = BuildBtn("▶ Run (F5)",    AccentGreen, RunScript_Click);
        var saveBtn = BuildBtn("💾 Save",        AccentBlue,  SaveScript_Click);
        var loadBtn = BuildBtn("📂 Load",        Color.FromArgb(50, 60, 80), LoadScript_Click);
        var clearBtn= BuildBtn("🗑 Clear Output", Color.FromArgb(60, 40, 40), ClearOutput_Click);

        toolbar.Controls.AddRange(new Control[] { templateLbl, _scriptCombo, runBtn, saveBtn, loadBtn, clearBtn });

        // Editor
        var editorLabel = new Label
        {
            Text = "PowerShell Script Editor",
            Dock = DockStyle.Top, Height = 24,
            ForeColor = AccentBlue,
            Font = new Font("Segoe UI", 9, FontStyle.Bold),
            Padding = new Padding(8, 4, 0, 0)
        };

        _editor = new RichTextBox
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(10, 14, 20),
            ForeColor = Color.FromArgb(201, 209, 217),
            Font = new Font("Cascadia Code", 10.5f),
            BorderStyle = BorderStyle.None,
            AcceptsTab = true,
            WordWrap = false,
            Text = Templates["(new script)"]
        };
        _editor.KeyDown += Editor_KeyDown;

        topPanel.Controls.Add(_editor);
        topPanel.Controls.Add(editorLabel);
        topPanel.Controls.Add(toolbar);

        // ── BOTTOM: output panel ──────────────────────────────────────────────
        var bottomPanel = new Panel { Dock = DockStyle.Fill, BackColor = BgColor };

        var outputLabel = new Label
        {
            Text = "Output",
            Dock = DockStyle.Top, Height = 24,
            ForeColor = AccentGreen,
            Font = new Font("Segoe UI", 9, FontStyle.Bold),
            Padding = new Padding(8, 4, 0, 0)
        };

        _output = new RichTextBox
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(6, 10, 14),
            ForeColor = AccentGreen,
            Font = new Font("Cascadia Code", 10f),
            BorderStyle = BorderStyle.None,
            ReadOnly = true,
            WordWrap = false
        };

        _statusLabel = new Label
        {
            Dock = DockStyle.Bottom, Height = 22,
            ForeColor = MutedClr, Font = new Font("Segoe UI", 8.5f),
            Text = "Ready — F5 to run script",
            Padding = new Padding(8, 2, 0, 0)
        };

        bottomPanel.Controls.Add(_output);
        bottomPanel.Controls.Add(outputLabel);
        bottomPanel.Controls.Add(_statusLabel);

        mainSplit.Panel1.Controls.Add(topPanel);
        mainSplit.Panel2.Controls.Add(bottomPanel);
        Controls.Add(mainSplit);
    }

    // ── F5 runs script ────────────────────────────────────────────────────────
    private void Editor_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.F5) { RunScript_Click(this, EventArgs.Empty); e.Handled = true; }
    }

    private void TemplateCombo_Changed(object? sender, EventArgs e)
    {
        if (_scriptCombo.SelectedItem is string name && Templates.TryGetValue(name, out var tmpl))
            _editor.Text = tmpl;
    }

    private async void RunScript_Click(object? sender, EventArgs e)
    {
        _output.Clear();
        _statusLabel.Text = "Running…";
        _statusLabel.ForeColor = Color.FromArgb(210, 153, 34);

        var script = _editor.Text;
        if (string.IsNullOrWhiteSpace(script)) { _statusLabel.Text = "Nothing to run."; return; }

        // Serialize projects as JSON into env variable
        var projJson = System.Text.Json.JsonSerializer.Serialize(_projects,
            new System.Text.Json.JsonSerializerOptions { WriteIndented = false });

        var tmpScript = Path.Combine(Path.GetTempPath(), $"gradual_script_{Guid.NewGuid():N}.ps1");
        await File.WriteAllTextAsync(tmpScript, script);

        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{tmpScript}\"",
                RedirectStandardOutput = true,
                RedirectStandardError  = true,
                UseShellExecute = false,
                CreateNoWindow  = true
            };
            psi.Environment["PROJ_JSON"] = projJson;

            using var proc = new System.Diagnostics.Process { StartInfo = psi };
            proc.Start();

            var stdout = await proc.StandardOutput.ReadToEndAsync();
            var stderr = await proc.StandardError.ReadToEndAsync();
            await proc.WaitForExitAsync();

            AppendOutput(stdout, Color.FromArgb(63, 185, 80));
            if (!string.IsNullOrWhiteSpace(stderr))
                AppendOutput(stderr, Color.FromArgb(248, 81, 73));

            _statusLabel.Text = $"Exit code: {proc.ExitCode} — {DateTime.Now:HH:mm:ss}";
            _statusLabel.ForeColor = proc.ExitCode == 0
                ? Color.FromArgb(63, 185, 80) : Color.FromArgb(248, 81, 73);
        }
        catch (Exception ex)
        {
            AppendOutput($"Error: {ex.Message}", Color.FromArgb(248, 81, 73));
            _statusLabel.Text = "Execution failed.";
            _statusLabel.ForeColor = Color.FromArgb(248, 81, 73);
        }
        finally
        {
            try { File.Delete(tmpScript); } catch { }
        }
    }

    private void SaveScript_Click(object? sender, EventArgs e)
    {
        using var dlg = new SaveFileDialog
        {
            InitialDirectory = _scriptDir,
            Filter = "PowerShell|*.ps1|All Files|*.*",
            FileName = "my_script.ps1"
        };
        if (dlg.ShowDialog() != DialogResult.OK) return;
        File.WriteAllText(dlg.FileName, _editor.Text);
        _statusLabel.Text = $"Saved: {dlg.FileName}";
    }

    private void LoadScript_Click(object? sender, EventArgs e)
    {
        using var dlg = new OpenFileDialog
        {
            InitialDirectory = _scriptDir,
            Filter = "PowerShell|*.ps1|All Files|*.*"
        };
        if (dlg.ShowDialog() != DialogResult.OK) return;
        _editor.Text = File.ReadAllText(dlg.FileName);
        _statusLabel.Text = $"Loaded: {Path.GetFileName(dlg.FileName)}";
    }

    private void ClearOutput_Click(object? sender, EventArgs e) => _output.Clear();

    private void AppendOutput(string text, Color color)
    {
        if (string.IsNullOrEmpty(text)) return;
        int start = _output.TextLength;
        _output.AppendText(text);
        _output.Select(start, text.Length);
        _output.SelectionColor = color;
        _output.SelectionLength = 0;
        _output.ScrollToCaret();
    }

    private static Button BuildBtn(string text, Color bg, EventHandler handler)
    {
        var btn = new Button
        {
            Text = text, Height = 30, AutoSize = true,
            BackColor = bg, ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9f),
            Cursor = Cursors.Hand, Margin = new Padding(0, 0, 6, 0)
        };
        btn.FlatAppearance.BorderSize = 0;
        btn.Click += handler;
        return btn;
    }
}
