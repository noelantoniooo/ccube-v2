// SettingsForm.cs
// -----------------------------------------------------------------------------
// The Configuration screen. It shows every setting the app uses and lets an
// admin change them.
//
// Two states:
//   * READ-ONLY (the default) - you can look and you can use "Test connection",
//     but every field is disabled.
//   * EDITING - after you click "Edit" and enter the admin password, the fields
//     unlock. "Save" writes them (encrypting DB passwords) and asks you to
//     restart so the app picks them up.
//
// This form is built in code (rather than the visual designer) because it's just
// a long list of labelled boxes - a helper method (AddText/AddNumber/AddCheck)
// keeps it short and consistent. Read it top to bottom.
// -----------------------------------------------------------------------------

using CCube.Core;

namespace CCube.Forms;

public sealed class SettingsForm : Form
{
    private readonly AppSettings _settings;

    // True if the user saved changes (so the caller can offer to restart).
    public bool SavedChanges { get; private set; }

    // Every control the admin may edit - toggled together by SetEditable().
    private readonly List<Control> _editable = new();

    // General
    private TextBox _station = null!;
    private CheckBox _demoMode = null!;
    // InExpress
    private TextBox _inxServer = null!, _inxDb = null!, _inxUser = null!, _inxPass = null!, _inxProc = null!;
    private Label _inxUserLbl = null!, _inxPassLbl = null!;
    private CheckBox _inxIntegrated = null!;
    private NumericUpDown _inxTimeout = null!;
    private Label _inxResult = null!;
    // CCube
    private TextBox _ccServer = null!, _ccDb = null!, _ccUser = null!, _ccPass = null!;
    private Label _ccUserLbl = null!, _ccPassLbl = null!;
    private CheckBox _ccIntegrated = null!;
    private Label _ccResult = null!;
    // PLC
    private TextBox _plcIp = null!;
    private NumericUpDown _plcRack = null!, _plcSlot = null!, _plcDb = null!;
    private Label _plcResult = null!;
    // Machine # masking
    private CheckBox _maskEnabled = null!;
    private Panel _maskPanel = null!;
    private readonly Dictionary<int, CheckBox> _machineChecks = new();
    // Buttons
    private Button _editButton = null!, _saveButton = null!, _changeAdminButton = null!;
    private Label _lockState = null!;

    public SettingsForm(AppSettings settings)
    {
        _settings = settings;
        BuildUi();
        LoadFromSettings();
        SetEditable(false); // always start locked

        // Re-flow once the form is actually shown - before that, Control.Visible
        // reports false for everything, so the reflow can't tell what's hidden.
        Shown += (_, _) => RelayoutBody();
    }

    // -------------------------------------------------------------------------
    // Building the screen
    // -------------------------------------------------------------------------

    private Panel _body = null!;

    // Each visual row (label+field, header, etc.). Lets us re-flow the body so the
    // space collapses when the SQL username/password rows are hidden.
    private readonly List<RowSlot> _rows = new();
    private int _firstRowTop;
    private sealed record RowSlot((Control Control, int Dy)[] Items, int Height);

    private void RegisterRow(int top, int height, params Control[] controls)
    {
        if (_rows.Count == 0)
        {
            _firstRowTop = top;
        }
        var items = new (Control, int)[controls.Length];
        for (int i = 0; i < controls.Length; i++)
        {
            items[i] = (controls[i], controls[i].Top - top);
        }
        _rows.Add(new RowSlot(items, height));
    }

    // Lay the rows out top-to-bottom, skipping any whose controls are hidden, so
    // hiding the credential rows removes the gap instead of leaving empty space.
    private void RelayoutBody()
    {
        // Positions must be set in the un-scrolled frame, otherwise setting Top
        // while the panel is scrolled drifts and adds space on each toggle. So
        // scroll to the top, lay out, then restore the scroll position.
        int scroll = -_body.AutoScrollPosition.Y;
        _body.SuspendLayout();
        _body.AutoScrollPosition = new Point(0, 0);

        int y = _firstRowTop;
        foreach (RowSlot row in _rows)
        {
            if (!row.Items[0].Control.Visible)
            {
                continue;
            }
            foreach ((Control control, int dy) in row.Items)
            {
                control.Top = y + dy;
            }
            y += row.Height;
        }

        _body.ResumeLayout();
        _body.AutoScrollPosition = new Point(0, scroll);
    }

