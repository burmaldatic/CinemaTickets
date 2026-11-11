using Microsoft.Data.SqlClient;
using System.Data;
using System.Drawing;
using System.Globalization;

namespace CinemaTickets;

public sealed class MainForm : Form
{
    private readonly DataGridView moviesGrid = MakeGrid();
    private readonly DataGridView sessionsGrid = MakeGrid();
    private readonly DataGridView ticketsGrid = MakeGrid();
    private readonly DataGridView salesGrid = MakeGrid();
    private readonly ComboBox sessionPicker = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 420 };
    private readonly DateTimePicker saleDatePicker = new() { Format = DateTimePickerFormat.Short, Width = 120 };
    private readonly DateTimePicker buyerDatePicker = new() { Format = DateTimePickerFormat.Short, Width = 130, MinDate = DateTime.Today };
    private readonly FlowLayoutPanel buyerMoviesPanel = new() { Dock = DockStyle.Fill, AutoScroll = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new Padding(12) };
    private readonly System.Windows.Forms.Timer sessionStatusTimer = new() { Interval = 60000 };
    private readonly CheckBox allDatesCheck = new() { Text = "Все даты", AutoSize = true };
    private readonly FlowLayoutPanel seatsPanel = new() { AutoScroll = true, Dock = DockStyle.Fill, Padding = new Padding(12), FlowDirection = FlowDirection.TopDown, WrapContents = false };
    private readonly Label selectionLabel = new() { AutoSize = true, Font = new Font("Segoe UI", 10, FontStyle.Bold), Text = "Выберите места" };
    private readonly TextBox customerBox = new() { Width = 220, Text = "Покупатель" };
    private readonly Label totalLabel = new() { AutoSize = true, Font = new Font("Segoe UI", 11, FontStyle.Bold), Text = "Итого: 0 ₽" };
    private readonly HashSet<int> selectedSeats = new();
    private decimal currentPrice;
    private int currentSessionId;
    private readonly bool isAdmin;
    private readonly string signedInCustomer;

    public MainForm(bool isAdmin, string customerName)
    {
        this.isAdmin = isAdmin;
        signedInCustomer = customerName;
        customerBox.Text = isAdmin ? "Покупатель" : customerName;
        customerBox.ReadOnly = !isAdmin;
        Text = isAdmin ? "CINEMA — панель администратора" : "CINEMA — покупка билетов";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(1000, 650);
        Size = new Size(1200, 780);
        WindowState = FormWindowState.Maximized;
        Font = new Font("Segoe UI", 9.5f);
        BackColor = Color.FromArgb(245, 247, 250);

        var header = new Panel { Name = "cinemaHeader", Dock = DockStyle.Top, Height = 82, BackColor = Color.FromArgb(12, 13, 18), Padding = new Padding(22, 12, 22, 12) };
        header.Controls.Add(new Label { Text = "CINEMA", ForeColor = Color.White, Font = new Font("Segoe UI", 23, FontStyle.Bold), AutoSize = true, Location = new Point(20, 7) });
        header.Controls.Add(new Label { Text = isAdmin ? "ПАНЕЛЬ АДМИНИСТРАТОРА" : "КИНО И БИЛЕТЫ", ForeColor = Color.FromArgb(178, 195, 220), Font = new Font("Segoe UI", 9, FontStyle.Bold), AutoSize = true, Location = new Point(190, 29) });
        var userLabel = new Label { Text = (isAdmin ? "Администратор" : signedInCustomer) + "   •   Выйти", ForeColor = Color.White, Font = new Font("Segoe UI", 9, FontStyle.Bold), AutoSize = true, Cursor = Cursors.Hand, Anchor = AnchorStyles.Top | AnchorStyles.Right, Location = new Point(900, 30) };
        userLabel.Click += (_, _) => Close();
        header.Controls.Add(userLabel);
        var tabs = new TabControl { Dock = DockStyle.Fill, Padding = new Point(14, 6), Font = new Font("Segoe UI", 10, FontStyle.Bold) };
        if (isAdmin) tabs.TabPages.Add(BuildHomeTab());
        tabs.TabPages.Add(isAdmin ? BuildMoviesTab() : BuildBuyerCatalogTab());
        if (isAdmin) tabs.TabPages.Add(BuildSessionsTab());
        if (isAdmin) { tabs.TabPages.Add(BuildTicketsTab()); tabs.TabPages.Add(BuildReportsTab()); }
        Controls.Add(tabs);
        Controls.Add(header);
        CinemaTheme.Apply(this);
        RefreshAll();
        Shown += (_, _) =>
        {
            if (!isAdmin) BeginInvoke(new Action(RefreshBuyerCatalog));
        };
        sessionStatusTimer.Tick += (_, _) => RefreshExpiredSessions();
        sessionStatusTimer.Start();
        FormClosed += (_, _) => sessionStatusTimer.Dispose();
    }

    private TabPage BuildBuyerCatalogTab()
    {
        var page = new TabPage("Фильмы и покупка билетов");
        var dateBar = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 62,
            Padding = new Padding(14, 12, 10, 8),
            WrapContents = false,
            BackColor = CinemaTheme.Surface
        };
        dateBar.Controls.Add(new Label
        {
            Text = "Выберите дату:",
            AutoSize = true,
            Padding = new Padding(0, 8, 8, 0),
            Font = new Font("Segoe UI", 10, FontStyle.Bold)
        });
        buyerDatePicker.Value = DateTime.Today;
        dateBar.Controls.Add(buyerDatePicker);
        allDatesCheck.Checked = false;
        dateBar.Controls.Add(allDatesCheck);
        buyerDatePicker.ValueChanged += (_, _) => RefreshBuyerCatalog();
        allDatesCheck.CheckedChanged += (_, _) => RefreshBuyerCatalog();
        buyerMoviesPanel.SizeChanged += (_, _) => ResizeBuyerCards();

        var content = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(8),
            BackColor = CinemaTheme.Background
        };
        content.RowStyles.Add(new RowStyle(SizeType.Percent, 52));
        content.RowStyles.Add(new RowStyle(SizeType.Percent, 48));
        content.RowStyles.Add(new RowStyle(SizeType.Absolute, 72));

        buyerMoviesPanel.BackColor = CinemaTheme.Background;
        seatsPanel.Dock = DockStyle.Fill;
        seatsPanel.BackColor = CinemaTheme.Surface;
        content.Controls.Add(buyerMoviesPanel, 0, 0);
        content.Controls.Add(seatsPanel, 0, 1);

        var bottom = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(10, 14, 8, 8),
            WrapContents = false,
            BackColor = CinemaTheme.Surface
        };
        bottom.Controls.Add(new Label { Text = "Покупатель:", AutoSize = true, Padding = new Padding(0, 8, 0, 0) });
        bottom.Controls.Add(customerBox);
        bottom.Controls.Add(selectionLabel);
        bottom.Controls.Add(totalLabel);
        bottom.Controls.Add(ActionButton("Оформить покупку", (_, _) => SellTickets()));
        content.Controls.Add(bottom, 0, 2);

        page.Controls.Add(content);
        page.Controls.Add(dateBar);
        return page;
    }

    private void RefreshBuyerCatalog()
    {
        if (isAdmin || buyerMoviesPanel.IsDisposed) return;

        buyerMoviesPanel.SuspendLayout();
        try
        {
            buyerMoviesPanel.Controls.Clear();

            bool showAllDates = allDatesCheck.Checked;
            DateTime selectedDate = buyerDatePicker.Value.Date;

            // Показываем только фильмы, у которых есть хотя бы один будущий,
            // не отменённый и не завершённый сеанс.
            var movies = Database.Query(@"
                SELECT m.MovieId, m.Title, m.Genre, m.DurationMinutes, m.AgeRating,
                       m.Description, m.PosterPath
                FROM Movies m
                WHERE EXISTS (
                    SELECT 1
                    FROM Sessions s
                    WHERE s.MovieId = m.MovieId
                      AND s.StartsAt > SYSDATETIME()
                      AND (@allDates = 1 OR
                           (s.StartsAt >= @sessionDate AND s.StartsAt < DATEADD(day, 1, @sessionDate)))
                      AND ISNULL(LTRIM(RTRIM(s.Status)), N'') NOT IN
                          (N'Отменён', N'Отменен', N'Завершён', N'Завершен')
                )
                ORDER BY m.Title",
                new SqlParameter("@allDates", showAllDates ? 1 : 0),
                new SqlParameter("@sessionDate", selectedDate));

            int added = 0;
            foreach (DataRow movie in movies.Rows)
            {
                int movieId = Convert.ToInt32(movie["MovieId"]);
                int cardWidth = Math.Max(420, buyerMoviesPanel.ClientSize.Width - 45);
                var card = new Panel
                {
                    Width = cardWidth,
                    Height = 190,
                    BackColor = CinemaTheme.Surface,
                    Padding = new Padding(12),
                    Margin = new Padding(4, 4, 4, 10),
                    BorderStyle = BorderStyle.FixedSingle
                };

                var poster = new PictureBox
                {
                    Location = new Point(12, 12),
                    Size = new Size(105, 145),
                    SizeMode = PictureBoxSizeMode.Zoom,
                    BackColor = CinemaTheme.SurfaceAlt
                };
                string posterPath = Convert.ToString(movie["PosterPath"]) ?? "";
                if (!string.IsNullOrWhiteSpace(posterPath) && File.Exists(posterPath))
                {
                    try
                    {
                        using var image = Image.FromFile(posterPath);
                        poster.Image = new Bitmap(image);
                    }
                    catch { poster.Image = null; }
                }

                int textLeft = 130;
                var title = new Label
                {
                    Text = Convert.ToString(movie["Title"]) ?? "",
                    AutoSize = false,
                    Width = Math.Max(180, cardWidth - textLeft - 150),
                    Height = 30,
                    Font = new Font("Segoe UI", 13, FontStyle.Bold),
                    ForeColor = CinemaTheme.Text,
                    Location = new Point(textLeft, 10),
                    AutoEllipsis = true
                };
                var info = new Label
                {
                    Text = $"{movie["Genre"]}  •  {movie["DurationMinutes"]} мин.  •  {movie["AgeRating"]}",
                    AutoSize = false,
                    Width = Math.Max(180, cardWidth - textLeft - 24),
                    Height = 22,
                    ForeColor = CinemaTheme.Muted,
                    Location = new Point(textLeft, 43),
                    AutoEllipsis = true
                };
                var description = new Label
                {
                    Text = Convert.ToString(movie["Description"]) ?? "",
                    AutoSize = false,
                    Width = Math.Max(180, cardWidth - textLeft - 24),
                    Height = 42,
                    AutoEllipsis = true,
                    ForeColor = CinemaTheme.Muted,
                    Location = new Point(textLeft, 68)
                };

                var detailsButton = ActionButton("Подробнее", (_, _) =>
                {
                    using var details = new MovieDetailsDialog(movie);
                    details.ShowDialog(this);
                });
                detailsButton.Location = new Point(Math.Max(textLeft, cardWidth - 130), 8);
                detailsButton.Size = new Size(112, 32);

                var sessionButtons = new FlowLayoutPanel
                {
                    Location = new Point(textLeft, 118),
                    Width = Math.Max(220, cardWidth - textLeft - 24),
                    Height = 54,
                    AutoScroll = true,
                    WrapContents = false,
                    BackColor = Color.Transparent,
                    FlowDirection = FlowDirection.LeftToRight
                };

                var sessions = Database.Query(@"
                    SELECT s.SessionId, s.StartsAt, s.Price, h.HallName
                    FROM Sessions s
                    INNER JOIN Halls h ON h.HallId = s.HallId
                    WHERE s.MovieId = @movieId
                      AND s.StartsAt > SYSDATETIME()
                      AND (@allDates = 1 OR
                           (s.StartsAt >= @sessionDate AND s.StartsAt < DATEADD(day, 1, @sessionDate)))
                      AND ISNULL(LTRIM(RTRIM(s.Status)), N'') NOT IN
                          (N'Отменён', N'Отменен', N'Завершён', N'Завершен')
                    ORDER BY s.StartsAt",
                    new SqlParameter("@movieId", movieId),
                    new SqlParameter("@allDates", showAllDates ? 1 : 0),
                    new SqlParameter("@sessionDate", selectedDate));

                foreach (DataRow session in sessions.Rows)
                {
                    int sessionId = Convert.ToInt32(session["SessionId"]);
                    decimal price = Convert.ToDecimal(session["Price"]);
                    DateTime startsAt = Convert.ToDateTime(session["StartsAt"]);
                    string hall = Convert.ToString(session["HallName"]) ?? "";
                    string datePrefix = showAllDates ? startsAt.ToString("dd.MM ") : "";
                    var timeButton = ActionButton($"{datePrefix}{startsAt:HH:mm} • {price:N0} ₽", (_, _) =>
                    {
                        currentSessionId = sessionId;
                        currentPrice = price;
                        selectedSeats.Clear();
                        LoadSeatsForSession();
                        selectionLabel.Text = $"Сеанс: {startsAt:dd.MM HH:mm} · {hall} · Мест: {selectedSeats.Count}";
                    });
                    timeButton.Height = 36;
                    timeButton.AutoSize = true;
                    sessionButtons.Controls.Add(timeButton);
                }

                card.Controls.Add(poster);
                card.Controls.Add(title);
                card.Controls.Add(info);
                card.Controls.Add(description);
                card.Controls.Add(detailsButton);
                card.Controls.Add(sessionButtons);
                buyerMoviesPanel.Controls.Add(card);
                added++;
            }

            if (added == 0)
            {
                string message = showAllDates
                    ? "Нет фильмов с доступными будущими сеансами."
                    : $"На {selectedDate:dd.MM.yyyy} нет фильмов с доступными сеансами. Включите «Все даты», чтобы посмотреть другие даты.";
                buyerMoviesPanel.Controls.Add(new Label
                {
                    Text = message,
                    AutoSize = true,
                    MaximumSize = new Size(Math.Max(300, buyerMoviesPanel.ClientSize.Width - 40), 0),
                    ForeColor = CinemaTheme.Text,
                    Font = new Font("Segoe UI", 11, FontStyle.Bold),
                    Padding = new Padding(14)
                });
            }
        }
        catch (Exception ex)
        {
            buyerMoviesPanel.Controls.Clear();
            buyerMoviesPanel.Controls.Add(new Label
            {
                Text = "Не удалось загрузить каталог:\r\n" + ex.Message,
                AutoSize = true,
                MaximumSize = new Size(Math.Max(300, buyerMoviesPanel.ClientSize.Width - 40), 0),
                ForeColor = Color.OrangeRed,
                Padding = new Padding(14)
            });
        }
        finally
        {
            buyerMoviesPanel.ResumeLayout(true);
        }
    }

    private void ResizeBuyerCards()
    {
        if (isAdmin || buyerMoviesPanel.IsDisposed) return;
        int width = Math.Max(420, buyerMoviesPanel.ClientSize.Width - 45);
        foreach (Control control in buyerMoviesPanel.Controls)
        {
            if (control is not Panel card) continue;
            card.Width = width;
            foreach (Control child in card.Controls)
            {
                if (child is Label label && label.Name != "unused")
                {
                    if (label.Location.X >= 125)
                        label.Width = Math.Max(180, width - label.Location.X - 24);
                }
                else if (child is FlowLayoutPanel flow)
                    flow.Width = Math.Max(220, width - flow.Location.X - 24);
            }
            var details = card.Controls.OfType<Button>().FirstOrDefault(b => b.Text == "Подробнее");
            if (details != null) details.Location = new Point(Math.Max(130, width - 130), 8);
        }
    }

    private void RefreshExpiredSessions()
    {
        try
        {
            int completed = Database.CompleteExpiredSessions();
            if (completed > 0)
            {
                LoadSessions();
                LoadBuyerCatalog();
                UpdateStats();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("Не удалось автоматически обновить статусы сеансов: " + ex.Message);
        }
    }

    private void LoadBuyerCatalog()
    {
        if (!isAdmin && IsHandleCreated) RefreshBuyerCatalog();
    }

    private TabPage BuildHomeTab()
    {
        var page = new TabPage("Главная");
        var panel = new FlowLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(28), AutoScroll = true, BackColor = Color.FromArgb(246, 248, 252) };
        var welcome = new Panel { Name = "welcomePanel", Width = 1000, Height = 165, BackColor = Color.FromArgb(25, 42, 68), Margin = new Padding(0, 0, 0, 22), Padding = new Padding(25) };
        welcome.Controls.Add(new Label { Text = "CINEMA", ForeColor = Color.White, Font = new Font("Segoe UI", 27, FontStyle.Bold), AutoSize = true, Location = new Point(24, 20) });
        welcome.Controls.Add(new Label { Text = "Управляйте кинотеатром в одном месте", ForeColor = Color.FromArgb(207, 220, 239), Font = new Font("Segoe UI", 12), AutoSize = true, Location = new Point(28, 82) });
        panel.Controls.Add(welcome);
        panel.Controls.Add(new Label { Text = "Панель управления", Font = new Font("Segoe UI", 18, FontStyle.Bold), AutoSize = true, Margin = new Padding(0, 0, 0, 16) });
        var stats = new TableLayoutPanel { ColumnCount = 3, RowCount = 1, Width = 1000, Height = 145, Margin = new Padding(0, 10, 0, 25) };
        stats.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33f)); stats.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33f)); stats.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.34f));
        stats.Controls.Add(StatCard("Фильмов", "SELECT COUNT(*) FROM Movies", Color.FromArgb(46, 125, 190)), 0, 0);
        stats.Controls.Add(StatCard("Сеансов", "SELECT COUNT(*) FROM Sessions WHERE StartsAt >= GETDATE()", Color.FromArgb(48, 145, 112)), 1, 0);
        stats.Controls.Add(StatCard("Продано билетов", "SELECT COUNT(*) FROM Tickets", Color.FromArgb(156, 102, 190)), 2, 0);
        panel.Controls.Add(stats);
        panel.Controls.Add(new Label { Text = "Для начала работы выберите нужный раздел в верхней части окна. ", MaximumSize = new Size(900, 0), AutoSize = true, Font = new Font("Segoe UI", 11), ForeColor = Color.DimGray });
        page.Controls.Add(panel);
        return page;
    }

    private Control StatCard(string title, string sql, Color color)
    {
        var card = new Panel { Dock = DockStyle.Fill, BackColor = color, Margin = new Padding(8), Padding = new Padding(16) };
        var value = new Label { Text = "—", ForeColor = Color.White, Font = new Font("Segoe UI", 25, FontStyle.Bold), AutoSize = true, Location = new Point(15, 20), Name = "statValue" };
        card.Controls.Add(value);
        card.Controls.Add(new Label { Text = title, ForeColor = Color.White, Font = new Font("Segoe UI", 11), AutoSize = true, Location = new Point(17, 78) });
        card.Tag = sql;
        return card;
    }

    private TabPage BuildMoviesTab()
    {
        var page = new TabPage("Фильмы");
        // FlowLayoutPanel автоматически размещает кнопки и сохраняет между ними отступы,
        // поэтому подписи не накладываются друг на друга при изменении размера окна.
        var bar = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 66,
            Padding = new Padding(12, 14, 12, 10),
            WrapContents = false,
            AutoScroll = true,
            BackColor = Color.FromArgb(235, 239, 245)
        };
        var detailsButton = ActionButton("Подробнее о фильме", (_, _) => ShowMovieDetails());
        detailsButton.Margin = new Padding(0, 0, 14, 0);
        bar.Controls.Add(detailsButton);
        if (isAdmin)
        {
            var addButton = ActionButton("Добавить фильм", (_, _) => AddMovie());
            var editButton = ActionButton("Изменить фильм", (_, _) => EditMovie());
            var deleteButton = ActionButton("Удалить фильм", (_, _) => DeleteMovie());
            addButton.Margin = editButton.Margin = deleteButton.Margin = new Padding(0, 0, 14, 0);
            bar.Controls.Add(addButton);
            bar.Controls.Add(editButton);
            bar.Controls.Add(deleteButton);
        }

        moviesGrid.CellDoubleClick += (_, e) =>
        {
            if (e.RowIndex >= 0) ShowMovieDetails();
        };
        page.Controls.Add(moviesGrid);
        page.Controls.Add(bar);
        return page;
    }

    private TabPage BuildSessionsTab()
    {
        var page = new TabPage("Сеансы");
        var bar = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 58,
            Padding = new Padding(10),
            WrapContents = false,
            AutoScroll = true,
            BackColor = Color.FromArgb(235, 239, 245)
        };

        bar.Controls.Add(ActionButton("Добавить сеанс", (_, _) => AddSession()));
        bar.Controls.Add(ActionButton("Удалить сеанс", (_, _) => DeleteSession()));
        bar.Controls.Add(ActionButton("Добавить зал", (_, _) => AddHall()));
        bar.Controls.Add(ActionButton("Завершить сеанс", (_, _) => SetSessionStatus("Завершён")));
        bar.Controls.Add(ActionButton("Отменить сеанс", (_, _) => SetSessionStatus("Отменён")));

        page.Controls.Add(sessionsGrid);
        page.Controls.Add(bar);
        return page;
    }

    private TabPage BuildSellTab()
    {
        var page = new TabPage(isAdmin ? "Продажа билетов" : "Покупка билетов");
        var top = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 55, Padding = new Padding(12), WrapContents = false };
        top.Controls.Add(new Label { Text = "Дата:", AutoSize = true, Padding = new Padding(0, 8, 0, 0) });
        saleDatePicker.Value = DateTime.Today;
        top.Controls.Add(saleDatePicker);
        top.Controls.Add(allDatesCheck);
        top.Controls.Add(new Label { Text = "Киносеанс:", AutoSize = true, Padding = new Padding(0, 8, 0, 0) });
        top.Controls.Add(sessionPicker);
        saleDatePicker.ValueChanged += (_, _) => LoadSessionPicker();
        allDatesCheck.CheckedChanged += (_, _) => LoadSessionPicker();
        sessionPicker.SelectedIndexChanged += (_, _) => LoadSeats();
        var bottom = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 62, Padding = new Padding(12), WrapContents = false };
        bottom.Controls.Add(new Label { Text = "Покупатель:", AutoSize = true, Padding = new Padding(0, 8, 0, 0) });
        bottom.Controls.Add(customerBox);
        bottom.Controls.Add(selectionLabel);
        bottom.Controls.Add(totalLabel);
        bottom.Controls.Add(ActionButton("Оформить покупку", (_, _) => SellTickets()));
        page.Controls.Add(seatsPanel); page.Controls.Add(bottom); page.Controls.Add(top);
        return page;
    }

    private TabPage BuildTicketsTab()
    {
        var page = new TabPage("Проданные билеты"); var bar = Toolbar();
        var refreshButton = ActionButton("Обновить", (_, _) => LoadTickets());
        var exportButton = ActionButton("Экспорт CSV", (_, _) => ExportGrid(ticketsGrid, "tickets.csv"));

        refreshButton.Location = new Point(10, 10);
        exportButton.Location = new Point(130, 10);

        bar.Controls.Add(refreshButton);
        bar.Controls.Add(exportButton);

        page.Controls.Add(ticketsGrid);
        page.Controls.Add(bar);

        return page;
    }

    private TabPage BuildReportsTab()
    {
        var page = new TabPage("Отчёты");
        var panel = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 65, Padding = new Padding(12) };
        var summary = new Label { Name = "reportSummary", AutoSize = true, Font = new Font("Segoe UI", 12, FontStyle.Bold), Padding = new Padding(5, 8, 20, 0) };
        panel.Controls.Add(summary);
        panel.Controls.Add(ActionButton("Обновить отчёт", (_, _) => LoadReport()));
        panel.Controls.Add(ActionButton("Экспорт CSV", (_, _) => ExportGrid(salesGrid, "sales_report.csv")));
        page.Controls.Add(salesGrid); page.Controls.Add(panel);
        return page;
    }

    private void RefreshAll()
    {
        Database.CompleteExpiredSessions();
        LoadMovies(); LoadSessions();
        if (isAdmin) LoadSessionPicker();
        else RefreshBuyerCatalog();
        LoadTickets(); LoadReport(); UpdateStats();
    }

    private void LoadMovies()
    {
        moviesGrid.DataSource = Database.Query("SELECT MovieId AS [ID], Title AS [Название], Genre AS [Жанр], DurationMinutes AS [Длительность, мин], AgeRating AS [Возраст], Description AS [Описание], PosterPath FROM Movies ORDER BY Title");
        if (moviesGrid.Columns.Contains("Описание")) moviesGrid.Columns["Описание"].Visible = false;
        if (moviesGrid.Columns.Contains("PosterPath")) moviesGrid.Columns["PosterPath"].Visible = false;
        if (moviesGrid.Columns.Contains("ID")) moviesGrid.Columns["ID"].FillWeight = 35;
        if (moviesGrid.Columns.Contains("Название")) moviesGrid.Columns["Название"].FillWeight = 150;
    }
    private void LoadSessions() => sessionsGrid.DataSource = Database.Query(@"SELECT s.SessionId AS [ID], m.Title AS [Фильм], h.HallName AS [Зал], s.StartsAt AS [Дата и время], s.Price AS [Цена, ₽], s.Status AS [Статус] FROM Sessions s JOIN Movies m ON m.MovieId=s.MovieId JOIN Halls h ON h.HallId=s.HallId ORDER BY s.StartsAt");
    private void LoadTickets() => ticketsGrid.DataSource = Database.Query(@"SELECT t.TicketId AS [№ билета], m.Title AS [Фильм], s.StartsAt AS [Сеанс], h.HallName AS [Зал], st.RowNumber AS [Ряд], st.SeatNumber AS [Место], p.CustomerName AS [Покупатель], t.Price AS [Цена, ₽], p.PurchasedAt AS [Дата покупки] FROM Tickets t JOIN Sessions s ON s.SessionId=t.SessionId JOIN Movies m ON m.MovieId=s.MovieId JOIN Halls h ON h.HallId=s.HallId JOIN Seats st ON st.SeatId=t.SeatId JOIN Purchases p ON p.PurchaseId=t.PurchaseId ORDER BY p.PurchasedAt DESC");

    private void LoadSessionPicker()
    {
        const string sql = @"SELECT s.SessionId,
            m.Title + N' | ' + h.HallName + N' | ' +
            CONVERT(nvarchar(16),s.StartsAt,120) + N' | ' +
            CAST(s.Price AS nvarchar(20)) + N' ₽' AS Caption, s.Price
            FROM Sessions s
            JOIN Movies m ON m.MovieId=s.MovieId
            JOIN Halls h ON h.HallId=s.HallId
            WHERE (@allDates=1 OR CAST(s.StartsAt AS date)=@saleDate)
              AND s.StartsAt >= DATEADD(minute,-30,GETDATE())
              AND s.Status = N'Запланирован'
            ORDER BY s.StartsAt";
        var dt = Database.Query(sql,
            new SqlParameter("@allDates", allDatesCheck.Checked ? 1 : 0),
            new SqlParameter("@saleDate", saleDatePicker.Value.Date));
        sessionPicker.DataSource = null;
        sessionPicker.DisplayMember = "Caption";
        sessionPicker.ValueMember = "SessionId";
        sessionPicker.DataSource = dt;
        if (dt.Rows.Count == 0)
        {
            currentSessionId = 0;
            currentPrice = 0;
            seatsPanel.Controls.Clear();
            seatsPanel.Controls.Add(new Label { Text = "На выбранную дату нет доступных сеансов.", AutoSize = true, Padding = new Padding(15) });
            selectedSeats.Clear();
            UpdateSelection();
            return;
        }
        sessionPicker.SelectedIndex = 0;
        LoadSeats();
    }

    private void LoadSeats()
    {
        if (sessionPicker.SelectedItem is not DataRowView row) return;
        currentSessionId = Convert.ToInt32(row["SessionId"]);
        currentPrice = Convert.ToDecimal(row["Price"]);
        LoadSeatsForSession();
    }

    private void LoadSeatsForSession()
    {
        seatsPanel.Controls.Clear(); selectedSeats.Clear(); UpdateSelection();
        if (currentSessionId <= 0) return;
        var legend = new Label
        {
            Text = "Зелёный — свободно     Красный — занято     Голубой — выбрано",
            AutoSize = true,
            ForeColor = Color.White,
            BackColor = Color.FromArgb(42, 46, 56),
            Padding = new Padding(12, 8, 12, 8),
            Margin = new Padding(10, 6, 10, 12),
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold)
        };
        seatsPanel.Controls.Add(legend);
        var dt = Database.Query(@"SELECT st.SeatId,st.RowNumber,st.SeatNumber,CASE WHEN t.TicketId IS NULL THEN 0 ELSE 1 END AS IsSold FROM Sessions s JOIN Seats st ON st.HallId=s.HallId LEFT JOIN Tickets t ON t.SessionId=s.SessionId AND t.SeatId=st.SeatId WHERE s.SessionId=@id ORDER BY st.RowNumber,st.SeatNumber", new SqlParameter("@id", currentSessionId));
        seatsPanel.Controls.Add(new Label { Text = "━━━━━━━━━━━━━━━━━━  Э К Р А Н  ━━━━━━━━━━━━━━━━━━", Width = 650, Height = 35, TextAlign = ContentAlignment.MiddleCenter, ForeColor = Color.FromArgb(230, 230, 236), BackColor = Color.FromArgb(42, 46, 56), Font = new Font("Segoe UI", 9, FontStyle.Bold), Margin = new Padding(30, 5, 30, 20) });
        int currentRow = 0;
        FlowLayoutPanel? rowPanel = null;
        foreach (DataRow seat in dt.Rows)
        {
            int r = Convert.ToInt32(seat["RowNumber"]), n = Convert.ToInt32(seat["SeatNumber"]), id = Convert.ToInt32(seat["SeatId"]);
            if (r != currentRow)
            {
                currentRow = r;
                rowPanel = new FlowLayoutPanel { Width = 700, Height = 48, WrapContents = false, Margin = new Padding(10, 3, 10, 3) };
                rowPanel.Controls.Add(new Label { Text = $"Ряд {r}", Width = 60, Height = 34, TextAlign = ContentAlignment.MiddleCenter, ForeColor = CinemaTheme.Muted, BackColor = Color.Transparent });
                seatsPanel.Controls.Add(rowPanel);
            }
            var button = new Button { Text = n.ToString(), Width = 48, Height = 34, Margin = new Padding(5, 0, 5, 0), Tag = id, FlatStyle = FlatStyle.Flat };
            bool sold = Convert.ToInt32(seat["IsSold"]) == 1;
            button.BackColor = sold ? Color.FromArgb(190, 42, 55) : Color.FromArgb(47, 105, 82);
            button.ForeColor = Color.White;
            button.FlatAppearance.BorderSize = 1;
            button.FlatAppearance.BorderColor = Color.FromArgb(75, 82, 94);
            button.Enabled = !sold;
            button.Click += (_, _) =>
            {
                if (selectedSeats.Contains(id)) { selectedSeats.Remove(id); button.BackColor = Color.FromArgb(47, 105, 82); }
                else { selectedSeats.Add(id); button.BackColor = Color.FromArgb(45, 155, 205); }
                UpdateSelection();
            };
            rowPanel!.Controls.Add(button);
        }
    }

    private void UpdateSelection()
    {
        selectionLabel.Text = $"Мест: {selectedSeats.Count}";
        totalLabel.Text = $"Итого: {selectedSeats.Count * currentPrice:N2} ₽";
    }

    private void SellTickets()
    {
        if (selectedSeats.Count == 0) { MessageBox.Show("Выберите хотя бы одно свободное место."); return; }
        string customer = customerBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(customer) || customer == "Покупатель") { MessageBox.Show("Введите имя покупателя."); customerBox.Focus(); return; }
        using var con = Database.Open();
        using var tx = con.BeginTransaction();
        try
        {
            decimal total = selectedSeats.Count * currentPrice;
            using var purchase = new SqlCommand("INSERT INTO Purchases(CustomerName,TotalAmount) OUTPUT INSERTED.PurchaseId VALUES(@name,@total)", con, tx);
            purchase.Parameters.AddWithValue("@name", customer); purchase.Parameters.AddWithValue("@total", total);
            int purchaseId = (int)purchase.ExecuteScalar()!;
            foreach (int seatId in selectedSeats)
            {
                using var ticket = new SqlCommand("INSERT INTO Tickets(SessionId,SeatId,PurchaseId,Price) VALUES(@session,@seat,@purchase,@price)", con, tx);
                ticket.Parameters.AddWithValue("@session", currentSessionId); ticket.Parameters.AddWithValue("@seat", seatId); ticket.Parameters.AddWithValue("@purchase", purchaseId); ticket.Parameters.AddWithValue("@price", currentPrice);
                ticket.ExecuteNonQuery();
            }
            tx.Commit();
            MessageBox.Show($"Покупка оформлена.\nКоличество билетов: {selectedSeats.Count}\nСумма: {total:N2} ₽", "Продажа выполнена", MessageBoxButtons.OK, MessageBoxIcon.Information);
            LoadSeatsForSession(); LoadTickets(); LoadReport(); UpdateStats();
        }
        catch (Exception ex) { tx.Rollback(); MessageBox.Show("Не удалось оформить покупку. Возможно, место уже продано.\n" + ex.Message, "Ошибка продажи", MessageBoxButtons.OK, MessageBoxIcon.Warning); LoadSeatsForSession(); }
    }

    private void ShowMovieDetails()
    {
        if (moviesGrid.CurrentRow == null) return;
        using var form = new MovieDetailsDialog(moviesGrid.CurrentRow);
        form.ShowDialog(this);
    }

    private void AddMovie()
    {
        using var form = new MovieDialog();
        if (form.ShowDialog(this) != DialogResult.OK) return;
        using var con = Database.Open();
        using var cmd = new SqlCommand("INSERT INTO Movies(Title,Genre,DurationMinutes,AgeRating,Description,PosterPath) VALUES(@t,@g,@d,@a,@x,@poster)", con);
        form.AddParameters(cmd); cmd.ExecuteNonQuery(); LoadMovies(); UpdateStats();
    }

    private void EditMovie()
    {
        if (moviesGrid.CurrentRow == null) return;
        int id = Convert.ToInt32(moviesGrid.CurrentRow.Cells["ID"].Value);
        using var form = new MovieDialog(moviesGrid.CurrentRow);
        if (form.ShowDialog(this) != DialogResult.OK) return;
        using var con = Database.Open();
        using var cmd = new SqlCommand("UPDATE Movies SET Title=@t,Genre=@g,DurationMinutes=@d,AgeRating=@a,Description=@x,PosterPath=@poster WHERE MovieId=@id", con);
        form.AddParameters(cmd); cmd.Parameters.AddWithValue("@id", id); cmd.ExecuteNonQuery(); LoadMovies(); LoadSessions(); LoadSessionPicker();
    }

    private void DeleteMovie()
    {
        if (moviesGrid.CurrentRow == null) return;
        int id = Convert.ToInt32(moviesGrid.CurrentRow.Cells["ID"].Value);
        if (MessageBox.Show("Удалить выбранный фильм? Фильм с существующими сеансами удалить нельзя.", "Подтверждение", MessageBoxButtons.YesNo) != DialogResult.Yes) return;
        try { using var con = Database.Open(); using var cmd = new SqlCommand("DELETE FROM Movies WHERE MovieId=@id", con); cmd.Parameters.AddWithValue("@id", id); cmd.ExecuteNonQuery(); LoadMovies(); UpdateStats(); }
        catch { MessageBox.Show("Нельзя удалить фильм, для которого уже созданы сеансы."); }
    }

    private void AddSession()
    {
        var movies = Database.Query("SELECT MovieId,Title FROM Movies ORDER BY Title");
        var halls = Database.Query("SELECT HallId,HallName FROM Halls ORDER BY HallName");
        if (movies.Rows.Count == 0 || halls.Rows.Count == 0)
        {
            MessageBox.Show("Сначала добавьте фильмы и залы.");
            return;
        }

        using var form = new SessionDialog(movies, halls);
        if (form.ShowDialog(this) != DialogResult.OK) return;
        if (form.StartsAt <= DateTime.Now)
        {
            MessageBox.Show(
                "Нельзя создать сеанс на прошедшую дату или время.",
                "Ошибка",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }

        using var con = Database.Open();

        // Получаем продолжительность выбранного фильма для проверки пересечения сеансов.
        int durationMinutes;
        using (var durationCommand = new SqlCommand(
            "SELECT DurationMinutes FROM Movies WHERE MovieId=@movieId", con))
        {
            durationCommand.Parameters.Add("@movieId", SqlDbType.Int).Value = form.MovieId;
            var durationResult = durationCommand.ExecuteScalar();
            if (durationResult == null || durationResult == DBNull.Value)
            {
                MessageBox.Show("Не удалось определить продолжительность фильма.", "Ошибка",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            durationMinutes = Convert.ToInt32(durationResult);
        }

        // Не допускаем пересечения сеансов в одном зале.
        // Проверяется весь интервал фильма, а не только совпадение времени начала.
        const string duplicateSql = @"
            SELECT COUNT(*)
            FROM Sessions s
            INNER JOIN Movies existingMovie ON existingMovie.MovieId = s.MovieId
            WHERE s.HallId = @hallId
              AND s.Status <> N'Отменён'
              AND @newStart < DATEADD(minute, existingMovie.DurationMinutes, s.StartsAt)
              AND DATEADD(minute, @newDuration, @newStart) > s.StartsAt;";

        using (var duplicateCheck = new SqlCommand(duplicateSql, con))
        {
            duplicateCheck.Parameters.Add("@hallId", SqlDbType.Int).Value = form.HallId;
            duplicateCheck.Parameters.Add("@newStart", SqlDbType.DateTime2).Value = form.StartsAt;
            duplicateCheck.Parameters.Add("@newDuration", SqlDbType.Int).Value = durationMinutes;

            int conflicts = Convert.ToInt32(duplicateCheck.ExecuteScalar());
            if (conflicts > 0)
            {
                MessageBox.Show(
                    "В выбранном зале на это время уже назначен другой сеанс. " +
                    "Учитывается продолжительность фильма, поэтому сеансы не могут пересекаться. " +
                    "Выберите другое время или зал.",
                    "Зал занят",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }
        }

        using var cmd = new SqlCommand(
            "INSERT INTO Sessions(MovieId,HallId,StartsAt,Price) VALUES(@m,@h,@d,@p)", con);
        cmd.Parameters.Add("@m", SqlDbType.Int).Value = form.MovieId;
        cmd.Parameters.Add("@h", SqlDbType.Int).Value = form.HallId;
        cmd.Parameters.Add("@d", SqlDbType.DateTime2).Value = form.StartsAt;
        cmd.Parameters.Add("@p", SqlDbType.Decimal).Value = form.Price;
        cmd.ExecuteNonQuery();

        LoadSessions();
        if (isAdmin) LoadSessionPicker();
        else RefreshBuyerCatalog();
        UpdateStats();
    }

    private void AddHall()
    {
        using var form = new HallDialog();
        if (form.ShowDialog(this) != DialogResult.OK) return;
        using var con = Database.Open();
        using var tx = con.BeginTransaction();
        try
        {
            int hallId;
            using (var cmd = new SqlCommand("INSERT INTO Halls(HallName,RowsCount,SeatsPerRow) OUTPUT INSERTED.HallId VALUES(@name,@rows,@seats)", con, tx))
            {
                cmd.Parameters.AddWithValue("@name", form.HallName);
                cmd.Parameters.AddWithValue("@rows", form.RowsCount);
                cmd.Parameters.AddWithValue("@seats", form.SeatsPerRow);
                hallId = (int)cmd.ExecuteScalar()!;
            }
            using (var seat = new SqlCommand("INSERT INTO Seats(HallId,RowNumber,SeatNumber) VALUES(@hall,@row,@number)", con, tx))
            {
                seat.Parameters.Add("@hall", SqlDbType.Int).Value = hallId;
                seat.Parameters.Add("@row", SqlDbType.Int);
                seat.Parameters.Add("@number", SqlDbType.Int);
                for (int r = 1; r <= form.RowsCount; r++)
                    for (int n = 1; n <= form.SeatsPerRow; n++)
                    {
                        seat.Parameters["@row"].Value = r;
                        seat.Parameters["@number"].Value = n;
                        seat.ExecuteNonQuery();
                    }
            }
            tx.Commit();
            MessageBox.Show($"Зал «{form.HallName}» добавлен. Создано мест: {form.RowsCount * form.SeatsPerRow}.", "Готово");
        }
        catch (Exception ex)
        {
            tx.Rollback();
            MessageBox.Show("Не удалось добавить зал. Проверьте название.\\n" + ex.Message, "Ошибка");
        }
    }

    private void DeleteSession()
    {
        if (sessionsGrid.CurrentRow == null) return;
        int id = Convert.ToInt32(sessionsGrid.CurrentRow.Cells["ID"].Value);
        try
        {
            using var con = Database.Open();
            using (var check = new SqlCommand("SELECT COUNT(*) FROM Tickets WHERE SessionId=@id", con))
            {
                check.Parameters.AddWithValue("@id", id);
                if (Convert.ToInt32(check.ExecuteScalar()) > 0)
                {
                    MessageBox.Show("У этого сеанса уже есть проданные билеты. Удаление невозможно. Используйте «Завершить сеанс» или «Отменить сеанс», чтобы сохранить историю продаж.",
                        "Удаление невозможно", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
            }
            if (MessageBox.Show("Удалить выбранный сеанс?", "Подтверждение", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            using var cmd = new SqlCommand("DELETE FROM Sessions WHERE SessionId=@id", con);
            cmd.Parameters.AddWithValue("@id", id);
            cmd.ExecuteNonQuery();
            LoadSessions(); LoadSessionPicker(); UpdateStats();
        }
        catch (Exception ex) { MessageBox.Show("Не удалось удалить сеанс. " + ex.Message); }
    }

    private void SetSessionStatus(string status)
    {
        if (sessionsGrid.CurrentRow == null)
        {
            MessageBox.Show("Сначала выберите сеанс в таблице.");
            return;
        }

        int id = Convert.ToInt32(sessionsGrid.CurrentRow.Cells["ID"].Value);
        string currentStatus = Convert.ToString(sessionsGrid.CurrentRow.Cells["Статус"].Value) ?? "";
        if (currentStatus != "Запланирован")
        {
            MessageBox.Show("Изменить статус можно только у запланированного сеанса.");
            return;
        }

        string action = status == "Завершён" ? "завершить" : "отменить";
        if (MessageBox.Show($"Вы действительно хотите {action} выбранный сеанс? Проданные билеты останутся в истории.",
            "Подтверждение", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            return;

        using var con = Database.Open();
        using var cmd = new SqlCommand("UPDATE Sessions SET Status=@status WHERE SessionId=@id AND Status=N'Запланирован'", con);
        cmd.Parameters.AddWithValue("@status", status);
        cmd.Parameters.AddWithValue("@id", id);
        int changed = cmd.ExecuteNonQuery();
        if (changed > 0)
        {
            LoadSessions();
            LoadSessionPicker();
            UpdateStats();
            MessageBox.Show($"Статус сеанса изменён: {status}. Проданные билеты сохранены.", "Готово", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        else
        {
            MessageBox.Show("Не удалось изменить статус. Возможно, сеанс уже был изменён.");
            LoadSessions();
        }
    }

    private void LoadReport()
    {
        salesGrid.DataSource = Database.Query(@"SELECT m.Title AS [Фильм],COUNT(t.TicketId) AS [Продано билетов],COALESCE(SUM(t.Price),0) AS [Выручка, ₽] FROM Movies m LEFT JOIN Sessions s ON s.MovieId=m.MovieId LEFT JOIN Tickets t ON t.SessionId=s.SessionId GROUP BY m.Title ORDER BY m.Title");
        var result = Database.Query("SELECT COUNT(*) AS Tickets, COALESCE(SUM(Price),0) AS Revenue FROM Tickets");
        if (result.Rows.Count > 0)
        {
            var label = Controls.Find("reportSummary", true).FirstOrDefault() as Label;
            if (label != null) label.Text = $"Всего продано: {result.Rows[0]["Tickets"]} бил.     Общая выручка: {Convert.ToDecimal(result.Rows[0]["Revenue"]):N2} ₽";
        }
    }

    private void UpdateStats()
    {
        foreach (Control c in Controls)
            if (c is TabControl tabs)
                foreach (TabPage page in tabs.TabPages)
                    foreach (Control child in page.Controls)
                        if (child is FlowLayoutPanel flow)
                            foreach (Control item in flow.Controls)
                                if (item is TableLayoutPanel table)
                                    foreach (Control card in table.Controls)
                                        if (card is Panel p && p.Tag is string sql)
                                            try { var v = Database.Query(sql); var label = p.Controls.Find("statValue", false).FirstOrDefault(); if (label != null) label.Text = v.Rows[0][0].ToString(); } catch { }
    }

    private static void ExportGrid(DataGridView grid, string fileName)
    {
        if (grid.DataSource == null) return;
        using var dialog = new SaveFileDialog { FileName = fileName, Filter = "CSV (*.csv)|*.csv", DefaultExt = "csv" };
        if (dialog.ShowDialog() != DialogResult.OK) return;
        var lines = new List<string>();
        lines.Add(string.Join(";", grid.Columns.Cast<DataGridViewColumn>().Select(c => Csv(c.HeaderText))));
        foreach (DataGridViewRow row in grid.Rows)
            if (!row.IsNewRow) lines.Add(string.Join(";", row.Cells.Cast<DataGridViewCell>().Select(c => Csv(Convert.ToString(c.Value, CultureInfo.InvariantCulture) ?? ""))));
        File.WriteAllLines(dialog.FileName, lines, System.Text.Encoding.UTF8);
        MessageBox.Show("Отчёт сохранён.");
    }

    private static string Csv(string value) => "\"" + value.Replace("\"", "\"\"") + "\"";
    private static DataGridView MakeGrid() => new() { Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false, AllowUserToDeleteRows = false, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, SelectionMode = DataGridViewSelectionMode.FullRowSelect, MultiSelect = false, BackgroundColor = Color.White, BorderStyle = BorderStyle.None, RowHeadersVisible = false };
    private static Panel Toolbar() => new() { Dock = DockStyle.Top, Height = 58, Padding = new Padding(10), BackColor = Color.FromArgb(235, 239, 245) };
    private static Button ActionButton(string text, EventHandler click)
    {
        var b = new Button { Text = text, AutoSize = true, Height = 34, FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(38, 103, 160), ForeColor = Color.White, Margin = new Padding(5, 0, 5, 0), Padding = new Padding(8, 0, 8, 0) };
        b.FlatAppearance.BorderSize = 0; b.Click += click; return b;
    }
}

internal sealed class MovieDialog : Form
{
    private readonly TextBox title = new() { Width = 300 };
    private readonly TextBox genre = new() { Width = 300 };
    private readonly NumericUpDown duration = new() { Minimum = 1, Maximum = 500, Value = 100, Width = 100 };
    private readonly ComboBox age = new() { Width = 100, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly TextBox description = new() { Width = 300, Height = 90, Multiline = true, ScrollBars = ScrollBars.Vertical };
    private readonly Label posterLabel = new() { AutoSize = true, MaximumSize = new Size(290, 0), Text = "Обложка не выбрана" };
    private string? selectedPosterPath;
    private readonly string? originalPosterPath;

    public MovieDialog(DataGridViewRow? row = null)
    {
        Text = row == null ? "Добавление фильма" : "Изменение фильма";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = false; MinimizeBox = false;
        ClientSize = new Size(500, 440);
        age.Items.AddRange(new object[] { "0+", "6+", "12+", "16+", "18+" });
        age.SelectedIndex = 2;
        if (row != null)
        {
            title.Text = Convert.ToString(row.Cells["Название"].Value);
            genre.Text = Convert.ToString(row.Cells["Жанр"].Value);
            duration.Value = Math.Clamp(Convert.ToDecimal(row.Cells["Длительность, мин"].Value), 1, 500);
            age.SelectedItem = Convert.ToString(row.Cells["Возраст"].Value);
            description.Text = Convert.ToString(row.Cells["Описание"].Value);
            originalPosterPath = Convert.ToString(row.Cells["PosterPath"].Value);
            selectedPosterPath = originalPosterPath;
            posterLabel.Text = string.IsNullOrWhiteSpace(originalPosterPath) ? "Обложка не выбрана" : Path.GetFileName(originalPosterPath);
        }

        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(15), ColumnCount = 2, RowCount = 7 };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 125));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        AddRow(layout, "Название:", title, 0);
        AddRow(layout, "Жанр:", genre, 1);
        AddRow(layout, "Длительность:", duration, 2);
        AddRow(layout, "Возраст:", age, 3);
        AddRow(layout, "Описание:", description, 4);
        var posterPanel = new FlowLayoutPanel { AutoSize = true, WrapContents = true, Dock = DockStyle.Fill };
        var choosePoster = new Button { Text = "Выбрать обложку…", AutoSize = true };
        choosePoster.Click += (_, _) => ChoosePoster();
        posterPanel.Controls.Add(choosePoster);
        posterPanel.Controls.Add(posterLabel);
        AddRow(layout, "Обложка:", posterPanel, 5);
        var buttons = new FlowLayoutPanel { FlowDirection = FlowDirection.RightToLeft, Dock = DockStyle.Fill };
        var ok = new Button { Text = "Сохранить", DialogResult = DialogResult.OK, Width = 100 };
        var cancel = new Button { Text = "Отмена", DialogResult = DialogResult.Cancel, Width = 90 };
        buttons.Controls.Add(ok); buttons.Controls.Add(cancel);
        layout.Controls.Add(buttons, 1, 6);
        Controls.Add(layout); AcceptButton = ok; CancelButton = cancel;
        CinemaTheme.Apply(this);
        ok.Click += (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(title.Text) || string.IsNullOrWhiteSpace(genre.Text))
            {
                MessageBox.Show("Заполните название и жанр.");
                DialogResult = DialogResult.None;
                return;
            }
            try { SaveSelectedPoster(); }
            catch (Exception ex)
            {
                MessageBox.Show("Не удалось сохранить обложку: " + ex.Message, "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                DialogResult = DialogResult.None;
            }
        };
    }

    public string? PosterPath { get; private set; }

    private void ChoosePoster()
    {
        using var dialog = new OpenFileDialog
        {
            Title = "Выберите обложку фильма",
            Filter = "Изображения|*.jpg;*.jpeg;*.png;*.bmp;*.webp|Все файлы|*.*"
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        selectedPosterPath = dialog.FileName;
        posterLabel.Text = Path.GetFileName(selectedPosterPath);
    }

    private void SaveSelectedPoster()
    {
        if (string.IsNullOrWhiteSpace(selectedPosterPath))
        {
            PosterPath = null;
            return;
        }
        if (string.Equals(selectedPosterPath, originalPosterPath, StringComparison.OrdinalIgnoreCase))
        {
            PosterPath = originalPosterPath;
            return;
        }
        string folder = Path.Combine(Application.LocalUserAppDataPath, "Posters");
        Directory.CreateDirectory(folder);
        string extension = Path.GetExtension(selectedPosterPath);
        string destination = Path.Combine(folder, Guid.NewGuid().ToString("N") + extension);
        File.Copy(selectedPosterPath, destination, false);
        PosterPath = destination;
    }

    private static void AddRow(TableLayoutPanel p, string label, Control c, int row)
    {
        p.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        p.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left, Padding = new Padding(0, 5, 0, 0) }, 0, row);
        p.Controls.Add(c, 1, row);
    }

    public void AddParameters(SqlCommand cmd)
    {
        cmd.Parameters.AddWithValue("@t", title.Text.Trim());
        cmd.Parameters.AddWithValue("@g", genre.Text.Trim());
        cmd.Parameters.AddWithValue("@d", (int)duration.Value);
        cmd.Parameters.AddWithValue("@a", age.Text);
        cmd.Parameters.AddWithValue("@x", description.Text.Trim());
        cmd.Parameters.AddWithValue("@poster", (object?)PosterPath ?? DBNull.Value);
    }
}

internal sealed class MovieDetailsDialog : Form
{
    public MovieDetailsDialog(DataGridViewRow row)
        : this(
            Convert.ToString(row.Cells["Название"].Value) ?? "Без названия",
            Convert.ToString(row.Cells["Жанр"].Value) ?? "",
            Convert.ToString(row.Cells["Длительность, мин"].Value) ?? "",
            Convert.ToString(row.Cells["Возраст"].Value) ?? "",
            Convert.ToString(row.Cells["Описание"].Value) ?? "",
            Convert.ToString(row.Cells["PosterPath"].Value) ?? "")
    {
    }

    public MovieDetailsDialog(DataRow row)
        : this(
            Convert.ToString(row["Title"]) ?? "Без названия",
            Convert.ToString(row["Genre"]) ?? "",
            Convert.ToString(row["DurationMinutes"]) ?? "",
            Convert.ToString(row["AgeRating"]) ?? "",
            Convert.ToString(row["Description"]) ?? "",
            Convert.ToString(row["PosterPath"]) ?? "")
    {
    }

    private MovieDetailsDialog(string title, string genre, string duration, string age, string description, string posterPath)
    {
        Text = "Информация о фильме";
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(650, 460);
        Size = new Size(800, 560);
        BackColor = Color.White;

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(22), ColumnCount = 2, RowCount = 2 };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 230));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));

        var poster = new PictureBox { Dock = DockStyle.Fill, SizeMode = PictureBoxSizeMode.Zoom, BackColor = Color.FromArgb(238, 241, 246), Margin = new Padding(0, 0, 20, 10) };
        if (!string.IsNullOrWhiteSpace(posterPath) && File.Exists(posterPath))
        {
            try { using var source = Image.FromFile(posterPath); poster.Image = new Bitmap(source); }
            catch { poster.Image = null; }
        }
        if (poster.Image == null)
            poster.Controls.Add(new Label { Text = "ОБЛОЖКА\nНЕ ДОБАВЛЕНА", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, ForeColor = Color.DimGray, Font = new Font("Segoe UI", 11, FontStyle.Bold) });

        var info = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, AutoScroll = true };
        info.Controls.Add(new Label { Text = title, AutoSize = true, Font = new Font("Segoe UI", 21, FontStyle.Bold), MaximumSize = new Size(480, 0) });
        info.Controls.Add(new Label { Text = genre, AutoSize = true, Font = new Font("Segoe UI", 11), ForeColor = Color.DimGray, Padding = new Padding(0, 8, 0, 12) });
        info.Controls.Add(new Label
        {
            Text = $"Продолжительность: {duration} мин.\nВозрастное ограничение: {age}",
            AutoSize = true, Font = new Font("Segoe UI", 10, FontStyle.Bold), Padding = new Padding(0, 5, 0, 15)
        });
        info.Controls.Add(new Label { Text = "Описание", AutoSize = true, Font = new Font("Segoe UI", 12, FontStyle.Bold), Padding = new Padding(0, 4, 0, 5) });
        info.Controls.Add(new Label
        {
            Text = string.IsNullOrWhiteSpace(description) ? "Описание пока не добавлено." : description,
            AutoSize = true, MaximumSize = new Size(480, 0), Font = new Font("Segoe UI", 10), Padding = new Padding(0, 0, 8, 10)
        });
        root.Controls.Add(poster, 0, 0);
        root.Controls.Add(info, 1, 0);
        var close = new Button { Text = "Закрыть", Width = 110, Height = 34, Anchor = AnchorStyles.Right };
        close.Click += (_, _) => Close();
        root.Controls.Add(close, 1, 1);
        Controls.Add(root);
        CinemaTheme.Apply(this);
    }
}

internal sealed class HallDialog : Form
{
    private readonly TextBox nameBox = new() { Width = 220 };
    private readonly NumericUpDown rowsBox = new() { Minimum = 1, Maximum = 30, Value = 5, Width = 100 };
    private readonly NumericUpDown seatsBox = new() { Minimum = 1, Maximum = 40, Value = 8, Width = 100 };
    public string HallName => nameBox.Text.Trim();
    public int RowsCount => (int)rowsBox.Value;
    public int SeatsPerRow => (int)seatsBox.Value;

    public HallDialog()
    {
        Text = "Добавление кинозала";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = false; MinimizeBox = false;
        ClientSize = new Size(390, 220);
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(15), ColumnCount = 2, RowCount = 4 };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 135));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        AddRow(layout, "Название зала:", nameBox, 0);
        AddRow(layout, "Количество рядов:", rowsBox, 1);
        AddRow(layout, "Мест в ряду:", seatsBox, 2);
        var buttons = new FlowLayoutPanel { FlowDirection = FlowDirection.RightToLeft, Dock = DockStyle.Fill };
        var ok = new Button { Text = "Добавить", DialogResult = DialogResult.OK, Width = 100 };
        var cancel = new Button { Text = "Отмена", DialogResult = DialogResult.Cancel, Width = 90 };
        buttons.Controls.Add(ok); buttons.Controls.Add(cancel);
        layout.Controls.Add(buttons, 1, 3);
        Controls.Add(layout); AcceptButton = ok; CancelButton = cancel;
        CinemaTheme.Apply(this);
        ok.Click += (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(nameBox.Text))
            {
                MessageBox.Show("Введите название зала.");
                DialogResult = DialogResult.None;
            }
        };
    }

    private static void AddRow(TableLayoutPanel p, string label, Control c, int row)
    {
        p.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        p.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left, Padding = new Padding(0, 5, 0, 0) }, 0, row);
        p.Controls.Add(c, 1, row);
    }
}

