// MainForm.Designer.cs
// -----------------------------------------------------------------------------
// The controls and layout of the operator run screen.
//
// Rows (top to bottom), held by a TableLayoutPanel so it resizes cleanly:
//   Row 0  Header       - green bar: brand, Manual Scan + Configuration buttons
//   Row 1  Health strip - a slim PLC / InExpress / Scanner status line
//   Row 2  Verdict      - the big PASS / REJECT / READY panel
//   Row 3  Main         - CASE LOGS table (left) + counters & reject reasons (right)
//   Row 4  Event Logs   - a live log of what the app is doing
//   Row 5  Manual Scan  - type a barcode by hand (hidden until you click Manual Scan)
//
// The status strip, the right-hand panel and the event rows are built in
// MainForm.cs (they change at runtime); here we lay out the frame.
// -----------------------------------------------------------------------------

namespace CCube.Forms;

partial class MainForm
{
    private System.ComponentModel.IContainer components = null!;

    private TableLayoutPanel root = null!;
    // Header
    private Panel pnlHeader = null!;
    private Label lblBrand = null!;
    private Label lblBrandSub = null!;
    private Label lblStation = null!;
    private Label lblModeBadge = null!;
    private Button btnConfig = null!;
    private Button btnManual = null!;
    private Button btnTheme = null!;
    // Health strip (contents built in code)
    private Panel pnlHealth = null!;
    // Verdict
    private Panel pnlVerdict = null!;
    private Label lblVerdict = null!;
    private Label lblVerdictDetail = null!;
    // Main
    private SplitContainer splitBody = null!;
    private SplitContainer splitMain = null!;
    private Panel pnlLogs = null!;
    private Label lblLogsHeader = null!;
    private ListView lvScans = null!;
    private ColumnHeader colTime = null!;
    private ColumnHeader colBarcode = null!;
    private ColumnHeader colVerdict = null!;
    private ColumnHeader colReason = null!;
    private ColumnHeader colMs = null!;
    private Panel pnlSide = null!;
    // Event logs
    private Panel pnlEvents = null!;
    private Label lblEventsHeader = null!;
    private ListView lvEvents = null!;
    private ColumnHeader colEvtTime = null!;
    private ColumnHeader colEvtLevel = null!;
    private ColumnHeader colEvtMsg = null!;
    // Manual scan
    private Panel pnlManual = null!;
    private TextBox txtManual = null!;
    private Button btnScan = null!;

