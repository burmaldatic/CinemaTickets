using Microsoft.Data.SqlClient;
using System.Data;

namespace CinemaTickets;

internal static class Database
{
    private const string MasterConnection = @"Server=(localdb)\MSSQLLocalDB;Integrated Security=true;TrustServerCertificate=True;";
    public const string ConnectionString = @"Server=(localdb)\MSSQLLocalDB;Database=CinemaTicketsDb;Integrated Security=true;TrustServerCertificate=True;";

    public static void Initialize()
    {
        using (var master = new SqlConnection(MasterConnection))
        {
            master.Open();
            using var cmd = master.CreateCommand();
            cmd.CommandText = "IF DB_ID(N'CinemaTicketsDb') IS NULL CREATE DATABASE CinemaTicketsDb;";
            cmd.ExecuteNonQuery();
        }

        using var con = Open();
        var sql = @"
IF OBJECT_ID('Movies','U') IS NULL
BEGIN
 CREATE TABLE Movies(
  MovieId INT IDENTITY(1,1) PRIMARY KEY,
  Title NVARCHAR(120) NOT NULL,
  Genre NVARCHAR(60) NOT NULL,
  DurationMinutes INT NOT NULL CHECK(DurationMinutes > 0),
  AgeRating NVARCHAR(10) NOT NULL,
  Description NVARCHAR(500) NULL
 );
END;
IF OBJECT_ID('Halls','U') IS NULL
BEGIN
 CREATE TABLE Halls(
  HallId INT IDENTITY(1,1) PRIMARY KEY,
  HallName NVARCHAR(50) NOT NULL,
  RowsCount INT NOT NULL,
  SeatsPerRow INT NOT NULL
 );
END;
IF OBJECT_ID('Seats','U') IS NULL
BEGIN
 CREATE TABLE Seats(
  SeatId INT IDENTITY(1,1) PRIMARY KEY,
  HallId INT NOT NULL REFERENCES Halls(HallId),
  RowNumber INT NOT NULL,
  SeatNumber INT NOT NULL,
  CONSTRAINT UQ_Seat UNIQUE(HallId, RowNumber, SeatNumber)
 );
END;
IF OBJECT_ID('Sessions','U') IS NULL
BEGIN
 CREATE TABLE Sessions(
  SessionId INT IDENTITY(1,1) PRIMARY KEY,
  MovieId INT NOT NULL REFERENCES Movies(MovieId),
  HallId INT NOT NULL REFERENCES Halls(HallId),
  StartsAt DATETIME2 NOT NULL,
  Price DECIMAL(10,2) NOT NULL CHECK(Price >= 0),
  Status NVARCHAR(20) NOT NULL CONSTRAINT DF_Sessions_Status DEFAULT N'Запланирован'
 );
END;
IF OBJECT_ID('Purchases','U') IS NULL
BEGIN
 CREATE TABLE Purchases(
  PurchaseId INT IDENTITY(1,1) PRIMARY KEY,
  PurchasedAt DATETIME2 NOT NULL DEFAULT SYSDATETIME(),
  CustomerName NVARCHAR(100) NOT NULL,
  TotalAmount DECIMAL(10,2) NOT NULL
 );
END;
IF OBJECT_ID('Tickets','U') IS NULL
BEGIN
 CREATE TABLE Tickets(
  TicketId INT IDENTITY(1,1) PRIMARY KEY,
  SessionId INT NOT NULL REFERENCES Sessions(SessionId),
  SeatId INT NOT NULL REFERENCES Seats(SeatId),
  PurchaseId INT NOT NULL REFERENCES Purchases(PurchaseId),
  Price DECIMAL(10,2) NOT NULL,
  CONSTRAINT UQ_Ticket_Session_Seat UNIQUE(SessionId, SeatId)
 );
END;";
        using (var cmd = new SqlCommand(sql, con)) cmd.ExecuteNonQuery();
        using (var migration = new SqlCommand(@"
IF COL_LENGTH('Movies','PosterPath') IS NULL
    ALTER TABLE Movies ADD PosterPath NVARCHAR(500) NULL;

IF COL_LENGTH('Sessions','Status') IS NULL
    ALTER TABLE Sessions ADD Status NVARCHAR(20) NOT NULL
        CONSTRAINT DF_Sessions_Status DEFAULT N'Запланирован' WITH VALUES;", con))
            migration.ExecuteNonQuery();

        using var check = new SqlCommand("SELECT COUNT(*) FROM Movies", con);
        if ((int)check.ExecuteScalar()! == 0)
        {
            using var seed = new SqlCommand(@"
INSERT INTO Movies(Title,Genre,DurationMinutes,AgeRating,Description) VALUES
(N'Интерстеллар',N'Фантастика',169,N'12+',N'Научно-фантастический фильм о путешествии через космос.'),
(N'Начало',N'Фантастика, триллер',148,N'12+',N'История о проникновении в сны людей.'),
(N'Корпорация монстров',N'Анимация, комедия',92,N'0+',N'Приключения работников фабрики монстров.');
INSERT INTO Halls(HallName,RowsCount,SeatsPerRow) VALUES(N'Зал 1',5,8),(N'Зал 2',4,6);
INSERT INTO Seats(HallId,RowNumber,SeatNumber)
SELECT h.HallId, r.n, s.n FROM Halls h
CROSS JOIN (SELECT 1 n UNION ALL SELECT 2 UNION ALL SELECT 3 UNION ALL SELECT 4 UNION ALL SELECT 5) r
CROSS JOIN (SELECT 1 n UNION ALL SELECT 2 UNION ALL SELECT 3 UNION ALL SELECT 4 UNION ALL SELECT 5 UNION ALL SELECT 6 UNION ALL SELECT 7 UNION ALL SELECT 8) s
WHERE r.n <= h.RowsCount AND s.n <= h.SeatsPerRow;
INSERT INTO Sessions(MovieId,HallId,StartsAt,Price)
SELECT 1,1,DATEADD(hour,2,CAST(CAST(GETDATE() AS date) AS datetime2)),350
UNION ALL SELECT 2,1,DATEADD(hour,5,CAST(CAST(GETDATE() AS date) AS datetime2)),400
UNION ALL SELECT 3,2,DATEADD(hour,3,CAST(CAST(GETDATE() AS date) AS datetime2)),300;", con);
            seed.ExecuteNonQuery();
        }
    }


    public static int CompleteExpiredSessions()
    {
        using var con = Open();
        using var cmd = new SqlCommand(@"
            UPDATE s
            SET Status = N'Завершён'
            FROM Sessions s
            INNER JOIN Movies m ON m.MovieId = s.MovieId
            WHERE s.Status = N'Запланирован'
              AND DATEADD(minute, m.DurationMinutes, s.StartsAt) <= SYSDATETIME();", con);
        return cmd.ExecuteNonQuery();
    }

    public static SqlConnection Open()
    {
        var con = new SqlConnection(ConnectionString);
        con.Open();
        return con;
    }

    public static DataTable Query(string sql, params SqlParameter[] parameters)
    {
        using var con = Open();
        using var cmd = new SqlCommand(sql, con);
        if (parameters.Length > 0) cmd.Parameters.AddRange(parameters);
        using var adapter = new SqlDataAdapter(cmd);
        var table = new DataTable();
        adapter.Fill(table);
        return table;
    }
}
