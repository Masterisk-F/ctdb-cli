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
                Console.WriteLine("Usage: ctdb-cli <command> <cue_file>");
                Console.WriteLine("Commands:");
                Console.WriteLine("  lookup   - Lookup metadata from CTDB");
                Console.WriteLine("  calc     - Calculate parity");
                Console.WriteLine("  verify   - Verify and repair");
                Console.WriteLine("  upload   - Upload parity");
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
                    case "upload":
                        service.Upload(cuePath);
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
    }
}

namespace CTDB.CLI.Services
{
    // Placeholder for CtdbService

}
