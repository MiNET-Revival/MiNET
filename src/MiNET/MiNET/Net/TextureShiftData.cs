using System.Collections.Generic;

namespace MiNET.Net
{
	public enum TextureShiftAction : byte
	{
		Invalid,
		Initialize,
		Start,
		SetEnabled,
		Sync
	}

	public sealed class TextureShiftData : IPacketDataObject
	{
		public TextureShiftAction Action { get; set; }
		public string CollectionName { get; set; } = string.Empty;
		public string FromStep { get; set; } = string.Empty;
		public string ToStep { get; set; } = string.Empty;
		public List<string> AllSteps { get; } = new();
		public long CurrentLengthTicks { get; set; }
		public long TotalLengthTicks { get; set; }
		public bool Enabled { get; set; }

		public void Write(Packet packet)
		{
			packet.Write((byte) Action);
			packet.Write(CollectionName);
			packet.Write(FromStep);
			packet.Write(ToStep);
			packet.WriteUnsignedVarInt((uint) AllSteps.Count);
			foreach (string step in AllSteps) packet.Write(step);
			packet.WriteUnsignedVarLong(CurrentLengthTicks);
			packet.WriteUnsignedVarLong(TotalLengthTicks);
			packet.Write(Enabled);
		}

		public static TextureShiftData Read(Packet packet)
		{
			var result = new TextureShiftData
			{
				Action = (TextureShiftAction) packet.ReadByte(),
				CollectionName = packet.ReadString(),
				FromStep = packet.ReadString(),
				ToStep = packet.ReadString()
			};
			int count = (int) packet.ReadUnsignedVarInt();
			for (int i = 0; i < count; i++) result.AllSteps.Add(packet.ReadString());
			result.CurrentLengthTicks = packet.ReadUnsignedVarLong();
			result.TotalLengthTicks = packet.ReadUnsignedVarLong();
			result.Enabled = packet.ReadBool();
			return result;
		}
	}
}
