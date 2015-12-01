using System.Data.SQLite;
using System.Threading.Tasks;

namespace Nekobot
{
    class SQL
    {
        static SQLiteConnection connection;

        internal static void LoadDB()
        {
            if (!System.IO.File.Exists("nekobot.db"))
                SQLiteConnection.CreateFile("nekobot.db");
            connection = new SQLiteConnection("Data Source=nekobot.db;Version=3;");
            connection.Open();
            ExecuteNonQuery("create table if not exists users (user varchar(17), perms int, ignored int)");
            ExecuteNonQuery("create table if not exists flags (channel varchar(17), nsfw int, music int, ignored int, chatbot int default -1)");
            try { ExecuteNonQuery("alter table flags add chatbot int default -1"); }
            catch (SQLiteException) { }
        }

        // SQL Helpers
        static SQLiteCommand SQLCommand(string sql)
        {
            return new SQLiteCommand(sql, connection);
        }

        internal static void ExecuteNonQuery(string sql)
        {
            SQLCommand(sql).ExecuteNonQuery();
        }

        internal static async Task ExecuteNonQueryAsync(string sql)
        {
            await SQLCommand(sql).ExecuteNonQueryAsync();
        }

        internal static bool ExecuteScalarPos(string sql)
        {
            return System.Convert.ToInt32(SQLCommand(sql).ExecuteScalar()) > 0;
        }

        internal static SQLiteDataReader ExecuteReader(string sql)
        {
            return SQLCommand(sql).ExecuteReader();
        }

        // Other stuff
        internal static void CloseAndDispose()
        {
            connection.Close();
            connection.Dispose();
        }
    }
}