    protected override void Dispose(bool disposing)
    {
        if (disposing && components != null)
        {
            components.Dispose();
        }
        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        components = new System.ComponentModel.Container();

        root = new TableLayoutPanel();
        pnlHeader = new Panel();
        lblBrand = new Label();
        lblBrandSub = new Label();
        lblStation = new Label();
        lblModeBadge = new Label();
        btnConfig = new Button();
        btnManual = new Button();
        btnTheme = new Button();
        pnlHealth = new Panel();
        pnlVerdict = new Panel();
        lblVerdict = new Label();
        lblVerdictDetail = new Label();
        splitBody = new SplitContainer();
        splitMain = new SplitContainer();
        pnlLogs = new Panel();
        lblLogsHeader = new Label();
        lvScans = new ListView();
        colTime = new ColumnHeader();
        colBarcode = new ColumnHeader();
        colVerdict = new ColumnHeader();
        colReason = new ColumnHeader();
        colMs = new ColumnHeader();
        pnlSide = new Panel();
        pnlEvents = new Panel();
        lblEventsHeader = new Label();
        lvEvents = new ListView();
        colEvtTime = new ColumnHeader();
        colEvtLevel = new ColumnHeader();
        colEvtMsg = new ColumnHeader();
        pnlManual = new Panel();
        txtManual = new TextBox();
        btnScan = new Button();

        SuspendLayout();

        // root
        root.Dock = DockStyle.Fill;
        root.BackColor = Theme.Background;
        root.ColumnCount = 1;
        root.RowCount = 5;
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 70F));   // header
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 32F));   // health strip
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 92F));   // verdict
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));   // main + event logs (draggable split)
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 0F));    // manual (hidden)

        // --- Header ---
        pnlHeader.Dock = DockStyle.Fill;
        pnlHeader.BackColor = Theme.HeaderGreen;
        lblBrand.Text = "C-CUBE";
        lblBrand.Font = Theme.BrandFont;
        lblBrand.ForeColor = Color.White;
        lblBrand.AutoSize = true;
        lblBrand.Location = new Point(22, 11);
        lblBrandSub.Text = "Central Case Checker  ·  PMFTC";
        lblBrandSub.Font = Theme.BrandSubFont;
        lblBrandSub.ForeColor = Theme.HeaderText;
        lblBrandSub.AutoSize = true;
        lblBrandSub.Location = new Point(24, 44);

        var headerRight = new FlowLayoutPanel
        {
            Dock = DockStyle.Right,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = Theme.HeaderGreen,
            Padding = new Padding(0, 18, 16, 0),
        };
        btnConfig.Text = "⚙  Configuration";
        btnConfig.Font = Theme.BoldFont;
        btnConfig.ForeColor = Theme.HeaderGreenDark;
        btnConfig.BackColor = Color.White;
        btnConfig.FlatStyle = FlatStyle.Flat;
        btnConfig.FlatAppearance.BorderSize = 0;
        btnConfig.AutoSize = true;                 // size to the label so text never clips
        btnConfig.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        btnConfig.Padding = new Padding(14, 6, 14, 6);
        btnConfig.Margin = new Padding(8, 0, 0, 0);
        btnConfig.Cursor = Cursors.Hand;
        btnConfig.Click += btnConfig_Click;
        btnManual.Text = "Manual Scan";
        btnManual.Font = Theme.BoldFont;
        btnManual.ForeColor = Color.White;
        btnManual.BackColor = Theme.HeaderGreenDark;
        btnManual.FlatStyle = FlatStyle.Flat;
        btnManual.FlatAppearance.BorderColor = Color.White;
        btnManual.FlatAppearance.BorderSize = 1;
        btnManual.AutoSize = true;
        btnManual.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        btnManual.Padding = new Padding(14, 6, 14, 6);
        btnManual.Margin = new Padding(8, 0, 0, 0);
        btnManual.Cursor = Cursors.Hand;
        btnManual.Click += btnManual_Click;
        lblStation.Text = "Station: -";
        lblStation.Font = Theme.NormalFont;
        lblStation.ForeColor = Theme.HeaderText;
        lblStation.AutoSize = true;
        lblStation.Margin = new Padding(14, 16, 8, 0);
        lblModeBadge.Text = "  MANUAL  ";
        lblModeBadge.Font = Theme.BoldFont;
        lblModeBadge.ForeColor = Color.FromArgb(70, 45, 0);
        lblModeBadge.BackColor = Theme.LedAmber;
        lblModeBadge.AutoSize = false;
        lblModeBadge.Size = new Size(84, 24);
        lblModeBadge.TextAlign = ContentAlignment.MiddleCenter;
        lblModeBadge.Margin = new Padding(12, 13, 6, 0);
        // Light/dark toggle (shows a moon in light mode, a sun in dark mode).
        btnTheme.Text = Theme.Dark ? "☀" : "🌙";
        btnTheme.Font = new Font("Segoe UI", 12F);
        btnTheme.ForeColor = Theme.HeaderGreenDark;
        btnTheme.BackColor = Color.White;
        btnTheme.FlatStyle = FlatStyle.Flat;
        btnTheme.FlatAppearance.BorderSize = 0;
        btnTheme.Size = new Size(40, 32);
        btnTheme.Margin = new Padding(8, 0, 0, 0);
        btnTheme.Cursor = Cursors.Hand;
        btnTheme.Click += btnTheme_Click;
        // Right-to-left flow: the theme toggle is right-most, then Configuration,
        // then Manual Scan, the station and the badge.
        headerRight.Controls.Add(btnTheme);
        headerRight.Controls.Add(btnConfig);
        headerRight.Controls.Add(btnManual);
        headerRight.Controls.Add(lblStation);
        headerRight.Controls.Add(lblModeBadge);
        pnlHeader.Controls.Add(headerRight);
        pnlHeader.Controls.Add(lblBrand);
        pnlHeader.Controls.Add(lblBrandSub);

        // --- Health strip (built in code) ---
        pnlHealth.Dock = DockStyle.Fill;
        pnlHealth.BackColor = Theme.Surface;
        pnlHealth.Padding = new Padding(20, 0, 0, 0);

        // --- Verdict ---
        pnlVerdict.Dock = DockStyle.Fill;
        pnlVerdict.BackColor = Theme.Idle;
        lblVerdict.Text = "READY";
        lblVerdict.Font = Theme.VerdictFont;
        lblVerdict.ForeColor = Theme.Text;
        lblVerdict.Dock = DockStyle.Fill;
        lblVerdict.TextAlign = ContentAlignment.MiddleCenter;
        lblVerdictDetail.Text = "Waiting for the first case…";
        lblVerdictDetail.Font = Theme.DetailFont;
        lblVerdictDetail.ForeColor = Theme.Text;
        lblVerdictDetail.Dock = DockStyle.Bottom;
        lblVerdictDetail.Height = 26;
        lblVerdictDetail.TextAlign = ContentAlignment.MiddleCenter;
        pnlVerdict.Controls.Add(lblVerdict);
        pnlVerdict.Controls.Add(lblVerdictDetail);

        // --- Main: Case Logs (left) + the right column, split by a draggable bar ---
        splitMain.Dock = DockStyle.Fill;
        splitMain.Orientation = Orientation.Vertical;
        splitMain.FixedPanel = FixedPanel.Panel2;   // keep the right column width on resize
        splitMain.SplitterWidth = 6;
        splitMain.BackColor = Theme.Border;          // makes the splitter bar visible
        splitMain.Panel1.BackColor = Theme.Background;
        splitMain.Panel1.Padding = new Padding(14, 10, 6, 6);
        splitMain.Panel2.BackColor = Theme.Background;
        splitMain.Panel2.Padding = new Padding(6, 10, 14, 6);

        pnlLogs.Dock = DockStyle.Fill;
        pnlLogs.BackColor = Theme.Surface;
        pnlLogs.BorderStyle = BorderStyle.FixedSingle;
        lblLogsHeader.Text = "CASE LOGS";
        lblLogsHeader.Font = Theme.SectionFont;
        lblLogsHeader.ForeColor = Theme.Accent;
        lblLogsHeader.Dock = DockStyle.Top;
        lblLogsHeader.Height = 24;
        lblLogsHeader.TextAlign = ContentAlignment.MiddleLeft;
        lblLogsHeader.Padding = new Padding(12, 0, 0, 0);
        lblLogsHeader.BackColor = Theme.SurfaceAlt;
        lvScans.Dock = DockStyle.Fill;
        lvScans.View = View.Details;
        lvScans.FullRowSelect = true;
        lvScans.BackColor = Theme.Surface;
        lvScans.ForeColor = Theme.Text;
        lvScans.Font = Theme.NormalFont;
        lvScans.BorderStyle = BorderStyle.None;
        lvScans.HeaderStyle = ColumnHeaderStyle.Nonclickable;
        colTime.Text = "Time";
        colTime.Width = 90;
        colBarcode.Text = "Barcode";
        colBarcode.Width = 300;
        colVerdict.Text = "Verdict";
        colVerdict.Width = 90;
        colReason.Text = "Reason";
        colReason.Width = 170;
        colMs.Text = "ms";
        colMs.Width = 60;
        colMs.TextAlign = HorizontalAlignment.Right;
        lvScans.Columns.Add(colTime);
        lvScans.Columns.Add(colBarcode);
        lvScans.Columns.Add(colVerdict);
        lvScans.Columns.Add(colReason);
        lvScans.Columns.Add(colMs);
        pnlLogs.Controls.Add(lvScans);
        pnlLogs.Controls.Add(lblLogsHeader);

        pnlSide.Dock = DockStyle.Fill;
        pnlSide.BackColor = Theme.Background;

        splitMain.Panel1.Controls.Add(pnlLogs);
        splitMain.Panel2.Controls.Add(pnlSide);

        // --- Event logs ---
        pnlEvents.Dock = DockStyle.Fill;
        pnlEvents.BackColor = Theme.Surface;
        pnlEvents.BorderStyle = BorderStyle.FixedSingle;
        pnlEvents.Margin = new Padding(0);
        pnlEvents.Padding = new Padding(14, 0, 14, 8);
        lblEventsHeader.Text = "EVENT LOGS";
        lblEventsHeader.Font = Theme.SectionFont;
        lblEventsHeader.ForeColor = Theme.Accent;
        lblEventsHeader.Dock = DockStyle.Top;
        lblEventsHeader.Height = 22;
        lblEventsHeader.TextAlign = ContentAlignment.MiddleLeft;
        lvEvents.Dock = DockStyle.Fill;
        lvEvents.View = View.Details;
        lvEvents.FullRowSelect = true;
        lvEvents.BackColor = Theme.Surface;
        lvEvents.ForeColor = Theme.Text;
        lvEvents.Font = Theme.NormalFont;
        lvEvents.BorderStyle = BorderStyle.None;
        lvEvents.HeaderStyle = ColumnHeaderStyle.Nonclickable;
        colEvtTime.Text = "Time";
        colEvtTime.Width = 90;
        colEvtLevel.Text = "Level";
        colEvtLevel.Width = 70;
        colEvtMsg.Text = "Message";
        colEvtMsg.Width = 900;
        lvEvents.Columns.Add(colEvtTime);
        lvEvents.Columns.Add(colEvtLevel);
        lvEvents.Columns.Add(colEvtMsg);
        pnlEvents.Controls.Add(lvEvents);
        pnlEvents.Controls.Add(lblEventsHeader);

        // --- Manual scan (hidden until toggled) ---
        pnlManual.Dock = DockStyle.Fill;
        pnlManual.BackColor = Theme.SurfaceAlt;
        pnlManual.Visible = false;
        var manualHint = new Label
        {
            Text = "Manual scan:",
            Font = Theme.BoldFont,
            ForeColor = Theme.Muted,
            AutoSize = true,
            Location = new Point(18, 14),
        };
        txtManual.Font = Theme.NormalFont;
        txtManual.Size = new Size(440, 26);
        txtManual.Location = new Point(120, 10);
        txtManual.BackColor = Theme.Surface;
        txtManual.ForeColor = Theme.Text;
        txtManual.BorderStyle = BorderStyle.FixedSingle;
        txtManual.MaxLength = Plc.Db100Layout.BarcodeMaxLength; // 40 - same as the PLC field
        txtManual.PlaceholderText = "Type or scan a barcode, then press Enter";
        txtManual.KeyDown += txtManual_KeyDown;
        txtManual.KeyPress += txtManual_KeyPress; // barcode characters only
        btnScan.Text = "Scan";
        btnScan.Font = Theme.BoldFont;
        btnScan.ForeColor = Color.White;
        btnScan.BackColor = Theme.Accent;
        btnScan.FlatStyle = FlatStyle.Flat;
        btnScan.FlatAppearance.BorderSize = 0;
        btnScan.Size = new Size(120, 28);
        btnScan.Location = new Point(580, 9);
        btnScan.Cursor = Cursors.Hand;
        btnScan.Click += btnScan_Click;
        pnlManual.Controls.Add(manualHint);
        pnlManual.Controls.Add(txtManual);
        pnlManual.Controls.Add(btnScan);

        // place rows
        // Case Logs area (top) and Event Logs (bottom) share a draggable splitter.
        splitBody.Dock = DockStyle.Fill;
        splitBody.Orientation = Orientation.Horizontal;
        splitBody.FixedPanel = FixedPanel.Panel2;   // keep the Event Logs height on resize
        splitBody.SplitterWidth = 6;
        splitBody.BackColor = Theme.Border;          // makes the splitter bar visible
        splitBody.Panel1.Controls.Add(splitMain);
        splitBody.Panel2.Controls.Add(pnlEvents);

        root.Controls.Add(pnlHeader, 0, 0);
        root.Controls.Add(pnlHealth, 0, 1);
        root.Controls.Add(pnlVerdict, 0, 2);
        root.Controls.Add(splitBody, 0, 3);
        root.Controls.Add(pnlManual, 0, 4);

        // MainForm
        AutoScaleDimensions = new SizeF(7F, 15F);
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(1040, 740);
        Controls.Add(root);
        BackColor = Theme.Background;
        ForeColor = Theme.Text;
        Text = "C-Cube  -  Shipping Case Validation";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(940, 680);
        WindowState = FormWindowState.Maximized;
        Load += MainForm_Load;
        FormClosing += MainForm_FormClosing;
        ResumeLayout(false);
    }
}
