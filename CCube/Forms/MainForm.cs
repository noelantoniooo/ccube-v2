// MainForm.cs
// -----------------------------------------------------------------------------
// The code behind the operator run screen. It:
//   * builds the scan engine (Live PLC or Manual-scan, from settings) and starts it,
//   * builds the health strip and the counters / reject-reasons panel,
//   * mirrors the app's log into the on-screen Event Logs,
//   * updates the big PASS/REJECT panel, counters and lists as scans run,
//   * lets you type a barcode by hand via the Manual Scan panel.
//
// Threads: engine events and log entries arrive on background threads. WinForms
// only lets the UI thread touch controls, so every handler hops back via OnUi(...).
// -----------------------------------------------------------------------------

using CCube.Core;
using CCube.Data;
using CCube.Models;
using CCube.Plc;

namespace CCube.Forms;

public partial class MainForm : Form
{
    private AppSettings _settings = new();
    private IPlcLink? _plcLink;
    private ScanEngine? _engine;
    private readonly HealthMonitor _health = new();

    private bool _manualVisible;
    private readonly ToolTip _toolTip = new();

    // Health strip labels.
    private Label _ledPlc = null!, _ledInx = null!, _ledScanner = null!;
    // Product Information panel - the value labels for the last/current scan.
    private Label _piCasePacker = null!, _piProduct = null!, _piFa = null!, _piProduction = null!;

    public MainForm()
    {
        InitializeComponent();
        LoadLogo();
        BuildHealthStrip();
        BuildProductInfo();

        // Give buttons a subtle hover/press response so clicks feel responsive.
        AddClickFeedback(btnConfig);
        AddClickFeedback(btnManual);
        AddClickFeedback(btnTheme);
        AddClickFeedback(btnScan);

        // Theme the list column headers (WinForms draws them white by default,
        // which looks wrong in dark mode).
        ThemeListHeaders(lvScans);
        ThemeListHeaders(lvEvents);
    }

    private static void ThemeListHeaders(ListView list)
    {
        list.OwnerDraw = true;
        list.DrawColumnHeader += (_, e) =>
        {
            using var bg = new SolidBrush(Theme.SurfaceAlt);
            e.Graphics.FillRectangle(bg, e.Bounds);
            TextFormatFlags flags = TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis
                | (e.Header!.TextAlign == HorizontalAlignment.Right
                    ? TextFormatFlags.Right : TextFormatFlags.Left);
            Rectangle r = e.Bounds;
            r.X += 6;
            r.Width -= 10;
            TextRenderer.DrawText(e.Graphics, e.Header.Text, Theme.BoldFont, r, Theme.Muted, flags);
        };
        // Draw the rows normally (this keeps each row's own text colour).
        list.DrawItem += (_, e) => e.DrawDefault = true;
        list.DrawSubItem += (_, e) => e.DrawDefault = true;

        // An empty trailing column soaks up the leftover header space so there's
        // no white gap after the last real column (visible in dark mode).
        var filler = new ColumnHeader { Text = "" };
        list.Columns.Add(filler);
        void SizeFiller()
        {
            int used = 0;
            for (int i = 0; i < list.Columns.Count - 1; i++)
            {
                used += list.Columns[i].Width;
            }
            // Leave a few pixels so the columns never total more than the client
            // width (which would show a horizontal scrollbar).
            filler.Width = Math.Max(0, list.ClientSize.Width - used - 4);
        }
        list.SizeChanged += (_, _) => SizeFiller();
        list.HandleCreated += (_, _) => SizeFiller();
        SizeFiller();
    }

