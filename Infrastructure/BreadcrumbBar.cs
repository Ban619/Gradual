namespace Gradual.Infrastructure;

/// <summary>
/// Feature 68 — Breadcrumb navigation bar.
/// A lightweight panel that shows "Home > Section > Sub-section" style navigation.
/// Add it to a parent container, then call Navigate() to push a new crumb.
/// </summary>
public sealed class BreadcrumbBar : Panel
{
    private static readonly Color BgClr     = Color.FromArgb(8, 12, 18);
    private static readonly Color CrumbClr  = Color.FromArgb(88, 166, 255);
    private static readonly Color SepClr    = Color.FromArgb(48, 54, 61);
    private static readonly Color ActiveClr = Color.FromArgb(201, 209, 217);

    private readonly Stack<(string label, Action? onClick)> _crumbs = new();
    private readonly FlowLayoutPanel _flow;

    /// <summary>Fires when user clicks a breadcrumb to navigate back.</summary>
    public event EventHandler<string>? Navigated;

    public BreadcrumbBar()
    {
        Dock      = DockStyle.Top;
        Height    = 30;
        BackColor = BgClr;
        Padding   = new Padding(8, 5, 8, 5);

        _flow = new FlowLayoutPanel
        {
            Dock            = DockStyle.Fill,
            FlowDirection   = FlowDirection.LeftToRight,
            WrapContents    = false,
            BackColor       = Color.Transparent,
            AutoSize        = false
        };
        Controls.Add(_flow);
    }

    /// <summary>Push a new level onto the breadcrumb trail.</summary>
    public void Navigate(string label, Action? onClick = null)
    {
        _crumbs.Push((label, onClick));
        Rebuild();
    }

    /// <summary>Pop the last crumb and navigate back.</summary>
    public void GoBack()
    {
        if (_crumbs.Count > 1)
        {
            _crumbs.Pop();
            Rebuild();
            var top = _crumbs.Peek();
            top.onClick?.Invoke();
            Navigated?.Invoke(this, top.label);
        }
    }

    /// <summary>Reset to a single root crumb.</summary>
    public void SetRoot(string label, Action? onClick = null)
    {
        _crumbs.Clear();
        _crumbs.Push((label, onClick));
        Rebuild();
    }

    public string CurrentCrumb => _crumbs.TryPeek(out var c) ? c.label : "";

    // ── Rebuild the visual trail ──────────────────────────────────────────────
    private void Rebuild()
    {
        _flow.SuspendLayout();
        _flow.Controls.Clear();

        var items = _crumbs.ToArray();
        Array.Reverse(items); // stack is LIFO, we want first-pushed first

        for (int i = 0; i < items.Length; i++)
        {
            var (label, action) = items[i];
            bool isLast = i == items.Length - 1;

            var lbl = new Label
            {
                Text      = label,
                AutoSize  = true,
                Font      = new Font("Segoe UI", 8.5f, isLast ? FontStyle.Bold : FontStyle.Regular),
                ForeColor = isLast ? ActiveClr : CrumbClr,
                Cursor    = isLast ? Cursors.Default : Cursors.Hand,
                Padding   = new Padding(0, 1, 0, 0),
                Margin    = new Padding(0)
            };

            if (!isLast)
            {
                var capturedAction = action;
                var capturedLabel  = label;
                // Pop back to this crumb on click
                lbl.Click += (s, e) =>
                {
                    while (_crumbs.Count > 0 && _crumbs.Peek().label != capturedLabel)
                        _crumbs.Pop();
                    capturedAction?.Invoke();
                    Rebuild();
                    Navigated?.Invoke(this, capturedLabel);
                };
                // Hover underline effect
                lbl.MouseEnter += (s, e) => lbl.Font = new Font("Segoe UI", 8.5f, FontStyle.Underline);
                lbl.MouseLeave += (s, e) => lbl.Font = new Font("Segoe UI", 8.5f, FontStyle.Regular);
            }

            _flow.Controls.Add(lbl);

            // Separator
            if (!isLast)
            {
                _flow.Controls.Add(new Label
                {
                    Text      = " › ",
                    AutoSize  = true,
                    ForeColor = SepClr,
                    Font      = new Font("Segoe UI", 8.5f),
                    Margin    = new Padding(0),
                    Padding   = new Padding(0, 1, 0, 0)
                });
            }
        }

        _flow.ResumeLayout(performLayout: true);
    }
}