    private void BuildUi()
    {
        Text = "C-Cube  -  Configuration";
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(720, 720);
        BackColor = Theme.Background;
        ForeColor = Theme.Text;
        Font = Theme.NormalFont;
        MinimumSize = new Size(700, 520);

        _body = new Panel { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(24, 16, 24, 16) };
        Controls.Add(_body);

        int y = 8;
        _lockState = MakeLabel("🔒  Read-only. Click Edit and enter the admin password to change.",
            24, y, Theme.Muted);
        _lockState.AutoSize = true;
        _body.Controls.Add(_lockState);
        RegisterRow(y, 40, _lockState);
        y += 40;

        // --- General ---
        y = Section("General", y);
        _station = AddText("Station ID", ref y);
        _demoMode = AddCheck("Demo mode (fake PLC — run/test without hardware)", ref y);

        // --- InExpress ---
        y = Section("InExpress database (validation)", y);
        _inxServer = AddText("Server", ref y);
        _inxDb = AddText("Database", ref y);
        _inxIntegrated = AddCheck("Use Windows login (integrated security)", ref y);
        (_inxUserLbl, _inxUser) = AddLabeledText("SQL username", ref y);
        (_inxPassLbl, _inxPass) = AddLabeledText("SQL password", ref y, password: true);
        _inxProc = AddText("Validate procedure", ref y);
        _inxTimeout = AddNumber("Validation Timeout (secs)", ref y, 1, 30);
        var (btnTestInx, inxResult) = AddTestRow("Test InExpress connection", ref y);
        _inxResult = inxResult;
        btnTestInx.Click += async (_, _) =>
        {
            string conn = SqlConnStringsFromFields(_inxServer, _inxDb, _inxIntegrated, _inxUser, _inxPass);
            await RunTest(btnTestInx, _inxResult, () => ConnectionTester.TestSql(conn));
        };
        _inxIntegrated.CheckedChanged += (_, _) =>
            ApplyIntegratedToggle(_inxIntegrated, _inxUser, _inxPass, _inxUserLbl, _inxPassLbl);

        // --- CCube ---
        y = Section("C-Cube database (history)", y);
        _ccServer = AddText("Server", ref y);
        _ccDb = AddText("Database", ref y);
        _ccIntegrated = AddCheck("Use Windows login (integrated security)", ref y);
        (_ccUserLbl, _ccUser) = AddLabeledText("SQL username", ref y);
        (_ccPassLbl, _ccPass) = AddLabeledText("SQL password", ref y, password: true);
        var (btnTestCc, ccResult) = AddTestRow("Test C-Cube connection", ref y);
        _ccResult = ccResult;
        btnTestCc.Click += async (_, _) =>
        {
            string conn = SqlConnStringsFromFields(_ccServer, _ccDb, _ccIntegrated, _ccUser, _ccPass);
            await RunTest(btnTestCc, _ccResult, () => ConnectionTester.TestSql(conn));
        };
        _ccIntegrated.CheckedChanged += (_, _) =>
            ApplyIntegratedToggle(_ccIntegrated, _ccUser, _ccPass, _ccUserLbl, _ccPassLbl);

        // --- PLC ---
        y = Section("PLC (Siemens S7-1200)", y);
        _plcIp = AddText("IP address", ref y);
        _plcRack = AddNumber("Rack", ref y, 0, 7);
        _plcSlot = AddNumber("Slot", ref y, 0, 31);
        _plcDb = AddNumber("Data block number", ref y, 1, 60000);
        var (btnTestPlc, plcResult) = AddTestRow("Test PLC reachability", ref y);
        _plcResult = plcResult;
        btnTestPlc.Click += async (_, _) =>
        {
            string ip = _plcIp.Text.Trim();
            await RunTest(btnTestPlc, _plcResult, () => ConnectionTester.TestPlc(ip));
        };

        // --- Machine # masking ---
        y = Section("Machine # masking", y);
        _maskEnabled = AddCheck("Enable masking", ref y);
        y = AddMachineGrid(ref y);
        // The machine grid only shows when masking is enabled.
        _maskEnabled.CheckedChanged += (_, _) =>
        {
            _maskPanel.Visible = _maskEnabled.Checked;
            RelayoutBody();
        };

        // (No Security section on the page - the admin password is only asked for
        // when you click Edit, and can be changed from the button in the bar below.)

        // --- Bottom button bar ---
        var bar = new Panel { Dock = DockStyle.Bottom, Height = 56, BackColor = Theme.SurfaceAlt };

        // Left buttons flow left-to-right, so hiding one (e.g. Save) closes the gap.
        var leftButtons = new FlowLayoutPanel
        {
            Dock = DockStyle.Left,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = Theme.SurfaceAlt,
            Padding = new Padding(12, 11, 0, 0),
        };
        _editButton = BarButton("Edit", Theme.Accent);
        _editButton.Click += (_, _) => BeginEdit();
        _saveButton = BarButton("Save", Theme.Pass);
        _saveButton.Click += (_, _) => SaveChanges();
        var closeButton = BarButton("Close", Theme.Idle);
        closeButton.ForeColor = Theme.Text; // dark text on the light Close button
        closeButton.Click += (_, _) => Close();
        leftButtons.Controls.Add(_editButton);
        leftButtons.Controls.Add(_saveButton);
        leftButtons.Controls.Add(closeButton);

        // "Change password…" sits on the right and is only shown while editing.
        _changeAdminButton = BarButton("Change password…", Theme.Panel);
        _changeAdminButton.Width = 174;
        _changeAdminButton.ForeColor = Theme.Accent;
        _changeAdminButton.FlatAppearance.BorderColor = Theme.Border;
        _changeAdminButton.FlatAppearance.BorderSize = 1;
        _changeAdminButton.Location = new Point(ClientSize.Width - 190, 11);
        _changeAdminButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        _changeAdminButton.Click += (_, _) => ChangeAdminPassword();
        _editable.Add(_changeAdminButton);

        foreach (Button b in new[] { _editButton, _saveButton, closeButton, _changeAdminButton })
        {
            AddClickFeedback(b);
        }
        bar.Controls.Add(leftButtons);
        bar.Controls.Add(_changeAdminButton);
        Controls.Add(bar);
    }