    private void MainForm_Load(object? sender, EventArgs e)
    {
        try
        {
            _settings = SettingsStore.Load();
            lblStation.Text = $"Station: {_settings.StationId}";

            // Mirror the app log into the on-screen Event Logs.
            AppLog.Entry += OnLogEntry;

            IValidator validator = new InExpressValidator(
                _settings.InExpress.BuildConnectionString(),
                _settings.InExpress.ProcName,
                _settings.InExpress.TimeoutSeconds);

            // Every scan is saved to TWO places: the CCube database (when configured)
            // AND a daily CSV backup on disk. The CSV always runs so history survives
            // a database outage.
            var sinks = new List<IHistorySink>();
            if (_settings.CCube.IsConfigured)
            {
                sinks.Add(new SqlHistoryRepository(_settings.CCube.BuildConnectionString()));
            }
            sinks.Add(new CsvHistoryRepository(_settings.HistoryCsvFolder));
            IHistorySink history = sinks.Count == 1 ? sinks[0] : new CompositeHistorySink(sinks.ToArray());

            _plcLink = new S7PlcLink(_settings.Plc);

            _engine = new ScanEngine(_plcLink, validator, history, _health, _settings.StationId);
            _engine.ScanCompleted += OnScanCompleted;
            _health.Changed += OnHealthChanged;
            _engine.Start();

            OnHealthChanged(_health);

            // Manual Scan starts hidden; the operator opens it with the button.
            ShowManual(false);

            // Give Event Logs a sensible starting height once the form is laid out
            // (the user can drag the splitter to resize it).
            BeginInvoke(new Action(SetInitialLayout));
            AppLog.Info("Run screen ready.");
        }
        catch (Exception ex)
        {
            AppLog.Error("Failed to start the run screen.", ex);
            MessageBox.Show("Failed to start:\n\n" + ex.Message, "C-Cube",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void MainForm_FormClosing(object? sender, FormClosingEventArgs e)
    {
        // The station must not be closable from the UI. There are no window buttons,
        // and this blocks Alt+F4 too (CloseReason.UserClosing). The app can still be
        // ended from Task Manager, and it may restart ITSELF (theme/settings change),
        // which comes through as ApplicationExitCall - not UserClosing - so it works.
        if (e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            return;
        }

        try
        {
            AppLog.Entry -= OnLogEntry;
            _engine?.Stop();
            _engine?.Dispose();
            _plcLink?.Dispose();
        }
        catch (Exception ex)
        {
            AppLog.Error("Error while shutting down.", ex);
        }
    }

    // -------------------------------------------------------------------------
    // Health strip
    // -------------------------------------------------------------------------

    private void BuildHealthStrip()
    {
        var flow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            BackColor = Theme.Surface,
        };
        _ledPlc = MakeLed("PLC");
        _ledInx = MakeLed("INEXPRESS");
        _ledScanner = MakeLed("SCANNER");
        flow.Controls.Add(_ledPlc);
        flow.Controls.Add(_ledInx);
        flow.Controls.Add(_ledScanner);
        pnlHealth.Controls.Add(flow);
    }

    private static Label MakeLed(string name) => new()
    {
        Text = $"●  {name}  —",
        Font = Theme.BoldFont,
        ForeColor = Theme.LedGrey,
        AutoSize = false,
        Size = new Size(220, 32),
        TextAlign = ContentAlignment.MiddleLeft,
        Margin = new Padding(0, 0, 24, 0),
    };

    private static void SetLed(Label led, string name, Health health, string detail = "")
    {
        // Green only when confirmed good; anything else (down or not-yet-known)
        // shows red, so a missing device never looks harmless.
        (Color colour, string state) = health == Health.Up
            ? (Theme.LedGreen, "OK")
            : (Theme.LedRed, string.IsNullOrEmpty(detail) ? "DOWN" : detail);
        led.ForeColor = colour;
        led.Text = $"●  {name}  {state}";
    }

    // -------------------------------------------------------------------------
    // Header logo
    // -------------------------------------------------------------------------

    // Load the ML+ Solutions logo shipped next to the .exe (Assets\ml-logo.png).
    // Loaded via a byte copy so the file isn't left locked; missing/bad file just
    // leaves the header without a logo (never crashes the screen).
    private void LoadLogo()
    {
        try
        {
            string path = Path.Combine(AppContext.BaseDirectory, "Assets", "ml-logo.png");
            if (File.Exists(path))
            {
                byte[] bytes = File.ReadAllBytes(path);
                using var ms = new MemoryStream(bytes);
                pbLogo.Image = new Bitmap(ms);
            }
        }
        catch (Exception ex)
        {
            AppLog.Warn("Could not load the header logo.", ex);
        }
    }

    // -------------------------------------------------------------------------
    // Product Information (labels above Case Logs showing the last/current scan)
    // -------------------------------------------------------------------------

    private void BuildProductInfo()
    {
        var header = new Label
        {
            Text = "PRODUCT INFORMATION",
            Font = Theme.SectionFont,
            ForeColor = Theme.Accent,
            Dock = DockStyle.Top,
            Height = 22,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(12, 0, 0, 0),
            BackColor = Theme.SurfaceAlt,
        };

        var table = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 4,
            RowCount = 1,
            BackColor = Theme.Surface,
            Padding = new Padding(10, 6, 10, 8),
        };
        for (int i = 0; i < 4; i++)
        {
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
        }

        _piCasePacker = AddInfoField(table, 0, "CASE PACKER #");
        _piProduct = AddInfoField(table, 1, "PRODUCT DESCRIPTION");
        _piFa = AddInfoField(table, 2, "FA CODE");
        _piProduction = AddInfoField(table, 3, "PRODUCTION DATE/TIME");

        // Fill added first, header (Top) after, so the header sits above the values.
        pnlProductInfo.Controls.Add(table);
        pnlProductInfo.Controls.Add(header);
    }

    // One "CAPTION over big value" field inside the Product Information row.
    private static Label AddInfoField(TableLayoutPanel table, int col, string caption)
    {
        var holder = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Theme.Surface,
            Margin = new Padding(2, 0, 8, 0),
        };
        var cap = new Label
        {
            Text = caption,
            Font = Theme.SmallFont,
            ForeColor = Theme.Muted,
            Dock = DockStyle.Top,
            Height = 16,
            TextAlign = ContentAlignment.BottomLeft,
        };
        var value = new Label
        {
            Text = "—",
            Font = Theme.CardStateFont,
            ForeColor = Theme.Text,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            AutoEllipsis = true,
        };
        holder.Controls.Add(value);
        holder.Controls.Add(cap);
        table.Controls.Add(holder, col, 0);
        return value;
    }

