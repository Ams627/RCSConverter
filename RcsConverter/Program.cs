using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;

namespace RcsConverter
{
    internal class Program
    {
        private static void ExecProcess(string program, params string[] args)
        {
            var procInfo = new ProcessStartInfo()
            {
                FileName = program,
                Arguments = string.Join(" ", args)
            };
            Process.Start(procInfo);
        }
        private static bool Compare(string s, string t)
        {
            return string.Equals(s, t, StringComparison.OrdinalIgnoreCase);
        }

        private static void Main(string[] args)
        {   
            try
            {
                Console.WriteLine("Started");
                var startTime = DateTime.Now;
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

                if (args.Count() > 0 && Compare(args[0], "showsettings"))
                {
                    ExecProcess("chrome", Settings.GetSettingsFile());
                    Environment.Exit(0);
                }
                if (args.Count() > 0 && Compare(args[0], "settings"))
                {
                    var appname = new GetRegisteredApp(".txt")?.Appname;
                    if (string.IsNullOrEmpty(appname))
                    {
                        throw new Exception($"Cannot get the default application to open {Settings.GetSettingsFile()}");
                    }
                    ExecProcess(appname, Settings.GetSettingsFile());
                    Environment.Exit(0);
                }

                Settings settings = new Settings();
                if (args.Count() == 0)
                {
                    var codeBase = System.Reflection.Assembly.GetEntryAssembly().CodeBase;
                    var progname = Path.GetFileNameWithoutExtension(codeBase);
                    Console.WriteLine($"Usage:");
                    Console.WriteLine($"    {progname} all - produce databases for all stations with ITSO fares.");
                    Console.WriteLine($"    {progname} alltocs - produce databases for all tocs specified in the settings file.");
                    Console.WriteLine($"    {progname} tocs <toc1> <toc2> - produce databases for the specified tocs.");
                    Console.WriteLine($"    {progname} settings - edit settings file.\n");

                    var tempSettings = new Settings();
                    if (tempSettings.PerTocNlcList.Count() == 0)
                    {
                        Console.WriteLine("No tocs are specifed in the settings file currently");
                    }
                    else
                    {
                        Console.WriteLine($"Current tocs in settings file: ");
                        Console.Write("    ");
                        foreach (var toc in settings.PerTocNlcList.Keys)
                        {
                            Console.Write($"{toc} ");
                        }
                    }
                    Environment.Exit(-1);
                }

                if (args[0] == "all")
                {
                    settings.SetOption = Settings.SetOptions.AllStations;
                }
                else if (args[0] == "alltocs")
                {
                    settings.SetOption = Settings.SetOptions.AllSetsInSettingsFile;
                }
                else if (args[0] == "tocs" && args.Count() > 1)
                {
                    var tocspec = args.ToList().Skip(1);
                    var settingsTocSet = settings.PerTocNlcList.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase);
                    if (tocspec.Intersect(settings.PerTocNlcList.Keys).Count() != tocspec.Count())
                    {
                        foreach (var toc in tocspec)
                        {
                            if (!settingsTocSet.Contains(toc))
                            {
                                Console.Error.WriteLine($"There is no toc {toc} specified in the settings file {settings.SettingsFile}");
                            }
                        }
                        Environment.Exit(-1);
                    }
                    settings.SetsToProduce = new List<string>(tocspec);
                }

                settings.Warnings.ForEach(x => Console.WriteLine($"{x}"));
                var filenameProcessor = new FilenameProcessor(settings);

                var flowFilename = filenameProcessor.GetFlowRefreshFilename();
                if (string.IsNullOrEmpty(flowFilename))
                {
                    Console.WriteLine($"No flow filename in folder {settings.GetFolder("RCSFlow")}: nothing to process");
                }
                else
                {
                    var rjisProcessor = new RJISProcessor(settings, filenameProcessor.RJISZipname);

                    var rcsRefreshProcessor = new RCSFlowProcessor(settings, flowFilename);
                    rcsRefreshProcessor.Process();

                    var updateProcessorList = new List<RCSFlowProcessor>();
                    foreach (var updateFilename in filenameProcessor.FlowUpdateFilenames)
                    {
                        var processor = new RCSFlowProcessor(settings, updateFilename);
                        processor.Process();
                        updateProcessorList.Add(processor);

                        var deletionAmmendmentDups =
                            processor.RcsFlowList
                            .Where(x => x.recordType == RCSFRecordType.Delete || x.recordType == RCSFRecordType.Amend)
                            .GroupBy(x => x.LookupKey)
                            .Where(x => x.Count() > 1)
                            .Select(x => x.Key)
                            .ToList();

                        if (deletionAmmendmentDups.Count > 0)
                        {
                            Console.WriteLine($"Duplicate keys found in update file {updateFilename}:");
                            foreach (var dup in deletionAmmendmentDups)
                            {
                                Console.WriteLine($"{dup.Substring(0, 5)} {dup.Substring(5, 4)} {dup.Substring(9, 4)}");
                            }
                        }

                        // get a list of keys (route, origin, destination) which have either Amend or Delete types and delete
                        // these keys from the RCS refresh list:
                        var keysToDelete =
                            processor.RcsFlowList
                            .Where(x => x.recordType == RCSFRecordType.Delete || x.recordType == RCSFRecordType.Amend).Select(x => x.LookupKey);

                        foreach (var key in keysToDelete)
                        {
                            if (rcsRefreshProcessor.RcsFlowLookup.Contains(key))
                            {
                                rcsRefreshProcessor.RcsFlowLookup[key].First().recordType = RCSFRecordType.ForDeletion;
                            }
                        }

                        var deletionCount = rcsRefreshProcessor.RcsFlowList.Where(x => x.recordType == RCSFRecordType.ForDeletion).Count();
                        Console.WriteLine($"{updateFilename} has {deletionCount} records for deletion.");

                        // actually delete the records:
                        rcsRefreshProcessor.RcsFlowList.RemoveAll(x => x.recordType == RCSFRecordType.ForDeletion);

                        // add the amend and insert records from the update file to the refresh database:
                        foreach (var rcsflow in processor.RcsFlowList)
                        {
                            if (rcsflow.recordType == RCSFRecordType.Amend || rcsflow.recordType == RCSFRecordType.Delete)
                            {
                                rcsRefreshProcessor.RcsFlowList.Add(rcsflow);
                            }
                        }

                        // sort and reindex the refresh database:
                        rcsRefreshProcessor.RcsFlowList.Sort((x1, x2) => x1.LookupKey.CompareTo(x2.LookupKey));
                        rcsRefreshProcessor.ReIndex();
                    } // foreach update

                    if (!rjisProcessor.RJISAvailable)
                    {
                        throw new Exception("Cannot create per station databases as there is no RJIS data available.");
                    }

                    var dbCreator = new DbCreator(rcsRefreshProcessor.RcsFlowList, rjisProcessor, settings);
                    dbCreator.CreateIndividualDBs();
                }
                var endtime = DateTime.Now;
                var minutes = (endtime - startTime).Minutes;
                var seconds = (endtime - startTime).Seconds;
                Console.WriteLine($"Duration: {minutes:D2}:{seconds:D2}.");
                Console.WriteLine("Finished.");
            }
            catch (Exception ex)
            {
                var codeBase = System.Reflection.Assembly.GetEntryAssembly().CodeBase;
                var progname = Path.GetFileNameWithoutExtension(codeBase);
                Console.Error.WriteLine(progname + ": Error: " + ex.Message);
            }
        }
    }
}
