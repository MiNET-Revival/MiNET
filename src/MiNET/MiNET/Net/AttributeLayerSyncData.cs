using System;
using System.Collections.Generic;

namespace MiNET.Net
{
	public enum AttributeLayerPayloadType : uint { UpdateLayers, UpdateSettings, UpdateEnvironment, RemoveEnvironment }
	public enum EnvironmentAttributeType : uint { Boolean, Float, Color }

	public sealed class AttributeLayerSyncData : IPacketDataObject
	{
		public AttributeLayerPayloadType PayloadType { get; set; }
		public List<AttributeLayerData> Layers { get; set; } = new List<AttributeLayerData>();
		public string LayerName { get; set; } = string.Empty;
		public int DimensionId { get; set; }
		public AttributeLayerSettings Settings { get; set; } = new AttributeLayerSettings();
		public List<EnvironmentAttributeData> EnvironmentAttributes { get; set; } = new List<EnvironmentAttributeData>();
		public List<string> RemoveAttributeNames { get; set; } = new List<string>();

		public void Write(Packet packet)
		{
			packet.WriteUnsignedVarInt((uint) PayloadType);
			switch (PayloadType)
			{
				case AttributeLayerPayloadType.UpdateLayers: WriteList(packet, Layers, value => value.Write(packet)); break;
				case AttributeLayerPayloadType.UpdateSettings: WriteTarget(packet); Settings.Write(packet); break;
				case AttributeLayerPayloadType.UpdateEnvironment: WriteTarget(packet); WriteList(packet, EnvironmentAttributes, value => value.Write(packet)); break;
				case AttributeLayerPayloadType.RemoveEnvironment: WriteTarget(packet); WriteList(packet, RemoveAttributeNames, packet.Write); break;
				default: throw new ArgumentOutOfRangeException(nameof(PayloadType));
			}
		}

		public static AttributeLayerSyncData Read(Packet packet)
		{
			var value = new AttributeLayerSyncData {PayloadType = (AttributeLayerPayloadType) packet.ReadUnsignedVarInt()};
			switch (value.PayloadType)
			{
				case AttributeLayerPayloadType.UpdateLayers: value.Layers = ReadList(packet, () => AttributeLayerData.Read(packet)); break;
				case AttributeLayerPayloadType.UpdateSettings: value.ReadTarget(packet); value.Settings = AttributeLayerSettings.Read(packet); break;
				case AttributeLayerPayloadType.UpdateEnvironment: value.ReadTarget(packet); value.EnvironmentAttributes = ReadList(packet, () => EnvironmentAttributeData.Read(packet)); break;
				case AttributeLayerPayloadType.RemoveEnvironment: value.ReadTarget(packet); value.RemoveAttributeNames = ReadList(packet, packet.ReadString); break;
				default: throw new InvalidOperationException($"Unknown attribute-layer payload type {(uint) value.PayloadType}.");
			}
			return value;
		}

		private void WriteTarget(Packet packet) { packet.Write(LayerName); packet.WriteSignedVarInt(DimensionId); }
		private void ReadTarget(Packet packet) { LayerName = packet.ReadString(); DimensionId = packet.ReadSignedVarInt(); }
		internal static void WriteList<T>(Packet packet, IList<T> values, Action<T> writer) { packet.WriteLength(values?.Count ?? 0); if (values != null) foreach (var value in values) writer(value); }
		internal static List<T> ReadList<T>(Packet packet, Func<T> reader) { var values = new List<T>(); var count = packet.ReadLength(); for (var i = 0; i < count; i++) values.Add(reader()); return values; }
	}

	public sealed class AttributeLayerData
	{
		public string Name { get; set; } = string.Empty;
		public string NoiseName { get; set; } // Added in protocol v1001.
		public int DimensionId { get; set; }
		public AttributeLayerSettings Settings { get; set; } = new AttributeLayerSettings();
		public List<EnvironmentAttributeData> EnvironmentAttributes { get; set; } = new List<EnvironmentAttributeData>();

		public void Write(Packet packet) { packet.Write(Name); WriteOptional(packet, NoiseName, packet.Write); packet.WriteSignedVarInt(DimensionId); Settings.Write(packet); AttributeLayerSyncData.WriteList(packet, EnvironmentAttributes, value => value.Write(packet)); }
		public static AttributeLayerData Read(Packet packet) => new AttributeLayerData {Name = packet.ReadString(), NoiseName = ReadOptional(packet, packet.ReadString), DimensionId = packet.ReadSignedVarInt(), Settings = AttributeLayerSettings.Read(packet), EnvironmentAttributes = AttributeLayerSyncData.ReadList(packet, () => EnvironmentAttributeData.Read(packet))};
		internal static void WriteOptional<T>(Packet packet, T value, Action<T> writer) where T : class { packet.Write(value != null); if (value != null) writer(value); }
		internal static T ReadOptional<T>(Packet packet, Func<T> reader) where T : class => packet.ReadBool() ? reader() : null;
	}

