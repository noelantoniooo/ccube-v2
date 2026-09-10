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

    private int _total;
    private int _pass;
    private int _reject;
    private bool _manualVisible;
    private readonly ToolTip _toolTip = new();

    // Health strip labels.
    private Label _ledPlc = null!, _ledInx = null!, _ledScanner = null!;
    // Counters + reject-reason breakdown.
    private Label _lblTotal = null!, _lblPass = null!, _lblReject = null!;
    private readonly Dictionary<ResultCode, Label> _reasonCounts = new();
    // Rejects-monitoring grid (one row per case-packer, counts by shift).
    private ListView _lvMonitor = null!;
    private readonly Dictionary<string, ListViewItem> _monitorRows = new();

    public MainForm()
    {
        InitializeComponent();
        BuildHealthStrip();
        BuildSidePanel();

        // Header logo (safe if the file is missing - the box just stays blank).
        pbLogo.Image = LoadLogo("logo-controlsystems.png");

        // Give buttons a subtle hover/press response so clicks feel responsive.
        AddClickFeedback(btnConfig);
        AddClickFeedback(btnManual);
        AddClickFeedback(btnTheme);
        AddClickFeedback(btnScan);

        // Theme the list column headers (WinForms draws them white by default,
        // which looks wrong in dark mode).
        ThemeListHeaders(lvScans);
        ThemeListHeaders(lvEvents);
        ThemeListHeaders(_lvMonitor);
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

    // Loads a header logo from the Resources folder next to the .exe. Returns null
    // if the file isn't there (replace the PNG to change the logo).
    private static Image? LoadLogo(string fileName)
    {
        try
        {
            string path = Path.Combine(AppContext.BaseDirectory, "Resources", fileName);
            if (File.Exists(path))
            {
                // Read via a stream copy so the file isn't left locked on disk.
                byte[] bytes = File.ReadAllBytes(path);
                using var ms = new MemoryStream(bytes);
                return Image.FromStream(ms);
            }
            AppLog.Warn($"Header logo not found: {path}");
        }
        catch (Exception ex)
        {
            AppLog.Warn($"Could not load header logo '{fileName}'.", ex);
        }
        return null;
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

            IHistorySink history = _settings.CCube.IsConfigured
                ? new SqlHistoryRepository(_settings.CCube.BuildConnectionString())
                : new NullHistorySink();

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
    // Side panel (counters + reject reasons)
    // -------------------------------------------------------------------------

    private void BuildSidePanel()
    {
        // The right column stacks three sections: counters (top), rejects
        // monitoring (per case-packer, by shift), and the reject-reason totals.

        // --- Reject Reasons (fixed height at the bottom so all six always show) ---
        var reasonsPanel = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 172, // header + all six reason rows, always fully visible
            BackColor = Theme.Surface,
            BorderStyle = BorderStyle.FixedSingle,
        };
        var reasons = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Theme.Surface,
            ColumnCount = 2,
            Padding = new Padding(4, 2, 4, 4),
        };
        reasons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        reasons.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 56F));

        ResultCode[] rejectReasons =
        {
            ResultCode.NotValid, ResultCode.ScannerNoRead, ResultCode.BadBarcode,
            ResultCode.Timeout, ResultCode.Error, ResultCode.NotReady,
        };
        reasons.RowCount = rejectReasons.Length + 1;
        for (int r = 0; r < rejectReasons.Length; r++)
        {
            reasons.RowStyles.Add(new RowStyle(SizeType.Absolute, 23F));
        }
        reasons.RowStyles.Add(new RowStyle(SizeType.Percent, 100F)); // filler

        for (int row = 0; row < rejectReasons.Length; row++)
        {
            ResultCode code = rejectReasons[row];
            var name = new Label
            {
                Text = ReasonText(code),
                Font = Theme.NormalFont,
                ForeColor = Theme.Text,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(8, 0, 0, 0),
            };
            var count = new Label
            {
                Text = "0",
                Font = Theme.BoldFont,
                ForeColor = Theme.Muted,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleRight,
                Padding = new Padding(0, 0, 10, 0),
            };
            reasons.Controls.Add(name, 0, row);
            reasons.Controls.Add(count, 1, row);
            _reasonCounts[code] = count;
        }
        reasonsPanel.Controls.Add(reasons);
        reasonsPanel.Controls.Add(SideHeader("REJECT REASONS"));

        // --- Rejects Monitoring (per case-packer, counts by shift; scrolls inside) ---
        var monitorPanel = new Panel
        {
            Dock = DockStyle.Fill, // takes the leftover space (scrolls inside if many packers)
            BackColor = Theme.Surface,
            BorderStyle = BorderStyle.FixedSingle,
        };
        _lvMonitor = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true,
            BackColor = Theme.Surface,
            ForeColor = Theme.Text,
            Font = Theme.NormalFont,
            BorderStyle = BorderStyle.None,
            HeaderStyle = ColumnHeaderStyle.Nonclickable,
        };
        _lvMonitor.Columns.Add("Packer", 66);
        _lvMonitor.Columns.Add("1st", 54, HorizontalAlignment.Right);
        _lvMonitor.Columns.Add("2nd", 54, HorizontalAlignment.Right);
        _lvMonitor.Columns.Add("3rd", 54, HorizontalAlignment.Right);
        _lvMonitor.Columns.Add("Total", 62, HorizontalAlignment.Right);
        monitorPanel.Controls.Add(_lvMonitor);
        monitorPanel.Controls.Add(SideHeader("REJECTS MONITORING  ·  BY PACKER"));

        // --- Counters (top) ---
        var counters = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 74,
            ColumnCount = 3,
            RowCount = 1,
            BackColor = Theme.Background,
        };
        for (int i = 0; i < 3; i++)
        {
            counters.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F / 3F));
        }
        _lblTotal = MakeCounterTile(counters, 0, "TOTAL", Theme.Text);
        _lblPass = MakeCounterTile(counters, 1, "PASS", Theme.Pass);
        _lblReject = MakeCounterTile(counters, 2, "REJECT", Theme.Reject);

        // Add the Fill section first, then Bottom, then Top, so the stack is
        // counters (top) → monitoring (fills the middle) → reasons (bottom).
        pnlSide.Controls.Add(monitorPanel);
        pnlSide.Controls.Add(reasonsPanel);
        pnlSide.Controls.Add(counters);
    }

    private static Label SideHeader(string text) => new()
    {
        Text = text,
        Font = Theme.SectionFont,
        ForeColor = Theme.Accent,
        Dock = DockStyle.Top,
        Height = 22,
        TextAlign = ContentAlignment.MiddleLeft,
        Padding = new Padding(8, 0, 0, 0),
        BackColor = Theme.SurfaceAlt,
    };

    // 3-shift plant clock: 1st 06:00-14:00, 2nd 14:00-22:00, 3rd 22:00-06:00.
    private static int ShiftIndex(DateTime when) => when.Hour switch
    {
        >= 6 and < 14 => 0,
        >= 14 and < 22 => 1,
        _ => 2,
    };

    private void UpdateMonitor(ScanRecord record)
    {
        string packer = BarcodeFields.CasePackerId(record.Barcode);
        int shift = ShiftIndex(record.ScannedAtLocal);

        if (!_monitorRows.TryGetValue(packer, out ListViewItem? row))
        {
            row = new ListViewItem(packer);
            row.SubItems.Add("0"); // 1st
            row.SubItems.Add("0"); // 2nd
            row.SubItems.Add("0"); // 3rd
            row.SubItems.Add("0"); // total
            _lvMonitor.Items.Add(row);
            _monitorRows[packer] = row;
        }
        row.SubItems[shift + 1].Text = (int.Parse(row.SubItems[shift + 1].Text) + 1).ToString();
        row.SubItems[4].Text = (int.Parse(row.SubItems[4].Text) + 1).ToString();
    }

    private static Label MakeCounterTile(TableLayoutPanel grid, int col, string caption, Color colour)
    {
        var tile = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Theme.Surface,
            BorderStyle = BorderStyle.FixedSingle,
            Margin = TileMargin(col),
        };
        var number = new Label
        {
            Text = "0",
            Font = Theme.CounterFont,
            ForeColor = colour,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
        };
        var cap = new Label
        {
            Text = caption,
            Font = Theme.SmallFont,
            ForeColor = Theme.Muted,
            Dock = DockStyle.Bottom,
            Height = 18,
            TextAlign = ContentAlignment.MiddleCenter,
        };
        tile.Controls.Add(number);
        tile.Controls.Add(cap);
        grid.Controls.Add(tile, col, 0);
        return number;
    }

    private static Padding TileMargin(int col) => col switch
    {
        0 => new Padding(0, 4, 4, 4),
        2 => new Padding(4, 4, 0, 4),
        _ => new Padding(4, 4, 4, 4),
    };

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
            if (!string.IsNullOrEmpty(record.ProductName))
            {
                detail += "    •    " + record.ProductName;
            }
            detail += "    •    " + ReasonText(record.ResultCode);
            lblVerdictDetail.Text = detail;

            _total++;
            if (record.IsPass)
            {
                _pass++;
            }
            else
            {
                _reject++;
                if (_reasonCounts.TryGetValue(record.ResultCode, out Label? counter))
                {
                    counter.Text = (int.Parse(counter.Text) + 1).ToString();
                    counter.ForeColor = Theme.Reject;
                }
                UpdateMonitor(record); // per-case-packer / shift breakdown
            }
            _lblTotal.Text = _total.ToString();
            _lblPass.Text = _pass.ToString();
            _lblReject.Text = _reject.ToString();

            var item = new ListViewItem(record.ScannedAtLocal.ToString("HH:mm:ss"));
            item.SubItems.Add(record.Barcode);
            item.SubItems.Add(record.Verdict);
            item.SubItems.Add(ReasonText(record.ResultCode));
            item.SubItems.Add(record.TotalMs.ToString());
            item.ForeColor = record.IsPass ? Theme.Pass : Theme.Reject;
            lvScans.Items.Insert(0, item);
            while (lvScans.Items.Count > 100)
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
        // Event Logs height (horizontal split below Case Logs); Panel2 = Event Logs.
        SizeSplitter(splitBody, size: splitBody.Height, desiredPanel2: 150, panel1Min: 200, panel2Min: 64);
        // Right-column width (vertical split beside Case Logs); Panel2 = right column.
        SizeSplitter(splitMain, size: splitMain.Width, desiredPanel2: 366, panel1Min: 380, panel2Min: 260);
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
        pnlManual.Visible = show;
        root.RowStyles[4].Height = show ? 48 : 0;
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

    // Barcodes are letters, digits and a few separators - block spaces/symbols.
    private void txtManual_KeyPress(object? sender, KeyPressEventArgs e)
    {
        if (char.IsControl(e.KeyChar))
        {
            return; // allow Backspace, Enter, etc.
        }
        bool allowed = char.IsLetterOrDigit(e.KeyChar) || e.KeyChar is '-' or '_' or '.';
        if (!allowed)
        {
            e.Handled = true;
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
            txtManual.SelectAll();
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
