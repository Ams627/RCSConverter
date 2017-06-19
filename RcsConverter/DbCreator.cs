using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Data.SQLite;
using System.Xml.Linq;

namespace RcsConverter
{
    class DbCreator
    {
        Dictionary<string, SortedSet<RCSFlow>> dbStationLookup = new Dictionary<string, SortedSet<RCSFlow>>();
        readonly List<RCSFlow> rcsFlowList;
        readonly RJISProcessor rjisprocessor;
        readonly Settings settings;

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

        void PrepareDatabase(SQLiteConnection sqliteConnection)
        {
            sqliteConnection.Open();
            SimpleSQLOperation.Run("PRAGMA journal_mode=OFF", sqliteConnection);
            SimpleSQLOperation.Run("CREATE TABLE IF NOT EXISTS RCS_FLOW_DB(SeqNo INTEGER PRIMARY KEY, Date date)", sqliteConnection);
            SimpleSQLOperation.Run("CREATE TABLE IF NOT EXISTS RCS_FLOW_ROUTE_DB(pID INTEGER PRIMARY KEY, Orig VARCHAR, Dest VARCHAR, Route VARCHAR)", sqliteConnection);
            SimpleSQLOperation.Run("CREATE TABLE IF NOT EXISTS RCS_FLOW_TICKET_DB(pID INTEGER REFERENCES RCS_FLOW_ROUTE_DB(pID) ON DELETE CASCADE, FTOT VARCHAR, DFrom date, DUntil date, FulfilMethod VARCHAR, pDate date, pRef VARCHAR, SeasonDetails VARCHAR)", sqliteConnection);

            SimpleSQLOperation.Run($"INSERT INTO RCS_FLOW_DB(SeqNo, Date) VALUES(0, \"{DateTime.Today.ToString("yyyy-MM-dd HH:mm:ss.F")}\")", sqliteConnection);
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

            List<string> tocsToProduce = null;
            switch (settings.SetOption)
            {
                case Settings.SetOptions.AllStations:
                    tocsToProduce = new List<string> { "all" };
                    break;
                case Settings.SetOptions.AllSetsInSettingsFile:
                    tocsToProduce = new List<string>(settings.PerTocNlcList.Keys);
                    break;
                case Settings.SetOptions.SpecificSets:
                    tocsToProduce = new List<string>(settings.SetsToProduce);
                    break;
            }

            // first for each TOC:
            foreach (var toc in tocsToProduce)
            {
                settings.PerTocTicketTypeList.TryGetValue(toc, out var validTicketTypes);

                Directory.CreateDirectory(Path.Combine(dbFolder, toc));
                var batchname = Path.Combine(dbFolder, toc, "sqldumper.bat");
                using (var outfile = new StreamWriter(batchname))
                {
                    var stationList = toc == "all" ? dbStationLookup.Keys.ToList() : settings.PerTocNlcList[toc].ToList();
                    var stationsCompleted = 0;
                    foreach (var station in stationList)
                    {
                        Console.Write($"{toc}: done {stationsCompleted} stations from {stationList.Count()}\r");
                        if (dbStationLookup.TryGetValue(station, out var singleStationRCSFlowList) && rcsFlowList.Count() > 0)
                        {
                            var xmlOutputName = Path.Combine(dbFolder, toc, $"RCSFlow-{station}.xml");
                            var outputDoc = new XDocument(new XElement("ParkeonRCSFlow",
                                from flow in singleStationRCSFlowList
                                select new XElement("F",
                                   new XAttribute("r", flow.Route),
                                   new XAttribute("o", flow.Origin),
                                   new XAttribute("d", flow.Destination),

                                   from ticket in flow.TicketList
                                   select
                                 new XElement("T", new XAttribute("t", ticket.TicketCode),
                                 from ff in ticket.FFList
                                 select
                                     new XElement("FF",
                                         ff.EndDate == null ? null : new XAttribute("u", ff.EndDate),
                                         ff.StartDate == null ? null : new XAttribute("f", ff.StartDate),
                                         ff.SeasonIndicator == null ? null : new XAttribute("s", ff.SeasonIndicator),
                                         ff.QuoteDate == null ? null : new XAttribute("p", ff.QuoteDate),
                                         ff.Key == null ? null : new XAttribute("k", ff.Key),
                                         new XAttribute("fm", "00001")
                                     )))));

                            outputDoc.Save(xmlOutputName);

                            if (settings.Sqlite)
                            {
                                var databaseName = Path.Combine(dbFolder, toc, $"RCSFlow-{station}.sqlite");
                                UpdateBatch(outfile, databaseName);
                                SQLiteConnection.CreateFile(databaseName);
                                var insertions = 0;

                                using (var sqliteConnection = new SQLiteConnection("Data Source=" + databaseName + ";"))
                                using (var routeCmd = new RouteCommand(sqliteConnection))
                                using (var ticketCmd = new TicketCommand(sqliteConnection))
                                {
                                    PrepareDatabase(sqliteConnection);
                                    using (var transaction = sqliteConnection.BeginTransaction())
                                    {
                                        var pid = 0;
                                        foreach (var flow in singleStationRCSFlowList)
                                        {
                                            var allTicketTypesValid = validTicketTypes == null || validTicketTypes.Count == 0;
                                            var anyValidTicketTypes = allTicketTypesValid || validTicketTypes.Intersect(flow.TicketList.Select(x => x.TicketCode)).Any();

                                            if (anyValidTicketTypes)
                                            {
                                                routeCmd.AddRoute(pid, flow.Route, flow.Origin, flow.Destination);

                                                foreach (var ticket in flow.TicketList)
                                                {
                                                    if (allTicketTypesValid || validTicketTypes.Contains(ticket.TicketCode))
                                                    {
                                                        foreach (var ff in ticket.FFList)
                                                        {
                                                            ticketCmd.AddTicket(pid, ticket.TicketCode, ff.StartDate, ff.EndDate, ff.SeasonIndicator, ff.QuoteDate, ff.Key);
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
                                if (insertions == 0)
                                {
                                    File.Delete(databaseName);
                                }
                            }
                        }
                        stationsCompleted++;
                    } // foreach station
                } // using batch file
            } // foreach TOC
            var dbDuration = DateTime.Now - startdbTime;
            Console.WriteLine($"Database creation: duration: {dbDuration.Minutes:D2}:{dbDuration.Seconds:D2}");
        } // end method
    }
}
