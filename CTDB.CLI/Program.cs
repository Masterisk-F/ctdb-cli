using System;
using System.IO;

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
                return;
            }

            string command = args[0];
            string cuePath = args[1];

            if (!File.Exists(cuePath))
            {
                Console.WriteLine($"Error: CUE file not found: {cuePath}");
                return;
            }

            try
            {
                var service = new Services.CtdbService();

                switch (command.ToLower())
                {
                    case "lookup":
                        service.Lookup(cuePath);
                        break;
                    case "calc":
                        service.CalculateParity(cuePath);
                        break;
                    case "verify":
                        service.Verify(cuePath);
                        break;
                    case "submit":
                        {
                            string? drive = GetArgValue(args, "--drive");
                            string? qualityStr = GetArgValue(args, "--quality");

                            if (string.IsNullOrEmpty(drive) || string.IsNullOrEmpty(qualityStr))
                            {
                                Console.WriteLine("Error: --drive and --quality are required for submit.");
                                Console.WriteLine("Usage: ctdb-cli submit <cue_file> --drive <name> --quality <1-100>");
                                return;
                            }

                            if (!int.TryParse(qualityStr, out int quality) || quality < 1 || quality > 100)
                            {
                                Console.WriteLine("Error: --quality must be an integer between 1 and 100.");
                                return;
                            }

                            service.Submit(cuePath, drive, quality);
                        }
                        break;
                    case "repair":
                        service.Repair(cuePath);
                        break;
                    default:
                        Console.WriteLine($"Unknown command: {command}");
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
            }
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
