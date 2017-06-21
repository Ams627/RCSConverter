using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RcsConverter
{
    class FlowDb : IDisposable
    {
        SQLiteConnection sqliteConnection;
        SQLiteTransaction transaction;
        RouteCommand routeCommand;
        TicketCommand ticketCommand;
        int flowNumber;
        string filename;

        public FlowDb(string filename)
        {
            this.filename = filename;
            flowNumber = -1;
            SQLiteConnection.CreateFile(filename);
            sqliteConnection = new SQLiteConnection($"Data Source={filename};");
            sqliteConnection.Open();
            SimpleSQLOperation.Run("PRAGMA journal_mode=OFF", sqliteConnection);
            SimpleSQLOperation.Run("CREATE TABLE IF NOT EXISTS RCS_FLOW_DB(SeqNo INTEGER PRIMARY KEY, Date date)", sqliteConnection);
            SimpleSQLOperation.Run("CREATE TABLE IF NOT EXISTS RCS_FLOW_ROUTE_DB(pID INTEGER PRIMARY KEY, Orig VARCHAR, Dest VARCHAR, Route VARCHAR)", sqliteConnection);
            SimpleSQLOperation.Run("CREATE TABLE IF NOT EXISTS RCS_FLOW_TICKET_DB(pID INTEGER REFERENCES RCS_FLOW_ROUTE_DB(pID) ON DELETE CASCADE, FTOT VARCHAR, DFrom date, DUntil date, FulfilMethod VARCHAR, pDate date, pRef VARCHAR, SeasonDetails VARCHAR)", sqliteConnection);
            SimpleSQLOperation.Run($"INSERT INTO RCS_FLOW_DB(SeqNo, Date) VALUES(0, \"{DateTime.Today.ToString("yyyy-MM-dd HH:mm:ss.F")}\")", sqliteConnection);

            routeCommand = new RouteCommand(sqliteConnection);
            ticketCommand = new TicketCommand(sqliteConnection);
            transaction = sqliteConnection.BeginTransaction();
        }

        public void Dispose()
        {
            SimpleSQLOperation.Run("CREATE INDEX ORIGDEST ON RCS_FLOW_ROUTE_DB(ORIG, DEST)", sqliteConnection);
            SimpleSQLOperation.Run("CREATE INDEX PID ON RCS_FLOW_TICKET_DB(pID)", sqliteConnection);
            transaction.Commit();
            ticketCommand.Dispose();
            routeCommand.Dispose();
            sqliteConnection.Dispose();
        }

        public void AddRoute(string route, string origin, string destination)
        {
            flowNumber++;
            routeCommand.AddRoute(flowNumber, route, origin, destination);
        }

        public void AddTicket(string ticketCode, string startDate, string endDate, string seasonIndicator, string quoteDate, string key)
        {
            ticketCommand.AddTicket(flowNumber, ticketCode, startDate, endDate, seasonIndicator, quoteDate, key);
        }


    }
}