    // ---- Layout helpers (keep the code above short and consistent) ----

    private int Section(string title, int y)
    {
        int top = y;
        y += 12;
        var header = MakeLabel(title, 20, y, Theme.Accent);
        header.Font = Theme.BoldFont;
        header.AutoSize = true;
        _body.Controls.Add(header);
        RegisterRow(top, 42, header);
        return y + 30;
    }

    private static Label MakeLabel(string text, int x, int y, Color colour) => new()
    {
        Text = text,
        Location = new Point(x, y),
        ForeColor = colour,
        AutoSize = true,
    };

    private TextBox AddText(string label, ref int y, int width = 380, bool password = false)
        => AddLabeledText(label, ref y, password, width).box;

    // Like AddText, but also returns the label so callers can show/hide the whole row.
    private (Label label, TextBox box) AddLabeledText(string label, ref int y,
        bool password = false, int width = 380)
    {
        var lbl = MakeLabel(label, 24, y + 4, Theme.Text);
        _body.Controls.Add(lbl);
        var box = new TextBox
        {
            Location = new Point(230, y),
            Size = new Size(width, 27),
            BackColor = Theme.PanelDark,
            ForeColor = Theme.Text,
            BorderStyle = BorderStyle.FixedSingle,
            UseSystemPasswordChar = password,
        };
        _body.Controls.Add(box);
        _editable.Add(box);
        RegisterRow(y, 36, lbl, box);
        y += 36;
        return (lbl, box);
    }