    // -------------------------------------------------------------------------
    // Engine + log events (arrive on background threads)
    // -------------------------------------------------------------------------

    private void OnScanCompleted(ScanRecord record)
    {
        OnUi(() =>
        {
            if (record.IsPass)
            {
                pnlVerdict.BackColor = Theme.Pass;
                lblVerdict.Text = "PASS";
            }
            else
            {
                pnlVerdict.BackColor = Theme.Reject;
                lblVerdict.Text = "REJECT";
            }
            lblVerdict.ForeColor = Color.White;
            lblVerdictDetail.ForeColor = Color.White;

            string detail = record.Barcode;
            if (!string.IsNullOrEmpty(record.ProductDescription))
            {
                detail += "    •    " + record.ProductDescription;
            }
            detail += "    •    " + ReasonText(record.ResultCode);
            lblVerdictDetail.Text = detail;

            // Product Information panel: show this (the latest) scan's details.
            _piCasePacker.Text = record.CasePacker ?? "—";
            _piProduct.Text = record.ProductDescription ?? "—";
            _piFa.Text = record.FaCode ?? "—";
            _piProduction.Text = record.ProductionAt?.ToString("yyyy-MM-dd HH:mm:ss") ?? "—";

            // One Case Logs row - the 9 columns the operator sees (Id, Station,
            // Barcode, Case Packer #, Product, FA Code, Production time, result,
            // Scanned time). "—" stands in for a value we don't have.
            var item = new ListViewItem(record.InExpressId?.ToString() ?? "—");
            item.SubItems.Add(record.StationId);
            item.SubItems.Add(record.Barcode);
            item.SubItems.Add(record.CasePacker ?? "—");
            item.SubItems.Add(record.ProductDescription ?? "—");
            item.SubItems.Add(record.FaCode ?? "—");
            item.SubItems.Add(record.ProductionAt?.ToString("yyyy-MM-dd HH:mm:ss") ?? "—");
            item.SubItems.Add(record.ValidationResult);
            item.SubItems.Add(record.ScannedAtLocal.ToString("yyyy-MM-dd HH:mm:ss"));
            item.ForeColor = record.IsPass ? Theme.Pass : Theme.Reject;
            lvScans.Items.Insert(0, item);
            while (lvScans.Items.Count > 200)
            {
                lvScans.Items.RemoveAt(lvScans.Items.Count - 1);
            }
        });
    }

