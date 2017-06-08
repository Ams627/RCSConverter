using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;
using System.Diagnostics;
using System.IO;
using System.Xml.Linq;

namespace RcsConverter
{
    class Settings
    {
        /// <summary>
        /// Path for settings.xml - normally %appdata%/Parkeon/RcsConverter
        /// </summary>
        private string settingsFile;

        /// <summary>
        /// a dictionary of folder types - keys are any of the following: RJIS, RCSFLOW, TEMP, IDMS - each
        /// of these come from settings .xml under the Folders key. Each folder is specified with a Folder element with
        /// attributes Name and Location.
        /// </summary>
        private Dictionary<string, string> folders = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public string MyProperty { get; set; }
        public HashSet<string> TicketTypes { get; set; }


        public Settings()
        {
            var versionInfo = FileVersionInfo.GetVersionInfo(Assembly.GetEntryAssembly().Location);
            var companyName = versionInfo.CompanyName;
            var productName = versionInfo.ProductName;
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
            var doc = XDocument.Load(settingsFile);
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
                if (string.IsNullOrWhiteSpace(ticket) || ticket.Length != 3 || ticket.Any(c=>!char.IsUpper(c)))
                {
                    throw new Exception($"Invalid ticket type {ticket} specified.");
                }
            }
        }

        string GetFolder(string name)
        {
            folders.TryGetValue(name, out var result);
            return result;
        }


    }
}
