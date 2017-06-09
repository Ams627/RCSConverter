using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace RcsConverter
{
    class FilenameProcessor
    {
        private Settings settings;
        private string flowRefreshFilename;
        private List<string> flowUpdateFilenames;

        public FilenameProcessor(Settings settings)
        {
            this.settings = settings;
            GetFilenames();
        }

        public int GetFilenameSerialNumber(string path)
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
        public void GetFilenames()
        {
            var (ok, rcsFlowFolder) = settings.GetFolder("RCSFlow");
            if (!ok)
            {
                throw new Exception($"You must define the RCSFlow folder in the settings file {settings.SettingsFile}");
            }
            var rcsRefreshFiles = Directory.GetFiles(rcsFlowFolder, "RCS_R_F*").ToList();

            // remove the files we don't want to get a more precise match as Directory.GetFiles
            // does not allow use of regex - also remove files that are not .zip or .xml:
            rcsRefreshFiles.RemoveAll(s => !Regex.Match(s, @"RCS_R_F_\d{6}_\d{5}.xml$|RCS_R_F_\d{6}_\d{5}.zip$").Success);

            // comparison Func just for RCS files:
            Func<string, string, int> RCSComparer = (s1, s2) =>
            {
                return GetFilenameSerialNumber(s1).CompareTo(GetFilenameSerialNumber(s2));
            };
             
            if (rcsRefreshFiles.Count > 0)
            {
                rcsRefreshFiles.Sort(new Comparison<string>(RCSComparer));
                flowRefreshFilename = rcsRefreshFiles.Last();
            }

            // process the RCS flow update files (deltas):
            var rcsUpdateFiles = Directory.GetFiles(rcsFlowFolder, "RCS_U_F*").ToList();
            var distinctRcsUpdateFiles = rcsUpdateFiles.GroupBy(x => GetFilenameSerialNumber(x)).Select(g => g.First()).ToList();

            // remove the files we don't want to get a more precise match as Directory.GetFiles
            // does not allow use of regex - also remove files that are not .zip or .xml:
            rcsRefreshFiles.RemoveAll(s => !Regex.Match(s, @"RCS_U_F_\d{6}_\d{5}.xml$|RCS_U_F_\d{6}_\d{5}.zip$").Success);
            

        }
    }
}


            //if (rcsfiles.Count() == 0)
            //{
            //    throw new Exception($"No RCS flow files (zip or xml) found in the folder {rcsFlowFolder}");
            //}
