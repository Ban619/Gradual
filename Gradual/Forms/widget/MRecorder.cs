namespace Gradual.Forms;

/// <summary>
/// Feature 99 — Keyboard macro recorder.
/// Records a named sequence of key actions (simulate via WinForms SendKeys)
/// and lets the user replay them.
/// </summary>
public class MRecorder : Form
{
    private static readonly Color BgColor   = Color.FromArgb(13, 17, 23);
    private static readonly Color CardColor = Color.FromArgb(22, 27, 34);
    private static readonly Color AccentBlue = Color.FromArgb(88, 166, 255);
    private static readonly Color TextClr  = Color.FromArgb(201, 209, 217);
    private static readonly Color MutedClr = Color.FromArgb(139, 148, 158);
    private static readonly Color AccentRed = Color.FromArgb(248, 81, 73);
    private static readonly Color AccentGreen = Color.FromArgb(63, 185, 80);

    private readonly string _macroFile;
    private List<MacroEntry> _macros = new();

    private ListBox _macroList = null!;
    private RichTextBox _sequenceBox = null!;
    private TextBox _nameBox = null!;
    private Button _recordBtn = null!;
    private Button _playBtn = null!;
    private Button _saveBtn = null!;
    private Button _deleteBtn = null!;
    private Label _statusLabel = null!;

    private bool _isRecording = false;
    private List<string> _recordBuffer = new();

    public MRecorder(string dataDirectory)
    {
        _macroFile = Path.Combine(dataDirectory, "macros.json");
        LoadMacros();
        BuildUI();
    }

