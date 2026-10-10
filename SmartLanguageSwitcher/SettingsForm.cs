using System.Drawing;
namespace SmartLanguageSwitcher;
internal sealed class SettingsForm : Form
{
    public SettingsForm(Settings settings)
    {
        Text = "Smart Language Switcher";
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(580, 330);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false; MinimizeBox = false; StartPosition = FormStartPosition.CenterScreen;
        var title = new Label { Text = "Keyboard layout switching", Font = new Font(Font, FontStyle.Bold), AutoSize = true, Location = new(20, 20) };
        var shortcut = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Location = new(20, 60), Width = 540 };
        shortcut.Items.AddRange(["Alt + Shift", "Win + Space (best effort)", "Ctrl + Shift"]);
        shortcut.SelectedIndex = (int)settings.Shortcut;
        var startup = new CheckBox { Text = "Start with Windows", AutoSize = true, Location = new(20, 105) };
        try { startup.Checked = Startup.IsEnabled(); } catch { startup.Checked = settings.StartWithWindows; }
        var info = new Label { Location = new(20, 145), Size = new(540, 115), Text =
            "Hold Alt / Ctrl / Win, then tap Shift / Space. First tap selects the previous\nlayout; further taps cycle installed layouts. Release the modifier to commit.\n\nDisable the matching Windows Alt+Shift / Ctrl+Shift hotkey to avoid conflicts.\nWin+Space is reserved by Windows and cannot be guaranteed.\nRelease all shortcut keys before changing settings." };
        var save = new Button { Text = "Save", Location = new(450, 280), Size = new(110, 30) };
        save.Click += (_, _) => {
            try
            {
                Startup.Set(startup.Checked);
                settings.StartWithWindows = startup.Checked;
                settings.Shortcut = (ShortcutKind)shortcut.SelectedIndex;
                if (!settings.Save()) throw new IOException("Unable to save settings.");
                Close();
            }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning); }
        };
        Controls.AddRange([title, shortcut, startup, info, save]);
        AcceptButton = save;
    }
}