    // A grid of machine-number checkboxes. The list of machines comes from
    // settings.json (Masking.AllMachines) - not hardcoded here - so it's just edit +
    // restart to change. Built as one panel so the whole block can be shown/hidden
    // (and its space reclaimed) with the master checkbox.
    private int AddMachineGrid(ref int y)
    {
        const int cols = 9, cellW = 66, cellH = 30;
        IReadOnlyList<int> machines = _settings.Masking.Roster();
        int rows = (machines.Count + cols - 1) / cols; // ceil
        _maskPanel = new Panel
        {
            Location = new Point(24, y),
            Size = new Size(cols * cellW, Math.Max(1, rows) * cellH + 4),
            BackColor = Theme.Background,
        };
        int idx = 0;
        foreach (int machine in machines)
        {
            var box = new CheckBox
            {
                Text = machine.ToString(),
                Location = new Point((idx % cols) * cellW, (idx / cols) * cellH),
                Size = new Size(cellW - 6, cellH - 4),
                ForeColor = Theme.Text,
                AutoSize = false,
            };
            _machineChecks[machine] = box;
            _editable.Add(box);
            _maskPanel.Controls.Add(box);
            idx++;
        }
        _body.Controls.Add(_maskPanel);
        RegisterRow(y, _maskPanel.Height + 12, _maskPanel);
        return y + _maskPanel.Height + 12;
    }

    private CheckBox AddCheck(string label, ref int y)
    {
        var box = new CheckBox
        {
            Text = label,
            Location = new Point(230, y),
            AutoSize = true,
            ForeColor = Theme.Text,
        };
        _body.Controls.Add(box);
        _editable.Add(box);
        RegisterRow(y, 34, box);
        y += 34;
        return box;
    }

    private NumericUpDown AddNumber(string label, ref int y, int min, int max)
    {
        var lbl = MakeLabel(label, 24, y + 4, Theme.Text);
        _body.Controls.Add(lbl);
        var box = new NumericUpDown
        {
            Location = new Point(230, y),
            Size = new Size(100, 27),
            Minimum = min,
            Maximum = max,
            BackColor = Theme.PanelDark,
            ForeColor = Theme.Text,
            BorderStyle = BorderStyle.FixedSingle,
        };
        _body.Controls.Add(box);
        _editable.Add(box);
        RegisterRow(y, 36, lbl, box);
        y += 36;
        return box;
    }

    // A "Test connection" button plus a result label under it. Returns both so
    // the caller can wire an async click that shows a loader on the button.
    private (Button button, Label result) AddTestRow(string buttonText, ref int y)
    {
        int top = y;
        var button = new Button
        {
            Text = buttonText,
            Location = new Point(230, y),
            Size = new Size(210, 32),
            BackColor = Theme.SurfaceAlt,
            ForeColor = Theme.Accent,
            Font = Theme.BoldFont,
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand,
        };
        button.FlatAppearance.BorderColor = Theme.Accent;
        button.FlatAppearance.BorderSize = 1;
        AddClickFeedback(button);
        _body.Controls.Add(button);
        y += 40;

        // The result goes on its OWN full-width row so a long error message wraps
        // onto a second line instead of running off the edge.
        var result = new Label
        {
            Location = new Point(26, y),
            AutoSize = false,
            Size = new Size(620, 34),
            Font = Theme.SmallFont,
            ForeColor = Theme.Muted,
        };
        _body.Controls.Add(result);
        RegisterRow(top, 80, button, result);

        y += 40;
        return (button, result);
    }

