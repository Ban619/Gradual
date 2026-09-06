using Gradual.Infrastructure;
using Gradual.Services;

namespace Gradual.Forms;

/// <summary>
/// Slide-in notification center panel (Features 52–54, 56).
/// Docked to the right edge of the main form as a modeless overlay.
/// </summary>
public class NCP : Form
{
    private readonly NotificationService _notificationService;

    private FlowLayoutPanel _listPanel = null!;
    private Label _unreadBadge = null!;
    private Button _markAllReadBtn = null!;
    private Button _dndToggleBtn = null!;
    private Panel _overdueBanner = null!;
    private Label _overdueBannerLabel = null!;

    private static readonly Color Bg        = Color.FromArgb(13, 17, 23);
    private static readonly Color CardColor = Color.FromArgb(22, 27, 34);
    private static readonly Color Border    = Color.FromArgb(48, 54, 61);
    private static readonly Color TextColor = Color.FromArgb(201, 209, 217);
    private static readonly Color MutedText = Color.FromArgb(139, 148, 158);
    private static readonly Color CritColor = Color.FromArgb(248, 81, 73);
    private static readonly Color WarnColor = Color.FromArgb(210, 153, 34);
    private static readonly Color InfoColor = Color.FromArgb(88, 166, 255);

    public NCP(NotificationService notificationService)
    {
        _notificationService = notificationService;
        _notificationService.NotificationPosted += (s, n) =>
        {
            if (IsHandleCreated)
                BeginInvoke(() => _ = RefreshAsync());
        };
        BuildUI();
        _ = RefreshAsync();
    }

