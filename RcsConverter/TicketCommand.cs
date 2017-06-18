using System;
using System.Data.SQLite;

namespace RcsConverter
{
    class TicketCommand : IDisposable
    {
        SQLiteCommand cmdTicket;
        SQLiteConnection db;
        SQLiteParameter sqlParamPid;
        SQLiteParameter sqlParamFTOT;
        SQLiteParameter sqlParamDFrom;
        SQLiteParameter sqlParamDUntil;
        SQLiteParameter sqlParamFulfilMethod;
        SQLiteParameter sqlParamPDate;
        SQLiteParameter sqlParamPRef;
        SQLiteParameter sqlParamSeasonDetails;

        public TicketCommand(SQLiteConnection db)
        {
            this.db = db;
            sqlParamPid = new SQLiteParameter();
            sqlParamFTOT = new SQLiteParameter();
            sqlParamDFrom = new SQLiteParameter();
            sqlParamDUntil = new SQLiteParameter();
            sqlParamFulfilMethod = new SQLiteParameter();
            sqlParamPDate = new SQLiteParameter();
            sqlParamPRef = new SQLiteParameter();
            sqlParamSeasonDetails = new SQLiteParameter();

            var insertTicket = "INSERT INTO RCS_FLOW_TICKET_DB(pID, FTOT, DFrom, Duntil, FulfilMethod, pDate, pRef, SeasonDetails) VALUES(?, ?, ?, ?, ?, ?, ?, ?)";
            cmdTicket = new SQLiteCommand(insertTicket, db);
            cmdTicket.Parameters.Add(sqlParamPid);
            cmdTicket.Parameters.Add(sqlParamFTOT);
            cmdTicket.Parameters.Add(sqlParamDFrom);
            cmdTicket.Parameters.Add(sqlParamDUntil);
            cmdTicket.Parameters.Add(sqlParamFulfilMethod);
            cmdTicket.Parameters.Add(sqlParamPDate);
            cmdTicket.Parameters.Add(sqlParamPRef);
            cmdTicket.Parameters.Add(sqlParamSeasonDetails);
        }

        public void AddTicket(int pid, string ticketCode, string startDate, string endDate, string season, string quoteDate, string key)
        {
            sqlParamPid.Value = pid;
            sqlParamFTOT.Value = ticketCode;
            sqlParamDFrom.Value = startDate;
            sqlParamDUntil.Value = endDate;
            sqlParamSeasonDetails.Value = season;
            sqlParamPDate.Value = quoteDate;
            sqlParamPRef.Value = key;
            sqlParamFulfilMethod.Value = "00001";
            cmdTicket.ExecuteNonQuery();
        }

        public void Dispose()
        {
            cmdTicket.Dispose();
        }
    }
}
