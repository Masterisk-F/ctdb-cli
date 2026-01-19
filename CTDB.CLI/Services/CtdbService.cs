using System;
using System.Collections.Generic;
using CUETools.CTDB;
using CUETools.CDImage;
using CUETools.Processor;
using CUETools.AccurateRip;
using CUETools.Codecs;
using System.Net;
using System.IO;
using CTDB.CLI.Models;

namespace CTDB.CLI.Services
{
    public class CtdbService
    {
        private readonly CUEConfig _config;
        private readonly TextWriter _logger;

        public CtdbService(TextWriter? logger = null)
        {
            _logger = logger ?? Console.Out;
            // Register FLAC decoder manually (to avoid relying on plugins folder structure in published builds)
            if (!CUEProcessorPlugins.decs.Exists(d => d is CUETools.Codecs.Flake.DecoderSettings))
            {
                CUEProcessorPlugins.decs.Add(new CUETools.Codecs.Flake.DecoderSettings());
            }
            _config = new CUEConfig();
        }

        public LookupResult? Lookup(string cuePath)
        {
            _logger.WriteLine($"Processing CUE file: {cuePath}");

            try
            {
                var cueSheet = new CUESheet(_config);
                cueSheet.Open(cuePath);

                var toc = cueSheet.TOC;
                _logger.WriteLine($"TOC ID: {toc.TOCID}");
                _logger.WriteLine($"Track Count: {toc.AudioTracks}");

                string urlbase = "http://db.cuetools.net";
                string url = urlbase
                    + "/lookup2.php"
                    + "?version=3"
                    + "&ctdb=1"
                    + "&fuzzy=1"
                    + "&metadata=default"
                    + "&toc=" + toc.ToString();
                
                _logger.WriteLine($"Fetching XML from: {url}");
                
                var request = (HttpWebRequest)WebRequest.Create(url);
                request.Proxy = _config.GetProxy();
                request.UserAgent = "ctdb-cli (" + Environment.OSVersion.VersionString + ")";
                
                using (var response = (HttpWebResponse)request.GetResponse())
                using (var stream = response.GetResponseStream())
                using (var reader = new StreamReader(stream))
                {
                    string xml = reader.ReadToEnd();
                    _logger.WriteLine(xml);
                    return new LookupResult { RawXml = xml };
                }
            }
            catch (Exception ex)
            {
                _logger.WriteLine($"Error during lookup: {ex.Message}");
                return null;
            }
        }

        public CalcResult? CalculateParity(string cuePath)
        {
            _logger.WriteLine($"Calculating parity for: {cuePath}");
            try
            {
                var cueSheet = new CUESheet(_config);
                cueSheet.Open(cuePath);
                var toc = cueSheet.TOC;

                _logger.WriteLine($"TOC ID: {toc.TOCID}");

                var ar = new AccurateRipVerify(toc, _config.GetProxy());
                if (!FeedAudioToAR(ar, cueSheet, cuePath)) return null;

                var result = new CalcResult
                {
                    TocId = toc.TOCID,
                    CtdbCrc = ar.CTDBCRC(0).ToString("x8"),
                    Tracks = new List<TrackCalcResult>()
                };

                _logger.WriteLine($"CTDB CRC: {result.CtdbCrc}");
                for (int i = 0; i < toc.AudioTracks; i++)
                {
                    var trackResult = new TrackCalcResult
                    {
                        Number = i + 1,
                        Crc = ar.CRC(i).ToString("x8"),
                        Crc32 = ar.CRC32(i).ToString("x8")
                    };
                    result.Tracks.Add(trackResult);
                    _logger.WriteLine($"Track {trackResult.Number} CRC: {trackResult.Crc}  CRC32: {trackResult.Crc32}");
                }
                return result;
            }
            catch (Exception ex)
            {
                _logger.WriteLine($"Error during calculation: {ex.Message}");
                _logger.WriteLine(ex.StackTrace);
                return null;
            }
        }

