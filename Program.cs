using System;
using System.Windows.Forms;

namespace CinemaTickets;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        try
        {
            Database.Initialize();
            using var login = new LoginForm();
            if (login.ShowDialog() != DialogResult.OK) return;
            Application.Run(new MainForm(login.IsAdmin, login.CustomerName));
        }
        catch (Exception ex)
        {
            MessageBox.Show("Не удалось запустить приложение. Проверьте наличие SQL Server LocalDB.\n\n" + ex.Message,
                "Ошибка запуска", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
