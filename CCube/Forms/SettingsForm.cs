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
    // InExpress
    private TextBox _inxServer = null!, _inxDb = null!, _inxUser = null!, _inxPass = null!, _inxProc = null!;
    private CheckBox _inxIntegrated = null!;
    private NumericUpDown _inxTimeout = null!;
    private Label _inxResult = null!;
    // CCube
    private TextBox _ccServer = null!, _ccDb = null!, _ccUser = null!, _ccPass = null!;
    private CheckBox _ccIntegrated = null!;
    private Label _ccResult = null!;
    // PLC
    private TextBox _plcIp = null!;
    private NumericUpDown _plcRack = null!, _plcSlot = null!, _plcDb = null!;
    private Label _plcResult = null!;
    // Buttons
    private Button _editButton = null!, _saveButton = null!, _changeAdminButton = null!;
    private Label _lockState = null!;

    public SettingsForm(AppSettings settings)
    {
        _settings = settings;
        BuildUi();
        LoadFromSettings();
        SetEditable(false); // always start locked
    }

    // -------------------------------------------------------------------------
    // Building the screen
    // -------------------------------------------------------------------------

    private Panel _body = null!;

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
        y += 40;

        // --- General ---
        y = Section("General", y);
        _station = AddText("Station ID", ref y);

        // --- InExpress ---
        y = Section("InExpress database (validation)", y);
        _inxServer = AddText("Server", ref y);
        _inxDb = AddText("Database", ref y);
        _inxIntegrated = AddCheck("Use Windows login (integrated security)", ref y);
        _inxUser = AddText("SQL username", ref y);
        _inxPass = AddText("SQL password", ref y, password: true);
        _inxProc = AddText("Validate procedure", ref y);
        _inxTimeout = AddNumber("Timeout (seconds)", ref y, 1, 30);
        var (btnTestInx, inxResult) = AddTestRow("Test InExpress connection", ref y);
        _inxResult = inxResult;
        btnTestInx.Click += async (_, _) =>
        {
            string conn = SqlConnStringsFromFields(_inxServer, _inxDb, _inxIntegrated, _inxUser, _inxPass);
            await RunTest(btnTestInx, _inxResult, () => ConnectionTester.TestSql(conn));
        };
        _inxIntegrated.CheckedChanged += (_, _) => ApplyIntegratedToggle(_inxIntegrated, _inxUser, _inxPass);

        // --- CCube ---
        y = Section("C-Cube database (history)", y);
        _ccServer = AddText("Server", ref y);
        _ccDb = AddText("Database", ref y);
        _ccIntegrated = AddCheck("Use Windows login (integrated security)", ref y);
        _ccUser = AddText("SQL username", ref y);
        _ccPass = AddText("SQL password", ref y, password: true);
        var (btnTestCc, ccResult) = AddTestRow("Test C-Cube connection", ref y);
        _ccResult = ccResult;
        btnTestCc.Click += async (_, _) =>
        {
            string conn = SqlConnStringsFromFields(_ccServer, _ccDb, _ccIntegrated, _ccUser, _ccPass);
            await RunTest(btnTestCc, _ccResult, () => ConnectionTester.TestSql(conn));
        };
        _ccIntegrated.CheckedChanged += (_, _) => ApplyIntegratedToggle(_ccIntegrated, _ccUser, _ccPass);

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

        // (No Security section on the page - the admin password is only asked for
        // when you click Edit, and can be changed from the button in the bar below.)

        // --- Bottom button bar ---
        var bar = new Panel { Dock = DockStyle.Bottom, Height = 56, BackColor = Theme.SurfaceAlt };
        _editButton = BarButton("Edit", Theme.Accent, 16);
        _editButton.Click += (_, _) => BeginEdit();
        _saveButton = BarButton("Save", Theme.Pass, 120);
        _saveButton.Click += (_, _) => SaveChanges();
        var closeButton = BarButton("Close", Theme.Idle, 224);
        closeButton.ForeColor = Theme.Text; // dark text on the light Close button
        closeButton.Click += (_, _) => Close();
        // "Change password…" sits on the right and is only usable while editing.
        _changeAdminButton = BarButton("Change password…", Theme.Panel, ClientSize.Width - 190);
        _changeAdminButton.Width = 174;
        _changeAdminButton.ForeColor = Theme.Accent;
        _changeAdminButton.FlatAppearance.BorderColor = Theme.Border;
        _changeAdminButton.FlatAppearance.BorderSize = 1;
        _changeAdminButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        _changeAdminButton.Click += (_, _) => ChangeAdminPassword();
        _editable.Add(_changeAdminButton);
        foreach (Button b in new[] { _editButton, _saveButton, closeButton, _changeAdminButton })
        {
            AddClickFeedback(b);
        }
        bar.Controls.Add(_editButton);
        bar.Controls.Add(_saveButton);
        bar.Controls.Add(closeButton);
        bar.Controls.Add(_changeAdminButton);
        Controls.Add(bar);
    }

    // ---- Layout helpers (keep the code above short and consistent) ----

    private int Section(string title, int y)
    {
        y += 12;
        var header = MakeLabel(title, 20, y, Theme.Accent);
        header.Font = Theme.BoldFont;
        header.AutoSize = true;
        _body.Controls.Add(header);
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
    {
        _body.Controls.Add(MakeLabel(label, 24, y + 4, Theme.Text));
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
        y += 36;
        return box;
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
        y += 34;
        return box;
    }

    private NumericUpDown AddNumber(string label, ref int y, int min, int max)
    {
        _body.Controls.Add(MakeLabel(label, 24, y + 4, Theme.Text));
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
        y += 36;
        return box;
    }

    // A "Test connection" button plus a result label under it. Returns both so
    // the caller can wire an async click that shows a loader on the button.
    private (Button button, Label result) AddTestRow(string buttonText, ref int y)
    {
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

    private Button BarButton(string text, Color colour, int x)
    {
        var button = new Button
        {
            Text = text,
            Location = new Point(x, 11),
            Size = new Size(96, 34),
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
    }

    private void SaveChanges()
    {
        try
        {
            _settings.StationId = _station.Text.Trim();

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
        // Respect the integrated-security toggles even while editing.
        if (editable)
        {
            ApplyIntegratedToggle(_inxIntegrated, _inxUser, _inxPass);
            ApplyIntegratedToggle(_ccIntegrated, _ccUser, _ccPass);
        }

        _saveButton.Enabled = editable;
        _editButton.Enabled = !editable;
        _changeAdminButton.Visible = editable; // only offer it while editing
        _lockState.Text = editable
            ? "✏️  Editing. Make your changes, then click Save."
            : "🔒  Read-only. Click Edit and enter the admin password to change.";
        _lockState.ForeColor = editable ? Theme.Accent : Theme.Muted;
    }

    // When "use Windows login" is ticked, the username/password boxes don't apply.
    private static void ApplyIntegratedToggle(CheckBox integrated, TextBox user, TextBox pass)
    {
        bool useSql = !integrated.Checked;
        user.Enabled = useSql && integrated.Enabled;
        pass.Enabled = useSql && integrated.Enabled;
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
