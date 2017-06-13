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
        public void CreateIndividualDBs(HashSet<string> nlcSet)
        {
            BuildNLCIndex();
            var (ok, dbFolder) = settings.GetFolder("Database");
            if (!ok)
            {
                throw new Exception($"Cannot get database folder from settings file {settings.SettingsFile}");
            }

            foreach (var station in nlcSet)
            {
                if (dbStationLookup.ContainsKey(station))
                {
                    var databaseName = Path.Combine(dbFolder, $"RCSFlow-{station}.sqlite");
                    SQLiteConnection.CreateFile(databaseName);
                    using (var sqliteConnection = new SQLiteConnection("Data Source=" + databaseName + ";"))
                    {
                        sqliteConnection.Open();
                        SimpleSQLOperation.Run("CREATE TABLE IF NOT EXISTS RCS_FLOW_DB(SeqNo INTEGER PRIMARY KEY, Date date)", sqliteConnection);
                        SimpleSQLOperation.Run("CREATE TABLE IF NOT EXISTS RCS_FLOW_ROUTE_DB(pID INTEGER PRIMARY KEY, Orig VARCHAR, Dest VARCHAR, Route VARCHAR)", sqliteConnection);
                        SimpleSQLOperation.Run("CREATE TABLE IF NOT EXISTS RCS_FLOW_TICKET_DB(pID INTEGER REFERENCES RCS_FLOW_ROUTE_DB(pID) ON DELETE CASCADE, FTOT VARCHAR, DFrom date, DUntil date, FulfilMethod VARCHAR, pDate date, pRef VARCHAR, SeasonDetails VARCHAR)", sqliteConnection);
                    }
                }
            }
        }
    }
}
