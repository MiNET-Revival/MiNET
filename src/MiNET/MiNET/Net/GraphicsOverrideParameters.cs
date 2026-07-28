using System.Collections.Generic;
using System.Numerics;

namespace MiNET.Net
{
	public readonly record struct GraphicsParameterKeyframe(float Time, Vector3 Value);

	public enum GraphicsOverrideParameterType : byte
	{
		SkyZenithColor = 0,
		SkyHorizonColor = 1,
		HorizonBlendMin = 2,
		HorizonBlendMax = 3,
		HorizonBlendStart = 4,
		HorizonBlendMieStart = 5,
		RayleighStrength = 6,
		SunMieStrength = 7,
		MoonMieStrength = 8,
		SunGlareShape = 9
	}

	public sealed class GraphicsOverrideParameters : IPacketDataObject
	{
		public List<GraphicsParameterKeyframe> Values { get; } = new();
		public string BiomeIdentifier { get; set; } = string.Empty;
		public GraphicsOverrideParameterType ParameterType { get; set; }
		public bool Reset { get; set; }

		public void Write(Packet packet)
		{
			packet.WriteUnsignedVarInt((uint) Values.Count);
			foreach (var value in Values)
			{
				packet.Write(value.Time);
				packet.Write(value.Value);
			}

			packet.Write(BiomeIdentifier);
			packet.Write((byte) ParameterType);
			packet.Write(Reset);
		}

		public static GraphicsOverrideParameters Read(Packet packet)
		{
			var parameters = new GraphicsOverrideParameters();
			var count = packet.ReadUnsignedVarInt();
			for (var i = 0u; i < count; i++)
			{
				parameters.Values.Add(new GraphicsParameterKeyframe(packet.ReadFloat(), packet.ReadVector3()));
			}

			parameters.BiomeIdentifier = packet.ReadString();
			parameters.ParameterType = (GraphicsOverrideParameterType) packet.ReadByte();
			parameters.Reset = packet.ReadBool();
			return parameters;
		}
	}
}
