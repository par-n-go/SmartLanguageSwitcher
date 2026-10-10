using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
namespace SmartLanguageSwitcher;
internal sealed class SettingsForm : Form
{
    private static readonly Color Background = Color.FromArgb(18, 23, 36);
    private static readonly Color Surface = Color.FromArgb(29, 36, 53);
    private static readonly Color Muted = Color.FromArgb(161, 173, 195);
    private static readonly Color Accent = Color.FromArgb(97, 231, 212);
    [DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(nint hwnd, int attribute, ref int value, int size);
    public SettingsForm(Settings settings, bool preview = false)
    {
        Text = "Smart Language Switcher";
        Icon = AppIcon.Load();
        Disposed += (_, _) => Icon?.Dispose();
        Font = new Font("Segoe UI", 10);
        BackColor = Background; ForeColor = Color.White;
        AutoScaleDimensions = new SizeF(96, 96); AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(540, 390);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false; MinimizeBox = false; StartPosition = FormStartPosition.CenterScreen;
        HandleCreated += (_, _) => { int dark = 1; DwmSetWindowAttribute(Handle, 20, ref dark, sizeof(int)); };
        var image = new PictureBox { Image = Icon.ToBitmap(), SizeMode = PictureBoxSizeMode.Zoom, Location = new(28, 27), Size = new(48, 48), TabStop = false };
        image.Disposed += (_, _) => image.Image?.Dispose();
        var title = Label("Smart Language Switcher", 90, 26, 425, 30, 16, Color.White, true);
        var subtitle = Label("Hold. Tap. Release.", 91, 61, 415, 24, 9, Muted);
        var heading = Label("SHORTCUT", 28, 115, 200, 23, 8, Muted, true);
        var shortcutPanel = new Panel { Location = new(28, 146), Size = new(484, 48), BackColor = Surface };
        Round(shortcutPanel, 10);
        ShortcutKind selected = settings.Shortcut;
        var names = new[] { "Alt + Shift", "Win + Space", "Ctrl + Shift" };
        var hint = Label("", 28, 207, 484, 24, 9, Muted);
        void UpdateHint() => hint.Text = selected == ShortcutKind.WinSpace ? "Best effort · Windows may override this shortcut." : "Turn off the matching Windows language shortcut.";
        for (int i = 0; i < names.Length; i++)
        {
            var mode = (ShortcutKind)i;
            var option = new RadioButton { Text = names[i], Appearance = Appearance.Button, FlatStyle = FlatStyle.Flat,
                TextAlign = ContentAlignment.MiddleCenter, Location = new(4 + i * 160, 4), Size = new(156, 40),
                Checked = settings.Shortcut == mode, UseVisualStyleBackColor = false, Cursor = Cursors.Hand,
                AccessibleName = names[i], TabIndex = i };
            option.FlatAppearance.BorderSize = 0;
            void Style() { option.BackColor = option.Checked ? Accent : Surface; option.ForeColor = option.Checked ? Background : Color.White; }
            option.CheckedChanged += (_, _) => { Style(); if (option.Checked) { selected = mode; UpdateHint(); } };
            Style(); Round(option, 8); shortcutPanel.Controls.Add(option);
        }
        UpdateHint();
        var startupLabel = Label("Start with Windows", 28, 262, 380, 26, 10, Color.White);
        var startup = new CheckBox { Appearance = Appearance.Button, FlatStyle = FlatStyle.Flat, TextAlign = ContentAlignment.MiddleCenter,
            Location = new(448, 255), Size = new(64, 36), UseVisualStyleBackColor = false, AccessibleName = "Start with Windows", Cursor = Cursors.Hand };
        startup.FlatAppearance.BorderSize = 0;
        try { startup.Checked = Startup.IsEnabled(); } catch { startup.Checked = settings.StartWithWindows; }
        void StyleStartup() { startup.Text = startup.Checked ? "On" : "Off"; startup.BackColor = startup.Checked ? Accent : Surface; startup.ForeColor = startup.Checked ? Background : Muted; }
        startup.CheckedChanged += (_, _) => StyleStartup(); StyleStartup(); Round(startup, 18);
        var version = Label("v1.0.1", 28, 337, 150, 24, 9, Muted);
        var save = new Button { Text = "Save", Location = new(392, 325), Size = new(120, 40), FlatStyle = FlatStyle.Flat,
            BackColor = Accent, ForeColor = Background, Cursor = Cursors.Hand, UseVisualStyleBackColor = false };
        save.FlatAppearance.BorderSize = 0; Round(save, 10);
        save.Click += (_, _) => {
            if (preview) { Close(); return; }
            try
            {
                Startup.Set(startup.Checked);
                settings.StartWithWindows = startup.Checked; settings.Shortcut = selected;
                if (!settings.Save()) throw new IOException("Could not save settings.");
                Close();
            }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning); }
        };
        Controls.AddRange([image, title, subtitle, heading, shortcutPanel, hint, startupLabel, startup, version, save]);
        AcceptButton = save;
    }
    private static Label Label(string text, int x, int y, int width, int height, float size, Color color, bool bold = false) => new()
    {
        Text = text, Location = new(x, y), Size = new(width, height), ForeColor = color,
        Font = new Font("Segoe UI", size, bold ? FontStyle.Bold : FontStyle.Regular), AutoEllipsis = true
    };
    private static void Round(Control control, int radius)
    {
        void Apply()
        {
            int diameter = Math.Max(2, (int)(radius * 2 * control.DeviceDpi / 96f));
            using var path = new GraphicsPath();
            path.AddArc(0, 0, diameter, diameter, 180, 90);
            path.AddArc(control.Width - diameter, 0, diameter, diameter, 270, 90);
            path.AddArc(control.Width - diameter, control.Height - diameter, diameter, diameter, 0, 90);
            path.AddArc(0, control.Height - diameter, diameter, diameter, 90, 90); path.CloseFigure();
            var old = control.Region; control.Region = new Region(path); old?.Dispose();
        }
        control.SizeChanged += (_, _) => Apply(); Apply();
    }
}

