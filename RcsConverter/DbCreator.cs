using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RcsConverter
{
    class DbCreator
    {
        List<RCSFlow> rcsFlowList;
        Dictionary<string, List<RCSFlow>> dbStationLookup = new Dictionary<string, List<RCSFlow>>();
        RJISProcessor rjisprocessor;

        public DbCreator(List<RCSFlow> rcsFlowList, RJISProcessor rjisFlowProcessor)
        {
            this.rcsFlowList = rcsFlowList;
            this.rjisprocessor = rjisFlowProcessor;
        }

        void AddStationEntry(string key, RCSFlow rcsFlow)
        {
            if (!dbStationLookup.TryGetValue(key, out var list))
            {
                list = new List<RCSFlow>();
                dbStationLookup.Add(key, list);
            }
            list.Add(rcsFlow);

        }

        public void CreateIndividualDBS()
        {
            // build index
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
    }
}
