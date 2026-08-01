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
	Cylinder = 6,
	Pyramid = 7,
	Ellipsoid = 8,
	Cone = 9,
	NumShapeTypes = 10
}

public sealed class PacketShapeData
{
	public long NetworkId { get; set; }
	public ScriptDebugShapeType? ShapeType { get; set; }
	public Vector3? Location { get; set; }
	public float? Scale { get; set; }
	public Vector3? Rotation { get; set; }
	public float? TimeLeftTotalSeconds { get; set; }
	public float? MaximumRenderDistance { get; set; }
	public uint? Color { get; set; }
	public int? Dimension { get; set; }
	public long? AttachedToEntityId { get; set; }
	public string Text { get; set; }
	public bool? TextUseRotation { get; set; }
	public uint? TextBackgroundColor { get; set; }
	public bool? TextDepthTest { get; set; }
	public bool? TextShowBackface { get; set; }
	public bool? TextShowTextBackface { get; set; }
	public Vector3? BoxBound { get; set; }
	public Vector3? EndLocation { get; set; }
	public float? ArrowHeadLength { get; set; }
	public float? ArrowHeadRadius { get; set; }
	public byte? NumberOfSegments { get; set; }
	public Vector2 RadiusX { get; set; }
	public Vector2 RadiusZ { get; set; }
	public Vector2 Radii { get; set; }
	public Vector3 EllipsoidRadii { get; set; }
	public float Width { get; set; }
	public float? Depth { get; set; }
	public float Height { get; set; }

	public void Write(Packet packet)
	{
		packet.WriteUnsignedVarLong(NetworkId);
		WriteOptional(packet, ShapeType, value => packet.Write((byte) value));
		WriteOptional(packet, Location, packet.Write);
		WriteOptional(packet, Scale, packet.Write);
		WriteOptional(packet, Rotation, packet.Write);
		WriteOptional(packet, TimeLeftTotalSeconds, packet.Write);
		WriteOptional(packet, MaximumRenderDistance, packet.Write);
		WriteOptional(packet, Color, packet.Write);
		WriteOptional(packet, Dimension, packet.WriteVarInt);
		WriteOptional(packet, AttachedToEntityId, packet.WriteUnsignedVarLong);

		packet.WriteUnsignedVarInt(GetPayloadType(ShapeType));
		switch (ShapeType)
		{
			case ScriptDebugShapeType.Arrow:
				WriteOptional(packet, EndLocation, packet.Write);
				WriteOptional(packet, ArrowHeadLength, packet.Write);
				WriteOptional(packet, ArrowHeadRadius, packet.Write);
				WriteOptional(packet, NumberOfSegments, packet.Write);
				break;
			case ScriptDebugShapeType.Text:
				packet.Write(Text ?? string.Empty);
				packet.Write(TextUseRotation ?? false);
				WriteOptional(packet, TextBackgroundColor, packet.Write);
				packet.Write(TextDepthTest ?? false);
				packet.Write(TextShowBackface ?? false);
				packet.Write(TextShowTextBackface ?? false);
				break;
			case ScriptDebugShapeType.Box:
				packet.Write(BoxBound ?? Vector3.Zero);
				break;
			case ScriptDebugShapeType.Line:
				packet.Write(EndLocation ?? Vector3.Zero);
				break;
			case ScriptDebugShapeType.Sphere:
			case ScriptDebugShapeType.Circle:
				packet.Write(NumberOfSegments ?? 0);
				break;
			case ScriptDebugShapeType.Cylinder:
				packet.Write(RadiusX);
				packet.Write(RadiusZ);
				packet.Write(Height);
				packet.Write(NumberOfSegments ?? 0);
				break;
			case ScriptDebugShapeType.Pyramid:
				packet.Write(Width);
				WriteOptional(packet, Depth, packet.Write);
				packet.Write(Height);
				break;
			case ScriptDebugShapeType.Ellipsoid:
				packet.Write(EllipsoidRadii);
				packet.Write(NumberOfSegments ?? 0);
				break;
			case ScriptDebugShapeType.Cone:
				packet.Write(Radii);
				packet.Write(Height);
				packet.Write(NumberOfSegments ?? 0);
				break;
		}
	}

