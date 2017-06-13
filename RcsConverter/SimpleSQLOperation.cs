using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data.SQLite;

namespace RcsConverter
{
    static class SimpleSQLOperation
    {
        // run a simple non-query on the specified database
        public static void Run(string sqlstring, SQLiteConnection db)
        {
            SQLiteCommand command = new SQLiteCommand(sqlstring, db);

            command.ExecuteNonQuery();
            // dispose the sqlitecommand or else we will not get the file handle back
            // when we close the database
            command.Dispose();
        }

        public static void SimpleInsert(SQLiteConnection db, string tablename, string fieldname1, string fieldname2, Object value1, Object value2)
        {
            string seqnoInsertString = "INSERT INTO " + tablename + "(" + fieldname1 + ", " + fieldname2 + ")" + "VALUES(?, ?)";
            SQLiteParameter q1 = new SQLiteParameter();
            SQLiteParameter q2 = new SQLiteParameter();
            using (SQLiteCommand seqnoInsertCommand = new SQLiteCommand(seqnoInsertString, db))
            {
                seqnoInsertCommand.Parameters.Add(q1);
                seqnoInsertCommand.Parameters.Add(q2);
                q1.Value = value1;
                q2.Value = value2;
                seqnoInsertCommand.ExecuteNonQuery();
            }
        }
    }
}
