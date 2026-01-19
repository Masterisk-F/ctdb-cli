using System;
using System.Collections.Generic;
using System.Xml.Serialization;

namespace CTDB.CLI.Models
{
    [XmlRoot("ctdb", Namespace = "http://db.cuetools.net/ns/mmd-1.0#")]
    public class CtdbXmlResult
    {
        [XmlElement("lookup_result", Namespace = "")]
        public LookupResult? Lookup { get; set; }

        [XmlElement("verify_result", Namespace = "")]
        public VerifyResult? Verify { get; set; }

        [XmlElement("submit_result", Namespace = "")]
        public SubmitResult? Submit { get; set; }

        [XmlElement("repair_result", Namespace = "")]
        public RepairResult? Repair { get; set; }

        [XmlElement("calc_result", Namespace = "")]
        public CalcResult? Calc { get; set; }
    }

    public class LookupResult
    {
        [XmlText]
        public string? RawXml { get; set; }
    }

    public class VerifyResult
    {
        [XmlAttribute("toc")]
        public string? Toc { get; set; }

        [XmlAttribute("status")]
        public string? Status { get; set; }

        [XmlAttribute("message")]
        public string? Message { get; set; }

        [XmlAttribute("confidence")]
        public int Confidence { get; set; }

        [XmlAttribute("total_entries")]
        public int TotalEntries { get; set; }

        [XmlElement("entry")]
        public List<VerifyEntry>? Entries { get; set; }
    }

    public class VerifyEntry
    {
        [XmlAttribute("id")]
        public string? Id { get; set; }

        [XmlAttribute("conf")]
        public int Confidence { get; set; }

        [XmlAttribute("crc")]
        public string? Crc { get; set; }

        [XmlAttribute("offset")]
        public int Offset { get; set; }

        [XmlAttribute("status")]
        public string? Status { get; set; }

        [XmlAttribute("has_errors")]
        public bool HasErrors { get; set; }

        [XmlAttribute("can_recover")]
        public bool CanRecover { get; set; }

        [XmlAttribute("used_for_repair")]
        public bool UsedForRepair { get; set; }

        [XmlElement("track")]
        public List<TrackVerifyResult>? Tracks { get; set; }

        [XmlElement("repair")]
        public RepairInfo? Repair { get; set; }
    }

    public class TrackVerifyResult
    {
        [XmlAttribute("number")]
        public int Number { get; set; }

        [XmlAttribute("local_crc")]
        public string? LocalCrc { get; set; }

        [XmlAttribute("remote_crc")]
        public string? RemoteCrc { get; set; }

        [XmlAttribute("matched")]
        public bool Matched { get; set; }
    }

    public class RepairInfo
    {
        [XmlAttribute("correctable_errors")]
        public long CorrectableErrors { get; set; }

        [XmlAttribute("affected_sectors")]
        public string? AffectedSectors { get; set; }
    }

    public class SubmitResult
    {
        [XmlAttribute("status")]
        public string? Status { get; set; }

        [XmlAttribute("message")]
        public string? Message { get; set; }

        [XmlElement("submitted_metadata")]
        public SubmittedMetadata? Metadata { get; set; }

        [XmlElement("response")]
        public SubmitResponse? Response { get; set; }

        [XmlElement("raw_response")]
        public string? RawResponse { get; set; }
    }

    public class SubmittedMetadata
    {
        [XmlAttribute("artist")]
        public string? Artist { get; set; }

        [XmlAttribute("title")]
        public string? Title { get; set; }

        [XmlAttribute("barcode")]
        public string? Barcode { get; set; }

        [XmlAttribute("drive")]
        public string? Drive { get; set; }

        [XmlAttribute("quality")]
        public int Quality { get; set; }
    }

    public class SubmitResponse
    {
        [XmlAttribute("status")]
        public string? Status { get; set; }

        [XmlAttribute("message")]
        public string? Message { get; set; }

        [XmlAttribute("parity_needed")]
        public bool ParityNeeded { get; set; }
    }

    public class RepairResult
    {
        [XmlAttribute("status")]
        public string? Status { get; set; }

        [XmlAttribute("message")]
        public string? Message { get; set; }

        [XmlAttribute("output_path")]
        public string? OutputPath { get; set; }

        [XmlAttribute("samples_written")]
        public long SamplesWritten { get; set; }

        [XmlElement("entry")]
        public List<VerifyEntry>? Entries { get; set; }
    }

    public class CalcResult
    {
        [XmlAttribute("status")]
        public string? Status { get; set; }

        [XmlAttribute("message")]
        public string? Message { get; set; }

        [XmlAttribute("toc_id")]
        public string? TocId { get; set; }

        [XmlAttribute("ctdb_crc")]
        public string? CtdbCrc { get; set; }

        [XmlElement("track")]
        public List<TrackCalcResult>? Tracks { get; set; }
    }

    public class TrackCalcResult
    {
        [XmlAttribute("number")]
        public int Number { get; set; }

        [XmlAttribute("crc")]
        public string? Crc { get; set; }

        [XmlAttribute("crc32")]
        public string? Crc32 { get; set; }
    }
}