    private void OnHealthChanged(HealthMonitor h)
    {
        OnUi(() =>
        {
            SetLed(_ledPlc, "PLC", h.Plc, h.PlcDetail);
            SetLed(_ledInx, "INEXPRESS", h.InExpress);
            SetLed(_ledScanner, "SCANNER", h.Scanner);
        });
    }

    private void OnLogEntry(DateTime when, string level, string message)
    {
        OnUi(() =>
        {
            var item = new ListViewItem(when.ToString("HH:mm:ss"));
            item.SubItems.Add(level);
            item.SubItems.Add(message);
            item.ForeColor = level switch
            {
                "ERROR" => Theme.Reject,
                "WARN" => Theme.Amber,
                _ => Theme.Muted,
            };
            lvEvents.Items.Insert(0, item);
            while (lvEvents.Items.Count > 200)
            {
                lvEvents.Items.RemoveAt(lvEvents.Items.Count - 1);
            }
        });
    }

    // -------------------------------------------------------------------------
    // Manual scan
    // -------------------------------------------------------------------------

    // Give the two splitters sensible starting positions once the form is laid
    // out (large), then set their minimum drag sizes. The operator can drag
    // either bar to resize afterwards.
    private void SetInitialLayout()
    {
        FitCaseLogColumns();
        // Event Logs height (horizontal split below Case Logs); Panel2 = Event Logs.
        SizeSplitter(splitBody, size: splitBody.Height, desiredPanel2: 150, panel1Min: 200, panel2Min: 64);
    }

    // ListView column widths are NOT auto-scaled by WinForms, so on a high-DPI
    // screen the designer widths (set at 96 DPI) are too narrow and the bold header
    // captions get cut ("Case Packe..."). Scale each width to the screen's DPI and
    // make sure it's at least wide enough to draw the full header caption.
    private void FitCaseLogColumns()
    {
        foreach (ColumnHeader col in lvScans.Columns)
        {
            if (string.IsNullOrEmpty(col.Text))
            {
                continue; // the empty trailing filler column sizes itself
            }
            int scaled = LogicalToDeviceUnits(col.Width);
            int captionNeeds = TextRenderer.MeasureText(col.Text, Theme.BoldFont).Width
                + LogicalToDeviceUnits(24); // owner-draw padding + breathing room
            col.Width = Math.Max(scaled, captionNeeds);
        }
    }

    // Sets a splitter to leave 'desiredPanel2' for Panel2, then tightens the min
    // drag sizes - in that order, because SplitterDistance must already satisfy
    // the minimums when they're applied.
    private static void SizeSplitter(SplitContainer sc, int size, int desiredPanel2, int panel1Min, int panel2Min)
    {
        try
        {
            int hardMax = size - sc.Panel2MinSize - sc.SplitterWidth;
            if (hardMax <= sc.Panel1MinSize)
            {
                return; // container still too small; leave the defaults
            }
            sc.SplitterDistance = Math.Clamp(
                size - desiredPanel2 - sc.SplitterWidth, sc.Panel1MinSize, hardMax);

            if (panel1Min <= sc.SplitterDistance &&
                panel2Min <= size - sc.SplitterDistance - sc.SplitterWidth)
            {
                sc.Panel1MinSize = panel1Min;
                sc.Panel2MinSize = panel2Min;
            }
        }
        catch (Exception ex)
        {
            AppLog.Warn("Could not size a splitter.", ex);
        }
    }

    private void btnManual_Click(object? sender, EventArgs e) => ShowManual(!_manualVisible);

    // Flip light/dark. The theme is chosen at start-up, so we save the choice and
    // restart the app to redraw everything cleanly in the new palette.
    private void btnTheme_Click(object? sender, EventArgs e)
    {
        try
        {
            _settings.ThemeDark = !_settings.ThemeDark;
            SettingsStore.Save(_settings);
            AppLog.Info($"Switching to {(_settings.ThemeDark ? "dark" : "light")} theme (restarting).");
            Application.Restart();
        }
        catch (Exception ex)
        {
            AppLog.Error("Failed to switch theme.", ex);
        }
    }

