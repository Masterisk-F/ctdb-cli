using System;
using System.Collections.Generic;
using CUETools.CTDB;
using CUETools.CDImage;
using CUETools.Processor;
using CUETools.AccurateRip;
using CUETools.Codecs;
using System.Net;
using System.IO;

namespace CTDB.CLI.Services
{
    public class CtdbService
    {
        private readonly CUEConfig _config;

        public CtdbService()
        {
            // Register FLAC decoder manually (to avoid relying on plugins folder structure in published builds)
            if (!CUEProcessorPlugins.decs.Exists(d => d is CUETools.Codecs.Flake.DecoderSettings))
            {
                CUEProcessorPlugins.decs.Add(new CUETools.Codecs.Flake.DecoderSettings());
            }
            _config = new CUEConfig();
        }

        public void Lookup(string cuePath)
        {
            Console.WriteLine($"Processing CUE file: {cuePath}");

            try
            {
                var cueSheet = new CUESheet(_config);
                cueSheet.Open(cuePath);

                var toc = cueSheet.TOC;
                Console.WriteLine($"TOC ID: {toc.TOCID}");
                Console.WriteLine($"Track Count: {toc.AudioTracks}");

                string urlbase = "http://db.cuetools.net";
                string url = urlbase
                    + "/lookup2.php"
                    + "?version=3"
                    + "&ctdb=1"
                    + "&fuzzy=1"
                    + "&metadata=default"
                    + "&toc=" + toc.ToString();
                
                Console.WriteLine($"Fetching XML from: {url}");
                
                var request = (HttpWebRequest)WebRequest.Create(url);
                request.Proxy = _config.GetProxy();
                request.UserAgent = "ctdb-cli (" + Environment.OSVersion.VersionString + ")";
                
                using (var response = (HttpWebResponse)request.GetResponse())
                using (var stream = response.GetResponseStream())
                using (var reader = new StreamReader(stream))
                {
                    string xml = reader.ReadToEnd();
                    Console.WriteLine(xml);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during lookup: {ex.Message}");
            }
        }

        public void CalculateParity(string cuePath)
        {
            Console.WriteLine($"Calculating parity for: {cuePath}");
            try
            {
                var cueSheet = new CUESheet(_config);
                cueSheet.Open(cuePath);
                var toc = cueSheet.TOC;

                Console.WriteLine($"TOC ID: {toc.TOCID}");

                var ar = new AccurateRipVerify(toc, _config.GetProxy());
                if (!FeedAudioToAR(ar, cueSheet, cuePath)) return;

                Console.WriteLine($"CTDB CRC: {ar.CTDBCRC(0):x8}");
                for (int i = 0; i < toc.AudioTracks; i++)
                {
                     Console.WriteLine($"Track {i+1} CRC: {ar.CRC(i):x8}  CRC32: {ar.CRC32(i):x8}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during calculation: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
            }
        }

        public void Verify(string cuePath)
        {
            Console.WriteLine($"Verifying: {cuePath}");
            try
            {
                var cueSheet = new CUESheet(_config);
                cueSheet.Open(cuePath);
                var toc = cueSheet.TOC;

                Console.WriteLine($"TOC ID: {toc.TOCID}");

                var ar = new AccurateRipVerify(toc, _config.GetProxy());
                var ctdb = new CUEToolsDB(toc, _config.GetProxy());
                ctdb.Init(ar); // Initialize for parity validation BEFORE feeding data

                if (!FeedAudioToAR(ar, cueSheet, cuePath)) return;

                Console.WriteLine("Contacting CTDB...");
                ctdb.ContactDB(null, "ctdb-cli", null, true, false, CTDBMetadataSearch.Default);

                if (ctdb.Metadata == null && ctdb.Entries == null)
                {
                     Console.WriteLine($"CTDB returned {ctdb.QueryResponseStatus}");
                }

                Console.WriteLine("Verifying...");
                ctdb.DoVerify();
                
                Console.WriteLine($"Confidence: {ctdb.Confidence}");
                Console.WriteLine($"Status: {ctdb.Status}");
                Console.WriteLine($"Total Entries: {ctdb.Total}");
                
                int entryIndex = 0;
                foreach(var entry in ctdb.Entries)
                {
                    entryIndex++;
                    Console.WriteLine($"------------------------------------------------------------");
                    Console.WriteLine($"Entry {entryIndex}:");
                    Console.WriteLine($"  Conf: {entry.conf}, CRC: {entry.crc:x8}, Offset: {entry.offset}");
                    Console.WriteLine($"  Status: {entry.Status}");
                    Console.WriteLine($"  HasErrors: {entry.hasErrors}, CanRecover: {entry.canRecover}");
                    Console.WriteLine($"  TOC: {entry.toc.TOCID}");

                    if (entry.trackcrcs != null)
                    {
                        Console.WriteLine($"  Track CRCs:");
                        for (int i = 0; i < entry.trackcrcs.Length; i++)
                        {
                            uint localCrc = ctdb.Verify.TrackCRC(i + 1, -entry.offset);
                            uint remoteCrc = entry.trackcrcs[i];
                            bool matched = (localCrc == remoteCrc);
                            string matchStatus = matched ? "Matched" : "Unmatched";
                            
                            Console.WriteLine($"    Track {i + 1:00}: Local={localCrc:x8} Remote={remoteCrc:x8} [{matchStatus}]");
                        }
                    }

                    if (entry.hasErrors && entry.canRecover && entry.repair != null)
                    {
                        Console.WriteLine($"  Repair Info:");
                        Console.WriteLine($"    Correctable Errors: {entry.repair.CorrectableErrors}");
                        Console.WriteLine($"    Affected Sectors: {entry.repair.AffectedSectors}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during verification: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
            }
        }

        private bool FeedAudioToAR(AccurateRipVerify ar, CUESheet cueSheet, string cuePath)
        {
                // Find audio file
                string audioPath = null;
                var lines = File.ReadAllLines(cuePath);
                foreach(var line in lines)
                {
                    if (line.Trim().StartsWith("FILE"))
                    {
                        var parts = line.Trim().Split('\"');
                        if (parts.Length >= 2)
                        {
                            audioPath = Path.Combine(Path.GetDirectoryName(cuePath), parts[1]);
                            break; 
                        }
                    }
                }
                
                if (audioPath == null || !File.Exists(audioPath))
                {
                     // Fallback to checking same name as CUE
                     var altPath = Path.ChangeExtension(cuePath, ".wav");
                     if (File.Exists(altPath)) audioPath = altPath;
                }

                if (audioPath == null || !File.Exists(audioPath))
                {
                    Console.WriteLine("Audio file not found.");
                    return false;
                }

                Console.WriteLine($"Processing audio file: {audioPath}");
                
                var audioSource = AudioReadWrite.GetAudioSource(audioPath, null, _config);
                if (audioSource == null) {
                    Console.WriteLine("Failed to open audio source.");
                    return false;
                }

                var buffer = new AudioBuffer(audioSource, 1024 * 64); 
                
                while (audioSource.Read(buffer, -1) != 0)
                {
                    if (ar.Position + buffer.Length > ar.FinalSampleCount)
                    {
                        buffer.Length = (int)(ar.FinalSampleCount - ar.Position);
                    }

                    if (buffer.Length > 0)
                        ar.Write(buffer);

                    if (ar.Position >= ar.FinalSampleCount) break;
                }

                if (ar.Position < ar.FinalSampleCount)
                {
                    Console.WriteLine($"\nWarning: Audio file shorter than TOC. Padding {ar.FinalSampleCount - ar.Position} samples.");
                    Array.Clear(buffer.Bytes, 0, buffer.Bytes.Length);
                    while (ar.Position < ar.FinalSampleCount)
                    {
                        int remaining = (int)Math.Min(buffer.Bytes.Length / buffer.PCM.BlockAlign, ar.FinalSampleCount - ar.Position);
                        buffer.Length = remaining;
                        ar.Write(buffer);
                    }
                }
                Console.WriteLine($"Done. Final Position: {ar.Position}, Expected: {ar.FinalSampleCount}");
                return true;
        }

        public void Submit(string cuePath, string driveName, int quality)
        {
            Console.WriteLine($"Submitting: {cuePath}");
            
            // Dry-run control via CTDB_CLI_CALLER environment variable
            string? caller = Environment.GetEnvironmentVariable("CTDB_CLI_CALLER");
            bool isDryRun = string.IsNullOrEmpty(caller);
            
            if (isDryRun)
            {
                Console.WriteLine("[DRY-RUN MODE] CTDB_CLI_CALLER environment variable is not set.");
            }
            
            try
            {
                var cueSheet = new CUESheet(_config);
                cueSheet.Open(cuePath);
                var toc = cueSheet.TOC;

                Console.WriteLine($"TOC ID: {toc.TOCID}");

                var ar = new AccurateRipVerify(toc, _config.GetProxy());
                var ctdb = new CUEToolsDB(toc, _config.GetProxy());
                ctdb.Init(ar); // Initialize, enables parity calc

                if (!FeedAudioToAR(ar, cueSheet, cuePath)) return;

                // Get metadata
                string artist = string.IsNullOrEmpty(cueSheet.Metadata.Artist) ? string.Empty : cueSheet.Metadata.Artist;
                string title = string.IsNullOrEmpty(cueSheet.Metadata.Title) ? string.Empty : cueSheet.Metadata.Title;
                string barcode = string.IsNullOrEmpty(cueSheet.Metadata.Barcode) ? string.Empty : cueSheet.Metadata.Barcode;
                
                // Construct userAgent
                string userAgent = isDryRun ? "ctdb-cli(dry-run)" : $"ctdb-cli({caller})";
                
                // Display submission information
                Console.WriteLine("------------------------------------------------------------");
                Console.WriteLine("Submit Information:");
                Console.WriteLine($"  TOC ID     :{toc.TOCID}");
                Console.WriteLine($"  Confidence :1 (fixed)");
                Console.WriteLine($"  Quality    :{quality}");
                Console.WriteLine($"  Artist     :{(string.IsNullOrEmpty(artist) ? "(empty)" : artist)}");
                Console.WriteLine($"  Title      :{(string.IsNullOrEmpty(title) ? "(empty)" : title)}");
                Console.WriteLine($"  Barcode    :{(string.IsNullOrEmpty(barcode) ? "(empty)" : barcode)}");
                Console.WriteLine($"  Drive      :{driveName}");
                Console.WriteLine($"  UserAgent  :{userAgent}");
                Console.WriteLine("------------------------------------------------------------");
                
                if (isDryRun)
                {
                    Console.WriteLine("[DRY-RUN] Would submit with above information.");
                    Console.WriteLine("To actually submit, set CTDB_CLI_CALLER environment variable.");
                    return;
                }

                Console.WriteLine("Contacting CTDB...");
                ctdb.ContactDB(null, userAgent, driveName, true, false, CTDBMetadataSearch.Default);

                if (ctdb.QueryExceptionStatus != WebExceptionStatus.Success && 
                    (ctdb.QueryExceptionStatus != WebExceptionStatus.ProtocolError || ctdb.QueryResponseStatus != HttpStatusCode.NotFound))
                {
                    Console.WriteLine($"Cannot upload: CTDB access failed. {ctdb.DBStatus}");
                    return;
                }

                Console.WriteLine("Submitting...");
                
                // confidence is fixed at 1
                var resp = ctdb.Submit(1, quality, artist, title, barcode);
                
                if (resp != null)
                {
                    Console.WriteLine($"Submit Result: {resp.status}");
                    Console.WriteLine($"Message: {resp.message}");
                    Console.WriteLine($"Parity Needed: {resp.ParityNeeded}");
                }
                else
                {
                    Console.WriteLine("Submit returned null.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during submit: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
            }
        }

        public void Repair(string cuePath)
        {
            Console.WriteLine($"Repairing: {cuePath}");
            try
            {
                var cueSheet = new CUESheet(_config);
                cueSheet.Open(cuePath);
                var toc = cueSheet.TOC;

                Console.WriteLine($"TOC ID: {toc.TOCID}");

                // Verification phase: same as Verify
                var ar = new AccurateRipVerify(toc, _config.GetProxy());
                var ctdb = new CUEToolsDB(toc, _config.GetProxy());
                ctdb.Init(ar);

                string? audioPath = FindAudioPath(cuePath);
                if (audioPath == null)
                {
                    Console.WriteLine("Error: Audio file not found.");
                    return;
                }

                // Determine output file path and check if it already exists
                string outputPath = Path.Combine(
                    Path.GetDirectoryName(audioPath) ?? ".",
                    Path.GetFileNameWithoutExtension(audioPath) + "_repaired.wav"
                );

                if (File.Exists(outputPath))
                {
                    Console.WriteLine($"Error: Output file already exists: {outputPath}");
                    Console.WriteLine("Please remove or rename the existing file and try again.");
                    return;
                }

                if (!FeedAudioToAR(ar, cueSheet, cuePath)) return;

                Console.WriteLine("Contacting CTDB...");
                ctdb.ContactDB(null, "ctdb-cli", null, true, false, CTDBMetadataSearch.Default);

                if (ctdb.Metadata == null && ctdb.Entries == null)
                {
                     Console.WriteLine($"CTDB returned {ctdb.QueryResponseStatus}");
                     return;
                }

                Console.WriteLine("Verifying...");
                ctdb.DoVerify();

                Console.WriteLine($"Status: {ctdb.Status}");

                // Find a repairable entry
                CUETools.CTDB.DBEntry? repairableEntry = null;
                foreach (var entry in ctdb.Entries)
                {
                    if (entry.hasErrors && entry.canRecover && entry.repair != null)
                    {
                        // Select the entry with the highest confidence
                        if (repairableEntry == null || entry.conf > repairableEntry.conf)
                        {
                            repairableEntry = entry;
                        }
                    }
                }

                if (repairableEntry == null)
                {
                    // No repairable entry found
                    bool hasAnyErrors = false;
                    foreach (var entry in ctdb.Entries)
                    {
                        if (entry.hasErrors)
                        {
                            hasAnyErrors = true;
                            break;
                        }
                    }

                    if (!hasAnyErrors)
                    {
                        Console.WriteLine("No errors detected. The file does not need repair.");
                    }
                    else
                    {
                        Console.WriteLine("Errors detected but cannot be recovered.");
                        Console.WriteLine("The CTDB does not have sufficient parity data to repair this file.");
                    }
                    return;
                }

                Console.WriteLine($"Found repairable entry (confidence: {repairableEntry.conf}):");
                Console.WriteLine($"  Correctable Errors: {repairableEntry.repair.CorrectableErrors}");
                Console.WriteLine($"  Affected Sectors: {repairableEntry.repair.AffectedSectors}");

                // Repair and write phase
                Console.WriteLine($"Writing repaired audio to: {outputPath}");

                var audioSource = AudioReadWrite.GetAudioSource(audioPath, null, _config);
                if (audioSource == null)
                {
                    Console.WriteLine("Error: Failed to open audio source for repair.");
                    return;
                }

                var pcm = new AudioPCMConfig(16, 2, 44100);
                var encoderSettings = new CUETools.Codecs.WAV.EncoderSettings(pcm);
                var audioDest = new CUETools.Codecs.WAV.AudioEncoder(encoderSettings, outputPath, null);
                audioDest.FinalSampleCount = audioSource.Length;

                var buffer = new AudioBuffer(audioSource, 1024 * 64);
                long samplesWritten = 0;

                while (audioSource.Read(buffer, -1) != 0)
                {
                    // Fix errors in the buffer using repair.Write()
                    repairableEntry.repair.Write(buffer);
                    audioDest.Write(buffer);
                    samplesWritten += buffer.Length;
                }

                repairableEntry.repair.Close();
                audioDest.Close();
                audioSource.Close();

                Console.WriteLine();
                Console.WriteLine($"Repair completed successfully!");
                Console.WriteLine($"Output: {outputPath}");
                Console.WriteLine($"Samples written: {samplesWritten}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during repair: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
            }
        }

        // Helper to get the audio file path
        private string? FindAudioPath(string cuePath)
        {
            string? audioPath = null;
            var lines = File.ReadAllLines(cuePath);
            foreach (var line in lines)
            {
                if (line.Trim().StartsWith("FILE"))
                {
                    var parts = line.Trim().Split('"');
                    if (parts.Length >= 2)
                    {
                        audioPath = Path.Combine(Path.GetDirectoryName(cuePath) ?? ".", parts[1]);
                        break;
                    }
                }
            }

            if (audioPath == null || !File.Exists(audioPath))
            {
                var altPath = Path.ChangeExtension(cuePath, ".wav");
                if (File.Exists(altPath)) audioPath = altPath;
            }

            if (audioPath != null && File.Exists(audioPath))
            {
                return audioPath;
            }
            return null;
        }
    }
}
