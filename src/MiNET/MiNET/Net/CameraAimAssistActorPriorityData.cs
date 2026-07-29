using System.Collections.Generic;

namespace MiNET.Net
{
	public sealed class CameraAimAssistActorPriorityData
	{
		public int PresetIndex { get; set; }
		public int CategoryIndex { get; set; }
		public int ActorIndex { get; set; }
		public int Priority { get; set; }
	}

	public sealed class CameraAimAssistActorPriorityDataList : IPacketDataObject
	{
		public List<CameraAimAssistActorPriorityData> Values { get; } = new();

		public void Write(Packet packet)
		{
			packet.WriteUnsignedVarInt((uint) Values.Count);
			foreach (CameraAimAssistActorPriorityData value in Values)
			{
				packet.Write(value.PresetIndex);
				packet.Write(value.CategoryIndex);
				packet.Write(value.ActorIndex);
				packet.Write(value.Priority);
			}
		}

		public static CameraAimAssistActorPriorityDataList Read(Packet packet)
		{
			var result = new CameraAimAssistActorPriorityDataList();
			int count = (int) packet.ReadUnsignedVarInt();
			for (int i = 0; i < count; i++)
			{
				result.Values.Add(new CameraAimAssistActorPriorityData
				{
					PresetIndex = packet.ReadInt(),
					CategoryIndex = packet.ReadInt(),
					ActorIndex = packet.ReadInt(),
					Priority = packet.ReadInt()
				});
			}
			return result;
		}
	}
}
