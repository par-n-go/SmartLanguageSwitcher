using System.Drawing;
using static SmartLanguageSwitcher.Theme;
using System.Runtime.InteropServices;
namespace SmartLanguageSwitcher;
internal sealed class SettingsForm : Form
{
    [DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(nint hwnd, int attribute, ref int value, int size);
    private readonly bool renderOnly;
    protected override bool ShowWithoutActivation => renderOnly;
    public SettingsForm(Settings settings, bool preview = false, bool render = false)
    {
        renderOnly = render;
        Text = "Smart Language Switcher";
        Icon = AppIcon.Load();
        Disposed += (_, _) => Icon?.Dispose();
        Font = new Font("Segoe UI", 10);
        BackColor = Background; ForeColor = Color.White;
        AutoScaleDimensions = new SizeF(96, 96); AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(540, 445);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false; MinimizeBox = false; StartPosition = FormStartPosition.CenterScreen;
        HandleCreated += (_, _) => { int dark = 1; DwmSetWindowAttribute(Handle, 20, ref dark, sizeof(int)); };
        var image = new PictureBox { Image = LoadHeaderIcon(), SizeMode = PictureBoxSizeMode.Zoom, Location = new(28, 27), Size = new(48, 48), TabStop = false };
        image.Disposed += (_, _) => image.Image?.Dispose();
        var title = Label("Smart Language Switcher", 90, 26, 425, 30, 16, Color.White, true);
        title.AutoSize = true;
        title.MaximumSize = new Size(425, 0);
        var subtitle = Label("Hold. Tap. Release.", 91, 61, 415, 24, 9, Muted);
        var heading = Label("SHORTCUT", 28, 115, 200, 23, 8, Muted, true);
        var shortcutPanel = new SmoothPanel { Location = new(28, 146), Size = new(484, 48), BackColor = Surface };

        ShortcutKind selected = settings.Shortcut;
        var names = new[] { "Alt + Shift", "Win + Space", "Ctrl + Shift" };
        var hint = Label("", 28, 207, 484, 24, 9, Muted);
        void UpdateHint() => hint.Text = selected == ShortcutKind.WinSpace ? "Best effort · Windows may override this shortcut." : "Turn off the matching Windows language shortcut.";
        for (int i = 0; i < names.Length; i++)
        {
            var mode = (ShortcutKind)i;
            var option = new SmoothRadio { Text = names[i], Appearance = Appearance.Button, FlatStyle = FlatStyle.Flat,
                TextAlign = ContentAlignment.MiddleCenter, Location = new(4 + i * 160, 4), Size = new(156, 40),
                Checked = settings.Shortcut == mode, UseVisualStyleBackColor = false, Cursor = Cursors.Hand,
                AccessibleName = names[i], TabIndex = i };
            option.FlatAppearance.BorderSize = 0;
            void Style() { option.BackColor = option.Checked ? Accent : Surface; option.ForeColor = option.Checked ? Background : Color.White; }
            option.CheckedChanged += (_, _) => { Style(); if (option.Checked) { selected = mode; UpdateHint(); } };
            Style(); shortcutPanel.Controls.Add(option);
        }
        UpdateHint();
        var startupLabel = Label("Start with Windows", 28, 262, 380, 26, 10, Color.White);
        var startup = new SmoothToggle { Appearance = Appearance.Button, FlatStyle = FlatStyle.Flat, TextAlign = ContentAlignment.MiddleCenter,
            Location = new(448, 255), Size = new(64, 36), UseVisualStyleBackColor = false, AccessibleName = "Start with Windows", Cursor = Cursors.Hand };
        startup.FlatAppearance.BorderSize = 0;
        try { startup.Checked = Startup.IsEnabled(); } catch { startup.Checked = settings.StartWithWindows; }
        void StyleStartup() { startup.Text = startup.Checked ? "On" : "Off"; startup.BackColor = startup.Checked ? Accent : Surface; startup.ForeColor = startup.Checked ? Background : Muted; }
        startup.CheckedChanged += (_, _) => StyleStartup(); StyleStartup();
        var updateLabel = Label("Auto-update", 28, 314, 380, 26, 10, Color.White);
        var autoUpdate = new SmoothToggle { Appearance = Appearance.Button, FlatStyle = FlatStyle.Flat, TextAlign = ContentAlignment.MiddleCenter,
            Location = new(448, 307), Size = new(64, 36), UseVisualStyleBackColor = false, AccessibleName = "Auto-update", Cursor = Cursors.Hand,
            Checked = settings.AutoUpdate };
        autoUpdate.FlatAppearance.BorderSize = 0;
        void StyleUpdate() { autoUpdate.Text = autoUpdate.Checked ? "On" : "Off"; autoUpdate.BackColor = autoUpdate.Checked ? Accent : Surface; autoUpdate.ForeColor = autoUpdate.Checked ? Background : Muted; }
        autoUpdate.CheckedChanged += (_, _) => StyleUpdate(); StyleUpdate();
        var version = Label("v" + AutoUpdater.CurrentVersion.ToString(3), 28, 392, 150, 24, 9, Muted);
        var save = new SmoothButton { Text = "Save", Location = new(392, 380), Size = new(120, 40), FlatStyle = FlatStyle.Flat,
            BackColor = Accent, ForeColor = Background, Cursor = Cursors.Hand, UseVisualStyleBackColor = false };
        save.FlatAppearance.BorderSize = 0;
        save.Click += (_, _) => {
            if (preview) { Close(); return; }
            try
            {
                Startup.Set(startup.Checked);
                settings.AutoUpdate = autoUpdate.Checked;
                settings.StartWithWindows = startup.Checked; settings.Shortcut = selected;
                if (!settings.Save()) throw new IOException("Could not save settings.");
                Close();
            }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning); }
        };
        Controls.AddRange([image, title, subtitle, heading, shortcutPanel, hint, startupLabel, startup, updateLabel, autoUpdate, version, save]);
        void LayoutHeader()
        {
            title.Size = title.GetPreferredSize(new Size(title.MaximumSize.Width, 0));
            subtitle.Top = title.Bottom + (int)(4 * DeviceDpi / 96f);
        }
        title.SizeChanged += (_, _) => LayoutHeader();
        DpiChanged += (_, _) => LayoutHeader();
        LayoutHeader();
        AcceptButton = save;
        if (preview) ContextMenuStrip = TrayMenu.Create(() => { }, _ => { }, Close);
    }
    private Bitmap LoadHeaderIcon() { using var icon = new Icon(Icon!, new Size(256, 256)); return icon.ToBitmap(); }
    private static Label Label(string text, int x, int y, int width, int height, float size, Color color, bool bold = false) => new()
    {
        Text = text, Location = new(x, y), Size = new(width, height), ForeColor = color,
        Font = new Font("Segoe UI", size, bold ? FontStyle.Bold : FontStyle.Regular), AutoEllipsis = true
    };
}