    private void BuildUI()
    {
        Text = "Notifications";
        Size = new Size(370, 700);
        FormBorderStyle = FormBorderStyle.SizableToolWindow;
        BackColor = Bg;
        ForeColor = TextColor;
        Font = new Font("Segoe UI", 9.5f);
        StartPosition = FormStartPosition.Manual;
        var wa = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1920, 1080);
        Location = new Point(wa.Right - Width - 20, wa.Top + 40);
        TopMost = false;

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 4,
            ColumnCount = 1,
            Padding = new Padding(10),
            BackColor = Bg
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44)); // header
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40)); // controls
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 0));  // overdue banner (dynamic)
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // notification list

        // ── Header ────────────────────────────────────────────────────────────
        var headerRow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            BackColor = Color.Transparent
        };

        var titleLbl = new Label
        {
            Text = "🔔 Notifications",
            Font = new Font("Segoe UI", 13, FontStyle.Bold),
            ForeColor = InfoColor,
            AutoSize = true,
            Margin = new Padding(0, 6, 8, 0)
        };

        _unreadBadge = new Label
        {
            Text = "0",
            Font = new Font("Segoe UI", 9, FontStyle.Bold),
            ForeColor = Color.White,
            BackColor = CritColor,
            AutoSize = true,
            Padding = new Padding(5, 3, 5, 3),
            Margin = new Padding(0, 6, 0, 0)
        };

        headerRow.Controls.Add(titleLbl);
        headerRow.Controls.Add(_unreadBadge);
        layout.Controls.Add(headerRow, 0, 0);

        // ── Controls ──────────────────────────────────────────────────────────
        var controlRow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            BackColor = Color.Transparent
        };

        _markAllReadBtn = new Button
        {
            Text = "✓ Mark all read",
            Height = 30,
            AutoSize = true,
            BackColor = Color.FromArgb(30, 37, 44),
            ForeColor = TextColor,
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand,
            Margin = new Padding(0, 0, 8, 0)
        };
        _markAllReadBtn.FlatAppearance.BorderColor = Border;
        _markAllReadBtn.Click += async (s, e) => { await _notificationService.MarkAllReadAsync(); await RefreshAsync(); };

        _dndToggleBtn = new Button
        {
            Text = "🔕 DND: Off",
            Height = 30,
            AutoSize = true,
            BackColor = Color.FromArgb(30, 37, 44),
            ForeColor = MutedText,
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand
        };
        _dndToggleBtn.FlatAppearance.BorderColor = Border;
        _dndToggleBtn.Click += (s, e) =>
        {
            _notificationService.DoNotDisturb = !_notificationService.DoNotDisturb;
            _dndToggleBtn.Text = _notificationService.DoNotDisturb ? "🔕 DND: ON" : "🔕 DND: Off";
            _dndToggleBtn.ForeColor = _notificationService.DoNotDisturb ? WarnColor : MutedText;
        };

        controlRow.Controls.Add(_markAllReadBtn);
        controlRow.Controls.Add(_dndToggleBtn);
        layout.Controls.Add(controlRow, 0, 1);

        // ── Overdue Banner ────────────────────────────────────────────────────
        _overdueBanner = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(50, 20, 20),
            Visible = false,
            Height = 36
        };
        _overdueBanner.Paint += (s, e) =>
        {
            using var pen = new Pen(CritColor, 1);
            e.Graphics.DrawRectangle(pen, 0, 0, _overdueBanner.Width - 1, _overdueBanner.Height - 1);
        };
        _overdueBannerLabel = new Label
        {
            Dock = DockStyle.Fill,
            ForeColor = Color.FromArgb(255, 120, 120),
            Font = new Font("Segoe UI", 9, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(8, 0, 0, 0)
        };
        _overdueBanner.Controls.Add(_overdueBannerLabel);
        layout.Controls.Add(_overdueBanner, 0, 2);

        // ── Notification List ─────────────────────────────────────────────────
        var scroll = new Panel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            BackColor = Bg
        };

        _listPanel = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoSize = true,
            Width = 340,
            BackColor = Bg
        };

        scroll.Controls.Add(_listPanel);
        layout.Controls.Add(scroll, 0, 3);

        Controls.Add(layout);
    }

    public async Task RefreshAsync()
    {
        var all = await _notificationService.LoadAllAsync();
        var unread = all.Count(n => !n.IsRead);

        _unreadBadge.Text = unread.ToString();
        _unreadBadge.Visible = unread > 0;

        // Overdue banner
        var overdue = all.Where(n => n.Level == NotificationLevel.Critical && !n.IsRead).ToList();
        if (overdue.Any())
        {
            _overdueBanner.Visible = true;
            _overdueBannerLabel.Text = $"⚠ {overdue.Count} critical alert(s) — action required";
        }
        else
        {
            _overdueBanner.Visible = false;
        }

        _listPanel.Controls.Clear();
        foreach (var notif in all.OrderByDescending(n => n.CreatedAt).Take(50))
        {
            var card = CreateNotifCard(notif);
            _listPanel.Controls.Add(card);
        }

        if (!all.Any())
        {
            var empty = new Label
            {
                Text = "No notifications yet",
                ForeColor = MutedText,
                Font = new Font("Segoe UI", 10),
                AutoSize = true,
                Padding = new Padding(20)
            };
            _listPanel.Controls.Add(empty);
        }
    }

    private Panel CreateNotifCard(AppNotification notif)
    {
        var levelColor = notif.Level switch
        {
            NotificationLevel.Critical => CritColor,
            NotificationLevel.Warning  => WarnColor,
            _                          => InfoColor
        };

        var card = new Panel
        {
            Width = 336,
            Height = 72,
            BackColor = notif.IsRead ? CardColor : Color.FromArgb(28, 36, 52),
            Padding = new Padding(10, 8, 10, 8),
            Margin = new Padding(0, 0, 0, 4),
            Cursor = Cursors.Hand
        };
        card.Paint += (s, e) =>
        {
            using var pen = new Pen(levelColor, 2);
            e.Graphics.DrawLine(pen, 0, 0, 0, card.Height);
        };

        var icon = notif.Level switch
        {
            NotificationLevel.Critical => "⚠",
            NotificationLevel.Warning  => "⚡",
            _                          => "ℹ"
        };

        var titleLbl = new Label
        {
            Text = $"{icon} {notif.Title}",
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            ForeColor = notif.IsRead ? MutedText : TextColor,
            Location = new Point(14, 8),
            Size = new Size(290, 18),
            AutoEllipsis = true
        };

        var msgLbl = new Label
        {
            Text = notif.Message,
            Font = new Font("Segoe UI", 8.5f),
            ForeColor = MutedText,
            Location = new Point(14, 28),
            Size = new Size(290, 22),
            AutoEllipsis = true
        };

        var timeLbl = new Label
        {
            Text = GetTimeAgo(notif.CreatedAt),
            Font = new Font("Segoe UI", 7.5f),
            ForeColor = Color.FromArgb(80, 90, 110),
            Location = new Point(14, 52),
            AutoSize = true
        };

        card.Controls.Add(titleLbl);
        card.Controls.Add(msgLbl);
        card.Controls.Add(timeLbl);

        card.Click += async (s, e) => { await _notificationService.MarkReadAsync(notif.Id); await RefreshAsync(); };

        return card;
    }

    private static string GetTimeAgo(DateTime dt)
    {
        var span = DateTime.Now - dt;
        if (span.TotalMinutes < 1) return "just now";
        if (span.TotalMinutes < 60) return $"{(int)span.TotalMinutes}m ago";
        if (span.TotalHours < 24) return $"{(int)span.TotalHours}h ago";
        return $"{(int)span.TotalDays}d ago";
    }
}
