using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Reflection;
using System.Diagnostics;
using System.IO;
using System.Xml.Linq;
using System.Xml;

namespace RcsConverter
{
    class Settings
    {
        /// <summary>
        /// Path for settings.xml - normally %appdata%/Parkeon/RcsConverter
        /// </summary>
        public string SettingsFile { get; private set; }
        public string ProductName { get; private set; }

        /// <summary>
        /// a dictionary of folder types - keys are any of the following: RJIS, RCSFLOW, TEMP, IDMS - each
        /// of these come from settings .xml under the Folders key. Each folder is specified with a Folder element with
        /// attributes Name and Location.
        /// </summary>
        private Dictionary<string, string> folders = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public string CurrentTocName { get; set; }
        public HashSet<string> TicketTypes { get; set; }
        public Dictionary<string, HashSet<string>> PerTocNlcList { get; private set; }
        public Dictionary<string, HashSet<string>> PerTocTicketTypeList { get; private set; }
        public List<string> Warnings { get; private set; } = new List<string>();


        public Settings()
        {
            var versionInfo = FileVersionInfo.GetVersionInfo(Assembly.GetEntryAssembly().Location);
            var companyName = versionInfo.CompanyName;
            var productName = versionInfo.ProductName;

            // store for later access by other program areas:
            ProductName = productName;

            var appdataFolder = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var programFolder = Path.Combine(appdataFolder, companyName, productName);
            if (File.Exists(programFolder))
            {
                throw new Exception($"File {programFolder} exists but this program requires that path as a folder. It must not be an existing file.");
            }
            // create the program folder if it does not exist. We should never need to do this but we will do
            // it as an emergency procedure:
            Directory.CreateDirectory(programFolder);
            SettingsFile = Path.Combine(programFolder, "settings.xml");

            // LoadOptions.SetLineInfo sets the line number info for the settings file which is used for error reporting:
            var doc = XDocument.Load(SettingsFile, LoadOptions.SetLineInfo);
            folders = doc.Descendants("Folders").Elements("Folder").Select(folder => new
            {
                Name = (string)folder.Attribute("Name"),
                Location = (string)folder.Attribute("Location")
            }).ToDictionary(x => x.Name, x => x.Location);

            foreach (var folder in folders)
            {
                if (folder.Value == null)
                {
                    throw new Exception($"The location specified for {folder.Key} must not be empty.");
                }
            }

            TicketTypes = new HashSet<string>(doc.Descendants("TicketTypes").Elements("TicketType").Select(ticketType =>
                (string)ticketType.Attribute("Name")));

            foreach (var ticket in TicketTypes)
            {
                if (string.IsNullOrWhiteSpace(ticket) || ticket.Length != 3 || ticket.Any(c=>!char.IsUpper(c) && !char.IsDigit(c)))
                {
                    throw new Exception($"Invalid ticket type {ticket} specified.");
                }
            }

            var query = from stationsetName in doc.Descendants("StationSet")
                        let station = stationsetName.Elements("Stations").Attributes("Nlc")
                        select new
                        {
                            Origin = stationsetName.Attribute("Name"),
                            S = station
                        };
           
            var nodes = doc.Descendants().Where(x => x.Name.LocalName.Length > 0 && !char.IsUpper(x.Name.LocalName[0])).Distinct();
            foreach (var node in nodes)
            {
                var li = node as IXmlLineInfo;
                Warnings.Add($"Node name {node.Name.LocalName} does not meet schema rules (must start with upper case) at line number {li.LineNumber}");
            }

            var stations = doc.Descendants("Station").Where(x => x.Attribute("Nlc") == null);
            foreach (var invalidStation in stations)
            {
                var li = invalidStation as IXmlLineInfo;
                Warnings.Add($"Warning: <Station> node does not have an NLC code at line {li.LineNumber}");
            }
            stations = doc.Descendants("Station").Where(x => x.Attribute("Nlc") != null && !Regex.Match(x.Attribute("Nlc").Value, "^[0-9A-Z][0-9A-Z][0-9A-Z][0-9A-Z]$").Success);
            foreach (var invalidStation in stations)
            {
                var li = invalidStation as IXmlLineInfo;
                Warnings.Add($"Warning: <Station> node does not have a valid NLC code at line {li.LineNumber}");
            }

            var validStationSets = doc.Element("Settings").Elements("StationSets").Elements("StationSet").Where(x => x.Attribute("Name") != null);
            PerTocNlcList = validStationSets.ToDictionary(x => x.Attribute("Name").Value, x => x.Descendants("Station").Where(e => e.Attribute("Nlc") != null).Select(e => e.Attribute("Nlc").Value).ToHashSet(StringComparer.OrdinalIgnoreCase), StringComparer.OrdinalIgnoreCase);

            PerTocTicketTypeList = validStationSets.ToDictionary(
                x => x.Attribute("Name").Value,
                x => x.Descendants("TicketType").Where(e => e.Attribute("Code") != null).Select(e => e.Attribute("Code").Value).ToHashSet(StringComparer.OrdinalIgnoreCase), StringComparer.OrdinalIgnoreCase);

        }

        public (bool, string) GetFolder(string name)
        {
            return (folders.TryGetValue(name, out var result), result);
        }

    }
}
