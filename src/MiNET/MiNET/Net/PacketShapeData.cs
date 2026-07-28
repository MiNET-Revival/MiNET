using System;
using System.Numerics;

namespace MiNET.Net;

public enum ScriptDebugShapeType : byte
{
	Line = 0,
	Box = 1,
	Sphere = 2,
	Circle = 3,
	Text = 4,
	Arrow = 5,
	NumShapeTypes = 6
}

public sealed class PacketShapeData
{
	public long NetworkId { get; set; }
	public ScriptDebugShapeType? ShapeType { get; set; }
	public Vector3? Location { get; set; }
	public float? Scale { get; set; }
	public Vector3? Rotation { get; set; }
	public float? TimeLeftTotalSeconds { get; set; }
	public uint? Color { get; set; }
	public string Text { get; set; }
	public Vector3? BoxBound { get; set; }
	public Vector3? EndLocation { get; set; }
	public float? ArrowHeadLength { get; set; }
	public float? ArrowHeadRadius { get; set; }
	public byte? NumberOfSegments { get; set; }

	public void Write(Packet packet)
	{
		packet.WriteUnsignedVarLong(NetworkId);
		WriteOptional(packet, ShapeType, value => packet.Write((byte) value));
		WriteOptional(packet, Location, packet.Write);
		WriteOptional(packet, Scale, packet.Write);
		WriteOptional(packet, Rotation, packet.Write);
		WriteOptional(packet, TimeLeftTotalSeconds, packet.Write);
		WriteOptional(packet, Color, packet.Write);
		WriteOptional(packet, Text, packet.Write);
		WriteOptional(packet, BoxBound, packet.Write);
		WriteOptional(packet, EndLocation, packet.Write);
		WriteOptional(packet, ArrowHeadLength, packet.Write);
		WriteOptional(packet, ArrowHeadRadius, packet.Write);
		WriteOptional(packet, NumberOfSegments, packet.Write);
	}

	public static PacketShapeData Read(Packet packet)
	{
		return new PacketShapeData
		{
			NetworkId = packet.ReadUnsignedVarLong(),
			ShapeType = ReadOptional(packet, () => (ScriptDebugShapeType) packet.ReadByte()),
			Location = ReadOptional(packet, packet.ReadVector3),
			Scale = ReadOptional(packet, packet.ReadFloat),
			Rotation = ReadOptional(packet, packet.ReadVector3),
			TimeLeftTotalSeconds = ReadOptional(packet, packet.ReadFloat),
			Color = ReadOptional(packet, packet.ReadUint),
			Text = ReadOptionalReference(packet, packet.ReadString),
			BoxBound = ReadOptional(packet, packet.ReadVector3),
			EndLocation = ReadOptional(packet, packet.ReadVector3),
			ArrowHeadLength = ReadOptional(packet, packet.ReadFloat),
			ArrowHeadRadius = ReadOptional(packet, packet.ReadFloat),
			NumberOfSegments = ReadOptional(packet, packet.ReadByte)
		};
	}

	private static void WriteOptional<T>(Packet packet, T? value, Action<T> writer) where T : struct
	{
		packet.Write(value.HasValue);
		if (value.HasValue) writer(value.Value);
	}

	private static void WriteOptional(Packet packet, string value, Action<string> writer)
	{
		packet.Write(value != null);
		if (value != null) writer(value);
	}

	private static T? ReadOptional<T>(Packet packet, Func<T> reader) where T : struct =>
		packet.ReadBool() ? reader() : null;

	private static string ReadOptionalReference(Packet packet, Func<string> reader) =>
		packet.ReadBool() ? reader() : null;
}

public partial class Packet
{
	public void Write(PacketShapeData[] shapes)
	{
		WriteLength(shapes?.Length ?? 0);
		if (shapes == null) return;
		foreach (PacketShapeData shape in shapes) shape.Write(this);
	}

	public PacketShapeData[] ReadPacketShapeDatas()
	{
		int count = ReadLength();
		var shapes = new PacketShapeData[count];
		for (int i = 0; i < count; i++) shapes[i] = PacketShapeData.Read(this);
		return shapes;
	}
}
