using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data.SQLite;

namespace RcsConverter
{
    class DbCreator
    {
        List<RCSFlow> rcsFlowList;
        Dictionary<string, SortedSet<RCSFlow>> dbStationLookup = new Dictionary<string, SortedSet<RCSFlow>>();
        RJISProcessor rjisprocessor;
        Settings settings;

        public DbCreator(List<RCSFlow> rcsFlowList, RJISProcessor rjisFlowProcessor, Settings settings)
        {
            this.rcsFlowList = rcsFlowList;
            this.rjisprocessor = rjisFlowProcessor;
            this.settings = settings;
        }

        void AddStationEntry(string key, RCSFlow rcsFlow)
        {
            if (!dbStationLookup.TryGetValue(key, out var list))
            {
                list = new SortedSet<RCSFlow>();
                dbStationLookup.Add(key, list);
            }
            list.Add(rcsFlow);

        }

        /// <summary>
        /// Build a dictionary that maps an NLC code to a list of RCS flows.
        /// We take the RCS flow list and iterate over it - for the flow we add a key to the dictionary for
        /// the origin and destination - if that key already exists we add the flow to the list:
        /// </summary>
        void BuildNLCIndex()
        {
            foreach (var flow in rcsFlowList)
            {
                var origingGroupMembers = rjisprocessor.GetGroupMembers(flow.Origin);
                var destinationGroupMembers = rjisprocessor.GetGroupMembers(flow.Destination);
                if (origingGroupMembers != null)
                {
                    foreach (var station in origingGroupMembers)
                    {
                        AddStationEntry(station, flow);
                    }
                }
                else
                {
                    AddStationEntry(flow.Origin, flow);
                }
                if (destinationGroupMembers != null)
                {
                    foreach (var station in destinationGroupMembers)
                    {
                        AddStationEntry(station, flow);
                    }
                }
                else
                {
                    AddStationEntry(flow.Destination, flow);
                }
            }

        }

