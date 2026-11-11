using System.Drawing;
using System.Windows.Forms;

namespace CinemaTickets;

internal static class CinemaTheme
{
    internal static readonly Color Background = Color.FromArgb(15, 17, 22);
    internal static readonly Color Surface = Color.FromArgb(25, 28, 35);
    internal static readonly Color SurfaceAlt = Color.FromArgb(32, 36, 45);
    internal static readonly Color Border = Color.FromArgb(54, 59, 70);
    internal static readonly Color Text = Color.FromArgb(239, 240, 244);
    internal static readonly Color Muted = Color.FromArgb(157, 164, 177);
    internal static readonly Color Accent = Color.FromArgb(190, 42, 68);
    internal static readonly Color AccentHover = Color.FromArgb(218, 54, 75);

    public static void Apply(Form form)
    {
        form.BackColor = Background;
        form.ForeColor = Text;
        form.Font = new Font("Segoe UI", 9.5f);
        ApplyControls(form);
    }

    private static void ApplyControls(Control parent)
    {
        foreach (Control c in parent.Controls)
        {
            switch (c)
            {
                case DataGridView grid:
                    grid.BackgroundColor = Surface;
                    grid.GridColor = Border;
                    grid.BorderStyle = BorderStyle.None;
                    grid.EnableHeadersVisualStyles = false;
                    grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(38, 42, 52);
                    grid.ColumnHeadersDefaultCellStyle.ForeColor = Text;
                    grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = Color.FromArgb(38, 42, 52);
                    grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
                    grid.DefaultCellStyle.BackColor = Surface;
                    grid.DefaultCellStyle.ForeColor = Text;
                    grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(91, 38, 50);
                    grid.DefaultCellStyle.SelectionForeColor = Color.White;
                    grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(29, 32, 40);
                    grid.RowTemplate.Height = 38;
                    grid.ColumnHeadersHeight = 42;
                    grid.RowHeadersVisible = false;
                    break;
                case TabControl tabs:
                    tabs.BackColor = Background;
                    tabs.ForeColor = Text;
                    tabs.DrawMode = TabDrawMode.OwnerDrawFixed;
                    // Автоматическая ширина вкладки по заголовку: длинная вкладка покупателя не обрезается.
                    tabs.SizeMode = TabSizeMode.Normal;
                    tabs.DrawItem += (_, e) =>
                    {
                        bool selected = e.Index == tabs.SelectedIndex;
                        using var brush = new SolidBrush(selected ? Accent : SurfaceAlt);
                        e.Graphics.FillRectangle(brush, e.Bounds);
                        TextRenderer.DrawText(e.Graphics, tabs.TabPages[e.Index].Text,
                            new Font("Segoe UI", 9.5f, selected ? FontStyle.Bold : FontStyle.Regular),
                            e.Bounds, selected ? Color.White : Muted,
                            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                    };
                    break;
                case TabPage:
                    c.BackColor = Background;
                    c.ForeColor = Text;
                    break;
                case Button button:
                    button.FlatStyle = FlatStyle.Flat;
                    button.FlatAppearance.BorderSize = 0;
                    button.BackColor = Accent;
                    button.ForeColor = Color.White;
                    button.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
                    button.Cursor = Cursors.Hand;
                    button.Height = Math.Max(button.Height, 36);
                    button.FlatAppearance.MouseOverBackColor = AccentHover;
                    button.FlatAppearance.MouseDownBackColor = Color.FromArgb(145, 31, 49);
                    break;
                case TextBox box:
                    box.BackColor = SurfaceAlt;
                    box.ForeColor = Text;
                    box.BorderStyle = BorderStyle.FixedSingle;
                    break;
                case ComboBox combo:
                    combo.BackColor = SurfaceAlt;
                    combo.ForeColor = Text;
                    combo.FlatStyle = FlatStyle.Flat;
                    break;
                case DateTimePicker picker:
                    picker.CalendarMonthBackground = SurfaceAlt;
                    picker.CalendarForeColor = Text;
                    picker.CalendarTitleBackColor = Accent;
                    picker.CalendarTitleForeColor = Color.White;
                    picker.CalendarTrailingForeColor = Muted;
                    picker.BackColor = SurfaceAlt;
                    picker.ForeColor = Text;
                    break;
                case NumericUpDown numeric:
                    numeric.BackColor = SurfaceAlt;
                    numeric.ForeColor = Text;
                    numeric.BorderStyle = BorderStyle.FixedSingle;
                    break;
                case CheckBox check:
                    check.ForeColor = Text;
                    check.BackColor = Color.Transparent;
                    break;
                case RadioButton radio:
                    radio.ForeColor = Text;
                    radio.BackColor = Color.Transparent;
                    break;
                case Label label:
                    label.ForeColor = parent.BackColor.GetBrightness() < 0.20f ? Color.White : Text;
                    label.BackColor = Color.Transparent;
                    break;
                case FlowLayoutPanel flow:
                    flow.BackColor = Surface;
                    break;
                case TableLayoutPanel table:
                    table.BackColor = Surface;
                    break;
                case Panel panel:
                    panel.BackColor = panel.Name == "cinemaHeader" || panel.Name == "loginHero"
                        ? Color.FromArgb(10, 11, 15)
                        : panel.Name == "welcomePanel" ? Color.FromArgb(54, 23, 32) : Surface;
                    break;
            }
            ApplyControls(c);
        }
    }
}
