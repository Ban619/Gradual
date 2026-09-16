using System.Text.Json;

namespace Gradual.Forms;

/// <summary>
/// Secret developer overlay — toggled by F7.
/// Provides Notes (persisted), Dev Chat (file-based broadcast), and Calculator.
/// </summary>
public class DevWidget : Form
{
    // ── colours ───────────────────────────────────────────────────────────────
    private static readonly Color BgBase    = Color.FromArgb(13, 17, 27);
    private static readonly Color BgCard    = Color.FromArgb(22, 30, 46);
    private static readonly Color BgInput   = Color.FromArgb(18, 25, 40);
    private static readonly Color AccentGreen = Color.FromArgb(30, 215, 96);
    private static readonly Color AccentBlue  = Color.FromArgb(60, 140, 255);
    private static readonly Color AccentAmber = Color.FromArgb(250, 180, 30);
    private static readonly Color TextPrimary = Color.FromArgb(220, 230, 250);
    private static readonly Color TextMuted   = Color.FromArgb(120, 140, 170);
    private static readonly Color Border      = Color.FromArgb(38, 52, 78);

    // ── persistence ───────────────────────────────────────────────────────────
    private readonly string _notesPath;
    private readonly string _chatPath;
    private System.Windows.Forms.Timer? _chatPollTimer;

    // ── controls ──────────────────────────────────────────────────────────────
    private RichTextBox _notesBox = null!;
    private RichTextBox _chatDisplay = null!;
    private TextBox     _chatInput   = null!;
    private TextBox     _calcDisplay = null!;
    private string      _calcExpression = "";
    private bool        _calcNewInput   = true;

    // ── drag support ──────────────────────────────────────────────────────────
    private Point _dragStart;
    private bool  _dragging;