        void UpdateBatch(StreamWriter w, string filename)
        {
            w.WriteLine($"@echo off");
            w.WriteLine($"@echo {filename}");
            w.WriteLine($"sqlite3 -batch {filename} <<-HERE");
            w.WriteLine($"select * from rcs_flow_route_db;");
            w.WriteLine($"select * from rcs_flow_ticket_db;");
            w.WriteLine($"HERE");
        }
        public void CreateIndividualDBs()
        {
            var startdbTime = DateTime.Now;
            Console.WriteLine("started building database.");
            BuildNLCIndex();
            Console.WriteLine("Per TOC index into flow table built.");
            var (ok, dbFolder) = settings.GetFolder("Database");
            if (!ok)
            {
                throw new Exception($"Cannot get database folder from settings file {settings.SettingsFile}");
            }

            // first for each TOC:
            foreach (var toc in settings.PerTocNlcList.Keys)
            {
                if (!settings.PerTocTicketTypeList.TryGetValue(toc, out var validTicketTypes))
                {
                    throw new Exception($"Cannot find ticket types for station set {toc}.");
                }

                Directory.CreateDirectory(Path.Combine(dbFolder, toc));
                var batchname = Path.Combine(dbFolder, toc, "sqldumper.btm");
                using (var outfile = new StreamWriter(batchname))
                {
                    foreach (var station in settings.PerTocNlcList[toc])
                    {
                        if (dbStationLookup.TryGetValue(station, out var rcsFlowlist) && rcsFlowList.Count() > 0)
                        {
                            var databaseName = Path.Combine(dbFolder, toc, $"RCSFlow-{station}.sqlite");
                            UpdateBatch(outfile, databaseName);

                            SQLiteConnection.CreateFile(databaseName);
                            var insertions = 0;
                            using (var sqliteConnection = new SQLiteConnection("Data Source=" + databaseName + ";"))
                            {
                                sqliteConnection.Open();
                                SimpleSQLOperation.Run("PRAGMA journal_mode=OFF", sqliteConnection);
                                SimpleSQLOperation.Run("CREATE TABLE IF NOT EXISTS RCS_FLOW_DB(SeqNo INTEGER PRIMARY KEY, Date date)", sqliteConnection);
                                SimpleSQLOperation.Run("CREATE TABLE IF NOT EXISTS RCS_FLOW_ROUTE_DB(pID INTEGER PRIMARY KEY, Orig VARCHAR, Dest VARCHAR, Route VARCHAR)", sqliteConnection);
                                SimpleSQLOperation.Run("CREATE TABLE IF NOT EXISTS RCS_FLOW_TICKET_DB(pID INTEGER REFERENCES RCS_FLOW_ROUTE_DB(pID) ON DELETE CASCADE, FTOT VARCHAR, DFrom date, DUntil date, FulfilMethod VARCHAR, pDate date, pRef VARCHAR, SeasonDetails VARCHAR)", sqliteConnection);

                                SimpleSQLOperation.Run($"INSERT INTO RCS_FLOW_DB(SeqNo, Date) VALUES(0, \"{DateTime.Today.ToString("yyyy-MM-dd HH:mm:ss.F")}\")", sqliteConnection);

                                var insertRoute = "INSERT INTO RCS_FLOW_ROUTE_DB(pID, orig, dest, route) VALUES(?, ?, ?, ?)";
                                var insertTicket = "INSERT INTO RCS_FLOW_TICKET_DB(pID, FTOT, DFrom, Duntil, FulfilMethod, pDate, pRef, SeasonDetails) VALUES(?, ?, ?, ?, ?, ?, ?, ?)";

                                using (var routeCmd = new SQLiteCommand(insertRoute, sqliteConnection))
                                using (var ticketCmd = new SQLiteCommand(insertTicket, sqliteConnection))
                                {
                                    var sqlParamPID = new SQLiteParameter();
                                    var sqlParamOrigin = new SQLiteParameter();
                                    var sqlParamRoute = new SQLiteParameter();
                                    var sqlParamDestination = new SQLiteParameter();

                                    routeCmd.Parameters.Add(sqlParamPID);
                                    routeCmd.Parameters.Add(sqlParamOrigin);
                                    routeCmd.Parameters.Add(sqlParamDestination);
                                    routeCmd.Parameters.Add(sqlParamRoute);

                                    var sqlParamPID2 = new SQLiteParameter();
                                    var sqlParamFTOT = new SQLiteParameter();
                                    var sqlParamDFrom = new SQLiteParameter();
                                    var sqlParamDUntil = new SQLiteParameter();
                                    var sqlParamFulfilMethod = new SQLiteParameter();
                                    var sqlParamPDate = new SQLiteParameter();
                                    var sqlParamPRef = new SQLiteParameter();
                                    var sqlParamSeasonDetails = new SQLiteParameter();

                                    ticketCmd.Parameters.Add(sqlParamPID2);
                                    ticketCmd.Parameters.Add(sqlParamFTOT);
                                    ticketCmd.Parameters.Add(sqlParamDFrom);
                                    ticketCmd.Parameters.Add(sqlParamDUntil);
                                    ticketCmd.Parameters.Add(sqlParamFulfilMethod);
                                    ticketCmd.Parameters.Add(sqlParamPDate);
                                    ticketCmd.Parameters.Add(sqlParamPRef);
                                    ticketCmd.Parameters.Add(sqlParamSeasonDetails);

                                    using (var transaction = sqliteConnection.BeginTransaction())
                                    {
                                        var pid = 0;
                                        foreach (var flow in rcsFlowlist)
                                        {
                                            var allTicketTypesValid = validTicketTypes == null || validTicketTypes.Count == 0;
                                            var anyValidTicketTypes = allTicketTypesValid || validTicketTypes.Intersect(flow.TicketList.Select(x => x.TicketCode)).Any();

                                            if (anyValidTicketTypes)
                                            {
                                                sqlParamPID.Value = pid;
                                                sqlParamRoute.Value = flow.Route;
                                                sqlParamOrigin.Value = flow.Origin;
                                                sqlParamDestination.Value = flow.Destination;
                                                routeCmd.ExecuteNonQuery();

                                                sqlParamPID2.Value = pid;

                                                foreach (var ticket in flow.TicketList)
                                                {
                                                    if (allTicketTypesValid || validTicketTypes.Contains(ticket.TicketCode))
                                                    {
                                                        sqlParamFTOT.Value = ticket.TicketCode;
                                                        foreach (var ff in ticket.FFList)
                                                        {
                                                            sqlParamDFrom.Value = ff.StartDate;
                                                            sqlParamDUntil.Value = ff.EndDate;
                                                            sqlParamSeasonDetails.Value = ff.SeasonIndicator;
                                                            sqlParamPDate.Value = ff.QuoteDate;
                                                            sqlParamPRef.Value = ff.Key;
                                                            sqlParamFulfilMethod.Value = "00001";
                                                            ticketCmd.ExecuteNonQuery();
                                                            insertions++;
                                                        }
                                                    }
                                                }
                                                pid++;
                                            }
                                        }
                                        transaction.Commit();
                                        SimpleSQLOperation.Run("CREATE INDEX ORIGDEST ON RCS_FLOW_ROUTE_DB(ORIG, DEST)", sqliteConnection);
                                        SimpleSQLOperation.Run("CREATE INDEX PID ON RCS_FLOW_TICKET_DB(pID)", sqliteConnection);
                                    }
                                }
                            } // using SQLiteConnection
                            if (insertions == 0)
                            {
                                File.Delete(databaseName);
                            }
                        } 
                    } // foreach station
                } // using batch file
            } // foreach TOC
            var dbDuration = DateTime.Now - startdbTime;
            Console.WriteLine($"Database creation: duration: {dbDuration.Minutes:D2}:{dbDuration.Seconds:D2}");
        } // end method
    }
}