        public VerifyResult? Verify(string cuePath)
        {
            _logger.WriteLine($"Verifying: {cuePath}");
            try
            {
                var cueSheet = new CUESheet(_config);
                cueSheet.Open(cuePath);
                var toc = cueSheet.TOC;

                _logger.WriteLine($"TOC ID: {toc.TOCID}");

                var ar = new AccurateRipVerify(toc, _config.GetProxy());
                var ctdb = new CUEToolsDB(toc, _config.GetProxy());
                ctdb.Init(ar); // Initialize for parity validation BEFORE feeding data

                if (!FeedAudioToAR(ar, cueSheet, cuePath)) return null;

                _logger.WriteLine("Contacting CTDB...");
                ctdb.ContactDB(null, "ctdb-cli", null, true, false, CTDBMetadataSearch.Default);

                if (ctdb.Metadata == null && ctdb.Entries == null)
                {
                     _logger.WriteLine($"CTDB returned {ctdb.QueryResponseStatus}");
                }

                _logger.WriteLine("Verifying...");
                ctdb.DoVerify();
                
                _logger.WriteLine($"Confidence: {ctdb.Confidence}");
                _logger.WriteLine($"Status: {ctdb.Status}");
                _logger.WriteLine($"Total Entries: {ctdb.Total}");
                
                var result = new VerifyResult
                {
                    Toc = toc.TOCID,
                    Status = ctdb.Status.ToString(),
                    Confidence = ctdb.Confidence,
                    TotalEntries = ctdb.Total,
                    Entries = new List<VerifyEntry>()
                };

                int entryIndex = 0;
                foreach(var entry in ctdb.Entries)
                {
                    entryIndex++;
                    _logger.WriteLine($"------------------------------------------------------------");
                    _logger.WriteLine($"Entry {entryIndex}:");
                    _logger.WriteLine($"  Conf: {entry.conf}, CRC: {entry.crc:x8}, Offset: {entry.offset}");
                    _logger.WriteLine($"  Status: {entry.Status}");
                    _logger.WriteLine($"  HasErrors: {entry.hasErrors}, CanRecover: {entry.canRecover}");
                    _logger.WriteLine($"  TOC: {entry.toc.TOCID}");

                    var verifyEntry = new VerifyEntry
                    {
                        Id = entryIndex.ToString(), // Or should we use some other ID?
                        Confidence = entry.conf,
                        Crc = entry.crc.ToString("x8"),
                        Offset = entry.offset,
                        Status = entry.Status.ToString(),
                        HasErrors = entry.hasErrors,
                        CanRecover = entry.canRecover,
                        Tracks = new List<TrackVerifyResult>()
                    };

                    if (entry.trackcrcs != null)
                    {
                        _logger.WriteLine($"  Track CRCs:");
                        for (int i = 0; i < entry.trackcrcs.Length; i++)
                        {
                            uint localCrc = ctdb.Verify.TrackCRC(i + 1, -entry.offset);
                            uint remoteCrc = entry.trackcrcs[i];
                            bool matched = (localCrc == remoteCrc);
                            string matchStatus = matched ? "Matched" : "Unmatched";
                            
                            var trackResult = new TrackVerifyResult
                            {
                                Number = i + 1,
                                LocalCrc = localCrc.ToString("x8"),
                                RemoteCrc = remoteCrc.ToString("x8"),
                                Matched = matched
                            };
                            verifyEntry.Tracks.Add(trackResult);
                            _logger.WriteLine($"    Track {trackResult.Number:00}: Local={trackResult.LocalCrc} Remote={trackResult.RemoteCrc} [{matchStatus}]");
                        }
                    }

                    if (entry.hasErrors && entry.canRecover && entry.repair != null)
                    {
                        _logger.WriteLine($"  Repair Info:");
                        _logger.WriteLine($"    Correctable Errors: {entry.repair.CorrectableErrors}");
                        _logger.WriteLine($"    Affected Sectors: {entry.repair.AffectedSectors}");
                        verifyEntry.Repair = new RepairInfo
                        {
                            CorrectableErrors = entry.repair.CorrectableErrors,
                            AffectedSectors = entry.repair.AffectedSectors
                        };
                    }
                    result.Entries.Add(verifyEntry);
                }
                return result;
            }
            catch (Exception ex)
            {
                _logger.WriteLine($"Error during verification: {ex.Message}");
                _logger.WriteLine(ex.StackTrace);
                return null;
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
                    _logger.WriteLine("Audio file not found.");
                    return false;
                }

                _logger.WriteLine($"Processing audio file: {audioPath}");
                
                var audioSource = AudioReadWrite.GetAudioSource(audioPath, null, _config);
                if (audioSource == null) {
                    _logger.WriteLine("Failed to open audio source.");
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
                    _logger.WriteLine($"\nWarning: Audio file shorter than TOC. Padding {ar.FinalSampleCount - ar.Position} samples.");
                    Array.Clear(buffer.Bytes, 0, buffer.Bytes.Length);
                    while (ar.Position < ar.FinalSampleCount)
                    {
                        int remaining = (int)Math.Min(buffer.Bytes.Length / buffer.PCM.BlockAlign, ar.FinalSampleCount - ar.Position);
                        buffer.Length = remaining;
                        ar.Write(buffer);
                    }
                }
                _logger.WriteLine($"Done. Final Position: {ar.Position}, Expected: {ar.FinalSampleCount}");
                return true;
        }

