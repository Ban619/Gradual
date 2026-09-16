namespace Gradual.Forms;

/// <summary>
/// F1 keyboard shortcut overlay — shows all registered shortcuts (Feature 63).
/// Dismisses on Escape, F1, or clicking outside.
/// </summary>
public class KeyboardShortcutOverlay : Form
{
    private static readonly Color Bg     = Color.FromArgb(220, 13, 17, 23);  // semi-transparent
    private static readonly Color Card   = Color.FromArgb(240, 22, 27, 34);
    private static readonly Color TextClr = Color.FromArgb(201, 209, 217);
    private static readonly Color Accent = Color.FromArgb(88, 166, 255);
    private static readonly Color Header = Color.FromArgb(63, 185, 80);
    private static readonly Color Key    = Color.FromArgb(210, 153, 34);

    public KeyboardShortcutOverlay()
    {
        BuildUI();
    }

    private void BuildUI()
    {
        FormBorderStyle = FormBorderStyle.None;
        BackColor = Color.FromArgb(10, 14, 20);
        TransparencyKey = Color.Magenta; // none — use BackColor opacity
        Opacity = 0.96;
        Size = new Size(820, 640);
        StartPosition = FormStartPosition.CenterScreen;
        TopMost = true;
        KeyPreview = true;
        KeyDown += (s, e) => { if (e.KeyCode is Keys.F1 or Keys.Escape) Close(); };

        // Click-outside-to-dismiss via form deactivation
        Deactivate += (s, e) => Close();

        // ── Outer frame ────────────────────────────────────────────────────────
        var frame = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(13, 17, 23),
            Padding = new Padding(24, 20, 24, 20)
        };
        frame.Paint += (s, e) =>
        {
            using var pen = new Pen(Color.FromArgb(48, 54, 61), 1);
            e.Graphics.DrawRectangle(pen, 0, 0, frame.Width - 1, frame.Height - 1);
        };

        var titleLbl = new Label
        {
            Text = "⌨  Keyboard Shortcuts — press F1 or Esc to close",
            Font = new Font("Segoe UI", 12, FontStyle.Bold),
            ForeColor = Accent,
            AutoSize = true,
            Location = new Point(24, 18)
        };
        frame.Controls.Add(titleLbl);

        var grid = new TableLayoutPanel
        {
            Location = new Point(24, 52),
            Width = 770,
            AutoSize = true,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = Color.FromArgb(13, 17, 23)
        };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));

        var leftPanel = BuildShortcutPanel("📋 Projects & CRUD", new[]
        {
            ("Ctrl+N",          "New project"),
            ("Ctrl+S",          "Save project"),
            ("Ctrl+Z",          "Undo last delete"),
            ("Ctrl+D",          "Duplicate / Clone"),
            ("Ctrl+M",          "Mark 100% complete"),
        }, new[]
        {
            ("🔍 Search & Filter", (string?)null),
            ("Ctrl+F",             "Global quick-search"),
            ("Ctrl+Shift+F",       "Advanced filter dialog"),
            ("Escape",             "Clear filter / close"),
        }, new[]
        {
            ("📤 Import / Export", null),
            ("Ctrl+E",             "Export to Excel"),
            ("Ctrl+Shift+E",       "Export to CSV"),
            ("Ctrl+P",             "Print list"),
        }, new[]
        {
            ("⏱ Time & Analytics", null),
            ("Ctrl+T",             "Open Time Tracker"),
            ("Ctrl+A",             "Analytics Dashboard"),
        });

        var rightPanel = BuildShortcutPanel("🪟 Navigation & Tools", new[]
        {
            ("Ctrl+1",       "Projects / Duty view"),
            ("Ctrl+2",       "Board view"),
            ("Ctrl+3",       "Repository view"),
            ("Ctrl+4",       "Setup"),
            ("F5",           "Refresh"),
            ("F1",           "This help overlay"),
            ("F7",           "Developer widget"),
        }, new[]
        {
            ("🔔 Notifications & Data", (string?)null),
            ("Ctrl+Shift+N", "Notification center"),
            ("Ctrl+Shift+B", "Create backup"),
            ("Ctrl+Shift+R", "Open Recycle Bin"),
            ("Ctrl+Shift+H", "System Health"),
            ("Ctrl+Shift+S", "Settings"),
        }, new[]
        {
            ("🛠 Developer Tools", null),
            ("Ctrl+Shift+A", "Audit Log Viewer"),
            ("Ctrl+Shift+W", "What's New dialog"),
            ("Ctrl+Shift+M", "Macro Recorder"),
            ("Ctrl+Shift+C", "Script Runner"),
        });

        grid.Controls.Add(leftPanel,  0, 0);
        grid.Controls.Add(rightPanel, 1, 0);
        frame.Controls.Add(grid);

        Controls.Add(frame);
    }

    private Panel BuildShortcutPanel(string mainTitle, params (string key, string? desc)[][] sections)
    {
        var panel = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoSize = true,
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(13, 17, 23),
            Padding = new Padding(4, 0, 8, 0)
        };

        // Main section title
        panel.Controls.Add(MakeHeader(mainTitle));

        foreach (var section in sections)
        {
            foreach (var (key, desc) in section)
            {
                if (desc == null)
                {
                    // Sub-section header
                    panel.Controls.Add(MakeSubHeader(key));
                }
                else
                {
                    panel.Controls.Add(MakeShortcutRow(key, desc));
                }
            }
        }

        return panel;
    }

    private static Label MakeHeader(string TextClr)
    {
        return new Label
        {
            Text = TextClr,
            Font = new Font("Segoe UI", 10, FontStyle.Bold),
            ForeColor = Header,
            AutoSize = true,
            Margin = new Padding(0, 4, 0, 8)
        };
    }

    private static Label MakeSubHeader(string TextClr)
    {
        return new Label
        {
            Text = TextClr,
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
            ForeColor = Accent,
            AutoSize = true,
            Margin = new Padding(0, 10, 0, 4)
        };
    }

    private static Panel MakeShortcutRow(string shortcut, string description)
    {
        var row = new Panel
        {
            Height = 22,
            Width = 360,
            BackColor = Color.Transparent,
            Margin = new Padding(0, 1, 0, 1)
        };

        var keyLbl = new Label
        {
            Text = shortcut,
            Font = new Font("Consolas", 9, FontStyle.Bold),
            ForeColor = Key,
            BackColor = Color.FromArgb(30, 37, 44),
            AutoSize = true,
            Padding = new Padding(4, 1, 4, 1),
            Location = new Point(0, 1)
        };

        var descLbl = new Label
        {
            Text = description,
            Font = new Font("Segoe UI", 9),
            ForeColor = TextClr,
            AutoSize = true,
            Location = new Point(160, 3)
        };

        row.Controls.Add(keyLbl);
        row.Controls.Add(descLbl);
        return row;
    }
}

