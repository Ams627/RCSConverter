using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Xml;

namespace RcsConverter
{
    class RCSFlowProcessor
    {
        private readonly Settings settings;
        private readonly string filename;

        public List<RCSFlow> RcsFlowList { get; private set;}
        public Lookup<string, RCSFlow> RcsFlowLookup { get; private set; }

        public Lookup<string, RCSFlow> DatabaseLookup { get; private set; }

        public RCSFlowProcessor(Settings settings, string filename)
        {
            this.settings = settings;
            this.filename = filename;
        }

        private void ProcessXMLFile(string filename)
        {
            RcsFlowList = new List<RCSFlow>();
            var rcsflow = new RCSFlow();
            var rcsticket = new RCSTicket();
            var rcsff = new RCSFF();
            var numberOfDeletions = 0;
            var numberOfInsertions = 0;
            var numberOfAmends = 0;
            var lineNumber = 0;

            using (var reader = XmlReader.Create(filename))
            {
                while (reader.Read())
                {
                    if (reader.NodeType == XmlNodeType.Whitespace && reader.Value == "\n")
                    {
                        lineNumber++;
                    }
                    else if (reader.NodeType == XmlNodeType.Element)
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
                                else if (reader.Name == "i")
                                {
                                    rcsflow.SetRecordType(reader.Value);
                                    if (rcsflow.recordType == RCSFRecordType.Amend)
                                    {
                                        numberOfAmends++;
                                    }
                                    else if (rcsflow.recordType == RCSFRecordType.Delete)
                                    {
                                        numberOfDeletions++;
                                    }
                                    else if (rcsflow.recordType == RCSFRecordType.Insert)
                                    {
                                        numberOfInsertions++;
                                    }
                                }
                                else 
                                {
                                    throw new Exception("Invalid attribute in F element");
                                }
                            }
                            reader.MoveToElement();
                            if (reader.IsEmptyElement)
                            {
                                if (rcsflow.recordType != RCSFRecordType.Delete)
                                {
                                    throw new Exception("An empty F-Record is only permitted for deletions (i=\"D\").");
                                }
                                RcsFlowList.Add(rcsflow);
                                rcsflow = new RCSFlow();
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
                            rcsff.QuoteDate = null;
                            rcsff.SeasonIndicator = null;
                            rcsff.Key = null;

                            while (reader.MoveToNextAttribute())
                            {
                                if (reader.Name == "u")
                                {
                                    rcsff.EndDate = RCSParseUtils.CheckDate(reader.Value); 
                                }
                                else if (reader.Name == "f")
                                {
                                    rcsff.StartDate = RCSParseUtils.CheckDate(reader.Value);
                                }
                                else if (reader.Name == "p")
                                {
                                    rcsff.QuoteDate = RCSParseUtils.CheckDate(reader.Value);
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
                                rcsticket.FFList.Sort((ff1, ff2) => ff1.SortKey.CompareTo(ff2.SortKey));
                                rcsflow.TicketList.Add(rcsticket);
                                rcsticket = new RCSTicket();
                            }
                        }
                        else if (reader.Name == "F")
                        {
                            if (rcsflow.recordType == RCSFRecordType.Delete)
                            {
                                throw new Exception("F records must be empty when specifying a deletion");
                            }
                            if (rcsflow.TicketList.Count > 0)
                            {
                                rcsflow.TicketList.Sort((t1, t2)=>t1.TicketCode.CompareTo(t2.TicketCode));
                                RcsFlowList.Add(rcsflow);
                                rcsflow = new RCSFlow();
                                if (RcsFlowList.Count() % 10000 == 9999)
                                {
                                    Console.WriteLine($"Filename {filename} - record number {RcsFlowList.Count() + 1:n0}");
                                }
                            }
                        }
                    }
                }
            }
            var totaltickets = 0;
            foreach (var f in RcsFlowList)
            {
                foreach (var t in f.TicketList)
                {
                    totaltickets += t.FFList.Count();
                }
            }
            Console.WriteLine($"total tickets are {totaltickets}");
            Console.WriteLine($"total number amends for {filename} is {numberOfAmends}");
            Console.WriteLine($"total number deletions for {filename} is {numberOfDeletions}");
            Console.WriteLine($"total number inserts for {filename} is {numberOfInsertions}");
        }

        public void Process()
        {
            var fileToProcess = filename;
            // extract the file from the zip if a zip is found in the RCSFlow folder:
            if (filename.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            {
                var tempfolder = Path.GetTempPath();
                var progname = Settings.ProductName;
                tempfolder = Path.Combine(tempfolder, progname, "RCSunzip");
                if (!Directory.Exists(tempfolder))
                {
                    Directory.CreateDirectory(tempfolder);
                }
                using (var archive = ZipFile.OpenRead(filename))
                {
                    if (archive.Entries.Count != 1)
                    {
                        throw new Exception("There must be a single entry in the zip file {filename}.");
                    }
                    var zipEntry = archive.Entries.First();
                    if (!Path.GetFileNameWithoutExtension(zipEntry.FullName).Equals(Path.GetFileNameWithoutExtension(filename), StringComparison.OrdinalIgnoreCase))
                    {
                        throw new Exception($"The file in the zip archive {filename} does not match the name of the archive");
                    }
                    zipEntry.ExtractToFile(Path.Combine(tempfolder, zipEntry.FullName), true);
                }

                // set new path if we have unzipped a file:
                fileToProcess = Path.Combine(tempfolder, Path.GetFileNameWithoutExtension(filename) + ".xml");
            }

            ProcessXMLFile(fileToProcess);
            ReIndex();
        }


        public void ReIndex()
        {
            RcsFlowLookup = (Lookup<string, RCSFlow>)RcsFlowList.ToLookup(x => x.LookupKey, x => x);
        }
    }
}
