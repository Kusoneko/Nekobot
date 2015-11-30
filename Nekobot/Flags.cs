using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Threading.Tasks;
using Discord;

namespace Nekobot
{
    class Flags
    {
        public static bool GetIgnoredFlag(Channel chan, User user)
        {
            return GetIgnoredFlag(chan) || GetIgnoredFlag(user);
        }

        public static bool GetIgnoredFlag(User user)
        {
            return GetIgnoredFlag("user", "users", user.Id);
        }

        public static bool GetIgnoredFlag(Channel chan)
        {
            return GetIgnoredFlag("channel", "flags", chan.Id);
        }

        public static bool GetIgnoredFlag(string row, string table, long id)
        {
            SQLiteDataReader reader = Program.ExecuteReader($"select ignored from {table} where {row} = '{id}'");
            while (reader.Read())
                if (int.Parse(reader["ignored"].ToString()) == 1)
                    return true;
            return false;
        }

        public static async Task SetIgnoredFlag(string row, string table, long id, string insertdata, char symbol, string reply, Action<string> setreply)
        {
            bool in_table = Program.ExecuteScalarPos($"select count({row}) from {table} where {row}='{id}'");
            bool isIgnored = in_table && GetIgnoredFlag(row, table, id);
            await Program.ExecuteNonQueryAsync(in_table
                ? $"update {table} set ignored={Convert.ToInt32(!isIgnored)} where {row}='{id}'"
                : $"insert into {table} values ('{id}'{insertdata})");
            if (reply != "")
                reply += '\n';
            reply += $"<{symbol}{id}> is " + (isIgnored ? "now" : "no longer") + " ignored.";
            setreply(reply);
        }

        public static bool GetMusicFlag(User user)
        {
            SQLiteDataReader reader = Program.ExecuteReader("select channel from flags where music = 1");
            List<long> streams = new List<long>();
            while (reader.Read())
                streams.Add(Convert.ToInt64(reader["channel"].ToString()));
            return user.VoiceChannel != null && streams.Contains(user.VoiceChannel.Id);
        }

        public static bool GetNsfwFlag(Channel chan)
        {
            SQLiteDataReader reader = Program.ExecuteReader("select nsfw from flags where channel = '" + chan.Id + "'");
            while (reader.Read())
                if (int.Parse(reader["nsfw"].ToString()) == 1)
                    return true;
            return false;
        }
    }
}
