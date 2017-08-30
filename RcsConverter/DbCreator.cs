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
        /// <summary>
        /// The application settings:
        /// </summary>
        readonly Settings settings;

        /// <summary>
        /// master RCS flow list. This is a list of all elements in the most recent flow refresh file with all subsequent flow update files applied
        /// </summary>
        readonly List<RCSFlow> masterRcsFlowList;

        /// <summary>
        /// given an NLC code, get a set of RCS flows from the master RCS flow list:
        /// </summary>
        Dictionary<string, SortedSet<RCSFlow>> nlcToRcsFlowSet = new Dictionary<string, SortedSet<RCSFlow>>();

        readonly RJISProcessor rjisprocessor;

        /// <summary>
        /// Initialise an instance of the DbCreator class.
        /// </summary>
        /// <param name="rcsFlowList">The master list of RCS flows (representing F-elements and all their descendants in the original XML file)</param>
        /// <param name="rjisFlowProcessor">RJIS information - we use this to access the group statoin associations</param>
        /// <param name="settings">The application settings</param>
        public DbCreator(List<RCSFlow> rcsFlowList, RJISProcessor rjisFlowProcessor, Settings settings)
        {
            this.masterRcsFlowList = rcsFlowList;
            this.rjisprocessor = rjisFlowProcessor;
            this.settings = settings;
        }

        void AddStationEntry(string key, RCSFlow rcsFlow)
        {
            if (!nlcToRcsFlowSet.TryGetValue(key, out var list))
            {
                list = new SortedSet<RCSFlow>();
                nlcToRcsFlowSet.Add(key, list);
            }
            list.Add(rcsFlow);
        }

        /// <summary>
        /// Build a dictionary that maps an NLC code to a list of RCS flows in the master RCS flow list.
        /// We take the RCS flow list and iterate over it - for the flow we add a key to the dictionary for
        /// the origin and destination - if that key already exists we add the flow to the list:
        /// </summary>
        void BuildNLCIndex()
        {
            foreach (var flow in masterRcsFlowList)
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

        void UpdateBatchFile(StreamWriter w, string filename)
        {
            w.WriteLine($"@echo off");
            w.WriteLine($"@echo {filename}");
            w.WriteLine($"sqlite3 -batch {filename} <<-HERE");
            w.WriteLine($"select * from rcs_flow_route_db;");
            w.WriteLine($"select * from rcs_flow_ticket_db;");
            w.WriteLine($"HERE");
        }

        /// <summary>
        /// Open a database that already has an established SQLiteConnection. Turn journal mode off and then create
        /// the three tables necessary. Insert the date into the RCS_FLOW_DB table.
        /// </summary>
        /// <param name="sqliteConnection"></param>
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
                HashSet<string> validTicketTypes;
                if (toc == "all")
                {
                    validTicketTypes = settings.GlobalTicketTypes;
                }
                else
                {
                    settings.PerTocTicketTypeList.TryGetValue(toc, out validTicketTypes);
                }

                Directory.CreateDirectory(Path.Combine(dbFolder, toc));
                var batchname = Path.Combine(dbFolder, toc, "sqldumper.bat");
                
                using (var outfile = new StreamWriter(batchname))
                {
                    var stationList = toc == "all" ? nlcToRcsFlowSet.Keys.ToList() : settings.PerTocNlcList[toc].ToList();

                    FlowDb bigDatabase = null;

                    if (settings.Sqlite && settings.SqliteAll)
                    {
                        string bigDatabaseName = Path.Combine(dbFolder, toc, $"RCSFlow-all.sqlite");
                        bigDatabase = new FlowDb(bigDatabaseName);
                    }

                    var stationsCompleted = 0;
                    foreach (var station in stationList)
                    {
                        Console.Write($"{toc}: done {stationsCompleted} stations from {stationList.Count()}\r");
                        if (nlcToRcsFlowSet.TryGetValue(station, out var singleStationRCSFlowList) && singleStationRCSFlowList.Count() > 0)
                        {
                            // at this point we have found some RCS flows for this station, so generate a station XML file:
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

                            // if settings say we should produce an Sqlite file then do it!:
                            if (settings.Sqlite)
                            {
                                var databaseName = Path.Combine(dbFolder, toc, $"RCSFlow-{station}.sqlite");
                                UpdateBatchFile(outfile, databaseName);
                                var insertions = 0;

                                using (var flowDb = new FlowDb(databaseName))
                                {
                                    foreach (var flow in singleStationRCSFlowList)
                                    {
                                        var allTicketTypesValid = validTicketTypes == null || validTicketTypes.Count == 0;
                                        var anyValidTicketTypes = allTicketTypesValid || validTicketTypes.Intersect(flow.TicketList.Select(x => x.TicketCode)).Any();

                                        if (anyValidTicketTypes)
                                        {
                                            bigDatabase?.AddRoute(flow.Route, flow.Origin, flow.Destination);
                                            flowDb.AddRoute(flow.Route, flow.Origin, flow.Destination);

                                            foreach (var ticket in flow.TicketList)
                                            {
                                                if (allTicketTypesValid || validTicketTypes.Contains(ticket.TicketCode))
                                                {
                                                    foreach (var ff in ticket.FFList)
                                                    {
                                                        bigDatabase?.AddTicket(ticket.TicketCode, ff.StartDate, ff.EndDate, ff.SeasonIndicator, ff.QuoteDate, ff.Key);
                                                        flowDb.AddTicket(ticket.TicketCode, ff.StartDate, ff.EndDate, ff.SeasonIndicator, ff.QuoteDate, ff.Key);
                                                        insertions++;
                                                    }
                                                }
                                            }
                                        }
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
                    bigDatabase?.Dispose();
                } // using batch file

                var makezipName = Path.Combine(dbFolder, toc, "zipper.bat");
                using (var zipbatchfile = new StreamWriter(makezipName))
                {
                    zipbatchfile.WriteLine($"@echo off");
                    zipbatchfile.WriteLine($"del xmlfiles.zip >&nul");
                    zipbatchfile.WriteLine($"del sqlitefiles.zip >&nul");
                    zipbatchfile.WriteLine($"pkzip -add -silent r-xmlfiles.zip *.xml");
                    zipbatchfile.WriteLine($"pkzip -add -silent r-sqlitefiles.zip *.sqlite");
                }

            } // foreach TOC
            var dbDuration = DateTime.Now - startdbTime;
            Console.WriteLine($"Database creation: duration: {dbDuration.Minutes:D2}:{dbDuration.Seconds:D2}");
        } // end method
    }
}