    public DevWidget(string dataDirectory)
    {
        _notesPath = Path.Combine(dataDirectory, "dev_notes.txt");
        _chatPath  = Path.Combine(dataDirectory, "dev_chat.json");

        BuildUI();
        LoadNotes();

        // Poll chat file every 3 seconds so messages from other devs appear
        _chatPollTimer = new System.Windows.Forms.Timer { Interval = 3000 };
        _chatPollTimer.Tick += (_, _) => RefreshChat();
        _chatPollTimer.Start();
        RefreshChat();
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  UI Construction
    // ═══════════════════════════════════════════════════════════════════════════

    private void BuildUI()
    {
        Text            = "Dev Tools  [F7]";
        Size            = new Size(420, 560);
        MinimumSize     = new Size(380, 480);
        FormBorderStyle = FormBorderStyle.None;
        StartPosition   = FormStartPosition.Manual;
        BackColor       = BgBase;
        TopMost         = true;
        ShowInTaskbar   = false;
        Opacity         = 0.97;

        // Position bottom-right of screen
        var screen = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1920, 1080);
        Location = new Point(screen.Right - Width - 20, screen.Bottom - Height - 20);

        // ── Title bar ────────────────────────────────────────────────────────
        var titleBar = new Panel
        {
            Dock      = DockStyle.Top,
            Height    = 44,
            BackColor = Color.FromArgb(16, 22, 36),
            Cursor    = Cursors.SizeAll
        };

        var titleLabel = new Label
        {
            Text      = "🛠  Dev Tools",
            Font      = new Font("Segoe UI", 11F, FontStyle.Bold),
            ForeColor = TextPrimary,
            AutoSize  = false,
            Width     = 260,
            Height    = 44,
            TextAlign = ContentAlignment.MiddleLeft,
            Location  = new Point(14, 0)
        };

        var closeBtn = new Button
        {
            Text      = "✕",
            Width     = 36,
            Height    = 36,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.Transparent,
            ForeColor = TextMuted,
            Font      = new Font("Segoe UI", 10F),
            Cursor    = Cursors.Hand,
            Location  = new Point(Width - 44, 4)
        };
        closeBtn.FlatAppearance.BorderSize = 0;
        closeBtn.Click += (_, _) => Hide();

        // Drag the window by the title bar
        titleBar.MouseDown += (_, e) => { _dragging = true; _dragStart = e.Location; };
        titleBar.MouseMove += (_, e) =>
        {
            if (_dragging)
                Location = new Point(Location.X + e.X - _dragStart.X,
                                     Location.Y + e.Y - _dragStart.Y);
        };
        titleBar.MouseUp += (_, _) => _dragging = false;
        titleLabel.MouseDown += (_, e) => { _dragging = true; _dragStart = new Point(e.X + titleLabel.Left, e.Y); };
        titleLabel.MouseMove += (_, e) =>
        {
            if (_dragging)
                Location = new Point(Location.X + e.X + titleLabel.Left - _dragStart.X,
                                     Location.Y + e.Y - _dragStart.Y);
        };
        titleLabel.MouseUp += (_, _) => _dragging = false;

        titleBar.Controls.Add(titleLabel);
        titleBar.Controls.Add(closeBtn);
        closeBtn.Anchor = AnchorStyles.Top | AnchorStyles.Right;

        // ── Tab bar ──────────────────────────────────────────────────────────
        var tabBar = new Panel
        {
            Dock      = DockStyle.Top,
            Height    = 38,
            BackColor = BgCard
        };

        var tabs      = new[] { "📝  Notes", "💬  Chat", "🧮  Calc" };
        var tabColors = new[] { AccentGreen, AccentBlue, AccentAmber };
        var tabBtns   = new Button[3];
        int tabW      = (420 - 2) / 3;

        // Content pages
        var pages = new Panel[3];
        for (int i = 0; i < 3; i++)
            pages[i] = new Panel { Dock = DockStyle.Fill, BackColor = BgBase, Visible = false };

        pages[0].Controls.Add(BuildNotesPage());
        pages[1].Controls.Add(BuildChatPage());
        pages[2].Controls.Add(BuildCalcPage());

        for (int i = 0; i < tabs.Length; i++)
        {
            int idx = i;
            tabBtns[i] = new Button
            {
                Text      = tabs[i],
                Width     = tabW,
                Height    = 38,
                FlatStyle = FlatStyle.Flat,
                BackColor = BgCard,
                ForeColor = TextMuted,
                Font      = new Font("Segoe UI", 8.5F),
                Location  = new Point(idx * tabW, 0),
                Tag       = idx
            };
            tabBtns[i].FlatAppearance.BorderSize = 0;
            tabBtns[i].Click += (_, _) =>
            {
                for (int j = 0; j < 3; j++)
                {
                    pages[j].Visible         = j == idx;
                    tabBtns[j].BackColor     = j == idx ? Color.FromArgb(28, 38, 58) : BgCard;
                    tabBtns[j].ForeColor     = j == idx ? tabColors[idx] : TextMuted;
                }
            };
            tabBar.Controls.Add(tabBtns[i]);
        }
        // Activate first tab
        tabBtns[0].PerformClick();

        // ── Border painting ──────────────────────────────────────────────────
        Paint += (_, e) =>
        {
            using var pen = new Pen(Border, 1);
            e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
        };

        // ── Assemble ─────────────────────────────────────────────────────────
        Controls.Add(pages[2]);
        Controls.Add(pages[1]);
        Controls.Add(pages[0]);
        Controls.Add(tabBar);
        Controls.Add(titleBar);

        // Resize event: keep closeBtn right-anchored
        titleBar.Resize += (_, _) => closeBtn.Left = titleBar.Width - 44;
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  NOTES PAGE
    // ═══════════════════════════════════════════════════════════════════════════

    private Control BuildNotesPage()
    {
        var p = new Panel { Dock = DockStyle.Fill, BackColor = BgBase, Padding = new Padding(10) };

        var header = new Label
        {
            Text      = "Developer Notes",
            Font      = new Font("Segoe UI", 9F, FontStyle.Bold),
            ForeColor = AccentGreen,
            AutoSize  = true,
            Location  = new Point(10, 10)
        };

        _notesBox = new RichTextBox
        {
            Dock        = DockStyle.Fill,
            BackColor   = BgInput,
            ForeColor   = TextPrimary,
            Font        = new Font("Consolas", 10F),
            BorderStyle = BorderStyle.None,
            Margin      = new Padding(10),
            AcceptsTab  = true,
            ScrollBars  = RichTextBoxScrollBars.Vertical
        };
        _notesBox.TextChanged += (_, _) => SaveNotes();

        var saveLabel = new Label
        {
            Text      = "Auto-saved",
            Font      = new Font("Segoe UI", 7.5F),
            ForeColor = TextMuted,
            AutoSize  = true,
            Dock      = DockStyle.Bottom
        };

        var inner = new Panel { Dock = DockStyle.Fill, Padding = new Padding(10, 32, 10, 8) };
        inner.Controls.Add(_notesBox);
        inner.Controls.Add(saveLabel);

        p.Controls.Add(inner);
        p.Controls.Add(header);
        return p;
    }

    private void LoadNotes()
    {
        if (File.Exists(_notesPath))
            _notesBox.Text = File.ReadAllText(_notesPath);
    }

    private void SaveNotes()
    {
        try { File.WriteAllText(_notesPath, _notesBox.Text); } catch { }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  CHAT PAGE
    // ═══════════════════════════════════════════════════════════════════════════

    private Control BuildChatPage()
    {
        var p = new Panel { Dock = DockStyle.Fill, BackColor = BgBase, Padding = new Padding(10) };

        var header = new Label
        {
            Text      = "Dev Chat  (shared via data/dev_chat.json)",
            Font      = new Font("Segoe UI", 8F, FontStyle.Bold),
            ForeColor = AccentBlue,
            AutoSize  = true,
            Location  = new Point(10, 10)
        };

        _chatDisplay = new RichTextBox
        {
            Dock        = DockStyle.Fill,
            BackColor   = BgInput,
            ForeColor   = TextPrimary,
            Font        = new Font("Consolas", 9.5F),
            BorderStyle = BorderStyle.None,
            ReadOnly    = true,
            ScrollBars  = RichTextBoxScrollBars.Vertical
        };

        var inputRow = new Panel
        {
            Dock      = DockStyle.Bottom,
            Height    = 42,
            BackColor = BgCard
        };

        _chatInput = new TextBox
        {
            PlaceholderText = "Type a message… (Enter to send)",
            Dock            = DockStyle.Fill,
            BackColor       = BgInput,
            ForeColor       = TextPrimary,
            BorderStyle     = BorderStyle.None,
            Font            = new Font("Segoe UI", 9.5F),
            Margin          = new Padding(8, 11, 0, 0)
        };

        var sendBtn = new Button
        {
            Text      = "↵",
            Width     = 40,
            Dock      = DockStyle.Right,
            FlatStyle = FlatStyle.Flat,
            BackColor = AccentBlue,
            ForeColor = Color.White,
            Font      = new Font("Segoe UI", 11F, FontStyle.Bold),
            Cursor    = Cursors.Hand
        };
        sendBtn.FlatAppearance.BorderSize = 0;
        sendBtn.Click += (_, _) => SendChat();
        _chatInput.KeyDown += (_, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; SendChat(); } };

        inputRow.Controls.Add(_chatInput);
        inputRow.Controls.Add(sendBtn);

        var inner = new Panel { Dock = DockStyle.Fill, Padding = new Padding(10, 32, 10, 0) };
        inner.Controls.Add(_chatDisplay);
        inner.Controls.Add(inputRow);

        p.Controls.Add(inner);
        p.Controls.Add(header);
        return p;
    }

    private void SendChat()
    {
        var text = _chatInput.Text.Trim();
        if (string.IsNullOrEmpty(text)) return;

        var msgs = LoadChatMessages();
        msgs.Add(new ChatMessage { User = Environment.UserName, Text = text, Time = DateTime.Now });
        if (msgs.Count > 200) msgs = msgs.TakeLast(200).ToList();
        SaveChatMessages(msgs);
        _chatInput.Clear();
        RefreshChat();
    }

    private void RefreshChat()
    {
        var msgs = LoadChatMessages();
        if (!IsHandleCreated) return;
        BeginInvoke(() =>
        {
            _chatDisplay.Clear();
            foreach (var m in msgs)
            {
                var ts = m.Time.ToString("HH:mm");
                _chatDisplay.SelectionColor = AccentBlue;
                _chatDisplay.AppendText($"[{ts}] {m.User}");
                _chatDisplay.SelectionColor = TextMuted;
                _chatDisplay.AppendText($":  ");
                _chatDisplay.SelectionColor = TextPrimary;
                _chatDisplay.AppendText($"{m.Text}\n");
            }
            _chatDisplay.ScrollToCaret();
        });
    }

    private List<ChatMessage> LoadChatMessages()
    {
        try
        {
            if (!File.Exists(_chatPath)) return new();
            var json = File.ReadAllText(_chatPath);
            return JsonSerializer.Deserialize<List<ChatMessage>>(json) ?? new();
        }
        catch { return new(); }
    }

    private void SaveChatMessages(List<ChatMessage> msgs)
    {
        try { File.WriteAllText(_chatPath, JsonSerializer.Serialize(msgs)); } catch { }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  CALCULATOR PAGE
    // ═══════════════════════════════════════════════════════════════════════════

    private Control BuildCalcPage()
    {
        var p = new Panel { Dock = DockStyle.Fill, BackColor = BgBase, Padding = new Padding(10, 8, 10, 10) };

        // Display
        _calcDisplay = new TextBox
        {
            Text        = "0",
            Dock        = DockStyle.Top,
            Height      = 56,
            BackColor   = BgInput,
            ForeColor   = TextPrimary,
            Font        = new Font("Consolas", 20F, FontStyle.Bold),
            BorderStyle = BorderStyle.None,
            TextAlign   = HorizontalAlignment.Right,
            ReadOnly    = true,
            Margin      = new Padding(0, 0, 0, 8)
        };

        // Button grid
        var grid = new TableLayoutPanel
        {
            Dock        = DockStyle.Fill,
            ColumnCount = 4,
            RowCount    = 5,
            BackColor   = BgBase,
            Padding     = new Padding(0, 4, 0, 0)
        };
        for (int i = 0; i < 4; i++)
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
        for (int i = 0; i < 5; i++)
            grid.RowStyles.Add(new RowStyle(SizeType.Percent, 20F));

        var layout = new[]
        {
            "C",   "±",   "%",   "÷",
            "7",   "8",   "9",   "×",
            "4",   "5",   "6",   "−",
            "1",   "2",   "3",   "+",
            "0",   ".",   "⌫",   "="
        };

        for (int i = 0; i < layout.Length; i++)
        {
            var lbl = layout[i];
            bool isOp  = lbl is "÷" or "×" or "−" or "+";
            bool isEq  = lbl == "=";
            bool isAct = lbl is "C" or "⌫";

            var btn = new Button
            {
                Text      = lbl,
                Dock      = DockStyle.Fill,
                FlatStyle = FlatStyle.Flat,
                BackColor = isEq  ? AccentBlue :
                            isOp  ? Color.FromArgb(45, 65, 100) :
                            isAct ? Color.FromArgb(80, 40, 40) :
                                    BgCard,
                ForeColor = isOp || isEq ? Color.White : TextPrimary,
                Font      = new Font("Segoe UI", 12F, isEq ? FontStyle.Bold : FontStyle.Regular),
                Margin    = new Padding(2),
                Cursor    = Cursors.Hand
            };
            btn.FlatAppearance.BorderSize = 0;
            btn.FlatAppearance.MouseOverBackColor = isEq  ? Color.FromArgb(80, 160, 255) :
                                                   isOp   ? Color.FromArgb(55, 80, 120) :
                                                            Color.FromArgb(35, 48, 72);
            var cap = lbl;
            btn.Click += (_, _) => CalcPress(cap);
            grid.Controls.Add(btn);
        }

        p.Controls.Add(grid);
        p.Controls.Add(_calcDisplay);
        return p;
    }

    private void CalcPress(string key)
    {
        switch (key)
        {
            case "C":
                _calcExpression = "";
                _calcNewInput   = true;
                _calcDisplay.Text = "0";
                break;

            case "⌫":
                if (_calcDisplay.Text.Length > 1)
                    _calcDisplay.Text = _calcDisplay.Text[..^1];
                else
                    _calcDisplay.Text = "0";
                _calcExpression = _calcDisplay.Text == "0" ? "" : _calcExpression[..^1];
                break;

            case "=":
                try
                {
                    // Normalise operators
                    var expr = _calcExpression
                        .Replace("÷", "/")
                        .Replace("×", "*")
                        .Replace("−", "-");
                    var result = EvaluateExpression(expr);
                    _calcDisplay.Text = FormatCalcResult(result);
                    _calcExpression   = _calcDisplay.Text;
                    _calcNewInput     = true;
                }
                catch
                {
                    _calcDisplay.Text = "Error";
                    _calcExpression   = "";
                    _calcNewInput     = true;
                }
                break;

            case "±":
                if (double.TryParse(_calcDisplay.Text, out var negVal))
                {
                    var toggled = (-negVal).ToString("G");
                    _calcDisplay.Text = toggled;
                    // Replace last number in expression
                    var parts = System.Text.RegularExpressions.Regex
                        .Split(_calcExpression, @"(?<=[+\-×÷])|(?=[+\-×÷])");
                    if (parts.Length > 0)
                        _calcExpression = _calcExpression[..^parts[^1].Length] + toggled;
                }
                break;

            case "%":
                if (double.TryParse(_calcDisplay.Text, out var pctVal))
                {
                    var pct = (pctVal / 100.0).ToString("G");
                    _calcDisplay.Text = pct;
                    var parts = System.Text.RegularExpressions.Regex
                        .Split(_calcExpression, @"(?<=[+\-×÷])|(?=[+\-×÷])");
                    if (parts.Length > 0)
                        _calcExpression = _calcExpression[..^parts[^1].Length] + pct;
                }
                break;

            case "÷": case "×": case "−": case "+":
                if (!string.IsNullOrEmpty(_calcDisplay.Text) && _calcDisplay.Text != "Error")
                {
                    _calcExpression += _calcDisplay.Text + key;
                    _calcDisplay.Text = key;
                    _calcNewInput     = true;
                }
                break;

            default: // digit or "."
                if (_calcNewInput)
                {
                    _calcDisplay.Text = key == "." ? "0." : key;
                    _calcNewInput     = false;
                }
                else
                {
                    if (key == "." && _calcDisplay.Text.Contains('.')) break;
                    _calcDisplay.Text = _calcDisplay.Text == "0" && key != "."
                        ? key
                        : _calcDisplay.Text + key;
                }
                break;
        }
    }

    /// <summary>Simple recursive-descent expression evaluator — no eval() needed.</summary>
    private static double EvaluateExpression(string expr)
    {
        expr = expr.Trim();
        // Handle last operator with nothing after (e.g. "5+")
        if (string.IsNullOrEmpty(expr)) return 0;

        // Look for + or - (lowest precedence, scan right-to-left)
        int depth = 0;
        for (int i = expr.Length - 1; i >= 0; i--)
        {
            char c = expr[i];
            if (c == ')') depth++;
            else if (c == '(') depth--;
            else if (depth == 0 && (c == '+' || c == '-') && i > 0)
            {
                double left  = EvaluateExpression(expr[..i]);
                double right = EvaluateExpression(expr[(i + 1)..]);
                return c == '+' ? left + right : left - right;
            }
        }
        // Look for * or /
        for (int i = expr.Length - 1; i >= 0; i--)
        {
            char c = expr[i];
            if (depth == 0 && (c == '*' || c == '/') && i > 0)
            {
                double left  = EvaluateExpression(expr[..i]);
                double right = EvaluateExpression(expr[(i + 1)..]);
                return c == '*' ? left * right : left / right;
            }
        }
        // Strip parentheses
        if (expr.StartsWith('(') && expr.EndsWith(')'))
            return EvaluateExpression(expr[1..^1]);

        return double.Parse(expr, System.Globalization.CultureInfo.InvariantCulture);
    }

    private static string FormatCalcResult(double v)
    {
        if (double.IsInfinity(v) || double.IsNaN(v)) return "Error";
        // Show up to 10 sig figs, strip trailing zeros
        return v == Math.Floor(v) && Math.Abs(v) < 1e12
            ? ((long)v).ToString()
            : v.ToString("G10");
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  Lifecycle
    // ═══════════════════════════════════════════════════════════════════════════

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        // Hide instead of destroy so re-opening is instant
        if (e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            Hide();
        }
        base.OnFormClosing(e);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _chatPollTimer?.Dispose();
        base.Dispose(disposing);
    }

    // ── Chat message model ────────────────────────────────────────────────────
    private record ChatMessage
    {
        public string   User { get; init; } = "";
        public string   Text { get; init; } = "";
        public DateTime Time { get; init; }
    }
}