        public SubmitResult? Submit(string cuePath, string driveName, int quality)
        {
            _logger.WriteLine($"Submitting: {cuePath}");
            
            // Dry-run control via CTDB_CLI_CALLER environment variable
            string? caller = Environment.GetEnvironmentVariable("CTDB_CLI_CALLER");
            bool isDryRun = string.IsNullOrEmpty(caller);
            
            if (isDryRun)
            {
                _logger.WriteLine("[DRY-RUN MODE] CTDB_CLI_CALLER environment variable is not set.");
            }
            
            try
            {
                var cueSheet = new CUESheet(_config);
                cueSheet.Open(cuePath);
                var toc = cueSheet.TOC;

                _logger.WriteLine($"TOC ID: {toc.TOCID}");

                var ar = new AccurateRipVerify(toc, _config.GetProxy());
                var ctdb = new CUEToolsDB(toc, _config.GetProxy());
                ctdb.Init(ar); // Initialize, enables parity calc

                if (!FeedAudioToAR(ar, cueSheet, cuePath)) return null;

                // Get metadata
                string artist = string.IsNullOrEmpty(cueSheet.Metadata.Artist) ? string.Empty : cueSheet.Metadata.Artist;
                string title = string.IsNullOrEmpty(cueSheet.Metadata.Title) ? string.Empty : cueSheet.Metadata.Title;
                string barcode = string.IsNullOrEmpty(cueSheet.Metadata.Barcode) ? string.Empty : cueSheet.Metadata.Barcode;
                
                var result = new SubmitResult
                {
                    Metadata = new SubmittedMetadata
                    {
                        Artist = artist,
                        Title = title,
                        Barcode = barcode,
                        Drive = driveName,
                        Quality = quality
                    }
                };

                // Construct userAgent
                string userAgent = isDryRun ? "ctdb-cli(dry-run)" : $"ctdb-cli({caller})";
                
                // Display submission information
                _logger.WriteLine("------------------------------------------------------------");
                _logger.WriteLine("Submit Information:");
                _logger.WriteLine($"  TOC ID     :{toc.TOCID}");
                _logger.WriteLine($"  Confidence :1 (fixed)");
                _logger.WriteLine($"  Quality    :{quality}");
                _logger.WriteLine($"  Artist     :{(string.IsNullOrEmpty(artist) ? "(empty)" : artist)}");
                _logger.WriteLine($"  Title      :{(string.IsNullOrEmpty(title) ? "(empty)" : title)}");
                _logger.WriteLine($"  Barcode    :{(string.IsNullOrEmpty(barcode) ? "(empty)" : barcode)}");
                _logger.WriteLine($"  Drive      :{driveName}");
                _logger.WriteLine($"  UserAgent  :{userAgent}");
                _logger.WriteLine("------------------------------------------------------------");
                
                if (isDryRun)
                {
                    _logger.WriteLine("[DRY-RUN] Would submit with above information.");
                    _logger.WriteLine("To actually submit, set CTDB_CLI_CALLER environment variable.");
                    result.Response = new SubmitResponse { Status = "dry-run", Message = "Dry-run mode" };
                    return result;
                }

                _logger.WriteLine("Contacting CTDB...");
                ctdb.ContactDB(null, userAgent, driveName, true, false, CTDBMetadataSearch.Default);

                if (ctdb.QueryExceptionStatus != WebExceptionStatus.Success && 
                    (ctdb.QueryExceptionStatus != WebExceptionStatus.ProtocolError || ctdb.QueryResponseStatus != HttpStatusCode.NotFound))
                {
                    _logger.WriteLine($"Cannot upload: CTDB access failed. {ctdb.DBStatus}");
                    result.Response = new SubmitResponse { Status = "error", Message = $"CTDB access failed: {ctdb.DBStatus}" };
                    return result;
                }

                _logger.WriteLine("Submitting...");
                
                // confidence is fixed at 1
                var resp = ctdb.Submit(1, quality, artist, title, barcode);
                
                if (resp != null)
                {
                    _logger.WriteLine($"Submit Result: {resp.status}");
                    _logger.WriteLine($"Message: {resp.message}");
                    _logger.WriteLine($"Parity Needed: {resp.ParityNeeded}");
                    _logger.WriteLine($"Raw SubStatus: {ctdb.SubStatus}");

                    result.Response = new SubmitResponse
                    {
                        Status = resp.status,
                        Message = resp.message,
                        ParityNeeded = resp.ParityNeeded
                    };
                }
                else
                {
                    _logger.WriteLine("Submit returned null.");
                    result.Response = new SubmitResponse { Status = "error", Message = "Submit returned null" };
                }
                return result;
            }
            catch (Exception ex)
            {
                _logger.WriteLine($"Error during submit: {ex.Message}");
                _logger.WriteLine(ex.StackTrace);
                return null;
            }
        }

