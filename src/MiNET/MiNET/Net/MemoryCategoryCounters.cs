using System.Collections.Generic;

namespace MiNET.Net
{
	public sealed class MemoryCategoryCounter
	{
		public byte Category { get; set; }
		public ulong Bytes { get; set; }
	}

	public sealed class MemoryCategoryCounters : IPacketDataObject
	{
		public List<MemoryCategoryCounter> Values { get; } = new();

		public void Write(Packet packet)
		{
			packet.WriteUnsignedVarInt((uint) Values.Count);
			foreach (MemoryCategoryCounter value in Values)
			{
				packet.Write(value.Category);
				packet.Write(value.Bytes);
			}
		}

		public static MemoryCategoryCounters Read(Packet packet)
		{
			var result = new MemoryCategoryCounters();
			int count = (int) packet.ReadUnsignedVarInt();
			for (int i = 0; i < count; i++)
			{
				result.Values.Add(new MemoryCategoryCounter { Category = packet.ReadByte(), Bytes = packet.ReadUlong() });
			}
			return result;
		}
	}
}
