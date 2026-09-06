namespace Gradual;

partial class GradForm
{
    private System.ComponentModel.IContainer components = null;

    protected override void Dispose(bool disposing)
    {
        if (disposing && (components != null))
        {
            components.Dispose();
        }
        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        components = new System.ComponentModel.Container();
        this.AutoScaleDimensions = new SizeF(96F, 96F);
        this.AutoScaleMode = AutoScaleMode.Dpi;
        this.Text = "Gradual";
        this.ClientSize = new Size(1180, 720);
        this.StartPosition = FormStartPosition.CenterScreen;
        this.BackColor = Color.FromArgb(245, 247, 250);
        this.MinimumSize = new Size(980, 620);

        rootLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 2,
            Padding = new Padding(0),
            BackColor = Color.FromArgb(18, 27, 45)
        };
        rootLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180F));
        rootLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        rootLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 110F));
        rootLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        sidebarPanel = new Gradual.Controls.RoundedPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(11, 23, 40),
            BorderColor = Color.FromArgb(34, 54, 82),
            Radius = 0,
            Padding = new Padding(0),
            AutoScroll = false
        };

        navFlow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoSize = false,
            BackColor = Color.FromArgb(11, 23, 40),
            Padding = new Padding(0),
            AutoScroll = false
        };

        // App logo/brand at top
        var brandLabel = new Label
        {
            Text = "FT",
            AutoSize = false,
            Width = 180,
            Height = 56,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Segoe UI", 18F, FontStyle.Bold),
            ForeColor = Color.FromArgb(94, 129, 255),
            BackColor = Color.FromArgb(8, 17, 32),
            Margin = new Padding(0, 0, 0, 8)
        };
        navFlow.Controls.Add(brandLabel);

        // Separator
        var sep1 = new Panel { Width = 180, Height = 1, BackColor = Color.FromArgb(30, 50, 80), Margin = new Padding(0, 0, 0, 8) };
        navFlow.Controls.Add(sep1);

        var dutyBtn    = CreateNavItem("📋", "Duty",       true);
        var boardBtn   = CreateNavItem("📌", "Board",      false);
        var repoBtn    = CreateNavItem("📦", "Repository", false);
        var setupBtn   = CreateNavItem("⚙️", "Setup",      false);

        dutyBtn.Click    += navButton_Click;
        boardBtn.Click   += navButton_Click;
        repoBtn.Click    += navButton_Click;
        setupBtn.Click   += navButton_Click;

        navFlow.Controls.Add(dutyBtn);
        navFlow.Controls.Add(boardBtn);
        navFlow.Controls.Add(repoBtn);

        // Separator before Setup
        var sep2 = new Panel { Width = 180, Height = 1, BackColor = Color.FromArgb(30, 50, 80), Margin = new Padding(0, 8, 0, 8) };
        navFlow.Controls.Add(sep2);

        navFlow.Controls.Add(setupBtn);

        sidebarPanel.Controls.Add(navFlow);

        headerPanel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(8, 19, 36),
            Padding = new Padding(18, 14, 18, 12)
        };

        var titleLabel = new Label
        {
            Text = "Gradual",
            AutoSize = true,
            Font = new Font("Segoe UI", 22F, FontStyle.Bold),
            ForeColor = Color.White,
            Anchor = AnchorStyles.Left | AnchorStyles.Top
        };

        headerPanel.Controls.Add(titleLabel);


        toolbarFlow = new FlowLayoutPanel
        {
            Dock = DockStyle.Right,
            AutoSize = true,
            BackColor = Color.FromArgb(8, 19, 36),
            FlowDirection = FlowDirection.LeftToRight,
            Margin = new Padding(0)
        };

        var addButton = new Button { Text = "＋ Add", Width = 92, Height = 36, Margin = new Padding(0, 0, 8, 0), FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(60, 125, 255), ForeColor = Color.White };
        var saveButton = new Button { Text = "💾 Save", Width = 92, Height = 36, Margin = new Padding(0, 0, 8, 0), FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(36, 96, 176), ForeColor = Color.White };
        openFolderButton = new Button { Text = "📁 Open", Width = 92, Height = 36, Margin = new Padding(0, 0, 8, 0), FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(36, 96, 176), ForeColor = Color.White };
        exportButton = new Button { Text = "⬇️ Export", Width = 96, Height = 36, Margin = new Padding(0, 0, 8, 0), FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(36, 96, 176), ForeColor = Color.White };
        themeButton = new Button { Text = "🌙 Theme", Width = 100, Height = 36, FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(36, 96, 176), ForeColor = Color.White };
        toolbarFlow.Controls.Add(addButton);
        toolbarFlow.Controls.Add(saveButton);
        toolbarFlow.Controls.Add(openFolderButton);
        toolbarFlow.Controls.Add(exportButton);
        toolbarFlow.Controls.Add(themeButton);
        headerPanel.Controls.Add(toolbarFlow);



        titleLabel.Location = new Point(18, 12);
        toolbarFlow.Location = new Point(750, 12);

        projectListView = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true,
            GridLines = true,
            HideSelection = false,
            MultiSelect = false,
            Font = new Font("Segoe UI", 10F),
            Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
        };
        // Weights: Project=3, Client=2, Status=1.5, Priority=1.5, Folder=5, Updated=2  (total=15)
        // Folder intentionally has the largest weight so it always gets the most space.
        int[] projWeights = { 30, 20, 15, 15, 50, 20 };  // ×10 to avoid floats
        projectListView.Columns.Add("Project",  -2);
        projectListView.Columns.Add("Client",   -2);
        projectListView.Columns.Add("Status",   -2);
        projectListView.Columns.Add("Priority", -2);
        projectListView.Columns.Add("Folder",   -2);
        projectListView.Columns.Add("Updated",  -2);
        projectListView.Resize += (_, _) => ApplyListViewWeights(projectListView, projWeights);
        projectListView.HandleCreated += (_, _) => ApplyListViewWeights(projectListView, projWeights);

        contentLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = Color.FromArgb(245, 247, 250),
            Padding = new Padding(0)
        };
        contentLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 43F));
        contentLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 57F));
        contentLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        leftPanel = new Gradual.Controls.RoundedPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(18),
            BackColor = Color.FromArgb(245, 247, 250),
            BorderColor = Color.FromArgb(218, 226, 236),
            Radius = 20,
            AutoScroll = true
        };

        rightPanel = new Panel
        {
            Dock        = DockStyle.Fill,
            Padding     = new Padding(0),
            BackColor   = Color.FromArgb(24, 38, 60)
        };

        // Inner layout: table fills left, sidebar docks right — zero gap
        var rightPanelLayout = new TableLayoutPanel
        {
            Dock            = DockStyle.Fill,
            ColumnCount     = 2,
            RowCount        = 1,
            Padding         = new Padding(0),
            Margin          = new Padding(0),
            CellBorderStyle = TableLayoutPanelCellBorderStyle.None,
            BackColor       = Color.Transparent
        };
        rightPanelLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F)); // table fills
        rightPanelLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 148F)); // sidebar fixed
        rightPanelLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        // Table container — zero padding/margin so ListView truly fills end-to-end
        var dataAreaPanel = new Panel
        {
            Dock      = DockStyle.Fill,
            BackColor = Color.White,
            Padding   = new Padding(0),
            Margin    = new Padding(0)
        };

        // Sidebar — flush left edge, 1px painted separator instead of a gap
        var rightSidebar = new Panel
        {
            Dock      = DockStyle.Fill,
            BackColor = Color.FromArgb(22, 34, 56),
            Padding   = new Padding(10, 10, 8, 10),
            Margin    = new Padding(0)
        };
        // Paint a 1px left border as a thin separator line
        rightSidebar.Paint += (_, e) =>
        {
            using var pen = new Pen(Color.FromArgb(52, 76, 108), 1);
            e.Graphics.DrawLine(pen, 0, 0, 0, rightSidebar.Height);
        };

        var sidebarFlow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = false,
            BackColor = Color.Transparent,
            Padding = new Padding(0)
        };

        // Sidebar Title
        var sidebarTitle = new Label
        {
            Text      = "Quick",
            AutoSize  = false,
            Width     = 128,
            Height    = 28,
            Font      = new Font("Segoe UI", 10F, FontStyle.Bold),
            ForeColor = Color.FromArgb(220, 230, 245),
            TextAlign = ContentAlignment.MiddleLeft,
            Margin    = new Padding(0, 0, 0, 10)
        };
        sidebarFlow.Controls.Add(sidebarTitle);

        // Status Filter Section
        var statusLabel = new Label
        {
            Text      = "Filter Status",
            AutoSize  = false,
            Width     = 128,
            Height    = 22,
            Font      = new Font("Segoe UI", 8.5F, FontStyle.Bold),
            ForeColor = Color.FromArgb(180, 195, 220),
            TextAlign = ContentAlignment.MiddleLeft,
            Margin    = new Padding(0, 0, 0, 4)
        };
        sidebarFlow.Controls.Add(statusLabel);

        var statusAllBtn = new Button
        {
            Text      = "📊 All",
            Width     = 128,
            Height    = 32,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(52, 76, 112),
            ForeColor = Color.White,
            Font      = new Font("Segoe UI", 8.5F),
            TextAlign = ContentAlignment.MiddleLeft,
            Padding   = new Padding(6, 0, 0, 0),
            Margin    = new Padding(0, 0, 0, 3),
            Tag       = "All"
        };
        statusAllBtn.FlatAppearance.BorderSize = 0;
        statusAllBtn.Click += sidebarStatusFilter_Click;
        sidebarFlow.Controls.Add(statusAllBtn);

        var statusActiveBtn = new Button
        {
            Text      = "✓ Active",
            Width     = 128,
            Height    = 32,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(42, 66, 102),
            ForeColor = Color.White,
            Font      = new Font("Segoe UI", 8.5F),
            TextAlign = ContentAlignment.MiddleLeft,
            Padding   = new Padding(6, 0, 0, 0),
            Margin    = new Padding(0, 0, 0, 3),
            Tag       = "Active"
        };
        statusActiveBtn.FlatAppearance.BorderSize = 0;
        statusActiveBtn.Click += sidebarStatusFilter_Click;
        sidebarFlow.Controls.Add(statusActiveBtn);

        var statusHoldBtn = new Button
        {
            Text      = "⏸ On Hold",
            Width     = 128,
            Height    = 32,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(42, 66, 102),
            ForeColor = Color.White,
            Font      = new Font("Segoe UI", 8.5F),
            TextAlign = ContentAlignment.MiddleLeft,
            Padding   = new Padding(6, 0, 0, 0),
            Margin    = new Padding(0, 0, 0, 3),
            Tag       = "On Hold"
        };
        statusHoldBtn.FlatAppearance.BorderSize = 0;
        statusHoldBtn.Click += sidebarStatusFilter_Click;
        sidebarFlow.Controls.Add(statusHoldBtn);

        var statusCompletedBtn = new Button
        {
            Text      = "✔ Completed",
            Width     = 128,
            Height    = 32,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(42, 66, 102),
            ForeColor = Color.White,
            Font      = new Font("Segoe UI", 8.5F),
            TextAlign = ContentAlignment.MiddleLeft,
            Padding   = new Padding(6, 0, 0, 0),
            Margin    = new Padding(0, 0, 0, 12),
            Tag       = "Completed"
        };
        statusCompletedBtn.FlatAppearance.BorderSize = 0;
        statusCompletedBtn.Click += sidebarStatusFilter_Click;
        sidebarFlow.Controls.Add(statusCompletedBtn);

        // Priority Filter Section
        var priorityLabel = new Label
        {
            Text      = "Priority",
            AutoSize  = false,
            Width     = 128,
            Height    = 22,
            Font      = new Font("Segoe UI", 8.5F, FontStyle.Bold),
            ForeColor = Color.FromArgb(180, 195, 220),
            TextAlign = ContentAlignment.MiddleLeft,
            Margin    = new Padding(0, 0, 0, 4)
        };
        sidebarFlow.Controls.Add(priorityLabel);

        var priorityHighBtn = new Button
        {
            Text      = "🔴 High",
            Width     = 128,
            Height    = 32,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(42, 66, 102),
            ForeColor = Color.White,
            Font      = new Font("Segoe UI", 8.5F),
            TextAlign = ContentAlignment.MiddleLeft,
            Padding   = new Padding(6, 0, 0, 0),
            Margin    = new Padding(0, 0, 0, 3),
            Tag       = "High"
        };
        priorityHighBtn.FlatAppearance.BorderSize = 0;
        priorityHighBtn.Click += sidebarPriorityFilter_Click;
        sidebarFlow.Controls.Add(priorityHighBtn);

        var priorityNormalBtn = new Button
        {
            Text      = "🟡 Normal",
            Width     = 128,
            Height    = 32,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(42, 66, 102),
            ForeColor = Color.White,
            Font      = new Font("Segoe UI", 8.5F),
            TextAlign = ContentAlignment.MiddleLeft,
            Padding   = new Padding(6, 0, 0, 0),
            Margin    = new Padding(0, 0, 0, 3),
            Tag       = "Normal"
        };
        priorityNormalBtn.FlatAppearance.BorderSize = 0;
        priorityNormalBtn.Click += sidebarPriorityFilter_Click;
        sidebarFlow.Controls.Add(priorityNormalBtn);

        var priorityLowBtn = new Button
        {
            Text      = "🟢 Low",
            Width     = 128,
            Height    = 32,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(42, 66, 102),
            ForeColor = Color.White,
            Font      = new Font("Segoe UI", 8.5F),
            TextAlign = ContentAlignment.MiddleLeft,
            Padding   = new Padding(6, 0, 0, 0),
            Margin    = new Padding(0, 0, 0, 12),
            Tag       = "Low"
        };
        priorityLowBtn.FlatAppearance.BorderSize = 0;
        priorityLowBtn.Click += sidebarPriorityFilter_Click;
        sidebarFlow.Controls.Add(priorityLowBtn);

        // Quick Actions Section
        var actionsLabel = new Label
        {
            Text      = "Actions",
            AutoSize  = false,
            Width     = 128,
            Height    = 22,
            Font      = new Font("Segoe UI", 8.5F, FontStyle.Bold),
            ForeColor = Color.FromArgb(180, 195, 220),
            TextAlign = ContentAlignment.MiddleLeft,
            Margin    = new Padding(0, 0, 0, 4)
        };
        sidebarFlow.Controls.Add(actionsLabel);

        var newProjectBtn = new Button
        {
            Text      = "➕ New",
            Width     = 128,
            Height    = 32,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(60, 125, 255),
            ForeColor = Color.White,
            Font      = new Font("Segoe UI", 8.5F, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft,
            Padding   = new Padding(6, 0, 0, 0),
            Margin    = new Padding(0, 0, 0, 3)
        };
        newProjectBtn.FlatAppearance.BorderSize = 0;
        newProjectBtn.Click += (sender, e) => { clearFormButton_Click(sender, e); projectNameTextBox.Focus(); };
        sidebarFlow.Controls.Add(newProjectBtn);

        var refreshBtn = new Button
        {
            Text      = "🔄 Refresh",
            Width     = 128,
            Height    = 32,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(42, 66, 102),
            ForeColor = Color.White,
            Font      = new Font("Segoe UI", 8.5F),
            TextAlign = ContentAlignment.MiddleLeft,
            Padding   = new Padding(6, 0, 0, 0),
            Margin    = new Padding(0, 0, 0, 3)
        };
        refreshBtn.FlatAppearance.BorderSize = 0;
        refreshBtn.Click += async (sender, e) => await LoadProjectsAsync();
        sidebarFlow.Controls.Add(refreshBtn);

        var biDashboardBtn = new Button
        {
            Text      = "📈 BI",
            Width     = 128,
            Height    = 34,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(180, 110, 30),
            ForeColor = Color.White,
            Font      = new Font("Segoe UI", 8.5F, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft,
            Padding   = new Padding(6, 0, 0, 0),
            Margin    = new Padding(0, 6, 0, 3),
            Cursor    = Cursors.Hand
        };
        biDashboardBtn.FlatAppearance.BorderSize           = 0;
        biDashboardBtn.FlatAppearance.MouseOverBackColor   = Color.FromArgb(210, 135, 45);
        biDashboardBtn.FlatAppearance.MouseDownBackColor   = Color.FromArgb(150, 88, 20);
        biDashboardBtn.Click += biDashboardBtn_Click;
        sidebarFlow.Controls.Add(biDashboardBtn);

        rightSidebar.Controls.Add(sidebarFlow);


        formTable = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            ColumnCount = 2,
            RowCount = 9,
            AutoSize = true,
            BackColor = Color.White
        };
        formTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40F));
        formTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60F));
        formTable.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        formTable.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        formTable.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        formTable.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        formTable.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        formTable.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        formTable.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        formTable.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        formTable.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        formTable.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        formTable.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var formTitle = new Label
        {
            Text = "Project Details",
            AutoSize = true,
            Font = new Font("Segoe UI", 14F, FontStyle.Bold),
            ForeColor = Color.FromArgb(35, 56, 88),
            Anchor = AnchorStyles.Left
        };
        formTable.Controls.Add(formTitle, 0, 0);
        formTable.SetColumnSpan(formTitle, 2);

        formTable.Controls.Add(new Label { Text = "Project Name", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 1);
        projectNameTextBox = new TextBox { Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top, Height = 30, Margin = new Padding(0, 0, 8, 8) };
        formTable.Controls.Add(projectNameTextBox, 1, 1);

        formTable.Controls.Add(new Label { Text = "Client", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 2);
        clientTextBox = new TextBox { Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top, Height = 30, Margin = new Padding(0, 0, 8, 8) };
        formTable.Controls.Add(clientTextBox, 1, 2);

        formTable.Controls.Add(new Label { Text = "Status", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 3);
        statusComboBox = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top, Height = 30, Margin = new Padding(0, 0, 8, 8) };
        statusComboBox.Items.AddRange(new object[] { "Active", "On Hold", "Completed" });
        statusComboBox.SelectedIndex = 0;
        formTable.Controls.Add(statusComboBox, 1, 3);

        formTable.Controls.Add(new Label { Text = "Priority", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 4);
        priorityComboBox = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top, Height = 30, Margin = new Padding(0, 0, 8, 8) };
        priorityComboBox.Items.AddRange(new object[] { "Low", "Normal", "High" });
        priorityComboBox.SelectedIndex = 1;
        formTable.Controls.Add(priorityComboBox, 1, 4);

        formTable.Controls.Add(new Label { Text = "Project Phases", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 5);
        projectPhasesTextBox = new TextBox { Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top, Height = 30, Margin = new Padding(0, 0, 8, 8) };
        formTable.Controls.Add(projectPhasesTextBox, 1, 5);

        formTable.Controls.Add(new Label { Text = "Working Folder", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 6);
        var folderLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Margin = new Padding(0, 0, 8, 8)
        };
        folderLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        folderLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 84F));
        folderTextBox = new TextBox { Dock = DockStyle.Fill, Height = 30 };
        var folderButton = new Button { Text = "Browse", Dock = DockStyle.Fill, Height = 30 };
        folderLayout.Controls.Add(folderTextBox, 0, 0);
        folderLayout.Controls.Add(folderButton, 1, 0);
        formTable.Controls.Add(folderLayout, 1, 6);

        formTable.Controls.Add(new Label { Text = "Notes", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 7);
        notesTextBox = new TextBox { Multiline = true, Dock = DockStyle.Fill, Height = 110, Margin = new Padding(0, 0, 8, 8), BackColor = Color.FromArgb(255, 255, 255), BorderStyle = BorderStyle.FixedSingle };
        formTable.Controls.Add(notesTextBox, 1, 7);

        formTable.Controls.Add(new Label { Text = "Attachments", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 8);
        var attachmentHeader = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            AutoSize = true,
            Margin = new Padding(0, 0, 8, 4)
        };
        attachmentCountBadge = new Label
        {
            Text = "0 files",
            AutoSize = true,
            Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
            ForeColor = Color.FromArgb(64, 115, 255),
            BackColor = Color.FromArgb(235, 242, 255),
            Padding = new Padding(6, 2, 6, 2),
            Margin = new Padding(0, 0, 8, 0)
        };
        attachmentHeader.Controls.Add(attachmentCountBadge);
        formTable.Controls.Add(attachmentHeader, 1, 8);

        var attachmentLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 2,
            Margin = new Padding(0, 0, 8, 8)
        };
        attachmentLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        attachmentLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90F));
        attachmentLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100F));
        attachmentLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        attachmentLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38F));
        attachmentListBox = new ListBox
        {
            Dock = DockStyle.Fill,
            Height = 110,
            AllowDrop = true,
            BorderStyle = BorderStyle.FixedSingle,
            DrawMode = DrawMode.OwnerDrawFixed,
            ItemHeight = 22
        };
        attachmentLayout.Controls.Add(attachmentListBox, 0, 0);
        attachmentLayout.SetColumnSpan(attachmentListBox, 3);

        browseAttachmentButton = new Button { Text = "📎 Add File", Dock = DockStyle.Fill, Height = 30, Margin = new Padding(0, 0, 4, 0) };
        openAttachmentButton = new Button { Text = "⬊ Open", Dock = DockStyle.Fill, Height = 30, Margin = new Padding(0, 0, 4, 0) };
        removeAttachmentButton = new Button { Text = "🗑 Remove", Dock = DockStyle.Fill, Height = 30, Margin = new Padding(4, 0, 0, 0) };
        attachmentLayout.Controls.Add(browseAttachmentButton, 0, 1);
        attachmentLayout.Controls.Add(openAttachmentButton, 1, 1);
        attachmentLayout.Controls.Add(removeAttachmentButton, 2, 1);
        formTable.Controls.Add(attachmentLayout, 1, 9);

        var buttonFlow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            AutoSize = true,
            Margin = new Padding(0, 8, 0, 0)
        };
        saveProjectButton = new Button { Text = "Save Project", Width = 140, Height = 36, Margin = new Padding(0, 0, 8, 0) };
        clearFormButton = new Button { Text = "Clear", Width = 80, Height = 36, Margin = new Padding(0, 0, 8, 0) };
        deleteProjectButton = new Button { Text = "Delete", Width = 80, Height = 36 };
        buttonFlow.Controls.Add(saveProjectButton);
        buttonFlow.Controls.Add(clearFormButton);
        buttonFlow.Controls.Add(deleteProjectButton);

        formTable.Controls.Add(buttonFlow, 0, 10);
        formTable.SetColumnSpan(buttonFlow, 2);



        // Create filterPanel before using it
        var filterPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 42,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Margin = new Padding(0, 0, 0, 8)
        };

        foreach (var status in new[] { "All", "Active", "On Hold", "Completed" })
        {
            var button = new Button
            {
                Text = status,
                Width = 90,
                Height = 32,
                Margin = new Padding(0, 0, 6, 0),
                FlatStyle = FlatStyle.Flat,
                BackColor = status == "All" ? Color.FromArgb(32, 54, 87) : Color.FromArgb(18, 32, 56),
                ForeColor = Color.White
            };
            button.Click += filterButton_Click;
            filterPanel.Controls.Add(button);
        }
        statusFilterPanel = filterPanel;

        searchTextBox = new TextBox
        {
            Dock = DockStyle.Top,
            Height = 34,
            Margin = new Padding(0, 0, 0, 8),
            PlaceholderText = "Search projects, clients, phases, files..."
        };

        // Assemble the left panel
        leftPanel.Controls.Add(searchTextBox);
        leftPanel.Controls.Add(filterPanel);
        leftPanel.Controls.Add(formTable);



        // Assemble data area with list
        dataAreaPanel.Controls.Add(projectListView);


        // Assemble right panel layout
        rightPanelLayout.Controls.Add(dataAreaPanel, 0, 0);
        rightPanelLayout.Controls.Add(rightSidebar, 1, 0);
        rightPanel.Controls.Add(rightPanelLayout);

        contentLayout.Controls.Add(leftPanel, 0, 0);
        contentLayout.Controls.Add(rightPanel, 1, 0);

        // Board panel — hidden until user clicks Board nav item
        boardPanel = new Gradual.Forms.BoardPanel
        {
            Dock    = DockStyle.Fill,
            Visible = false
        };

        // Repository panel — hidden until user clicks Repository nav item
        repositoryPanel = new Gradual.Forms.RepositoryPanel
        {
            Dock    = DockStyle.Fill,
            Visible = false
        };

        // Setup panel — hidden until user clicks Setup nav item
        setupPanel = new Gradual.Forms.SetupPanel(
            Gradual.Infrastructure.ServiceContainer.GetRequiredService<Gradual.Services.ConfigurationService>())
        {
            Dock    = DockStyle.Fill,
            Visible = false
        };

        // ── Duty wrapper: tab bar + Projects view + GitHub view ──────────────
        dutyPanel = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(245, 247, 250) };

        // Tab bar
        dutyTabBar = new Panel
        {
            Dock      = DockStyle.Top,
            Height    = 40,
            BackColor = Color.FromArgb(8, 16, 30)
        };
        dutyBtnProjects = new Button
        {
            Text      = "📋  Projects",
            Width     = 160,
            Height    = 40,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(20, 32, 60),
            ForeColor = Color.FromArgb(94, 129, 255),
            Font      = new Font("Segoe UI", 9.5F, FontStyle.Bold),
            Cursor    = Cursors.Hand,
            Location  = new Point(0, 0)
        };
        dutyBtnProjects.FlatAppearance.BorderSize = 0;
        dutyBtnGitHub = new Button
        {
            Text      = "🐙  GitHub",
            Width     = 160,
            Height    = 40,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.Transparent,
            ForeColor = Color.FromArgb(120, 150, 190),
            Font      = new Font("Segoe UI", 9.5F, FontStyle.Regular),
            Cursor    = Cursors.Hand,
            Location  = new Point(160, 0)
        };
        dutyBtnGitHub.FlatAppearance.BorderSize = 0;
        dutyBtnGitHub.FlatAppearance.MouseOverBackColor = Color.FromArgb(18, 30, 54);
        dutyTabBar.Controls.Add(dutyBtnProjects);
        dutyTabBar.Controls.Add(dutyBtnGitHub);

        // GitHub list view (same columns as projectListView)
        githubListView = new ListView
        {
            Dock          = DockStyle.Fill,
            View          = View.Details,
            FullRowSelect = true,
            GridLines     = true,
            HideSelection = false,
            MultiSelect   = false,
            Font          = new Font("Segoe UI", 10F),
            BackColor     = Color.FromArgb(245, 247, 250),
            Visible       = false
        };
        // Same weight ratios as projectListView — Folder gets the largest share.
        int[] ghWeights = { 30, 20, 15, 15, 50, 20 };
        githubListView.Columns.Add("Project",  -2);
        githubListView.Columns.Add("Client",   -2);
        githubListView.Columns.Add("Status",   -2);
        githubListView.Columns.Add("Priority", -2);
        githubListView.Columns.Add("Folder",   -2);
        githubListView.Columns.Add("Updated",  -2);
        githubListView.Resize += (_, _) => ApplyListViewWeights(githubListView, ghWeights);
        githubListView.HandleCreated += (_, _) => ApplyListViewWeights(githubListView, ghWeights);
        githubListView.VisibleChanged += (_, _) =>
        {
            if (githubListView.Visible) ApplyListViewWeights(githubListView, ghWeights);
        };

        dutyPanel.Controls.Add(contentLayout);    // Projects tab (fill)
        dutyPanel.Controls.Add(githubListView);   // GitHub tab (fill, hidden)
        dutyPanel.Controls.Add(dutyTabBar);       // always on top

        rootLayout.Controls.Add(sidebarPanel, 0, 0);
        rootLayout.SetRowSpan(sidebarPanel, 2);
        rootLayout.Controls.Add(headerPanel, 1, 0);
        rootLayout.Controls.Add(dutyPanel, 1, 1);            // Duty view
        rootLayout.Controls.Add(boardPanel, 1, 1);           // same cell — toggled visible
        rootLayout.Controls.Add(repositoryPanel, 1, 1);      // same cell — toggled visible
        rootLayout.Controls.Add(setupPanel, 1, 1);           // same cell — toggled visible
        Controls.Add(rootLayout);

        addButton.Click += (_, _) => projectNameTextBox.Focus();
        saveButton.Click += saveProjectButton_Click;
        openFolderButton.Click += openFolderButton_Click;
        exportButton.Click += exportButton_Click;
        themeButton.Click += themeButton_Click;
        folderButton.Click += browseFolderButton_Click;
        saveProjectButton.Click += saveProjectButton_Click;
        clearFormButton.Click += clearFormButton_Click;
        deleteProjectButton.Click += deleteProjectButton_Click;
        attachmentContextMenu = new ContextMenuStrip();
        attachmentContextMenu.Items.Add("Open", null, attachmentOpenToolStripMenuItem_Click);
        attachmentContextMenu.Items.Add("Remove", null, attachmentRemoveToolStripMenuItem_Click);
        attachmentListBox.ContextMenuStrip = attachmentContextMenu;
        attachmentToolTip = new ToolTip { AutomaticDelay = 150, UseAnimation = true, UseFading = true };

        searchTextBox.TextChanged += searchTextBox_TextChanged;
        attachmentListBox.DragEnter += attachmentListBox_DragEnter;
        attachmentListBox.DragDrop += attachmentListBox_DragDrop;
        attachmentListBox.DrawItem += attachmentListBox_DrawItem;
        attachmentListBox.MeasureItem += attachmentListBox_MeasureItem;
        attachmentListBox.MouseMove += attachmentListBox_MouseMove;
        attachmentListBox.DoubleClick += attachmentListBox_DoubleClick;
        attachmentListBox.SelectedIndexChanged += attachmentListBox_SelectedIndexChanged;
        browseAttachmentButton.Click += browseAttachmentButton_Click;
        openAttachmentButton.Click += openAttachmentButton_Click;
        removeAttachmentButton.Click += removeAttachmentButton_Click;
        projectListView.SelectedIndexChanged += projectListView_SelectedIndexChanged;
    }

    private static Button CreateSidebarButton(string text, bool active = false)
    {
        var button = new Button
        {
            Text = text,
            Height = 44,
            Dock = DockStyle.Top,
            Width = 150,
            BackColor = active ? Color.FromArgb(38, 72, 116) : Color.FromArgb(18, 32, 56),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Margin = new Padding(0, 0, 0, 8),
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(14, 0, 0, 0),
            Font = new Font("Segoe UI", 10F, FontStyle.Bold),
            Cursor = Cursors.Hand,
            UseVisualStyleBackColor = false,
            TextImageRelation = TextImageRelation.ImageBeforeText,
            Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top
        };

        button.FlatAppearance.BorderSize = 0;
        button.FlatAppearance.BorderColor = Color.FromArgb(18, 32, 56);
        button.FlatAppearance.MouseOverBackColor = Color.FromArgb(48, 76, 120);
        button.FlatAppearance.MouseDownBackColor = Color.FromArgb(24, 42, 72);
        button.MouseEnter += (_, _) => button.BackColor = Color.FromArgb(48, 76, 120);
        button.MouseLeave += (_, _) => button.BackColor = active ? Color.FromArgb(38, 72, 116) : Color.FromArgb(18, 32, 56);

        return button;
    }

    private static Button CreateNavItem(string icon, string label, bool active = false)
    {
        var activeColor   = Color.FromArgb(94, 129, 255);
        var bgActive      = Color.FromArgb(20, 32, 60);
        var bgNormal      = Color.Transparent;
        var bgHover       = Color.FromArgb(25, 40, 68);

        var btn = new Button
        {
            Text             = $"{icon}  {label}",
            Width            = 180,
            Height           = 52,
            FlatStyle        = FlatStyle.Flat,
            BackColor        = active ? bgActive : bgNormal,
            ForeColor        = active ? activeColor : Color.FromArgb(160, 185, 220),
            Font             = new Font("Segoe UI", 10F, active ? FontStyle.Bold : FontStyle.Regular),
            TextAlign        = ContentAlignment.MiddleLeft,
            Padding          = new Padding(20, 0, 0, 0),
            Margin           = new Padding(0),
            Cursor           = Cursors.Hand,
            UseVisualStyleBackColor = false,
        };

        btn.FlatAppearance.BorderSize  = 0;
        btn.FlatAppearance.BorderColor = Color.FromArgb(11, 23, 40); // same as sidebar bg — hides border
        btn.FlatAppearance.MouseOverBackColor  = bgHover;
        btn.FlatAppearance.MouseDownBackColor  = bgActive;

        // Left accent border for active state via Paint
        if (active)
        {
            btn.Paint += (s, e) =>
            {
                using var pen = new Pen(activeColor, 3);
                e.Graphics.DrawLine(pen, 0, 6, 0, btn.Height - 6);
            };
        }

        btn.MouseEnter += (_, _) => { if (btn.BackColor != bgActive) btn.BackColor = bgHover; };
        btn.MouseLeave += (_, _) => { btn.BackColor = active ? bgActive : bgNormal; };

        return btn;
    }

    private static Gradual.Controls.RoundedPanel CreateStatCard(string title, string value, Color color)
    {
        var panel = new Gradual.Controls.RoundedPanel
        {
            Width = 160,
            Height = 64,
            BackColor = Color.FromArgb(255, 255, 255),
            Margin = new Padding(6),
            BorderColor = Color.FromArgb(230, 235, 242),
            Radius = 16
        };

        var labelTitle = new Label
        {
            Text = title,
            AutoSize = true,
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            ForeColor = color,
            Location = new Point(12, 10)
        };

        var labelValue = new Label
        {
            Text = value,
            AutoSize = true,
            Font = new Font("Segoe UI", 18F, FontStyle.Bold),
            ForeColor = Color.FromArgb(30, 46, 74),
            Location = new Point(12, 28)
        };

        panel.Controls.Add(labelTitle);
        panel.Controls.Add(labelValue);

        panel.MouseEnter += (_, _) =>
        {
            panel.BackColor = Color.FromArgb(236, 243, 255);
            panel.BorderColor = color;
            panel.Invalidate();
        };

        panel.MouseLeave += (_, _) =>
        {
            panel.BackColor = Color.FromArgb(255, 255, 255);
            panel.BorderColor = Color.FromArgb(230, 235, 242);
            panel.Invalidate();
        };

        return panel;
    }

    private static Gradual.Controls.RoundedPanel CreateKpiCard(string title, string value, Color color)
    {
        var card = new Gradual.Controls.RoundedPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(255, 255, 255),
            BorderColor = Color.FromArgb(230, 235, 242),
            Radius = 14,
            Margin = new Padding(0, 0, 0, 8)
        };

        var labelTitle = new Label
        {
            Text = title,
            AutoSize = true,
            Font = new Font("Segoe UI", 10F, FontStyle.Bold),
            ForeColor = color,
            Location = new Point(12, 10)
        };

        var labelValue = new Label
        {
            Text = value,
            AutoSize = true,
            Font = new Font("Segoe UI", 18F, FontStyle.Bold),
            ForeColor = Color.FromArgb(30, 46, 74),
            Location = new Point(12, 28)
        };

        card.Controls.Add(labelTitle);
        card.Controls.Add(labelValue);
        return card;
    }

    private TableLayoutPanel rootLayout = null!;
    private TableLayoutPanel formTable = null!;
    private Gradual.Controls.RoundedPanel sidebarPanel = null!;
    private FlowLayoutPanel navFlow = null!;
    private Panel headerPanel = null!;
    private FlowLayoutPanel toolbarFlow = null!;
    private Button openFolderButton = null!;
    private Button exportButton = null!;
    private Button themeButton = null!;
    private Gradual.Controls.RoundedPanel leftPanel = null!;
    private Panel rightPanel = null!;
    private FlowLayoutPanel statusFilterPanel = null!;
    private TextBox searchTextBox = null!;
    private TextBox projectNameTextBox = null!;
    private TextBox clientTextBox = null!;
    private ComboBox statusComboBox = null!;
    private ComboBox priorityComboBox = null!;
    private TextBox projectPhasesTextBox = null!;
    private TextBox folderTextBox = null!;
    private TextBox notesTextBox = null!;
    private ListBox attachmentListBox = null!;
    private Label attachmentCountBadge = null!;
    private ContextMenuStrip attachmentContextMenu = null!;
    private ToolTip attachmentToolTip = null!;
    private Button openAttachmentButton = null!;
    private Button browseAttachmentButton = null!;
    private Button removeAttachmentButton = null!;
    private Button saveProjectButton = null!;
    private Button clearFormButton = null!;
    private Button deleteProjectButton = null!;
    private ListView projectListView = null!;
    private Gradual.Forms.BoardPanel boardPanel = null!;
    private Gradual.Forms.RepositoryPanel repositoryPanel = null!;
    private Gradual.Forms.SetupPanel setupPanel = null!;
    private TableLayoutPanel contentLayout = null!;
    // ── Duty tabs ──────────────────────────────────────────────────────────
    private Panel       dutyPanel       = null!;
    private Panel       dutyTabBar      = null!;
    private Button      dutyBtnProjects = null!;
    private Button      dutyBtnGitHub   = null!;
    private ListView    githubListView  = null!;
}