    private void BuildUI()
    {
        Text = "⌨ Keyboard Macro Recorder";
        Size = new Size(720, 520);
        MinimumSize = new Size(600, 400);
        BackColor = BgColor;
        ForeColor = TextClr;
        Font = new Font("Segoe UI", 9.5f);
        StartPosition = FormStartPosition.CenterScreen;
        KeyPreview = true;

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Padding = new Padding(12)
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 220));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        // ── Left: macro list ─────────────────────────────────────────────────
        var leftPanel = new Panel { Dock = DockStyle.Fill, BackColor = CardColor, Padding = new Padding(8) };

        var listTitle = new Label
        {
            Text = "📋 Saved Macros",
            Dock = DockStyle.Top, Height = 28,
            Font = new Font("Segoe UI", 10, FontStyle.Bold),
            ForeColor = AccentBlue
        };

        _macroList = new ListBox
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(13, 17, 23),
            ForeColor = TextClr,
            BorderStyle = BorderStyle.None,
            Font = new Font("Segoe UI", 9.5f),
            SelectionMode = SelectionMode.One
        };
        _macroList.SelectedIndexChanged += MacroList_SelectedIndexChanged;

        _deleteBtn = BuildBtn("🗑 Delete", AccentRed, Delete_Click);
        _deleteBtn.Dock = DockStyle.Bottom;

        leftPanel.Controls.Add(_macroList);
        leftPanel.Controls.Add(_deleteBtn);
        leftPanel.Controls.Add(listTitle);

        // ── Right: editor ────────────────────────────────────────────────────
        var rightPanel = new Panel { Dock = DockStyle.Fill, BackColor = BgColor, Padding = new Padding(8, 0, 0, 0) };

        // Name row
        var nameRow = new FlowLayoutPanel
        {
            Dock = DockStyle.Top, Height = 36, BackColor = Color.Transparent,
            FlowDirection = FlowDirection.LeftToRight, WrapContents = false
        };
        var nameLbl = new Label
        {
            Text = "Name:", AutoSize = true,
            ForeColor = MutedClr, Margin = new Padding(0, 8, 6, 0)
        };
        _nameBox = new TextBox
        {
            Width = 240, Height = 28,
            BackColor = CardColor, ForeColor = TextClr,
            BorderStyle = BorderStyle.FixedSingle,
            PlaceholderText = "macro name…",
            Margin = new Padding(0, 4, 0, 0)
        };
        nameRow.Controls.Add(nameLbl);
        nameRow.Controls.Add(_nameBox);

        // Sequence editor
        var seqLabel = new Label
        {
            Text = "Key Sequence (one action per line, e.g. ^n = Ctrl+N, {ENTER}, %{F4}):",
            Dock = DockStyle.Top, Height = 22, ForeColor = MutedClr, Font = new Font("Segoe UI", 8.5f)
        };

        _sequenceBox = new RichTextBox
        {
            Dock = DockStyle.Fill,
            BackColor = CardColor, ForeColor = TextClr,
            BorderStyle = BorderStyle.None,
            Font = new Font("Cascadia Code", 10f),
            AcceptsTab = true, WordWrap = false
        };

        // Button row
        var btnRow = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom, Height = 44, BackColor = Color.Transparent,
            FlowDirection = FlowDirection.LeftToRight, Padding = new Padding(0, 6, 0, 0)
        };
        _recordBtn = BuildBtn("⏺ Record", AccentRed, Record_Click);
        _playBtn   = BuildBtn("▶ Play",   AccentGreen, Play_Click);
        _saveBtn   = BuildBtn("💾 Save",   AccentBlue, Save_Click);
        btnRow.Controls.AddRange(new Control[] { _recordBtn, _playBtn, _saveBtn });

        // Status bar
        _statusLabel = new Label
        {
            Text = "Ready — select a macro to edit, or type a new sequence.",
            Dock = DockStyle.Bottom, Height = 22,
            ForeColor = MutedClr, Font = new Font("Segoe UI", 8.5f)
        };

        rightPanel.Controls.Add(_sequenceBox);
        rightPanel.Controls.Add(seqLabel);
        rightPanel.Controls.Add(nameRow);
        rightPanel.Controls.Add(btnRow);
        rightPanel.Controls.Add(_statusLabel);

        layout.Controls.Add(leftPanel, 0, 0);
        layout.Controls.Add(rightPanel, 1, 0);
        Controls.Add(layout);

        RefreshList();
    }

    // ── Recording ─────────────────────────────────────────────────────────────
    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (!_isRecording) return base.ProcessCmdKey(ref msg, keyData);

        // Stop recording on F9
        if (keyData == Keys.F9)
        {
            StopRecording();
            return true;
        }

        // Translate key to SendKeys syntax
        var token = KeyToSendKeys(keyData);
        if (!string.IsNullOrEmpty(token))
        {
            _recordBuffer.Add(token);
            _sequenceBox.AppendText(token + Environment.NewLine);
            _statusLabel.Text = $"Recording… {_recordBuffer.Count} actions (F9 to stop)";
        }
        return true; // consume all keys while recording
    }

    private void Record_Click(object? sender, EventArgs e)
    {
        if (_isRecording)
        {
            StopRecording();
        }
        else
        {
            _isRecording = true;
            _recordBuffer.Clear();
            _sequenceBox.Clear();
            _recordBtn.Text = "⏹ Stop (F9)";
            _recordBtn.BackColor = Color.FromArgb(210, 60, 30);
            _statusLabel.Text = "Recording… press keys. F9 to stop.";
            _statusLabel.ForeColor = Color.FromArgb(248, 81, 73);
        }
    }

    private void StopRecording()
    {
        _isRecording = false;
        _recordBtn.Text = "⏺ Record";
        _recordBtn.BackColor = Color.FromArgb(248, 81, 73);
        _statusLabel.Text = $"Stopped. {_recordBuffer.Count} actions recorded.";
        _statusLabel.ForeColor = Color.FromArgb(139, 148, 158);
    }

    // ── Playback ──────────────────────────────────────────────────────────────
    private async void Play_Click(object? sender, EventArgs e)
    {
        var lines = _sequenceBox.Lines
            .Where(l => !string.IsNullOrWhiteSpace(l)).ToList();

        if (!lines.Any())
        {
            _statusLabel.Text = "Nothing to play.";
            return;
        }

        _statusLabel.Text = $"Playing {lines.Count} actions…";
        Hide(); // hide so keys go to target window
        await Task.Delay(500);

        foreach (var line in lines)
        {
            try
            {
                System.Windows.Forms.SendKeys.SendWait(line.Trim());
                await Task.Delay(80); // small delay between actions
            }
            catch { /* ignore bad sequences */ }
        }

        Show();
        BringToFront();
        _statusLabel.Text = "Playback complete.";
    }

    // ── Save ──────────────────────────────────────────────────────────────────
    private void Save_Click(object? sender, EventArgs e)
    {
        var name = _nameBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            _statusLabel.Text = "Enter a macro name first.";
            return;
        }
        var sequence = _sequenceBox.Lines
            .Where(l => !string.IsNullOrWhiteSpace(l)).ToList();

        var existing = _macros.FirstOrDefault(m => m.Name == name);
        if (existing != null)
            existing.Sequence = sequence;
        else
            _macros.Add(new MacroEntry { Name = name, Sequence = sequence });

        SaveMacros();
        RefreshList();
        _statusLabel.Text = $"Saved '{name}'.";
    }

    // ── Delete ────────────────────────────────────────────────────────────────
    private void Delete_Click(object? sender, EventArgs e)
    {
        if (_macroList.SelectedItem is not string name) return;
        _macros.RemoveAll(m => m.Name == name);
        SaveMacros();
        RefreshList();
        _sequenceBox.Clear();
        _nameBox.Clear();
        _statusLabel.Text = $"Deleted '{name}'.";
    }

    private void MacroList_SelectedIndexChanged(object? sender, EventArgs e)
    {
        if (_macroList.SelectedItem is not string name) return;
        var macro = _macros.FirstOrDefault(m => m.Name == name);
        if (macro == null) return;
        _nameBox.Text = macro.Name;
        _sequenceBox.Lines = macro.Sequence.ToArray();
        _statusLabel.Text = $"'{name}' — {macro.Sequence.Count} actions";
    }

    // ── Helpers ───────────────────────────────────────────────────────────────
    private static string KeyToSendKeys(Keys keyData)
    {
        bool ctrl  = keyData.HasFlag(Keys.Control);
        bool shift = keyData.HasFlag(Keys.Shift);
        bool alt   = keyData.HasFlag(Keys.Alt);
        var key = keyData & ~Keys.Modifiers;

        if (key == Keys.ControlKey || key == Keys.ShiftKey ||
            key == Keys.Menu || key == Keys.None) return "";

        string keyStr = key switch
        {
            Keys.Enter  => "{ENTER}",
            Keys.Escape => "{ESC}",
            Keys.Tab    => "{TAB}",
            Keys.Delete => "{DELETE}",
            Keys.Back   => "{BACKSPACE}",
            Keys.F1 => "{F1}", Keys.F2 => "{F2}", Keys.F3 => "{F3}",
            Keys.F4 => "{F4}", Keys.F5 => "{F5}", Keys.F6 => "{F6}",
            Keys.F7 => "{F7}", Keys.F8 => "{F8}", Keys.F9 => "{F9}",
            Keys.F10 => "{F10}", Keys.F11 => "{F11}", Keys.F12 => "{F12}",
            Keys.Left => "{LEFT}", Keys.Right => "{RIGHT}",
            Keys.Up   => "{UP}",  Keys.Down  => "{DOWN}",
            Keys.Home => "{HOME}", Keys.End  => "{END}",
            _ => key.ToString().Length == 1 ? key.ToString().ToLower() : ""
        };
        if (string.IsNullOrEmpty(keyStr)) return "";

        if (ctrl)  keyStr = "^"  + keyStr;
        if (shift) keyStr = "+"  + keyStr;
        if (alt)   keyStr = "%"  + keyStr;
        return keyStr;
    }

    private void RefreshList()
    {
        _macroList.Items.Clear();
        foreach (var m in _macros) _macroList.Items.Add(m.Name);
    }

    private void LoadMacros()
    {
        try
        {
            if (File.Exists(_macroFile))
            {
                var json = File.ReadAllText(_macroFile);
                _macros = System.Text.Json.JsonSerializer.Deserialize<List<MacroEntry>>(json) ?? new();
            }
        }
        catch { _macros = new(); }
    }

    private void SaveMacros()
    {
        var json = System.Text.Json.JsonSerializer.Serialize(_macros,
            new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_macroFile, json);
    }

    private static Button BuildBtn(string text, Color bg, EventHandler handler)
    {
        var btn = new Button
        {
            Text = text, Height = 32, Width = 110,
            BackColor = bg, ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9, FontStyle.Bold),
            Cursor = Cursors.Hand, Margin = new Padding(0, 0, 8, 0)
        };
        btn.FlatAppearance.BorderSize = 0;
        btn.Click += handler;
        return btn;
    }
}

/// <summary>Stores a single named macro.</summary>
internal sealed class MacroEntry
{
    public string Name { get; set; } = "";
    public List<string> Sequence { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}
