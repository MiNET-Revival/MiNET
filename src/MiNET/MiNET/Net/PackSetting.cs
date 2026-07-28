using System;

namespace MiNET.Net
{
	public enum PackSettingType
	{
		Float = 0,
		Bool = 1,
		String = 2
	}

	public abstract class PackSetting : IPacketDataObject
	{
		public string Name { get; }
		public abstract PackSettingType Type { get; }

		protected PackSetting(string name)
		{
			Name = name ?? throw new ArgumentNullException(nameof(name));
		}

		public void Write(Packet packet)
		{
			packet.Write(Name);
			packet.WriteUnsignedVarInt((uint) Type);
			WriteValue(packet);
		}

		protected abstract void WriteValue(Packet packet);

		public static PackSetting Read(Packet packet)
		{
			var name = packet.ReadString();
			return (PackSettingType) packet.ReadUnsignedVarInt() switch
			{
				PackSettingType.Float => new FloatPackSetting(name, packet.ReadFloat()),
				PackSettingType.Bool => new BoolPackSetting(name, packet.ReadBool()),
				PackSettingType.String => new StringPackSetting(name, packet.ReadString()),
				var type => throw new InvalidOperationException($"Invalid pack setting type {(uint) type}.")
			};
		}
	}

	public sealed class FloatPackSetting : PackSetting
	{
		public float Value { get; }
		public override PackSettingType Type => PackSettingType.Float;

		public FloatPackSetting(string name, float value) : base(name)
		{
			Value = value;
		}

		protected override void WriteValue(Packet packet) => packet.Write(Value);
	}

	public sealed class BoolPackSetting : PackSetting
	{
		public bool Value { get; }
		public override PackSettingType Type => PackSettingType.Bool;

		public BoolPackSetting(string name, bool value) : base(name)
		{
			Value = value;
		}

		protected override void WriteValue(Packet packet) => packet.Write(Value);
	}

	public sealed class StringPackSetting : PackSetting
	{
		public string Value { get; }
		public override PackSettingType Type => PackSettingType.String;

		public StringPackSetting(string name, string value) : base(name)
		{
			Value = value ?? string.Empty;
		}

		protected override void WriteValue(Packet packet) => packet.Write(Value);
	}
}