    // Runs a connection test off the UI thread, showing a "Testing…" loader on the
    // button so the user knows it's working (a bad server can take a few seconds).
    private static async Task RunTest(Button button, Label result, Func<(bool ok, string message)> test)
    {
        button.Enabled = false;
        string original = button.Text;
        button.Text = "Testing…";
        result.ForeColor = Theme.Muted;
        result.Text = "Testing…";
        try
        {
            (bool ok, string message) = await Task.Run(test);
            result.ForeColor = ok ? Theme.LedGreen : Theme.LedRed;
            result.Text = (ok ? "✔ " : "✖ ") + message;
        }
        finally
        {
            button.Enabled = true;
            button.Text = original;
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

    private static Button BarButton(string text, Color colour)
    {
        var button = new Button
        {
            Text = text,
            Size = new Size(96, 34),
            Margin = new Padding(0, 0, 8, 0), // gap between flowed buttons
            BackColor = colour,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
        };
        button.FlatAppearance.BorderSize = 0;
        return button;
    }

    // -------------------------------------------------------------------------
    // Load / save
    // -------------------------------------------------------------------------

    private void LoadFromSettings()
    {
        _station.Text = _settings.StationId;
        _demoMode.Checked = _settings.DemoMode;

        _inxServer.Text = _settings.InExpress.Server;
        _inxDb.Text = _settings.InExpress.Database;
        _inxIntegrated.Checked = _settings.InExpress.IntegratedSecurity;
        _inxUser.Text = _settings.InExpress.Username;
        _inxPass.Text = _settings.InExpress.Password;
        _inxProc.Text = _settings.InExpress.ProcName;
        _inxTimeout.Value = Math.Clamp(_settings.InExpress.TimeoutSeconds, 1, 30);

        _ccServer.Text = _settings.CCube.Server;
        _ccDb.Text = _settings.CCube.Database;
        _ccIntegrated.Checked = _settings.CCube.IntegratedSecurity;
        _ccUser.Text = _settings.CCube.Username;
        _ccPass.Text = _settings.CCube.Password;

        _plcIp.Text = _settings.Plc.IpAddress;
        _plcRack.Value = Math.Clamp(_settings.Plc.Rack, 0, 7);
        _plcSlot.Value = Math.Clamp(_settings.Plc.Slot, 0, 31);
        _plcDb.Value = Math.Clamp(_settings.Plc.DbNumber, 1, 60000);

        _maskEnabled.Checked = _settings.Masking.Enabled;
        foreach ((int machine, CheckBox box) in _machineChecks)
        {
            box.Checked = _settings.Masking.MaskedMachines.Contains(machine);
        }
        _maskPanel.Visible = _maskEnabled.Checked;
    }

    private void SaveChanges()
    {
        try
        {
            _settings.StationId = _station.Text.Trim();
            _settings.DemoMode = _demoMode.Checked;

            _settings.InExpress.Server = _inxServer.Text.Trim();
            _settings.InExpress.Database = _inxDb.Text.Trim();
            _settings.InExpress.IntegratedSecurity = _inxIntegrated.Checked;
            _settings.InExpress.Username = _inxUser.Text.Trim();
            _settings.InExpress.Password = _inxPass.Text;
            _settings.InExpress.ProcName = _inxProc.Text.Trim();
            _settings.InExpress.TimeoutSeconds = (int)_inxTimeout.Value;

            _settings.CCube.Server = _ccServer.Text.Trim();
            _settings.CCube.Database = _ccDb.Text.Trim();
            _settings.CCube.IntegratedSecurity = _ccIntegrated.Checked;
            _settings.CCube.Username = _ccUser.Text.Trim();
            _settings.CCube.Password = _ccPass.Text;

            _settings.Plc.IpAddress = _plcIp.Text.Trim();
            _settings.Plc.Rack = (int)_plcRack.Value;
            _settings.Plc.Slot = (int)_plcSlot.Value;
            _settings.Plc.DbNumber = (int)_plcDb.Value;

            _settings.Masking.Enabled = _maskEnabled.Checked;
            _settings.Masking.MaskedMachines = _machineChecks
                .Where(kv => kv.Value.Checked)
                .Select(kv => kv.Key)
                .OrderBy(m => m)
                .ToList();

            SettingsStore.Save(_settings);
            SavedChanges = true;

            MessageBox.Show("Settings saved.\n\nRestart C-Cube for the changes to take effect.",
                "C-Cube", MessageBoxButtons.OK, MessageBoxIcon.Information);

            SetEditable(false);
        }
        catch (Exception ex)
        {
            AppLog.Error("Saving settings failed.", ex);
            MessageBox.Show("Could not save settings:\n\n" + ex.Message,
                "C-Cube", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    // -------------------------------------------------------------------------
    // Edit lock / unlock
    // -------------------------------------------------------------------------

    private void BeginEdit()
    {
        using var prompt = new AdminPromptForm(_settings.Admin.PasswordHash);
        if (prompt.ShowDialog(this) == DialogResult.OK)
        {
            SetEditable(true);
        }
    }

    private void SetEditable(bool editable)
    {
        foreach (Control control in _editable)
        {
            control.Enabled = editable;
        }
        ApplyIntegratedToggle(_inxIntegrated, _inxUser, _inxPass, _inxUserLbl, _inxPassLbl);
        ApplyIntegratedToggle(_ccIntegrated, _ccUser, _ccPass, _ccUserLbl, _ccPassLbl);

        // Show only one of Edit / Save at a time (Save appears after admin unlock,
        // Edit comes back after saving).
        _saveButton.Visible = editable;
        _editButton.Visible = !editable;
        _changeAdminButton.Visible = editable; // only offer it while editing
        _lockState.Text = editable
            ? "✏️  Editing. Make your changes, then click Save."
            : "🔒  Read-only. Click Edit and enter the admin password to change.";
        _lockState.ForeColor = editable ? Theme.Accent : Theme.Muted;
    }

    // When "use Windows login" is ticked, the SQL username/password fields don't
    // apply, so hide them (and their labels) and re-flow so the gap closes.
    private void ApplyIntegratedToggle(CheckBox integrated, TextBox user, TextBox pass,
        Label userLabel, Label passLabel)
    {
        bool showSql = !integrated.Checked;
        user.Visible = showSql;
        pass.Visible = showSql;
        userLabel.Visible = showSql;
        passLabel.Visible = showSql;
        RelayoutBody();
    }

    // Build a SQL connection string from the current field values (UI thread).
    // The test buttons are harmless reads, so they need no admin password.
    private static string SqlConnStringsFromFields(TextBox server, TextBox db,
        CheckBox integrated, TextBox user, TextBox pass) =>
        new CCubeSettings
        {
            Server = server.Text.Trim(),
            Database = db.Text.Trim(),
            IntegratedSecurity = integrated.Checked,
            Username = user.Text.Trim(),
            Password = pass.Text,
        }.BuildConnectionString();

    // -------------------------------------------------------------------------
    // Change admin password
    // -------------------------------------------------------------------------

    private void ChangeAdminPassword()
    {
        using var dialog = new Form
        {
            Text = "Change admin password",
            FormBorderStyle = FormBorderStyle.FixedDialog,
            StartPosition = FormStartPosition.CenterParent,
            ClientSize = new Size(400, 200),
            BackColor = Theme.Panel,
            ForeColor = Theme.Text,
            Font = Theme.NormalFont,
            MaximizeBox = false,
            MinimizeBox = false,
        };

        var newBox = new TextBox { UseSystemPasswordChar = true, Location = new Point(18, 40), Size = new Size(364, 27), BackColor = Theme.PanelDark, ForeColor = Theme.Text, BorderStyle = BorderStyle.FixedSingle };
        var confirmBox = new TextBox { UseSystemPasswordChar = true, Location = new Point(18, 104), Size = new Size(364, 27), BackColor = Theme.PanelDark, ForeColor = Theme.Text, BorderStyle = BorderStyle.FixedSingle };
        var ok = new Button { Text = "Save", DialogResult = DialogResult.OK, Location = new Point(212, 150), Size = new Size(84, 34), BackColor = Theme.Pass, ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
        ok.FlatAppearance.BorderSize = 0;
        var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Location = new Point(302, 150), Size = new Size(84, 34), BackColor = Theme.Idle, ForeColor = Theme.Text, FlatStyle = FlatStyle.Flat };
        cancel.FlatAppearance.BorderSize = 0;

        dialog.Controls.Add(MakeLabel("New password:", 18, 16, Theme.Text));
        dialog.Controls.Add(newBox);
        dialog.Controls.Add(MakeLabel("Confirm password:", 18, 80, Theme.Text));
        dialog.Controls.Add(confirmBox);
        dialog.Controls.Add(ok);
        dialog.Controls.Add(cancel);
        dialog.AcceptButton = ok;
        dialog.CancelButton = cancel;

        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        if (newBox.Text.Length < 6)
        {
            MessageBox.Show("Password must be at least 6 characters.", "C-Cube",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        if (newBox.Text != confirmBox.Text)
        {
            MessageBox.Show("The two passwords don't match.", "C-Cube",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        _settings.Admin.PasswordHash = PasswordHasher.Hash(newBox.Text);
        AppLog.Info("Admin password changed. Remember to click Save to store it.");
        MessageBox.Show("Admin password updated.\n\nClick Save to store the change.",
            "C-Cube", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }
}