    private void ShowManual(bool show)
    {
        _manualVisible = show;
        // The manual bar is row 0 of tblMain (a TableLayoutPanel), so it sits above
        // the Case Logs and can never overlap them or hide behind the taskbar. Set
        // the row height (DPI-scaled so the textbox isn't clipped on high-DPI), then
        // force the table to relayout - it doesn't react to a RowStyle change alone.
        tblMain.RowStyles[0].Height = show ? LogicalToDeviceUnits(52) : 0;
        pnlManual.Visible = show;
        tblMain.PerformLayout();
        lblModeBadge.Visible = show;

        // The button reads as a toggle: it names the mode you'd switch TO, and
        // looks pressed while manual entry is open.
        btnManual.Text = show ? "Auto Scan" : "Manual Scan";
        if (show)
        {
            btnManual.BackColor = Color.White;
            btnManual.ForeColor = Theme.HeaderGreenDark;
            btnManual.FlatAppearance.BorderSize = 0;
            ActiveControl = txtManual;
        }
        else
        {
            btnManual.BackColor = Theme.HeaderGreenDark;
            btnManual.ForeColor = Color.White;
            btnManual.FlatAppearance.BorderSize = 1;
        }
    }

    private void btnScan_Click(object? sender, EventArgs e) => DoManualScan();

    private void txtManual_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Enter)
        {
            e.SuppressKeyPress = true;
            DoManualScan();
        }
    }

    private async void DoManualScan()
    {
        string barcode = txtManual.Text.Trim();
        if (barcode.Length == 0)
        {
            return;
        }
        if (!BarcodeRules.IsPlausible(barcode))
        {
            _toolTip.Show(
                $"Enter a valid barcode ({BarcodeRules.MinLength}-{BarcodeRules.MaxLength} characters).",
                txtManual, 0, -30, 2500);
            return;
        }

        // Show a loader on the button; run the check off the UI thread so a slow
        // InExpress call can't freeze the screen.
        btnScan.Enabled = false;
        string original = btnScan.Text;
        btnScan.Text = "Scanning…";
        try
        {
            await Task.Run(() => _engine?.ManualCheck(barcode, scannerNoRead: false));
        }
        finally
        {
            btnScan.Enabled = true;
            btnScan.Text = original;
            // Clear the field so the operator can scan the next case straight away.
            txtManual.Clear();
            txtManual.Focus();
        }
    }

    // Adds hover/press colours so a click visibly registers.
    private static void AddClickFeedback(Button button)
    {
        button.FlatAppearance.MouseOverBackColor = Blend(button.BackColor, Color.White, 0.12);
        button.FlatAppearance.MouseDownBackColor = Blend(button.BackColor, Color.Black, 0.14);
    }

    private static Color Blend(Color a, Color b, double t) => Color.FromArgb(
        (int)(a.R + (b.R - a.R) * t),
        (int)(a.G + (b.G - a.G) * t),
        (int)(a.B + (b.B - a.B) * t));

    // -------------------------------------------------------------------------
    // Configuration
    // -------------------------------------------------------------------------

    private void btnConfig_Click(object? sender, EventArgs e)
    {
        try
        {
            using var form = new SettingsForm(_settings);
            form.ShowDialog(this);

            if (form.SavedChanges)
            {
                var answer = MessageBox.Show(
                    "Settings saved. Restart C-Cube now to apply them?",
                    "C-Cube", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (answer == DialogResult.Yes)
                {
                    AppLog.Info("Restarting to apply new settings.");
                    Application.Restart();
                }
            }
        }
        catch (Exception ex)
        {
            AppLog.Error("Failed to open the Configuration screen.", ex);
            MessageBox.Show("Could not open Configuration:\n\n" + ex.Message,
                "C-Cube", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private void OnUi(Action action)
    {
        if (IsDisposed || !IsHandleCreated)
        {
            return;
        }
        try
        {
            if (InvokeRequired)
            {
                BeginInvoke(action);
            }
            else
            {
                action();
            }
        }
        catch (Exception ex)
        {
            AppLog.Error("UI update failed.", ex);
        }
    }

    private static string ReasonText(ResultCode code) => code switch
    {
        ResultCode.Ok => "Valid",
        ResultCode.NotValid => "Not valid",
        ResultCode.ScannerNoRead => "Scanner no-read",
        ResultCode.BadBarcode => "Bad barcode",
        ResultCode.Timeout => "InExpress timeout",
        ResultCode.Error => "InExpress error",
        ResultCode.NotReady => "Not ready",
        _ => code.ToString(),
    };
}