        public RepairResult? Repair(string cuePath)
        {
            _logger.WriteLine($"Repairing: {cuePath}");
            try
            {
                var cueSheet = new CUESheet(_config);
                cueSheet.Open(cuePath);
                var toc = cueSheet.TOC;

                _logger.WriteLine($"TOC ID: {toc.TOCID}");

                // Verification phase: same as Verify
                var ar = new AccurateRipVerify(toc, _config.GetProxy());
                var ctdb = new CUEToolsDB(toc, _config.GetProxy());
                ctdb.Init(ar);

                string? audioPath = FindAudioPath(cuePath);
                if (audioPath == null)
                {
                    _logger.WriteLine("Error: Audio file not found.");
                    return null;
                }

                // Determine output file path and check if it already exists
                string outputPath = Path.Combine(
                    Path.GetDirectoryName(audioPath) ?? ".",
                    Path.GetFileNameWithoutExtension(audioPath) + "_repaired.wav"
                );

                if (File.Exists(outputPath))
                {
                    _logger.WriteLine($"Error: Output file already exists: {outputPath}");
                    _logger.WriteLine("Please remove or rename the existing file and try again.");
                    return null;
                }

                if (!FeedAudioToAR(ar, cueSheet, cuePath)) return null;

                _logger.WriteLine("Contacting CTDB...");
                ctdb.ContactDB(null, "ctdb-cli", null, true, false, CTDBMetadataSearch.Default);

                if (ctdb.Metadata == null && ctdb.Entries == null)
                {
                     _logger.WriteLine($"CTDB returned {ctdb.QueryResponseStatus}");
                     return null;
                }

                _logger.WriteLine("Verifying...");
                ctdb.DoVerify();

                _logger.WriteLine($"Status: {ctdb.Status}");

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

                var result = new RepairResult
                {
                    Entries = new List<VerifyEntry>()
                };

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
                        _logger.WriteLine("No errors detected. The file does not need repair.");
                        result.Status = "no errors";
                    }
                    else
                    {
                        _logger.WriteLine("Errors detected but cannot be recovered.");
                        _logger.WriteLine("The CTDB does not have sufficient parity data to repair this file.");
                        result.Status = "not recoverable";
                    }
                }
                else
                {
                    _logger.WriteLine($"Found repairable entry (confidence: {repairableEntry.conf}):");
                    _logger.WriteLine($"  Correctable Errors: {repairableEntry.repair.CorrectableErrors}");
                    _logger.WriteLine($"  Affected Sectors: {repairableEntry.repair.AffectedSectors}");
                    result.Status = "repairing";
                }

                // Populate all entries with used_for_repair flag
                int entryIndex = 0;
                foreach (var entry in ctdb.Entries)
                {
                    entryIndex++;
                    var verifyEntry = new VerifyEntry
                    {
                        Id = entryIndex.ToString(),
                        Confidence = entry.conf,
                        Crc = entry.crc.ToString("x8"),
                        Offset = entry.offset,
                        Status = entry.Status.ToString(),
                        HasErrors = entry.hasErrors,
                        CanRecover = entry.canRecover,
                        UsedForRepair = (entry == repairableEntry), // Set flag if this entry was used
                        Tracks = new List<TrackVerifyResult>()
                    };

                    if (entry.trackcrcs != null)
                    {
                        for (int i = 0; i < entry.trackcrcs.Length; i++)
                        {
                            uint localCrc = ctdb.Verify.TrackCRC(i + 1, -entry.offset);
                            uint remoteCrc = entry.trackcrcs[i];
                            verifyEntry.Tracks.Add(new TrackVerifyResult
                            {
                                Number = i + 1,
                                LocalCrc = localCrc.ToString("x8"),
                                RemoteCrc = remoteCrc.ToString("x8"),
                                Matched = (localCrc == remoteCrc)
                            });
                        }
                    }

                    if (entry.hasErrors && entry.canRecover && entry.repair != null)
                    {
                        verifyEntry.Repair = new RepairInfo
                        {
                            CorrectableErrors = entry.repair.CorrectableErrors,
                            AffectedSectors = entry.repair.AffectedSectors
                        };
                    }
                    result.Entries.Add(verifyEntry);
                }

                if (repairableEntry == null)
                {
                    return result;
                }

                // Repair and write phase
                _logger.WriteLine($"Writing repaired audio to: {outputPath}");

                var audioSource = AudioReadWrite.GetAudioSource(audioPath, null, _config);
                if (audioSource == null)
                {
                    _logger.WriteLine("Error: Failed to open audio source for repair.");
                    return result;
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

                _logger.WriteLine();
                _logger.WriteLine($"Repair completed successfully!");
                _logger.WriteLine($"Output: {outputPath}");
                _logger.WriteLine($"Samples written: {samplesWritten}");

                result.Status = "success";
                result.SamplesWritten = samplesWritten;
                return result;
            }
            catch (Exception ex)
            {
                _logger.WriteLine($"Error during repair: {ex.Message}");
                _logger.WriteLine(ex.StackTrace);
                return null;
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
