using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Reflection;
using System.Diagnostics;
using System.IO;
using System.Xml.Linq;
using System.Xml;

namespace RcsConverter
{
    class Settings
    {
        private static string settingsFile;
        public enum SetOptions
        {
            AllStations,
            AllSetsInSettingsFile,
            SpecificSets
        }

        public static string GetSettingsFile()
        {
            return settingsFile;
        }
        /// <summary>
        /// Path for settings.xml - normally %appdata%/Parkeon/RcsConverter
        /// </summary>
        public string SettingsFile { get; private set; }
        public static string ProductName { get; private set; }

        public SetOptions SetOption { get; set; }

        public List<string> SetsToProduce { get; set; }

        /// <summary>
        /// a dictionary of folder types - keys are any of the following: RJIS, RCSFLOW, TEMP, IDMS - each
        /// of these come from settings .xml under the Folders key. Each folder is specified with a Folder element with
        /// attributes Name and Location.
        /// </summary>
        private Dictionary<string, string> folders = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public string CurrentTocName { get; set; }
        public HashSet<string> TicketTypes { get; set; }
        public Dictionary<string, SortedSet<string>> PerTocNlcList { get; private set; }
        public Dictionary<string, HashSet<string>> PerTocTicketTypeList { get; private set; }
        public List<string> Warnings { get; private set; } = new List<string>();

        static Settings()
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
            settingsFile = Path.Combine(programFolder, "settings.xml");
        }

        public Settings()
        {
            // copy static settings file to instance:
            SettingsFile = settingsFile;

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

            // check ticket codes are all 3 characters and only upper case letters or digits:
            foreach (var ticket in TicketTypes)
            {
                if (string.IsNullOrWhiteSpace(ticket) || ticket.Length != 3 || ticket.Any(c=>!char.IsUpper(c) && !char.IsDigit(c)))
                {
                    throw new Exception($"Invalid ticket type {ticket} specified.");
                }
            }

            // Check all XML element names are in sentence case:
            var nodes = doc.Descendants().Where(x => x.Name.LocalName.Length > 0 && !char.IsUpper(x.Name.LocalName[0])).Distinct();
            foreach (var node in nodes)
            {
                var li = node as IXmlLineInfo;
                Warnings.Add($"Node name {node.Name.LocalName} does not meet schema rules (must start with upper case) at line {li.LineNumber}");
            }

            // Check all XML attribute names are in sentence case:
            var attributes = doc.Descendants().SelectMany(x => x.Attributes()).Where(y => y.Name.LocalName.Length > 0 && !char.IsUpper(y.Name.LocalName[0]));
            foreach (var attribute in attributes)
            {
                var li = attribute as IXmlLineInfo;
                Warnings.Add($"attribute name {attribute.Name.LocalName} does not meet schema rules (must start with upper case) at line {li.LineNumber}");
            }

            // Check for stations without Nlc codes:
            var stations = doc.Descendants("Station").Where(x => x.Attribute("Nlc") == null);
            foreach (var invalidStation in stations)
            {
                var li = invalidStation as IXmlLineInfo;
                Warnings.Add($"Warning: <Station> node does not have an NLC code at line {li.LineNumber}");
            }

            // Check all NLC codes are valid:
            stations = doc.Descendants("Station").Where(x => x.Attribute("Nlc") != null && !Regex.Match(x.Attribute("Nlc").Value, "^[0-9A-Z][0-9A-Z][0-9A-Z][0-9A-Z]$").Success);
            foreach (var invalidStation in stations)
            {
                var li = invalidStation as IXmlLineInfo;
                Warnings.Add($"Warning: <Station> node does not have a valid NLC code at line {li.LineNumber}");
            }

            var validStationSets = doc.Element("Settings").Elements("StationSets").Elements("StationSet").Where(x => x.Attribute("Name") != null);
            var allUsed = validStationSets.Where(x => x.Attribute("Name").Value.ToLower() == "all");
            if (allUsed.Count() != 0)
            {
                var badname = allUsed.First().Attribute("Name").Value;
                var li = allUsed.First() as IXmlLineInfo;
                throw new Exception($"You cannot use \"{badname}\" as the name of a station set (at line {li.LineNumber} in {SettingsFile}) - it is a reserved word.");
            }
            PerTocNlcList = validStationSets.ToDictionary(x => x.Attribute("Name").Value, x => x.Descendants("Station").Where(e => e.Attribute("Nlc") != null).Select(e => e.Attribute("Nlc").Value).ToSortedSet(StringComparer.OrdinalIgnoreCase), StringComparer.OrdinalIgnoreCase);

            PerTocTicketTypeList = validStationSets.ToDictionary(
                x => x.Attribute("Name").Value,
                x => x.Descendants("TicketType").Where(e => e.Attribute("Code") != null).Select(e => e.Attribute("Code").Value).ToHashSet(StringComparer.OrdinalIgnoreCase), StringComparer.OrdinalIgnoreCase);
            Console.WriteLine("");
        }

        public (bool, string) GetFolder(string name)
        {
            return (folders.TryGetValue(name, out var result), result);
        }

    }
}
