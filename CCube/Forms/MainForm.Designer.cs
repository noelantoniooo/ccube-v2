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
    private PictureBox pbLogo = null!;
    private Label lblBrand = null!;
    private Label lblBrandSub = null!;
    private Label lblStation = null!;
    private Label lblModeBadge = null!;
    private Button btnConfig = null!;
    private Button btnManual = null!;
    private Button btnInject = null!;
    private Button btnTheme = null!;
    // Health strip (contents built in code)
    private Panel pnlHealth = null!;
    // Verdict
    private Panel pnlVerdict = null!;
    private Label lblVerdict = null!;
    private Label lblVerdictDetail = null!;
    // Main
    private SplitContainer splitBody = null!;
    private TableLayoutPanel tblMain = null!;
    private Panel pnlProductInfo = null!;
    private Panel pnlLogs = null!;
    private Label lblLogsHeader = null!;
    private ListView lvScans = null!;
    private ColumnHeader colId = null!;
    private ColumnHeader colStation = null!;
    private ColumnHeader colBarcode = null!;
    private ColumnHeader colCasePacker = null!;
    private ColumnHeader colProduct = null!;
    private ColumnHeader colFa = null!;
    private ColumnHeader colProduction = null!;
    private ColumnHeader colResult = null!;
    private ColumnHeader colScanned = null!;
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
        pbLogo = new PictureBox();
        lblBrand = new Label();
        lblBrandSub = new Label();
        lblStation = new Label();
        lblModeBadge = new Label();
        btnConfig = new Button();
        btnManual = new Button();
        btnInject = new Button();
        btnTheme = new Button();
        pnlHealth = new Panel();
        pnlVerdict = new Panel();
        lblVerdict = new Label();
        lblVerdictDetail = new Label();
        splitBody = new SplitContainer();
        tblMain = new TableLayoutPanel();
        pnlProductInfo = new Panel();
        pnlLogs = new Panel();
        lblLogsHeader = new Label();
        lvScans = new ListView();
        colId = new ColumnHeader();
        colStation = new ColumnHeader();
        colBarcode = new ColumnHeader();
        colCasePacker = new ColumnHeader();
        colProduct = new ColumnHeader();
        colFa = new ColumnHeader();
        colProduction = new ColumnHeader();
        colResult = new ColumnHeader();
        colScanned = new ColumnHeader();
        pnlEvents = new Panel();
        lblEventsHeader = new Label();
        lvEvents = new ListView();
        colEvtTime = new ColumnHeader();
        colEvtLevel = new ColumnHeader();
        colEvtMsg = new ColumnHeader();
        pnlManual = new Panel();
        txtManual = new TextBox();

        SuspendLayout();

        // root
        root.Dock = DockStyle.Fill;
        root.BackColor = Theme.Background;
        root.ColumnCount = 1;
        root.RowCount = 4;
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 70F));   // header
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 32F));   // health strip
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 92F));   // verdict
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));   // main + event logs (draggable split)

        // --- Header ---
        pnlHeader.Dock = DockStyle.Fill;
        pnlHeader.BackColor = Theme.HeaderGreen;
        // ML+ Solutions logo, top-left, before the app name (image loaded in code).
        pbLogo.Location = new Point(16, 8);
        pbLogo.Size = new Size(54, 54);
        pbLogo.SizeMode = PictureBoxSizeMode.Zoom;
        pbLogo.BackColor = Color.Transparent;
        lblBrand.Text = "C-CUBE";
        lblBrand.Font = Theme.BrandFont;
        lblBrand.ForeColor = Color.White;
        lblBrand.AutoSize = true;
        lblBrand.Location = new Point(82, 11);
        lblBrandSub.Text = "Central Case Checker  ·  PMFTC";
        lblBrandSub.Font = Theme.BrandSubFont;
        lblBrandSub.ForeColor = Theme.HeaderText;
        lblBrandSub.AutoSize = true;
        lblBrandSub.Location = new Point(84, 44);

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
        // Demo-only: inject a fake PLC case. Hidden unless DemoMode is on (set in code).
        btnInject.Text = "⤵  Inject PLC case";
        btnInject.Font = Theme.BoldFont;
        btnInject.ForeColor = Color.White;
        btnInject.BackColor = Theme.LedAmber;
        btnInject.FlatStyle = FlatStyle.Flat;
        btnInject.FlatAppearance.BorderSize = 0;
        btnInject.AutoSize = true;
        btnInject.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        btnInject.Padding = new Padding(14, 6, 14, 6);
        btnInject.Margin = new Padding(8, 0, 0, 0);
        btnInject.Cursor = Cursors.Hand;
        btnInject.Visible = false;
        btnInject.Click += btnInject_Click;
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
        headerRight.Controls.Add(btnInject);
        headerRight.Controls.Add(lblStation);
        headerRight.Controls.Add(lblModeBadge);
        pnlHeader.Controls.Add(headerRight);
        pnlHeader.Controls.Add(pbLogo);
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

        // --- Product Information card (labels for the last scan), above Case Logs.
        //     Its header + value labels are built in code (BuildProductInfo). ---
        pnlProductInfo.Dock = DockStyle.Fill;
        pnlProductInfo.BackColor = Theme.Surface;
        pnlProductInfo.BorderStyle = BorderStyle.FixedSingle;
        pnlProductInfo.Margin = new Padding(0, 0, 0, 8);

        // --- Main: the Case Logs table fills the rest of the area (no side counters).
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
        lvScans.Scrollable = true;   // show a vertical scrollbar when rows overflow
        // The 9 columns: Id + Station from the app, Barcode + Case Packer + Production
        // time parsed from the barcode, Product + FA Code + result from InExpress,
        // and the time the app received the scan.
        // Widths chosen so each header caption shows in full (the owner-drawn header
        // trims ~16px for padding, so these are the caption width + margin + data).
        colId.Text = "Id";
        colId.Width = 60;
        colStation.Text = "Station";
        colStation.Width = 80;
        colBarcode.Text = "Barcode";
        colBarcode.Width = 320;
        colCasePacker.Text = "Case Packer #";
        colCasePacker.Width = 120;
        colProduct.Text = "Product Description";
        colProduct.Width = 210;
        colFa.Text = "FA Code";
        colFa.Width = 100;
        colProduction.Text = "Production Date/Time";
        colProduction.Width = 165;
        colResult.Text = "Validation Result";
        colResult.Width = 135;
        colScanned.Text = "Scanned Date/Time";
        colScanned.Width = 160;
        lvScans.Columns.Add(colId);
        lvScans.Columns.Add(colStation);
        lvScans.Columns.Add(colBarcode);
        lvScans.Columns.Add(colCasePacker);
        lvScans.Columns.Add(colProduct);
        lvScans.Columns.Add(colFa);
        lvScans.Columns.Add(colProduction);
        lvScans.Columns.Add(colResult);
        lvScans.Columns.Add(colScanned);
        pnlLogs.Controls.Add(lvScans);
        pnlLogs.Controls.Add(lblLogsHeader);


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

        // --- Manual scan bar. It's the TOP row of tblMain (above the Product
        //     Information card), hidden until toggled. Living here - not at the
        //     bottom of the window - means a maximized window's taskbar can never
        //     cover it, and a TableLayoutPanel row can never overlap the log below. ---
        pnlManual.Dock = DockStyle.Fill;
        pnlManual.Margin = new Padding(0, 0, 0, 8);
        pnlManual.BackColor = Theme.SurfaceAlt;
        pnlManual.BorderStyle = BorderStyle.FixedSingle;
        pnlManual.Visible = false;
        // The padding gives the textbox its margins and vertically centres it; Dock=Fill
        // then stretches it across the bar (no overflow, no anchor maths needed).
        pnlManual.Padding = new Padding(16, 14, 16, 8);
        // No Scan button and no label: a real barcode scanner types the code and sends
        // Enter, so the operator just scans into this box. It's big + monospaced so the
        // barcode is easy to read; the placeholder text explains what to do.
        txtManual.Dock = DockStyle.Fill;
        txtManual.Font = new Font("Consolas", 15F);
        txtManual.BackColor = Theme.Surface;
        txtManual.ForeColor = Theme.Text;
        txtManual.BorderStyle = BorderStyle.FixedSingle;
        txtManual.MaxLength = Plc.Db100Layout.BarcodeMaxLength; // 40 - same as the PLC field
        txtManual.PlaceholderText = "Scan or type a barcode, then press Enter";
        txtManual.KeyDown += txtManual_KeyDown;
        pnlManual.Controls.Add(txtManual);

        // place rows
        // Case Logs area (top) and Event Logs (bottom) share a draggable splitter -
        // drag it to make either panel bigger. A wider bar in the accent colour makes
        // it obvious it can be grabbed.
        splitBody.Dock = DockStyle.Fill;
        splitBody.Orientation = Orientation.Horizontal;
        splitBody.FixedPanel = FixedPanel.Panel2;   // keep the Event Logs height on window resize
        splitBody.SplitterWidth = 6;
        splitBody.BackColor = Theme.Border;          // subtle splitter bar (as before)
        splitBody.Panel1MinSize = 160;               // Case Logs can't be squashed away
        splitBody.Panel2MinSize = 60;                // Event Logs can shrink to give Case Logs room
        // Case Logs area: a 3-row table so the rows never overlap - manual scan bar
        // (row 0, height toggled 0/52), Product Information card (row 1), Case Logs
        // table (row 2, fills the rest).
        tblMain.Dock = DockStyle.Fill;
        tblMain.BackColor = Theme.Background;
        tblMain.ColumnCount = 1;
        tblMain.RowCount = 3;
        tblMain.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        tblMain.RowStyles.Add(new RowStyle(SizeType.Absolute, 0F));    // manual (hidden)
        tblMain.RowStyles.Add(new RowStyle(SizeType.Absolute, 128F));  // product info (bigger)
        tblMain.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));   // case logs (fills the rest)
        tblMain.Controls.Add(pnlManual, 0, 0);
        tblMain.Controls.Add(pnlProductInfo, 0, 1);
        tblMain.Controls.Add(pnlLogs, 0, 2);

        splitBody.Panel1.Padding = new Padding(14, 10, 14, 6);
        splitBody.Panel1.Controls.Add(tblMain);
        splitBody.Panel2.Controls.Add(pnlEvents);

        root.Controls.Add(pnlHeader, 0, 0);
        root.Controls.Add(pnlHealth, 0, 1);
        root.Controls.Add(pnlVerdict, 0, 2);
        root.Controls.Add(splitBody, 0, 3);

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
        // Lock the window: borderless (no title bar, so it can't be moved, resized,
        // or double-clicked to un-maximize) and maximized to the work area (see the
        // MaximizedBounds set in the constructor, which keeps the bottom above the
        // taskbar). With the FormClosing guard (blocks Alt+F4), the station can only
        // be ended via Task Manager, and it stays full screen at all times.
        FormBorderStyle = FormBorderStyle.None;
        WindowState = FormWindowState.Maximized;
        ControlBox = false;
        MinimizeBox = false;
        MaximizeBox = false;
        Load += MainForm_Load;
        FormClosing += MainForm_FormClosing;
        ResumeLayout(false);
    }
}