	public sealed class AttributeLayerSettings
	{
		public int Priority { get; set; }
		public float Weight { get; set; }
		public bool Enabled { get; set; }
		public bool TransitionsPaused { get; set; }
		public void Write(Packet packet) { packet.Write(Priority); packet.Write(Weight); packet.Write(Enabled); packet.Write(TransitionsPaused); }
		public static AttributeLayerSettings Read(Packet packet) => new AttributeLayerSettings {Priority = packet.ReadInt(), Weight = packet.ReadFloat(), Enabled = packet.ReadBool(), TransitionsPaused = packet.ReadBool()};
	}

	public sealed class EnvironmentAttributeData
	{
		public string Name { get; set; } = string.Empty;
		public EnvironmentAttributeValue From { get; set; }
		public EnvironmentAttributeValue Value { get; set; } = new EnvironmentAttributeValue();
		public EnvironmentAttributeValue To { get; set; }
		public uint CurrentTransitionTicks { get; set; }
		public uint TotalTransitionTicks { get; set; }
		public string EaseType { get; set; } = "linear";
		public uint LocalTransitionTicks { get; set; } // Added in protocol v1001.
		public bool NoiseTransition { get; set; } // Added in protocol v1001.

		public void Write(Packet packet) { packet.Write(Name); AttributeLayerData.WriteOptional(packet, From, value => value.Write(packet)); Value.Write(packet); AttributeLayerData.WriteOptional(packet, To, value => value.Write(packet)); packet.Write(CurrentTransitionTicks); packet.Write(TotalTransitionTicks); packet.Write(EaseType); packet.Write(LocalTransitionTicks); packet.Write(NoiseTransition); }
		public static EnvironmentAttributeData Read(Packet packet) => new EnvironmentAttributeData {Name = packet.ReadString(), From = AttributeLayerData.ReadOptional(packet, () => EnvironmentAttributeValue.Read(packet)), Value = EnvironmentAttributeValue.Read(packet), To = AttributeLayerData.ReadOptional(packet, () => EnvironmentAttributeValue.Read(packet)), CurrentTransitionTicks = packet.ReadUint(), TotalTransitionTicks = packet.ReadUint(), EaseType = packet.ReadString(), LocalTransitionTicks = packet.ReadUint(), NoiseTransition = packet.ReadBool()};
	}

	public sealed class EnvironmentAttributeValue
	{
		public EnvironmentAttributeType Type { get; set; }
		public bool BooleanValue { get; set; }
		public int? BooleanOperation { get; set; }
		public float FloatValue { get; set; }
		public int? FloatOperation { get; set; }
		public float? Minimum { get; set; }
		public float? Maximum { get; set; }
		public int ColorValue { get; set; }
		public int? ColorOperation { get; set; }

		public void Write(Packet packet)
		{
			packet.WriteUnsignedVarInt((uint) Type);
			switch (Type)
			{
				case EnvironmentAttributeType.Boolean: packet.Write(BooleanValue); WriteOptional(packet, BooleanOperation); break;
				case EnvironmentAttributeType.Float: packet.Write(FloatValue); WriteOptional(packet, FloatOperation); WriteOptional(packet, Minimum); WriteOptional(packet, Maximum); break;
				case EnvironmentAttributeType.Color: packet.Write(ColorValue); WriteOptional(packet, ColorOperation); break;
				default: throw new ArgumentOutOfRangeException(nameof(Type));
			}
		}

		public static EnvironmentAttributeValue Read(Packet packet)
		{
			var value = new EnvironmentAttributeValue {Type = (EnvironmentAttributeType) packet.ReadUnsignedVarInt()};
			switch (value.Type)
			{
				case EnvironmentAttributeType.Boolean: value.BooleanValue = packet.ReadBool(); value.BooleanOperation = ReadOptionalInt(packet); break;
				case EnvironmentAttributeType.Float: value.FloatValue = packet.ReadFloat(); value.FloatOperation = ReadOptionalInt(packet); value.Minimum = ReadOptionalFloat(packet); value.Maximum = ReadOptionalFloat(packet); break;
				case EnvironmentAttributeType.Color: value.ColorValue = packet.ReadInt(); value.ColorOperation = ReadOptionalInt(packet); break;
				default: throw new InvalidOperationException($"Unknown environment attribute type {(uint) value.Type}.");
			}
			return value;
		}

		private static void WriteOptional(Packet packet, int? value) { packet.Write(value.HasValue); if (value.HasValue) packet.Write(value.Value); }
		private static void WriteOptional(Packet packet, float? value) { packet.Write(value.HasValue); if (value.HasValue) packet.Write(value.Value); }
		private static int? ReadOptionalInt(Packet packet) => packet.ReadBool() ? packet.ReadInt() : (int?) null;
		private static float? ReadOptionalFloat(Packet packet) => packet.ReadBool() ? packet.ReadFloat() : (float?) null;
	}
}
