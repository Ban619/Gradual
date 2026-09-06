using Gradual.Infrastructure;
using Gradual.Interfaces;
using Gradual.Models;

namespace Gradual.Forms;

/// <summary>
/// Analytics dashboard — Features 71–80.
/// Renders charts and KPI cards using pure GDI+ (no external charting lib needed).
/// </summary>
public class Analytics_Dashboard : Form
{
    private readonly IProjectService _service;
    private List<ProjectRecord> _projects = new();

    // Colors
    private static readonly Color BgColor     = Color.FromArgb(13, 17, 23);
    private static readonly Color CardColor   = Color.FromArgb(22, 27, 34);
    private static readonly Color AccentBlue  = Color.FromArgb(88, 166, 255);
    private static readonly Color AccentGreen = Color.FromArgb(63, 185, 80);
    private static readonly Color AccentRed   = Color.FromArgb(248, 81, 73);
    private static readonly Color AccentAmber = Color.FromArgb(210, 153, 34);
    private static readonly Color TextColor   = Color.FromArgb(201, 209, 217);
    private static readonly Color MutedColor  = Color.FromArgb(139, 148, 158);

    // Chart panels (redrawn on data change)
    private Panel _clientLeaderboard = null!;
    private Panel _trendChart        = null!;
    private Panel _statusDonut       = null!;
    private Panel _priorityBar       = null!;
    private Panel _burndownChart     = null!;
    private Label _productivityLabel = null!;

    public Analytics_Dashboard(IProjectService service)
    {
        _service = service;
        BuildUI();
        _ = LoadDataAsync();
    }

    private void BuildUI()
    {
        Text = "📊 Analytics Dashboard";
        Size = new Size(1100, 720);
        MinimumSize = new Size(900, 600);
        BackColor = BgColor;
        ForeColor = TextColor;
        Font = new Font("Segoe UI", 9.5f);
        StartPosition = FormStartPosition.CenterScreen;

        // ── Top KPI bar ───────────────────────────────────────────────────────
        var kpiBar = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 100,
            BackColor = Color.FromArgb(8, 12, 18),
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Padding = new Padding(12, 10, 12, 10),
            AutoScroll = false
        };

        var kpiNames = new[] { "Total", "Active", "Overdue", "Starred", "Archived" };
        foreach (var name in kpiNames)
            kpiBar.Controls.Add(BuildKpiCard(name, "–", Color.FromArgb(30, 37, 44)));