	public static PacketShapeData Read(Packet packet)
	{
		var shape = new PacketShapeData
		{
			NetworkId = packet.ReadUnsignedVarLong(),
			ShapeType = ReadOptional(packet, () => (ScriptDebugShapeType) packet.ReadByte()),
			Location = ReadOptional(packet, packet.ReadVector3),
			Scale = ReadOptional(packet, packet.ReadFloat),
			Rotation = ReadOptional(packet, packet.ReadVector3),
			TimeLeftTotalSeconds = ReadOptional(packet, packet.ReadFloat),
			MaximumRenderDistance = ReadOptional(packet, packet.ReadFloat),
			Color = ReadOptional(packet, packet.ReadUint)
		};

		shape.Dimension = ReadOptional(packet, packet.ReadVarInt);
		shape.AttachedToEntityId = ReadOptional(packet, packet.ReadUnsignedVarLong);
		packet.ReadUnsignedVarInt(); // Payload discriminator; shape type defines the payload layout.

		switch (shape.ShapeType)
		{
			case ScriptDebugShapeType.Arrow:
				shape.EndLocation = ReadOptional(packet, packet.ReadVector3);
				shape.ArrowHeadLength = ReadOptional(packet, packet.ReadFloat);
				shape.ArrowHeadRadius = ReadOptional(packet, packet.ReadFloat);
				shape.NumberOfSegments = ReadOptional(packet, packet.ReadByte);
				break;
			case ScriptDebugShapeType.Text:
				shape.Text = packet.ReadString();
				shape.TextUseRotation = packet.ReadBool();
				shape.TextBackgroundColor = ReadOptional(packet, packet.ReadUint);
				shape.TextDepthTest = packet.ReadBool();
				shape.TextShowBackface = packet.ReadBool();
				shape.TextShowTextBackface = packet.ReadBool();
				break;
			case ScriptDebugShapeType.Box:
				shape.BoxBound = packet.ReadVector3();
				break;
			case ScriptDebugShapeType.Line:
				shape.EndLocation = packet.ReadVector3();
				break;
			case ScriptDebugShapeType.Sphere:
			case ScriptDebugShapeType.Circle:
				shape.NumberOfSegments = packet.ReadByte();
				break;
			case ScriptDebugShapeType.Cylinder:
				shape.RadiusX = packet.ReadVector2();
				shape.RadiusZ = packet.ReadVector2();
				shape.Height = packet.ReadFloat();
				shape.NumberOfSegments = packet.ReadByte();
				break;
			case ScriptDebugShapeType.Pyramid:
				shape.Width = packet.ReadFloat();
				shape.Depth = ReadOptional(packet, packet.ReadFloat);
				shape.Height = packet.ReadFloat();
				break;
			case ScriptDebugShapeType.Ellipsoid:
				shape.EllipsoidRadii = packet.ReadVector3();
				shape.NumberOfSegments = packet.ReadByte();
				break;
			case ScriptDebugShapeType.Cone:
				shape.Radii = packet.ReadVector2();
				shape.Height = packet.ReadFloat();
				shape.NumberOfSegments = packet.ReadByte();
				break;
		}

		return shape;
	}

	private static uint GetPayloadType(ScriptDebugShapeType? type) => type switch
	{
		null => 0,
		ScriptDebugShapeType.Arrow => 1,
		ScriptDebugShapeType.Text => 2,
		ScriptDebugShapeType.Box => 3,
		ScriptDebugShapeType.Line => 4,
		ScriptDebugShapeType.Sphere => 5,
		ScriptDebugShapeType.Circle => 5,
		ScriptDebugShapeType.Cylinder => 6,
		ScriptDebugShapeType.Pyramid => 7,
		ScriptDebugShapeType.Ellipsoid => 8,
		ScriptDebugShapeType.Cone => 9,
		_ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
	};

	private static void WriteOptional<T>(Packet packet, T? value, Action<T> writer) where T : struct
	{
		packet.Write(value.HasValue);
		if (value.HasValue) writer(value.Value);
	}

	private static T? ReadOptional<T>(Packet packet, Func<T> reader) where T : struct =>
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