internal sealed class SessionDialog : Form
{
    private readonly ComboBox movie = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 270 };
    private readonly ComboBox hall = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 270 };
    private readonly DateTimePicker date = new() { Format = DateTimePickerFormat.Custom, CustomFormat = "dd.MM.yyyy HH:mm", Width = 180, ShowUpDown = true };
    private readonly NumericUpDown price = new() { Minimum = 0, Maximum = 100000, Value = 350, Width = 120, Increment = 50 };
    public int MovieId => Convert.ToInt32(movie.SelectedValue);
    public int HallId => Convert.ToInt32(hall.SelectedValue);
    public DateTime StartsAt => date.Value;
    public decimal Price => price.Value;
    public SessionDialog(DataTable movies, DataTable halls)
    {
        Text = "Добавление сеанса"; FormBorderStyle = FormBorderStyle.FixedDialog; StartPosition = FormStartPosition.CenterParent; MaximizeBox = false; MinimizeBox = false; ClientSize = new Size(430, 260);
        movie.DataSource = movies; movie.DisplayMember = "Title"; movie.ValueMember = "MovieId"; hall.DataSource = halls; hall.DisplayMember = "HallName"; hall.ValueMember = "HallId"; date.Value = DateTime.Now.AddHours(2);
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(15), ColumnCount = 2, RowCount = 5 }; layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110)); layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        AddRow(layout, "Фильм:", movie, 0); AddRow(layout, "Зал:", hall, 1); AddRow(layout, "Дата и время:", date, 2); AddRow(layout, "Цена, ₽:", price, 3);
        var buttons = new FlowLayoutPanel { FlowDirection = FlowDirection.RightToLeft, Dock = DockStyle.Fill }; var ok = new Button { Text = "Сохранить", DialogResult = DialogResult.OK, Width = 100 }; var cancel = new Button { Text = "Отмена", DialogResult = DialogResult.Cancel, Width = 90 }; buttons.Controls.Add(ok); buttons.Controls.Add(cancel); layout.Controls.Add(buttons, 1, 4); Controls.Add(layout); AcceptButton = ok; CancelButton = cancel;
        CinemaTheme.Apply(this);
    }
    private static void AddRow(TableLayoutPanel p, string label, Control c, int row) { p.RowStyles.Add(new RowStyle(SizeType.AutoSize)); p.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left, Padding = new Padding(0, 5, 0, 0) }, 0, row); p.Controls.Add(c, 1, row); }
}
