using System.Collections.Generic;

namespace MiNET.Net
{
	public readonly record struct DiagnosticMemoryCounter(byte Category, long CurrentBytes);
	public readonly record struct EntityDiagnosticTiming(string DisplayName, string Entity, long TimeNanoseconds, byte PercentOfTotal);
	public readonly record struct SystemDiagnosticTiming(string DisplayName, long SystemIndex, long TimeNanoseconds, byte PercentOfTotal);
	public readonly record struct WhiskerScopeSummary(string Label, string Indentation, long TotalHighCostNanoseconds, long TotalMidCostNanoseconds, long TotalLowCostNanoseconds);

	public sealed class ServerboundDiagnosticsTail : IPacketDataObject
	{
		public List<DiagnosticMemoryCounter> MemoryCategories { get; } = new();
		public List<EntityDiagnosticTiming> EntityTimings { get; } = new();
		public List<SystemDiagnosticTiming> SystemTimings { get; } = new();
		public List<WhiskerScopeSummary> WhiskerScopes { get; } = new();

		public void Write(Packet packet)
		{
			packet.WriteLength(MemoryCategories.Count);
			foreach (var value in MemoryCategories)
			{
				packet.Write(value.Category);
				packet.Write(value.CurrentBytes);
			}

			packet.WriteLength(EntityTimings.Count);
			foreach (var value in EntityTimings)
			{
				packet.Write(value.DisplayName);
				packet.Write(value.Entity);
				packet.Write(value.TimeNanoseconds);
				packet.Write(value.PercentOfTotal);
			}

			packet.WriteLength(SystemTimings.Count);
			foreach (var value in SystemTimings)
			{
				packet.Write(value.DisplayName);
				packet.Write(value.SystemIndex);
				packet.Write(value.TimeNanoseconds);
				packet.Write(value.PercentOfTotal);
			}

			packet.WriteLength(WhiskerScopes.Count);
			foreach (var value in WhiskerScopes)
			{
				packet.Write(value.Label);
				packet.Write(value.Indentation);
				packet.Write(value.TotalHighCostNanoseconds);
				packet.Write(value.TotalMidCostNanoseconds);
				packet.Write(value.TotalLowCostNanoseconds);
			}
		}

		public static ServerboundDiagnosticsTail Read(Packet packet)
		{
			var result = new ServerboundDiagnosticsTail();
			for (var index = 0; index < packet.ReadLength(); index++)
				result.MemoryCategories.Add(new DiagnosticMemoryCounter(packet.ReadByte(), packet.ReadLong()));
			for (var index = 0; index < packet.ReadLength(); index++)
				result.EntityTimings.Add(new EntityDiagnosticTiming(packet.ReadString(), packet.ReadString(), packet.ReadLong(), packet.ReadByte()));
			for (var index = 0; index < packet.ReadLength(); index++)
				result.SystemTimings.Add(new SystemDiagnosticTiming(packet.ReadString(), packet.ReadLong(), packet.ReadLong(), packet.ReadByte()));
			for (var index = 0; index < packet.ReadLength(); index++)
				result.WhiskerScopes.Add(new WhiskerScopeSummary(packet.ReadString(), packet.ReadString(), packet.ReadLong(), packet.ReadLong(), packet.ReadLong()));
			return result;
		}
	}
}