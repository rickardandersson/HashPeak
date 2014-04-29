using System.Collections.Generic;
using Newtonsoft.Json;

namespace RA.HashPeak
{
	class Request
	{
		[JsonProperty(PropertyName = "command")]
		public string Command { get; set; }
		[JsonProperty(PropertyName = "parameter", NullValueHandling = NullValueHandling.Ignore)]
		public string Parameter { get; set; }
	}

	class StatusSection
	{
		public string Status { get; set; }
		public long When { get; set; }
		public int Code { get; set; }
		public string Msg { get; set; }
		public string Description { get; set; }
	}

	class VersionSection
	{
		public string CGMiner { get; set; }
		public string SGMiner { get; set; }
		public string Api { get; set; }
	}

	class GpuSection
	{
		public int GPU { get; set; }
		public string Enabled { get; set; }
		public string Status { get; set; }
		public double Temperature { get; set; }
		[JsonProperty(PropertyName = "Fan Speed")]
		public int FanSpeed { get; set; }
		[JsonProperty(PropertyName = "Fan Percent")]
		public int FanPercent { get; set; }
		[JsonProperty(PropertyName = "GPU Clock")]
		public int GpuClock { get; set; }
		[JsonProperty(PropertyName = "Memory Clock")]
		public int MemoryClock { get; set; }
		[JsonProperty(PropertyName = "GPU Voltage")]
		public double GpuVoltage { get; set; }
		[JsonProperty(PropertyName = "GPU Activity")]
		public int GpuActivity { get; set; }
		public int Powertune { get; set; }
		[JsonProperty(PropertyName = "MHS av")]
		public double MhsAv { get; set; }
		// This property name varies (!) depending on what the log interval is set to in cgminer/sgminer.
		// We solve this by some search and replace magic in Send().
		[JsonProperty(PropertyName = "MHS Xs")]
		public double MhsXs { get; set; }
		public int Accepted { get; set; }
		public int Rejected { get; set; }
		[JsonProperty(PropertyName = "Hardware Errors")]
		public int HardwareErrors { get; set; }
		public double Utility { get; set; }
		public string Intensity { get; set; }
		[JsonProperty(PropertyName = "Last Share Pool")]
		public int LastSharePool { get; set; }
		[JsonProperty(PropertyName = "Last Share Time")]
		public long LastShareTime { get; set; }
		[JsonProperty(PropertyName = "Total MH")]
		public double TotalMh { get; set; }
		[JsonProperty(PropertyName = "Diff1 Work")]
		public int Diff1Work { get; set; }
		[JsonProperty(PropertyName = "Difficulty Accepted")]
		public double DifficultyAccepted { get; set; }
		[JsonProperty(PropertyName = "Difficulty Rejected")]
		public double DifficultyRejected { get; set; }
		[JsonProperty(PropertyName = "Last Share Difficulty")]
		public double LastShareDifficulty { get; set; }
		[JsonProperty(PropertyName = "Last Valid Work")]
		public long LastValidWork { get; set; }
		[JsonProperty(PropertyName = "Device Hardware%")]
		public double DeviceHardwarePercent { get; set; }
		[JsonProperty(PropertyName = "Device Rejected%")]
		public double DeviceRejectedPercent { get; set; }
		[JsonProperty(PropertyName = "Device Elapsed")]
		public int DeviceElapsed { get; set; }
	}

	class VersionResponse
	{
		public List<StatusSection> Status { get; set; }
		public List<VersionSection> Version { get; set; }
		public int Id { get; set; }
	}

	class GpuResponse
	{
		public List<StatusSection> Status { get; set; }
		public List<GpuSection> Gpu { get; set; }
		public int Id { get; set; }
	}

	class StatusResponse
	{
		public List<StatusSection> Status { get; set; }
		public int Id { get; set; }
	}
}
