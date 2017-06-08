using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data.SQLite;

namespace RcsConverter
{
    class FlowDB
    {
        SQLiteConnection singleFlowDB;

        /// <summary>
        /// Cache of individual NLC dbs to write:
        /// </summary>
        LRUCache<string, SQLiteConnection> dbConnections = new LRUCache<string, SQLiteConnection>(50);
        public FlowDB()
        {
            SQLiteConnection.CreateFile("RCSFlow.sqlite");
        }
    }
}
