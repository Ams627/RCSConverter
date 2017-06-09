using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml;

namespace RcsConverter
{
    class RCSFlowProcessor
    {
        private readonly Settings settings;
        private List<RCSFlow> rcsFlowList;

        public RCSFlowProcessor(Settings settings)
        {
            this.settings = settings;
        }

        /// <summary>
        /// Get the latest RCS filename by serial number only - we don't care if it is zip or 
        /// </summary>
        /// <returns></returns>
        private string GetRCSFlowFilename()
        {
        }

        public void ProcessXMLFile(string filename)
        {
            rcsFlowList = new List<RCSFlow>();
            var rcsflow = new RCSFlow();
            var rcsticket = new RCSTicket();
            var rcsff = new RCSFF();

            using (var reader = XmlReader.Create(filename))
            {
                while (reader.Read())
                {
                    if (reader.NodeType == XmlNodeType.Element)
                    {
                        if (reader.Name == "F" )
                        {
                            while (reader.MoveToNextAttribute())
                            {
                                if (reader.Name == "r")
                                {
                                    rcsflow.Route = reader.Value;
                                }
                                else if (reader.Name == "o")
                                {
                                    rcsflow.Origin = reader.Value;
                                }
                                else if (reader.Name == "d")
                                {
                                    rcsflow.Destination = reader.Value;
                                }
                                else if (reader.Name != "i")
                                {
                                    throw new Exception("Invalid attribute in F element");
                                }
                            }
                        }
                        else if (reader.Name == "T")
                        {
                            while (reader.MoveToNextAttribute())
                            {
                                if (reader.Name == "t")
                                {
                                    rcsticket.TicketCode = reader.Value;
                                }
                                else
                                {
                                    throw new Exception("Invalid attribute in T element");
                                }
                            }
                        }
                        else if (reader.Name == "FF")
                        {
                            bool isItso = false;
                            DateTime tempDateTime;
                            rcsff.QuoteDate = null;
                            rcsff.SeasonIndicator = null;
                            rcsff.Key = null;

                            while (reader.MoveToNextAttribute())
                            {
                                if (reader.Name == "u")
                                {
                                    DateTime.TryParseExact("20" + reader.Value,
                                        "yyyyMMdd",
                                        System.Globalization.CultureInfo.InvariantCulture,
                                        System.Globalization.DateTimeStyles.None, out tempDateTime);
                                    rcsff.EndDate = tempDateTime;
                                }
                                else if (reader.Name == "f")
                                {
                                    DateTime.TryParseExact("20" + reader.Value,
                                        "yyyyMMdd",
                                        System.Globalization.CultureInfo.InvariantCulture,
                                        System.Globalization.DateTimeStyles.None, out tempDateTime);
                                    rcsff.StartDate = tempDateTime;
                                }
                                else if (reader.Name == "p")
                                {
                                    DateTime.TryParseExact("20" + reader.Value,
                                        "yyyyMMdd",
                                        System.Globalization.CultureInfo.InvariantCulture,
                                        System.Globalization.DateTimeStyles.None, out tempDateTime);
                                    rcsff.QuoteDate = tempDateTime;
                                }
                                else if (reader.Name == "k")
                                {
                                    rcsff.Key = reader.Value;
                                }
                                else if (reader.Name == "s")
                                {
                                    rcsff.SeasonIndicator = reader.Value;
                                }
                                else if (reader.Name == "fm")
                                {
                                    if (reader.Value == "00001")
                                    {
                                        isItso = true;
                                    }
                                }
                                else
                                {
                                    throw new Exception("Invalid attribute in FF element");
                                }
                            }
                            // if we found a fulfillment method of "00001" and the key is present then this is ITSO:
                            if (isItso && !string.IsNullOrWhiteSpace(rcsff.Key))
                            {
                                rcsticket.FFList.Add(rcsff);
                                rcsff = new RCSFF();
                            }
                        }
                    }
                    else if (reader.NodeType == XmlNodeType.EndElement)
                    {
                        if (reader.Name == "T")
                        {
                            if (rcsticket.FFList.Count > 0)
                            {
                                rcsflow.TicketList.Add(rcsticket);
                                rcsticket = new RCSTicket();
                            }
                        }
                        else if (reader.Name == "F")
                        {
                            if (rcsflow.TicketList.Count > 0)
                            {
                                rcsFlowList.Add(rcsflow);
                                rcsflow = new RCSFlow();
                                if (rcsFlowList.Count() % 1000 == 999)
                                {
                                    Console.WriteLine($"Record number {rcsFlowList.Count() + 1:n0}");
                                }
                            }
                                                    }
                    }
                }
            }
            var totaltickets = 0;
            foreach (var f in rcsFlowList)
            {
                foreach (var t in f.TicketList)
                {
                    totaltickets += t.FFList.Count();
                }
            }
            Console.WriteLine($"total tickets are {totaltickets}");
            var count = rcsFlowList.Where(x => (x.Origin == "9419" || x.Destination == "9419")).Count();

        }

        public void Process()
        {
            var rcsFlowFile = GetRCSFlowFilename();

            // extract the file from the zip if a zip is found in the RCSFlow folder:
            if (rcsFlowFile.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            {
                var tempfolder = Path.GetTempPath();
                var progname = settings.ProductName;
                tempfolder = Path.Combine(tempfolder, progname, DateTime.Now.ToFileTime().ToString("X16"));
                ZipFile.ExtractToDirectory(rcsFlowFile, tempfolder);
                var rcsFilenameComponent = Path.GetFileName(rcsFlowFile);

                // set new path:
                rcsFlowFile = Path.Combine(tempfolder, Path.GetFileNameWithoutExtension(rcsFlowFile) + ".xml");
            }

            ProcessXMLFile(rcsFlowFile);
        }
    }
}
