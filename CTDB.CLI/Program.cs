using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Xml.Serialization;
using System.Xml;
using CTDB.CLI.Models;

namespace CTDB.CLI
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length < 2)
            {
                Console.WriteLine("Usage: ctdb-cli <command> <cue_file> [options]");
                Console.WriteLine("Commands:");
                Console.WriteLine("  lookup   - Lookup metadata from CTDB");
                Console.WriteLine("  calc     - Calculate parity");
                Console.WriteLine("  verify   - Verify against CTDB");
                Console.WriteLine("  repair   - Repair using CTDB parity");
                Console.WriteLine("  submit   - Submit parity to CTDB");
                Console.WriteLine("             Required: --drive <name> --quality <1-100>");
                Console.WriteLine("             Note: Set CTDB_CLI_CALLER env var to enable actual submission.");
                Console.WriteLine("");
                Console.WriteLine("Options:");
                Console.WriteLine("  --xml    - Output result in XML format to stdout (logs go to stderr)");
                return;
            }

            string command = args[0];
            string cuePath = args[1];
            bool useXml = args.Contains("--xml", StringComparer.OrdinalIgnoreCase);

            if (!File.Exists(cuePath))
            {
                var writer = useXml ? Console.Error : Console.Out;
                writer.WriteLine($"Error: CUE file not found: {cuePath}");
                return;
            }

            try
            {
                var service = new Services.CtdbService(useXml ? Console.Error : Console.Out);
                CtdbXmlResult finalResult = new CtdbXmlResult();
                object? commandResult = null;

                switch (command.ToLower())
                {
                    case "lookup":
                        finalResult.Lookup = service.Lookup(cuePath);
                        commandResult = finalResult.Lookup;
                        if (useXml && finalResult.Lookup != null)
                        {
                            Console.WriteLine(finalResult.Lookup.RawXml);
                            return;
                        }
                        break;
                    case "calc":
                        finalResult.Calc = service.CalculateParity(cuePath);
                        commandResult = finalResult.Calc;
                        break;
                    case "verify":
                        finalResult.Verify = service.Verify(cuePath);
                        commandResult = finalResult.Verify;
                        break;
                    case "submit":
                        {
                            string? drive = GetArgValue(args, "--drive");
                            string? qualityStr = GetArgValue(args, "--quality");

                            if (string.IsNullOrEmpty(drive) || string.IsNullOrEmpty(qualityStr))
                            {
                                var writer = useXml ? Console.Error : Console.Out;
                                writer.WriteLine("Error: --drive and --quality are required for submit.");
                                writer.WriteLine("Usage: ctdb-cli submit <cue_file> --drive <name> --quality <1-100>");
                                return;
                            }

                            if (!int.TryParse(qualityStr, out int quality) || quality < 1 || quality > 100)
                            {
                                var writer = useXml ? Console.Error : Console.Out;
                                writer.WriteLine("Error: --quality must be an integer between 1 and 100.");
                                return;
                            }

                            finalResult.Submit = service.Submit(cuePath, drive, quality);
                            commandResult = finalResult.Submit;
                        }
                        break;
                    case "repair":
                        finalResult.Repair = service.Repair(cuePath);
                        commandResult = finalResult.Repair;
                        break;
                    default:
                        Console.Error.WriteLine($"Unknown command: {command}");
                        Environment.Exit(1);
                        break;
                }

                if (useXml)
                {
                    OutputXml(finalResult);
                }
                
                if (commandResult == null)
                {
                    Environment.Exit(1);
                }
            }
            catch (Exception ex)
            {
                var writer = useXml ? Console.Error : Console.Out;
                writer.WriteLine($"An error occurred: {ex.Message}");
                writer.WriteLine(ex.StackTrace);
                Environment.Exit(1);
            }
        }


        static void OutputXml(CtdbXmlResult result)
        {
            var serializer = new XmlSerializer(typeof(CtdbXmlResult));
            var settings = new XmlWriterSettings
            {
                Indent = true,
                OmitXmlDeclaration = false
            };

            using (var writer = XmlWriter.Create(Console.Out, settings))
            {
                serializer.Serialize(writer, result);
            }
            Console.WriteLine(); // Add newline after XML
        }

        // Helper method to get the value of a specified key from arguments
        static string? GetArgValue(string[] args, string key)
        {
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i].Equals(key, StringComparison.OrdinalIgnoreCase))
                {
                    return args[i + 1];
                }
            }
            return null;
        }
    }
}

namespace CTDB.CLI.Services
{
    // Placeholder for CtdbService

}
