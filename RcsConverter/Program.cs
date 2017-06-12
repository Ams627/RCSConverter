using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace RcsConverter
{
    internal class Program
    {
        static Dictionary<string, string> folders;
        static string settingsFile;

        /// <summary>
        /// the key is a group NLC and the value is a list of members of the group
        /// </summary>
        static Dictionary<string, HashSet<string>> groupDefinitions = new Dictionary<string, HashSet<string>>();

        private static void AddGroupMember(string groupnlc, string member)
        {
            if (!groupDefinitions.TryGetValue(groupnlc, out var list))
            {
                list = new HashSet<string>();
                groupDefinitions.Add(groupnlc, list);
            }
            list.Add(member);
        }

        private static string GetRJISLocFilename()
        {
            string rjisFolder;
            if (!folders.TryGetValue("RJIS", out rjisFolder))
            {
                throw new Exception($"You must define the RJIS folder in the settings file {settingsFile}");
            }
            var files = Directory.GetFiles(rjisFolder, "RJFAF*.loc");
            if (files.Count() == 0)
            {
                throw new Exception($"No RJIS .LOC file found in the folder {rjisFolder}");
            }
            
            return files.Last();
        }


        private static void Main(string[] args)
        {   
            try
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

                var settings = new Settings();
                var filenameProcessor = new FilenameProcessor(settings);

                var flowFilename = filenameProcessor.GetFlowRefreshFilename();
                if (string.IsNullOrEmpty(flowFilename))
                {
                    Console.WriteLine($"No flow filename in folder {settings.GetFolder("RCSFlow")}: nothing to process");
                }
                else
                {
                    var rcsProcessor = new RCSFlowProcessor(settings, flowFilename);
                    rcsProcessor.Process();

                    var updateProcessorList = new List<RCSFlowProcessor>();
                    foreach (var updateFilename in filenameProcessor.FlowUpdateFilenames)
                    {
                        var processor = new RCSFlowProcessor(settings, updateFilename);
                        processor.Process();
                        updateProcessorList.Add(processor);
                    }

                    
                }


                //var rjislocfile = GetRJISLocFilename();
                //using (var fileStream = File.OpenRead(rjislocfile))
                //using (var streamReader = new StreamReader(fileStream))
                //{
                //    string line;
                //    while ((line = streamReader.ReadLine()) != null)
                //    {
                //        if (line.Length == 289 && line.Substring(1, 3) == "L70")
                //        {
                //            var nlc = line.Substring(36, 4);
                //            var faregroup = line.Substring(69, 4);
                //            if (nlc != faregroup)
                //            {
                //                AddGroupMember(faregroup, nlc);
                //            }
                //        }
                //    }
                //}



            }
            catch (Exception ex)
            {
                var codeBase = System.Reflection.Assembly.GetExecutingAssembly().CodeBase;
                var progname = Path.GetFileNameWithoutExtension(codeBase);
                Console.Error.WriteLine(progname + ": Error: " + ex.Message);
            }
        }
    }
}
