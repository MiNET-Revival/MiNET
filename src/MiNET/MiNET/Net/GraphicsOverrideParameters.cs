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
		SunGlareShape = 9,
		Chlorophyll = 10,
		Cdom = 11,
		SuspendedSediment = 12,
		WavesDepth = 13,
		WavesFrequency = 14,
		WavesFrequencyScaling = 15,
		WavesSpeed = 16,
		WavesSpeedScaling = 17,
		WavesShape = 18,
		WavesOctaves = 19,
		WavesMix = 20,
		WavesPull = 21,
		WavesDirectionIncrement = 22,
		MidtonesContrast = 23,
		HighlightsContrast = 24,
		ShadowsContrast = 25
	}

	public sealed class GraphicsOverrideParameters : IPacketDataObject
	{
		public List<GraphicsParameterKeyframe> Values { get; } = new();
		public float? FloatValue { get; set; }
		public Vector3? VectorValue { get; set; }
		public string BiomeIdentifier { get; set; } = string.Empty;
		public string PlayerIdentifier { get; set; }
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

			packet.Write(FloatValue.HasValue);
			if (FloatValue.HasValue) packet.Write(FloatValue.Value);
			packet.Write(VectorValue.HasValue);
			if (VectorValue.HasValue) packet.Write(VectorValue.Value);
			packet.Write(BiomeIdentifier);
			packet.Write(PlayerIdentifier != null);
			if (PlayerIdentifier != null) packet.Write(PlayerIdentifier);
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

			parameters.FloatValue = packet.ReadBool() ? packet.ReadFloat() : null;
			parameters.VectorValue = packet.ReadBool() ? packet.ReadVector3() : null;
			parameters.BiomeIdentifier = packet.ReadString();
			parameters.PlayerIdentifier = packet.ReadBool() ? packet.ReadString() : null;
			parameters.ParameterType = (GraphicsOverrideParameterType) packet.ReadByte();
			parameters.Reset = packet.ReadBool();
			return parameters;
		}
	}
}
