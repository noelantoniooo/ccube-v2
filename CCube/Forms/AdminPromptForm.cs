// AdminPromptForm.cs
// -----------------------------------------------------------------------------
// A small pop-up that asks for the admin password. It's shown when someone
// clicks "Edit" on the Settings screen. If the password is correct it closes
// with DialogResult.OK; otherwise it stays open so they can try again.
//
// This form is built entirely in code (it's tiny), so there's no separate
// Designer file. Read it top to bottom.
// -----------------------------------------------------------------------------

using CCube.Core;

namespace CCube.Forms;

public sealed class AdminPromptForm : Form
{
    private readonly string _storedHash;
    private readonly TextBox _passwordBox;

    public AdminPromptForm(string storedHash)
    {
        _storedHash = storedHash;

        Text = "Admin password";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = false;
        MinimizeBox = false;
        ClientSize = new Size(400, 160);
        BackColor = Theme.Panel;
        ForeColor = Theme.Text;
        Font = Theme.NormalFont;

        var prompt = new Label
        {
            Text = "Enter the admin password to edit settings:",
            AutoSize = true,
            Location = new Point(18, 20),
            ForeColor = Theme.Text,
        };

        _passwordBox = new TextBox
        {
            UseSystemPasswordChar = true,
            Location = new Point(18, 52),
            Size = new Size(364, 27),
            BackColor = Theme.PanelDark,
            ForeColor = Theme.Text,
            BorderStyle = BorderStyle.FixedSingle,
        };
        _passwordBox.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                TryUnlock();
            }
        };

        var okButton = new Button
        {
            Text = "Unlock",
            Location = new Point(210, 104),
            Size = new Size(84, 34),
            BackColor = Theme.Accent,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
        };
        okButton.FlatAppearance.BorderSize = 0;
        okButton.Click += (_, _) => TryUnlock();

        var cancelButton = new Button
        {
            Text = "Cancel",
            Location = new Point(300, 104),
            Size = new Size(84, 34),
            BackColor = Theme.Idle,
            ForeColor = Theme.Text,
            FlatStyle = FlatStyle.Flat,
            DialogResult = DialogResult.Cancel,
        };
        cancelButton.FlatAppearance.BorderSize = 0;

        AcceptButton = okButton;
        CancelButton = cancelButton;

        Controls.Add(prompt);
        Controls.Add(_passwordBox);
        Controls.Add(okButton);
        Controls.Add(cancelButton);
    }

    private void TryUnlock()
    {
        if (PasswordHasher.Verify(_passwordBox.Text, _storedHash))
        {
            AppLog.Info("Admin password accepted; settings unlocked for editing.");
            DialogResult = DialogResult.OK;
            Close();
        }
        else
        {
            AppLog.Warn("Admin password attempt failed.");
            MessageBox.Show("Incorrect password.", "C-Cube",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _passwordBox.SelectAll();
            _passwordBox.Focus();
        }
    }
}
