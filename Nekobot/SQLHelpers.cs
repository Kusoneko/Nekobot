using System.Data.SQLite;
using System.Threading.Tasks;

namespace Nekobot
{
    partial class Program
    {
        static SQLiteConnection connection;

        static void LoadDB()
        {
            if (!System.IO.File.Exists("nekobot.db"))
                SQLiteConnection.CreateFile("nekobot.db");
            connection = new SQLiteConnection("Data Source=nekobot.db;Version=3;");
            connection.Open();
            ExecuteNonQuery("create table if not exists users (user varchar(17), perms int, ignored int)");
            ExecuteNonQuery("create table if not exists flags (channel varchar(17), nsfw int, music int, ignored int, chatbot int default -1)");
            try { ExecuteNonQuery("alter table flags add chatbot int default -1"); }
            catch (System.Data.SQLite.SQLiteException) { }
        }

        // SQL Helpers
        static SQLiteCommand SQLCommand(string sql)
        {
            return new SQLiteCommand(sql, connection);
        }

        public static void ExecuteNonQuery(string sql)
        {
            SQLCommand(sql).ExecuteNonQuery();
        }

        public static async Task ExecuteNonQueryAsync(string sql)
        {
            await SQLCommand(sql).ExecuteNonQueryAsync();
        }

        public static bool ExecuteScalarPos(string sql)
        {
            return System.Convert.ToInt32(SQLCommand(sql).ExecuteScalar()) > 0;
        }

        public static SQLiteDataReader ExecuteReader(string sql)
        {
            return SQLCommand(sql).ExecuteReader();
        }

        // Other stuff
        static void CloseAndDisposeConnection()
        {
            connection.Close();
            connection.Dispose();
        }
    }
}