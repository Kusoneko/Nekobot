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
            CreateTable("users (user varchar(17), perms int, ignored int, lastfm varchar(15))");
            CreateField("users", "lastfm varchar(15)");
            CreateTable("flags (channel varchar(17), nsfw int, music int, ignored int, chatbot int)");
            CreateField("flags", "chatbot int");
        }

        // SQL Helpers
        static SQLiteCommand Command(string sql) => new SQLiteCommand(sql, connection);
        static void CreateTable(string table) => ExecuteNonQuery($"create table if not exists {table}");
        static void CreateField(string table, string field)
        {
            try { ExecuteNonQuery($"alter table {table} add {field}"); } catch { }
        }
        static void ExecuteNonQuery(string sql) => Command(sql).ExecuteNonQuery();
        internal static async Task ExecuteNonQueryAsync(string sql) => await Command(sql).ExecuteNonQueryAsync();
        internal static bool ExecuteScalarPos(string sql) => System.Convert.ToInt32(Command(sql).ExecuteScalar()) > 0;
        // TODO: Remove insertdata for a function based on the row and field and newval.
        internal static bool InTable(string row, string table, long id)
            => ExecuteScalarPos($"select count({row}) from {table} where {row}='{id}'");
        internal static string AddOrUpdateCommand(string row, string table, long id, string field, string newval, string insertdata, bool in_table)
            => in_table
                ? $"update {table} set {field}={newval} where {row}='{id}'"
                : $"insert into {table} values ('{id}', {insertdata})";
        static string AddOrUpdateCommand(string row, string table, long id, string field, string newval, string insertdata)
            => AddOrUpdateCommand(row, table, id, field, newval, insertdata, InTable(row, table, id));
        static void AddOrUpdate(string row, string table, long id, string field, string newval, string insertdata)
            => ExecuteNonQuery(AddOrUpdateCommand(row, table, id, field, newval, insertdata));
        internal static void AddOrUpdateFlag(long id, string field, string newval, string insertdata)
            => AddOrUpdate("channel", "flags", id, field, newval, insertdata);
        static async Task AddOrUpdateAsync(string row, string table, long id, string field, string newval, string insertdata)
            => await ExecuteNonQueryAsync(AddOrUpdateCommand(row, table, id, field, newval, insertdata));
        internal static async Task AddOrUpdateFlagAsync(long id, string field, string newval, string insertdata)
            => await AddOrUpdateAsync("channel", "flags", id, field, newval, insertdata);
        internal static async Task AddOrUpdateUserAsync(long id, string field, string newval, string insertdata)
            => await AddOrUpdateAsync("user", "users", id, field, newval, insertdata);

        static SQLiteDataReader ExecuteReader(string sql) => Command(sql).ExecuteReader();
        internal static SQLiteDataReader ExecuteReader(string value, string table, string condition)
            => ExecuteReader($"select {value} from {table} where {condition}");
        /*internal static SQLiteDataReader ReadUsers(string condition, string value = "user")
            => ExecuteReader(value, "users", condition);*/
        internal static SQLiteDataReader ReadChannels(string condition, string value = "channel")
            => ExecuteReader(value, "flags", condition);
        internal static string ReadSingle(string row, string table, long id, string value)
        {
            var reader = ExecuteReader(value, table, $"{row} = '{id}'");
            return reader.Read() ? reader[value].ToString() : null;
        }
        internal static string ReadUser(long id, string value) => ReadSingle("user", "users", id, value);
        internal static string ReadChannel(long id, string value) => ReadSingle("channel", "flags", id, value);
        internal static int ReadInt(string data) => data != null ? int.Parse(data) : 0;
        internal static bool ReadBool(string data) => ReadInt(data) == 1;

        // Other stuff
        internal static void CloseAndDispose()
        {
            connection.Close();
            connection.Dispose();
        }
    }
}