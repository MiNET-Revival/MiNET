using System.Collections.Generic;

namespace MiNET.Net
{
	public sealed class VoxelShape
	{
		public byte XSize { get; set; }
		public byte YSize { get; set; }
		public byte ZSize { get; set; }
		public List<byte> Storage { get; } = new();
		public List<float> XCoordinates { get; } = new();
		public List<float> YCoordinates { get; } = new();
		public List<float> ZCoordinates { get; } = new();
	}

	public sealed class VoxelShapeNameEntry
	{
		public string Name { get; set; } = string.Empty;
		public ushort Id { get; set; }
	}

	public sealed class VoxelShapesData : IPacketDataObject
	{
		public List<VoxelShape> Shapes { get; } = new();
		public List<VoxelShapeNameEntry> Names { get; } = new();

		public void Write(Packet packet)
		{
			packet.WriteUnsignedVarInt((uint) Shapes.Count);
			foreach (VoxelShape shape in Shapes)
			{
				packet.Write(shape.XSize);
				packet.Write(shape.YSize);
				packet.Write(shape.ZSize);
				WriteBytes(packet, shape.Storage);
				WriteFloats(packet, shape.XCoordinates);
				WriteFloats(packet, shape.YCoordinates);
				WriteFloats(packet, shape.ZCoordinates);
			}
			packet.WriteUnsignedVarInt((uint) Names.Count);
			foreach (VoxelShapeNameEntry entry in Names)
			{
				packet.Write(entry.Name);
				packet.Write(entry.Id);
			}
		}

		public static VoxelShapesData Read(Packet packet)
		{
			var result = new VoxelShapesData();
			int shapeCount = (int) packet.ReadUnsignedVarInt();
			for (int i = 0; i < shapeCount; i++)
			{
				var shape = new VoxelShape { XSize = packet.ReadByte(), YSize = packet.ReadByte(), ZSize = packet.ReadByte() };
				ReadBytes(packet, shape.Storage);
				ReadFloats(packet, shape.XCoordinates);
				ReadFloats(packet, shape.YCoordinates);
				ReadFloats(packet, shape.ZCoordinates);
				result.Shapes.Add(shape);
			}
			int nameCount = (int) packet.ReadUnsignedVarInt();
			for (int i = 0; i < nameCount; i++)
			{
				result.Names.Add(new VoxelShapeNameEntry { Name = packet.ReadString(), Id = packet.ReadUshort() });
			}
			return result;
		}

		private static void WriteBytes(Packet packet, List<byte> values)
		{
			packet.WriteUnsignedVarInt((uint) values.Count);
			foreach (byte value in values) packet.Write(value);
		}

		private static void ReadBytes(Packet packet, List<byte> values)
		{
			int count = (int) packet.ReadUnsignedVarInt();
			for (int i = 0; i < count; i++) values.Add(packet.ReadByte());
		}

		private static void WriteFloats(Packet packet, List<float> values)
		{
			packet.WriteUnsignedVarInt((uint) values.Count);
			foreach (float value in values) packet.Write(value);
		}

		private static void ReadFloats(Packet packet, List<float> values)
		{
			int count = (int) packet.ReadUnsignedVarInt();
			for (int i = 0; i < count; i++) values.Add(packet.ReadFloat());
		}
	}
}
