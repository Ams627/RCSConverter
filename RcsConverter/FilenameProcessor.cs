using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace RcsConverter
{
    class FilenameProcessor
    {
        private Settings settings;
        private string flowRefreshFilename;
        public List<string> FlowUpdateFilenames { get; private set; } = new List<string>();

        public string RJISZipname { get; private set; }

        private Dictionary<string, string> RJISFilenames = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);

        public FilenameProcessor(Settings settings)
        {
            this.settings = settings;
            GetRCSFilenames();
            GetRJISFilenames();
        }

        public string GetRJISFilename(string ext)
        {
            string result = string.Empty;
            if (!string.IsNullOrEmpty(result))
            {
                if (ext[0] == '.')
                {
                    ext = ext.Substring(1);
                    RJISFilenames.TryGetValue(ext, out result);
                }
            }
            return result;
        }

        private int GetRJISFilenameSerialNumber(string path)
        {
            int i = -1;
            var name = Path.GetFileNameWithoutExtension(path);
            var l = name.Length;
            if (l >= 3)
            {
                i = Convert.ToInt32(name.Substring(l - 3));
            }
            return i;
        }

        private int GetRCSFilenameSerialNumber(string path)
        {
            int i = -1;
            var name = Path.GetFileNameWithoutExtension(path);
            var l = name.Length;
            if (l >= 5)
            {
                i = Convert.ToInt32(name.Substring(l - 5));
            }
            return i;
        }

        public string GetFlowRefreshFilename()
        {
            return flowRefreshFilename;
        }

        private void GetRCSFilenames()
        {
            var (ok, rcsFlowFolder) = settings.GetFolder("RCSFlow");
            if (!ok)
            {
                throw new Exception($"You must define the RCSFlow folder in the settings file {settings.SettingsFile}");
            }
            var rcsRefreshFiles = Directory.GetFiles(rcsFlowFolder, "RCS_R_F*").ToList();

            // remove the files we don't want to get a more precise match as Directory.GetFiles
            // does not allow use of regex - also remove files that are not .zip or .xml:
            rcsRefreshFiles.RemoveAll(s => !Regex.Match(s, @"RCS_R_F_\d{6}_\d{5}\.xml$|RCS_R_F_\d{6}_\d{5}\.zip$").Success);

            // comparison Func just for RCS files - we only compare the file serial number: we don't care about the 
            // date contained within the filename:
            Func<string, string, int> RCSComparer = (s1, s2) =>
            {
                return GetRCSFilenameSerialNumber(s1).CompareTo(GetRCSFilenameSerialNumber(s2));
            };

            // just take the most recent flow filename:
            if (rcsRefreshFiles.Count > 0)
            {
                rcsRefreshFiles.Sort(new Comparison<string>(RCSComparer));

                var refreshGroups = rcsRefreshFiles
                        .GroupBy(x => GetRCSFilenameSerialNumber(x))
                        .Select(g => new { Filename = g.FirstOrDefault(f => Path.GetExtension(f).ToLower() == ".xml") ?? g.First() });
                var lastname = refreshGroups.Last().Filename;
                flowRefreshFilename = lastname;
                //                flowRefreshFilename = rcsRefreshFiles.Last();
            }

            var refreshSerialNumber = GetRCSFilenameSerialNumber(flowRefreshFilename);

            // process the RCS flow update files (deltas):
            var rcsUpdateFiles = Directory.GetFiles(rcsFlowFolder, "RCS_U_F*").ToList();
            // remove the files we don't want to get a more precise match as Directory.GetFiles
            // does not allow use of regex - also remove files that are not .zip or .xml:
            rcsUpdateFiles.RemoveAll(s => !Regex.Match(s, @"RCS_U_F_\d{6}_\d{5}\.xml$|RCS_U_F_\d{6}_\d{5}\.zip$").Success);

            // remove the update files whose serial number is less than the refresh serial number (or the same but that is an error in the feed).
            rcsUpdateFiles.RemoveAll(s => GetRCSFilenameSerialNumber(s) <= refreshSerialNumber);

            if (rcsUpdateFiles.Count > 0)
            {
                // group the files by serial number (we may have both an XML and a ZIP file - the XML file will always get
                // priority if it is present.)
                var updateGroups = rcsUpdateFiles
                                        .GroupBy(x => GetRCSFilenameSerialNumber(x))
                                        .Select(g => new { Filename = g.FirstOrDefault(f => Path.GetExtension(f).ToLower() == ".xml") ?? g.First() });

                FlowUpdateFilenames = new List<string>(updateGroups.Select(x => x.Filename));
            }
            else
            {
                FlowUpdateFilenames.Clear();
            }
        }

        private void GetRJISFilenames()
        {
            var (ok, rjisDir) = settings.GetFolder("RJISZIPS");
            if (!ok)
            {
                throw new Exception($"A folder for RJIS files must be specified in the settings file {settings.SettingsFile}");
            }
            var refreshFilenames = Directory.GetFiles(rjisDir, "RJFAF*").ToList();
            // use regex to remove all non-precise matches:
            refreshFilenames.RemoveAll(s => !Regex.Match(s, @"RJFAF\d{3}\.[A-Z][A-Z][A-Z]$", RegexOptions.IgnoreCase).Success);
            var zips = refreshFilenames.Where(x => Path.GetExtension(x).ToLower() == ".zip").OrderBy(x=>GetRJISFilenameSerialNumber(x));
            RJISZipname = zips.FirstOrDefault();
        }
    }
}


            //if (rcsfiles.Count() == 0)
            //{
            //    throw new Exception($"No RCS flow files (zip or xml) found in the folder {rcsFlowFolder}");
            //}
