using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace RcsConverter
{
    class RJISProcessor
    {
        Settings settings;
        string rjisZipname;

        /// <summary>
        /// the key is a group NLC and the value is a list of members of the group
        /// </summary>
        Dictionary<string, HashSet<string>> groupDefinitions = new Dictionary<string, HashSet<string>>();

        public RJISProcessor(Settings settings, string rjisZipname)
        {
            this.settings = settings;
            this.rjisZipname = rjisZipname;
            ProcessFiles();
        }

        public HashSet<string> GetGroupMembers(string groupnlc)
        {
            groupDefinitions.TryGetValue(groupnlc, out var hashset);
            return hashset;
        }

        private void AddGroupMember(string groupnlc, string member)
        {
            if (!groupDefinitions.TryGetValue(groupnlc, out var list))
            {
                list = new HashSet<string>();
                groupDefinitions.Add(groupnlc, list);
            }
            list.Add(member);
        }

        private void ProcessFiles()
        {
            var tempfolder = Path.GetTempPath();
            var progname = settings.ProductName;
            tempfolder = Path.Combine(tempfolder, progname, "RCSunzip");
            if (!Directory.Exists(tempfolder))
            {
                Directory.CreateDirectory(tempfolder);
            }

            using (var archive = ZipFile.OpenRead(rjisZipname))
            {
                foreach (var entry in archive.Entries)
                {
                    var destinationPath = Path.Combine(tempfolder, entry.FullName);
                    if (!File.Exists(destinationPath))
                    {
                        entry.ExtractToFile(destinationPath);
                    }
                }
            }

            var locfiles = Directory.GetFiles(tempfolder, "RJFAF*.LOC").ToList();
            locfiles.RemoveAll(s=> !Regex.Match(s, @"RJFAF\d{3}\.LOC$", RegexOptions.IgnoreCase).Success);
            if (locfiles.Count < 1)
            {
                throw new Exception($"At least one RJIS loc file must be specified in the RJISZIPS folder in the settings file {settings.SettingsFile}");
            }

            var filename = locfiles.Last();

            using (var fileStream = File.OpenRead(filename))
            using (var streamReader = new StreamReader(fileStream))
            {
                string line;
                while ((line = streamReader.ReadLine()) != null)
                {
                    if (line.Length == 289 && line.Substring(1, 3) == "L70")
                    {
                        var nlc = line.Substring(36, 4);
                        var faregroup = line.Substring(69, 4);
                        if (nlc != faregroup)
                        {
                            AddGroupMember(faregroup, nlc);
                        }
                    }
                }
            }


        }



    }
}