        // ── Refresh button in kpi bar
        var refreshBtn = new Button
        {
            Text = "⟳ Refresh",
            Width = 90, Height = 34,
            BackColor = Color.FromArgb(36, 60, 100),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9f),
            Cursor = Cursors.Hand,
            Margin = new Padding(20, 22, 0, 0)
        };
        refreshBtn.FlatAppearance.BorderSize = 0;
        refreshBtn.Click += async (s, e) => await LoadDataAsync();
        kpiBar.Controls.Add(refreshBtn);

        // ── Export dashboard button
        var exportBtn = new Button
        {
            Text = "📷 Export PNG",
            Width = 110, Height = 34,
            BackColor = Color.FromArgb(30, 70, 30),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9f),
            Cursor = Cursors.Hand,
            Margin = new Padding(8, 22, 0, 0)
        };
        exportBtn.FlatAppearance.BorderSize = 0;
        exportBtn.Click += ExportAsPng_Click;
        kpiBar.Controls.Add(exportBtn);

        // ── Main grid ─────────────────────────────────────────────────────────
        var grid = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 2,
            Padding = new Padding(10),
            BackColor = BgColor
        };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30));
        grid.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        grid.RowStyles.Add(new RowStyle(SizeType.Percent, 50));

        _clientLeaderboard = BuildChartCard("🏆 Client Leaderboard", out _);
        _trendChart        = BuildChartCard("📈 Monthly Trend", out _);
        _statusDonut       = BuildChartCard("🍩 Status Distribution", out _);
        _priorityBar       = BuildChartCard("📊 Priority Breakdown", out _);
        _burndownChart     = BuildChartCard("📉 Completion Burndown", out _);

        var productivityCard = BuildChartCard("⚡ Productivity Score", out var prodInner);
        _productivityLabel = new Label
        {
            Font = new Font("Segoe UI", 36, FontStyle.Bold),
            ForeColor = AccentGreen,
            TextAlign = ContentAlignment.MiddleCenter,
            Dock = DockStyle.Fill,
            Text = "–"
        };
        prodInner.Controls.Add(_productivityLabel);

        grid.Controls.Add(_clientLeaderboard, 0, 0);
        grid.Controls.Add(_trendChart, 1, 0);
        grid.Controls.Add(_statusDonut, 2, 0);
        grid.Controls.Add(_priorityBar, 0, 1);
        grid.Controls.Add(_burndownChart, 1, 1);
        grid.Controls.Add(productivityCard, 2, 1);

        Controls.Add(grid);
        Controls.Add(kpiBar);
    }

    // ── Feature 71 — Client Leaderboard ──────────────────────────────────────
    private void DrawClientLeaderboard(Panel panel)
    {
        panel.Controls.Clear();
        var groups = _projects
            .Where(p => !string.IsNullOrWhiteSpace(p.Client))
            .GroupBy(p => p.Client)
            .OrderByDescending(g => g.Count())
            .Take(8)
            .ToList();

        if (!groups.Any()) return;
        int maxCount = groups.First().Count();

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = groups.Count,
            BackColor = Color.Transparent,
            Padding = new Padding(8, 4, 8, 4)
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 36));

        for (int i = 0; i < groups.Count; i++)
        {
            var g = groups[i];
            var bar = (float)g.Count() / maxCount;
            var rank = i == 0 ? "🥇" : i == 1 ? "🥈" : i == 2 ? "🥉" : $"  {i+1}.";

            var nameLbl = new Label
            {
                Text = $"{rank} {g.Key.Truncate(14)}",
                ForeColor = TextColor, AutoSize = false,
                Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font("Segoe UI", 9f),
                Margin = new Padding(0, 2, 4, 2)
            };

            var barPanel = new Panel
            {
                Dock = DockStyle.Fill, BackColor = Color.FromArgb(30, 37, 44),
                Height = 20, Margin = new Padding(0, 4, 4, 4)
            };
            var barFill = new Panel
            {
                Width = (int)(barPanel.Width == 0 ? 100 * bar : barPanel.Width * bar),
                Dock = DockStyle.Left,
                BackColor = i == 0 ? AccentAmber : AccentBlue
            };
            barPanel.Controls.Add(barFill);
            barPanel.Resize += (s, e) => barFill.Width = (int)(barPanel.Width * bar);

            var cntLbl = new Label
            {
                Text = g.Count().ToString(),
                ForeColor = MutedColor, AutoSize = false,
                Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI", 9f)
            };

            layout.Controls.Add(nameLbl, 0, i);
            layout.Controls.Add(barPanel, 1, i);
            layout.Controls.Add(cntLbl, 2, i);
        }
        panel.Controls.Add(layout);
    }

    // ── Feature 72 — Monthly Creation Trend ───────────────────────────────────
    private void DrawMonthlyTrend(Panel panel)
    {
        panel.Invalidate();
        panel.Paint -= MonthlyTrend_Paint;
        panel.Tag = _projects;
        panel.Paint += MonthlyTrend_Paint;
    }

    private void MonthlyTrend_Paint(object? sender, PaintEventArgs e)
    {
        if (sender is not Panel panel || panel.Tag is not List<ProjectRecord> projs) return;
        var g = e.Graphics;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

        var months = projs
            .GroupBy(p => new { p.CreatedAt.Year, p.CreatedAt.Month })
            .OrderBy(x => x.Key.Year).ThenBy(x => x.Key.Month)
            .TakeLast(6).ToList();

        if (months.Count < 2) return;

        int maxVal = months.Max(m => m.Count());
        int pad = 28, right = 16, top = 20, bottom = 36;
        float chartW = panel.Width - pad - right;
        float chartH = panel.Height - top - bottom;
        float stepX = chartW / (months.Count - 1);

        // Axes
        using var axisPen = new Pen(Color.FromArgb(50, 60, 70));
        g.DrawLine(axisPen, pad, top, pad, panel.Height - bottom);
        g.DrawLine(axisPen, pad, panel.Height - bottom, panel.Width - right, panel.Height - bottom);

        // Line
        var pts = months.Select((m, i) =>
            new PointF(pad + i * stepX, top + chartH * (1f - (float)m.Count() / maxVal))
        ).ToArray();

        using var linePen = new Pen(AccentBlue, 2.5f);
        g.DrawLines(linePen, pts);

        // Dots + labels
        foreach (var (pt, m) in pts.Zip(months))
        {
            g.FillEllipse(new SolidBrush(AccentBlue), pt.X - 5, pt.Y - 5, 10, 10);
            var lbl = $"{m.Key.Month}/{m.Key.Year % 100}";
            g.DrawString(lbl, new Font("Segoe UI", 7f), new SolidBrush(MutedColor),
                pt.X - 16, panel.Height - bottom + 4);
            g.DrawString(m.Count().ToString(), new Font("Segoe UI", 7.5f, FontStyle.Bold),
                new SolidBrush(TextColor), pt.X - 8, pt.Y - 18);
        }
    }

    // ── Feature 74 — Status Donut ─────────────────────────────────────────────
    private void DrawStatusDonut(Panel panel)
    {
        panel.Paint -= StatusDonut_Paint;
        panel.Tag = _projects;
        panel.Paint += StatusDonut_Paint;
        panel.Invalidate();
    }

    private void StatusDonut_Paint(object? sender, PaintEventArgs e)
    {
        if (sender is not Panel panel || panel.Tag is not List<ProjectRecord> projs) return;
        var g = e.Graphics;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

        var groups = projs.GroupBy(p => p.Status)
            .OrderByDescending(x => x.Count()).Take(6).ToList();
        if (!groups.Any()) return;

        int total = projs.Count;
        var colors = new[] { AccentBlue, AccentGreen, AccentAmber, AccentRed,
            Color.FromArgb(139, 100, 200), Color.FromArgb(200, 80, 140) };

        int donutSize = Math.Min(panel.Width, panel.Height) - 60;
        int x = (panel.Width - donutSize) / 2;
        int y = (panel.Height - donutSize) / 2;
        var rect = new Rectangle(x, y, donutSize, donutSize);
        var inner = new Rectangle(x + donutSize / 4, y + donutSize / 4, donutSize / 2, donutSize / 2);

        float startAngle = -90f;
        for (int i = 0; i < groups.Count; i++)
        {
            float sweep = 360f * groups[i].Count() / total;
            g.FillPie(new SolidBrush(colors[i % colors.Length]), rect, startAngle, sweep);
            startAngle += sweep;
        }

        // Donut hole
        g.FillEllipse(new SolidBrush(CardColor), inner);
        g.DrawString($"{total}\nProjects", new Font("Segoe UI", 9, FontStyle.Bold),
            new SolidBrush(TextColor), inner, new StringFormat
                { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center });

        // Legend
        float legendY = 8;
        for (int i = 0; i < groups.Count; i++)
        {
            var pct = 100f * groups[i].Count() / total;
            g.FillRectangle(new SolidBrush(colors[i % colors.Length]), 8, legendY, 12, 12);
            g.DrawString($"{groups[i].Key} ({pct:F0}%)", new Font("Segoe UI", 7.5f),
                new SolidBrush(TextColor), 24, legendY);
            legendY += 16;
        }
    }

    // ── Feature 77 — Priority bar chart ──────────────────────────────────────
    private void DrawPriorityBar(Panel panel)
    {
        panel.Paint -= PriorityBar_Paint;
        panel.Tag = _projects;
        panel.Paint += PriorityBar_Paint;
        panel.Invalidate();
    }

    private void PriorityBar_Paint(object? sender, PaintEventArgs e)
    {
        if (sender is not Panel panel || panel.Tag is not List<ProjectRecord> projs) return;
        var g = e.Graphics;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

        var order = new[] { "Critical", "High", "Medium", "Low" };
        var colorMap = new Dictionary<string, Color>
        {
            ["Critical"] = AccentRed, ["High"] = AccentAmber,
            ["Medium"] = AccentBlue,  ["Low"] = AccentGreen
        };

        var counts = order.Select(o => (name: o, count: projs.Count(p => p.Priority == o))).ToList();
        int maxCount = counts.Max(c => c.count);
        if (maxCount == 0) return;

        int barH = 30, gap = 10, padL = 70, padR = 40, padT = 10;
        for (int i = 0; i < counts.Count; i++)
        {
            var (name, count) = counts[i];
            float barW = count == 0 ? 0 : (panel.Width - padL - padR) * (float)count / maxCount;
            int y = padT + i * (barH + gap);

            g.DrawString(name, new Font("Segoe UI", 9f),
                new SolidBrush(MutedColor), 4, y + 6);
            g.FillRectangle(new SolidBrush(colorMap.GetValueOrDefault(name, AccentBlue)),
                padL, y, barW, barH);
            g.DrawString(count.ToString(), new Font("Segoe UI", 9, FontStyle.Bold),
                new SolidBrush(TextColor), padL + barW + 6, y + 7);
        }
    }

    // ── Feature 73 — Burndown chart (completion % histogram) ─────────────────
    private void DrawBurndown(Panel panel)
    {
        panel.Paint -= Burndown_Paint;
        panel.Tag = _projects;
        panel.Paint += Burndown_Paint;
        panel.Invalidate();
    }

    private void Burndown_Paint(object? sender, PaintEventArgs e)
    {
        if (sender is not Panel panel || panel.Tag is not List<ProjectRecord> projs) return;
        var g = e.Graphics;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

        var buckets = new int[5]; // 0-20, 21-40, 41-60, 61-80, 81-100
        foreach (var p in projs)
        {
            int idx = Math.Min(4, p.CompletionPercent / 20);
            buckets[idx]++;
        }
        int maxBucket = buckets.Max();
        if (maxBucket == 0) return;

        string[] labels = { "0–20%", "21–40%", "41–60%", "61–80%", "81–100%" };
        var palette = new[] { AccentRed, AccentAmber, AccentBlue, AccentGreen,
            Color.FromArgb(100, 220, 120) };

        int padL = 10, padR = 10, padT = 10, padB = 40;
        float barW = (panel.Width - padL - padR) / 5f - 8;
        float chartH = panel.Height - padT - padB;

        for (int i = 0; i < 5; i++)
        {
            float h = maxBucket > 0 ? chartH * buckets[i] / maxBucket : 0;
            float bx = padL + i * ((panel.Width - padL - padR) / 5f) + 4;
            float by = padT + chartH - h;

            var rect = new RectangleF(bx, by, barW, h);
            g.FillRectangle(new SolidBrush(palette[i]), rect);
            g.DrawString(labels[i], new Font("Segoe UI", 7f),
                new SolidBrush(MutedColor), bx, panel.Height - padB + 4);
            if (buckets[i] > 0)
                g.DrawString(buckets[i].ToString(), new Font("Segoe UI", 8f, FontStyle.Bold),
                    new SolidBrush(TextColor), bx + barW / 2 - 6, by - 16);
        }
    }

    // ── Feature 79 — Productivity Score ──────────────────────────────────────
    private int CalcProductivityScore()
    {
        if (!_projects.Any()) return 0;
        double completed  = _projects.Count(p => p.CompletionPercent >= 100);
        double total      = _projects.Count;
        double noOverdue  = _projects.Count(p => !p.IsOverdue);
        double score = ((completed / total) * 50) + ((noOverdue / total) * 30)
            + (Math.Min(10, total) * 2); // 20 pts for volume
        return (int)Math.Min(100, score);
    }

    // ── KPI card builder ──────────────────────────────────────────────────────
    private static Panel BuildKpiCard(string title, string value, Color bg)
    {
        var card = new Panel
        {
            Width = 150, Height = 80,
            BackColor = bg,
            Margin = new Padding(6),
            Padding = new Padding(10, 6, 10, 6)
        };
        var titleLbl = new Label
        {
            Text = title, Dock = DockStyle.Top, Height = 26,
            ForeColor = Color.FromArgb(139, 148, 158),
            Font = new Font("Segoe UI", 8.5f)
        };
        var valueLbl = new Label
        {
            Text = value, Dock = DockStyle.Fill, Name = $"kpi_{title}",
            ForeColor = Color.FromArgb(201, 209, 217),
            Font = new Font("Segoe UI", 22, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft
        };
        card.Controls.Add(valueLbl);
        card.Controls.Add(titleLbl);
        return card;
    }

    private Panel BuildChartCard(string title, out Panel inner)
    {
        var outer = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = CardColor,
            Margin = new Padding(5),
            Padding = new Padding(12, 8, 8, 8)
        };
        outer.Paint += (s, e) =>
        {
            using var p = new Pen(Color.FromArgb(48, 54, 61));
            e.Graphics.DrawRectangle(p, 0, 0, outer.Width - 1, outer.Height - 1);
        };

        var titleLbl = new Label
        {
            Text = title, Dock = DockStyle.Top, Height = 28,
            Font = new Font("Segoe UI", 10, FontStyle.Bold),
            ForeColor = AccentBlue
        };
        inner = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent };
        outer.Controls.Add(inner);
        outer.Controls.Add(titleLbl);
        return outer;
    }

    // ── Feature 80 — Export dashboard as PNG ─────────────────────────────────
    private void ExportAsPng_Click(object? sender, EventArgs e)
    {
        using var dlg = new SaveFileDialog
        {
            Filter = "PNG Image|*.png",
            FileName = $"analytics_{DateTime.Today:yyyy-MM-dd}.png"
        };
        if (dlg.ShowDialog() != DialogResult.OK) return;

        using var bmp = new Bitmap(ClientSize.Width, ClientSize.Height);
        DrawToBitmap(bmp, new Rectangle(Point.Empty, ClientSize));
        bmp.Save(dlg.FileName, System.Drawing.Imaging.ImageFormat.Png);
        MessageBox.Show($"Saved: {dlg.FileName}", "Export PNG",
            MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    // ── Data load ─────────────────────────────────────────────────────────────
    private async Task LoadDataAsync()
    {
        var result = await _service.GetAllProjectsAsync();
        if (!result.Success || result.Data == null) return;
        _projects = result.Data.Where(p => !p.IsDeleted).ToList();

        BeginInvoke(() =>
        {
            UpdateKpiCards();
            DrawClientLeaderboard(_clientLeaderboard.Controls.OfType<Panel>().FirstOrDefault()
                ?? GetInnerPanel(_clientLeaderboard));
            DrawMonthlyTrend(GetInnerPanel(_trendChart));
            DrawStatusDonut(GetInnerPanel(_statusDonut));
            DrawPriorityBar(GetInnerPanel(_priorityBar));
            DrawBurndown(GetInnerPanel(_burndownChart));
            _productivityLabel.Text = CalcProductivityScore().ToString();
            _productivityLabel.ForeColor = CalcProductivityScore() >= 70
                ? AccentGreen : CalcProductivityScore() >= 40 ? AccentAmber : AccentRed;
        });
    }

    private Panel GetInnerPanel(Panel card) =>
        card.Controls.OfType<Panel>().FirstOrDefault(p => p.Dock == DockStyle.Fill)
        ?? card;

    private void UpdateKpiCards()
    {
        var kpiBar = Controls.OfType<FlowLayoutPanel>().FirstOrDefault();
        if (kpiBar == null) return;

        var vals = new[]
        {
            _projects.Count.ToString(),
            _projects.Count(p => p.Status == "Active").ToString(),
            _projects.Count(p => p.IsOverdue).ToString(),
            _projects.Count(p => p.IsStarred).ToString(),
            _projects.Count(p => p.Status == "Archived").ToString()
        };

        int i = 0;
        foreach (var card in kpiBar.Controls.OfType<Panel>().Take(5))
        {
            var lbl = card.Controls.OfType<Label>()
                .FirstOrDefault(l => l.Name.StartsWith("kpi_"));
            if (lbl != null) lbl.Text = vals[i++];
        }
    }
}
