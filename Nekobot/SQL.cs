﻿using System.Data.SQLite;
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
            CreateTable("users (user varchar(17), perms int, ignored int, lastfm varchar(15), entrance_gesture varchar(512))");
            CreateField("users", "lastfm varchar(15)");
            CreateField("users", "entrance_gesture varchar(512)");
            CreateTable("flags (channel varchar(17), nsfw int, music int, ignored int, chatbot int)");
            CreateField("flags", "chatbot int");
            CreateTable("roles (role varchar(17), ignored int)");
            CreateTable("servers (server varchar(17), welcome int, default_roles varchar(256), welcomechannel varchar(17), sayleft int, leftchannel varchar(17))");
            CreateField("servers", "welcomechannel varchar(17)");
            CreateField("servers", "sayleft int");
            CreateField("servers", "leftchannel varchar(17)");
        }

        // SQL Helpers
        static string InsertData(string table, string field, string newval)
        {
            // There's gotta be a better way than this, but I've got nothing...
            if (table == "users")
            {
                switch(field)
                {
                    case "perms": return $"{newval}, 0, '', ''";
                    case "ignored": return $"0, {newval}, '', ''";
                    case "lastfm": return $"0, 0, {newval}, ''";
                    case "entrance_gesture": return $"0, 0, '', {newval}";
                }
            }
            else if (table == "flags")
            {
                switch(field)
                {
                    case "nsfw": return $"{newval}, 0, 0, -1";
                    case "music": return $"0, {newval}, 0, -1";
                    case "ignored": return $"0, 0, {newval}, -1";
                    case "chatbot": return $"0, 0, 0, {newval}";
                }
            }
            else if (table == "servers")
            {
                switch (field)
                {
                    case "welcome": return $"{newval}, '', '', 0, ''";
                    case "default_roles": return $"1, {newval}, '', 0, ''";
                    case "welcomechannel": return $"1, '', {newval}, 0, ''";
                    case "sayleft": return $"1, '', '', {newval}, ''";
                    case "leftchannel": return $"1, '', '', 0, {newval}";
                }
            }
            /*else if (table == "roles")
            {
                switch(field)
                {
                    case "ignored": return $"{newval}";
                }
            }*/
            else return newval;
            throw new System.Exception($"Unknown field '{field}' in table '{table}.'");
        }
        static SQLiteCommand Command(string sql) => new SQLiteCommand(sql, connection);
        static void CreateTable(string table) => ExecuteNonQuery($"create table if not exists {table}");
        static void CreateField(string table, string field)
        {
            try { ExecuteNonQuery($"alter table {table} add {field}"); } catch { }
        }
        internal static void ExecuteNonQuery(string sql) => Command(sql).ExecuteNonQuery();
        internal static async Task ExecuteNonQueryAsync(string sql) => await Command(sql).ExecuteNonQueryAsync();
        internal static bool ExecuteScalarPos(string sql) => System.Convert.ToInt32(Command(sql).ExecuteScalar()) > 0;
        internal static bool InTable(string row, string table, ulong id)
            => ExecuteScalarPos($"select count({row}) from {table} where {row}='{id}'");
        internal static string AddOrUpdateCommand(string row, string table, ulong id, string field, string newval, bool in_table)
            => in_table
                ? $"update {table} set {field}={newval} where {row}='{id}'"
                : $"insert into {table} values ('{id}', {InsertData(table, field, newval)})";
        static string AddOrUpdateCommand(string row, string table, ulong id, string field, string newval)
            => AddOrUpdateCommand(row, table, id, field, newval, InTable(row, table, id));
        static void AddOrUpdate(string row, string table, ulong id, string field, string newval)
            => ExecuteNonQuery(AddOrUpdateCommand(row, table, id, field, newval));
        internal static void AddOrUpdateFlag(ulong id, string field, string newval)
            => AddOrUpdate("channel", "flags", id, field, newval);
        internal static void AddOrUpdateServer(ulong id, string field, string newval)
            => AddOrUpdate("server", "servers", id, field, newval);
        static async Task AddOrUpdateAsync(string row, string table, ulong id, string field, string newval)
            => await ExecuteNonQueryAsync(AddOrUpdateCommand(row, table, id, field, newval));
        internal static async Task AddOrUpdateFlagAsync(ulong id, string field, string newval)
            => await AddOrUpdateAsync("channel", "flags", id, field, newval);
        internal static async Task AddOrUpdateUserAsync(ulong id, string field, string newval)
            => await AddOrUpdateAsync("user", "users", id, field, newval);

        static SQLiteDataReader ExecuteReader(string sql) => Command(sql).ExecuteReader();
        internal static SQLiteDataReader ExecuteReader(string value, string table, string condition)
            => ExecuteReader($"select {value} from {table} where {condition}");
        internal static SQLiteDataReader ReadUsers(string condition, string value = "user")
            => ExecuteReader(value, "users", condition);
        internal static SQLiteDataReader ReadChannels(string condition, string value = "channel")
            => ExecuteReader(value, "flags", condition);
        internal static SQLiteDataReader ReadRoles(string condition, string value = "role")
            => ExecuteReader(value, "roles", condition);
        internal static SQLiteDataReader ReadServers(string condition, string value = "server")
            => ExecuteReader(value, "servers", condition);
        internal static string ReadSingle(string row, string table, ulong id, string value)
        {
            var reader = ExecuteReader(value, table, $"{row} = '{id}'");
            return reader.Read() ? reader[value].ToString() : null;
        }
        internal static string ReadUser(ulong id, string value) => ReadSingle("user", "users", id, value);
        internal static string ReadChannel(ulong id, string value) => ReadSingle("channel", "flags", id, value);
        internal static string ReadServer(ulong id, string value) => ReadSingle("server", "servers", id, value);
        internal static int ReadInt(string data) => data != null ? int.Parse(data) : 0;
        internal static bool ReadBool(string data, bool ifnull = false) => data == null ? ifnull : int.Parse(data) == 1;

        // Other stuff
        internal static void CloseAndDispose()
        {
            connection.Close();
            connection.Dispose();
        }
    }
}