using System.Drawing;
using System.Windows.Forms;

namespace CinemaTickets;

internal sealed class LoginForm : Form
{
    private readonly RadioButton buyerRole = new() { Text = "Покупатель", Checked = true, AutoSize = true, Font = new Font("Segoe UI", 10, FontStyle.Bold) };
    private readonly RadioButton adminRole = new() { Text = "Администратор", AutoSize = true, Font = new Font("Segoe UI", 10, FontStyle.Bold) };
    private readonly TextBox nameBox = new() { Width = 290, Font = new Font("Segoe UI", 10), Text = "Покупатель" };
    private readonly TextBox loginBox = new() { Width = 290, Font = new Font("Segoe UI", 10), Text = "admin" };
    private readonly TextBox passwordBox = new() { Width = 290, Font = new Font("Segoe UI", 10), UseSystemPasswordChar = true };
    private readonly Label nameLabel = new() { Text = "Ваше имя", AutoSize = true, ForeColor = Color.FromArgb(62, 75, 96) };
    private readonly Label loginLabel = new() { Text = "Логин администратора", AutoSize = true, ForeColor = Color.FromArgb(62, 75, 96) };
    private readonly Label passwordLabel = new() { Text = "Пароль", AutoSize = true, ForeColor = Color.FromArgb(62, 75, 96) };

    public bool IsAdmin => adminRole.Checked;
    public string CustomerName => IsAdmin ? "Администратор" : nameBox.Text.Trim();

    public LoginForm()
    {
        Text = "CINEMA — вход";
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false; MinimizeBox = false;
        ClientSize = new Size(760, 500);
        BackColor = Color.FromArgb(244, 247, 252);
        Font = new Font("Segoe UI", 9.5f);

        var hero = new Panel { Name = "loginHero", Dock = DockStyle.Left, Width = 285, BackColor = Color.FromArgb(21, 35, 59) };
        hero.Controls.Add(new Label { Text = "CINEMA", ForeColor = Color.White, Font = new Font("Segoe UI", 29, FontStyle.Bold), AutoSize = true, Location = new Point(28, 105) });
        hero.Controls.Add(new Label { Text = "Билеты на большие\nистории", ForeColor = Color.FromArgb(210, 222, 241), Font = new Font("Segoe UI", 15), AutoSize = true, Location = new Point(33, 165) });
        hero.Controls.Add(new Label { Text = "ВЫБИРАЙ. СМОТРИ. ВПЕЧАТЛЯЙСЯ.", ForeColor = Color.FromArgb(139, 166, 204), Font = new Font("Segoe UI", 8, FontStyle.Bold), AutoSize = true, Location = new Point(32, 420) });

        var content = new Panel { Name = "loginContent", Dock = DockStyle.Fill, Padding = new Padding(42, 35, 35, 25) };
        content.Controls.Add(new Label { Text = "Добро пожаловать!", Font = new Font("Segoe UI", 22, FontStyle.Bold), ForeColor = Color.FromArgb(25, 39, 62), AutoSize = true, Location = new Point(38, 25) });
        content.Controls.Add(new Label { Text = "Выберите тип входа, чтобы продолжить", Font = new Font("Segoe UI", 9.5f), ForeColor = Color.Gray, AutoSize = true, Location = new Point(41, 69) });
        var rolePanel = new FlowLayoutPanel { Location = new Point(40, 115), Width = 370, Height = 42, WrapContents = false };
        rolePanel.Controls.Add(buyerRole); rolePanel.Controls.Add(adminRole);
        content.Controls.Add(rolePanel);
        nameLabel.Location = new Point(42, 177); nameBox.Location = new Point(42, 200);
        loginLabel.Location = new Point(42, 177); loginBox.Location = new Point(42, 200);
        passwordLabel.Location = new Point(42, 244); passwordBox.Location = new Point(42, 267);
        content.Controls.Add(nameLabel); content.Controls.Add(nameBox);
        content.Controls.Add(loginLabel); content.Controls.Add(loginBox);
        content.Controls.Add(passwordLabel); content.Controls.Add(passwordBox);
        var hint = new Label { Text = "Для демонстрации: логин admin, пароль admin123", AutoSize = true, ForeColor = Color.Gray, Font = new Font("Segoe UI", 8), Location = new Point(43, 307) };
        content.Controls.Add(hint);
        var enter = new Button { Text = "ВОЙТИ", Location = new Point(42, 355), Width = 290, Height = 43, BackColor = Color.FromArgb(35, 105, 177), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 10, FontStyle.Bold), Cursor = Cursors.Hand };
        enter.FlatAppearance.BorderSize = 0;
        enter.Click += (_, _) => Authenticate();
        content.Controls.Add(enter);
        buyerRole.CheckedChanged += (_, _) => UpdateRoleFields();
        adminRole.CheckedChanged += (_, _) => UpdateRoleFields();
        AcceptButton = enter;
        Controls.Add(content); Controls.Add(hero);
        UpdateRoleFields();
        CinemaTheme.Apply(this);
    }

    private void UpdateRoleFields()
    {
        bool admin = adminRole.Checked;
        nameLabel.Visible = nameBox.Visible = !admin;
        loginLabel.Visible = loginBox.Visible = passwordLabel.Visible = passwordBox.Visible = admin;
        // подсказка о тестовой учётной записи показывается только в режиме администратора
        foreach (Control control in Controls)
            if (control is Panel panel)
                foreach (Control child in panel.Controls)
                    if (child is Label label && label.Text.StartsWith("Для демонстрации:")) label.Visible = admin;
    }

    private void Authenticate()
    {
        if (IsAdmin)
        {
            if (loginBox.Text.Trim() != "admin" || passwordBox.Text != "admin123")
            {
                MessageBox.Show("Неверный логин или пароль администратора.", "Ошибка авторизации", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                passwordBox.Clear(); passwordBox.Focus(); return;
            }
        }
        else if (string.IsNullOrWhiteSpace(nameBox.Text))
        {
            MessageBox.Show("Введите ваше имя.", "Вход", MessageBoxButtons.OK, MessageBoxIcon.Information);
            nameBox.Focus(); return;
        }
        DialogResult = DialogResult.OK;
        Close();
    }
}
