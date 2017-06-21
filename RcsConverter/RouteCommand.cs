using System;
using System.Data.SQLite;

namespace RcsConverter
{
    class RouteCommand : IDisposable
    {
        SQLiteCommand cmdRoute;
        SQLiteConnection db;
        SQLiteParameter flowIdParam, routeParam, origParam, destParam;


        public RouteCommand(SQLiteConnection db)
        {
            this.db = db;
            flowIdParam = new SQLiteParameter();
            routeParam = new SQLiteParameter();
            origParam = new SQLiteParameter();
            destParam = new SQLiteParameter();

            var insertRoute = "INSERT INTO RCS_FLOW_ROUTE_DB(pID, orig, dest, route) VALUES(?, ?, ?, ?)";
            cmdRoute = new SQLiteCommand(insertRoute, db);
            cmdRoute.Parameters.Add(flowIdParam);
            cmdRoute.Parameters.Add(origParam);
            cmdRoute.Parameters.Add(destParam);
            cmdRoute.Parameters.Add(routeParam);
        }

        public void AddRoute(int flowid, string route, string orig, string dest)
        {
            flowIdParam.Value = flowid;
            routeParam.Value = route;
            origParam.Value = orig;
            destParam.Value = dest;
            cmdRoute.ExecuteNonQuery();
        }

        public void Dispose()
        {
            cmdRoute.Dispose();
        }
    }
}
