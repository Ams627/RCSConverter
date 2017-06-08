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
            var (ok, rcsFlowFolder) = settings.GetFolder("RCSFlow");
            if (!ok)
            {
                throw new Exception($"You must define the RCSFlow folder in the settings file {settings.SettingsFile}");
            }
            var rcsfiles = Directory.GetFiles(rcsFlowFolder, "RCS_R_F*").ToList();
            // get a more precise match as Directory.GetFiles does not allow use of regex - also remove 
            // files that are not .zip or .xml:
            rcsfiles.RemoveAll(s => !Regex.Match(s, @"RCS_R_F_\d{6}_\d{5}.xml$|RCS_R_F_\d{6}_\d{5}.zip$").Success);

            // comparison Func just for RCS files:
            Func<string, string, int> RCSComparer = (s1, s2) =>
            {
                var name1 = Path.GetFileNameWithoutExtension(s1);
                var serial1 = name1.Substring(name1.Length - 5);
                var n1 = Convert.ToInt32(serial1);
                var name2 = Path.GetFileNameWithoutExtension(s1);
                var serial2 = name2.Substring(name1.Length - 5);
                var n2 = Convert.ToInt32(serial2);
                return n1.CompareTo(n2);
            };

            rcsfiles.Sort(new Comparison<string>(RCSComparer));

            if (rcsfiles.Count() == 0)
            {
                throw new Exception($"No RCS flow files (zip or xml) found in the folder {rcsFlowFolder}");
            }
            return rcsfiles.Last();
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
