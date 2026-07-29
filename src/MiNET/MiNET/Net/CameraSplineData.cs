using System.Collections.Generic;
using System.Numerics;

namespace MiNET.Net
{
	public sealed class CameraSplineProgress
	{
		public float Value { get; set; }
		public float Time { get; set; }
		public string Ease { get; set; } = string.Empty;
	}

	public sealed class CameraSplineRotationOption
	{
		public Vector3 Value { get; set; }
		public float Time { get; set; }
		public string Ease { get; set; } = string.Empty;
	}

	public sealed class CameraSplineDefinition
	{
		public string Name { get; set; } = string.Empty;
		public float TotalTime { get; set; }
		public string Type { get; set; } = string.Empty;
		public List<Vector3> Curve { get; } = new();
		public List<CameraSplineProgress> Progress { get; } = new();
		public List<CameraSplineRotationOption> Rotations { get; } = new();
	}

	public sealed class CameraSplineData : IPacketDataObject
	{
		public List<CameraSplineDefinition> Splines { get; } = new();

		public void Write(Packet packet)
		{
			packet.WriteUnsignedVarInt((uint) Splines.Count);
			foreach (CameraSplineDefinition spline in Splines)
			{
				packet.Write(spline.Name);
				packet.Write(spline.TotalTime);
				packet.Write(spline.Type);
				packet.WriteUnsignedVarInt((uint) spline.Curve.Count);
				foreach (Vector3 point in spline.Curve) packet.Write(point);
				packet.WriteUnsignedVarInt((uint) spline.Progress.Count);
				foreach (CameraSplineProgress frame in spline.Progress)
				{
					packet.Write(frame.Value);
					packet.Write(frame.Time);
					packet.Write(frame.Ease);
				}
				packet.WriteUnsignedVarInt((uint) spline.Rotations.Count);
				foreach (CameraSplineRotationOption rotation in spline.Rotations)
				{
					packet.Write(rotation.Value);
					packet.Write(rotation.Time);
					packet.Write(rotation.Ease);
				}
			}
		}

		public static CameraSplineData Read(Packet packet)
		{
			var result = new CameraSplineData();
			int count = (int) packet.ReadUnsignedVarInt();
			for (int i = 0; i < count; i++)
			{
				var spline = new CameraSplineDefinition
				{
					Name = packet.ReadString(),
					TotalTime = packet.ReadFloat(),
					Type = packet.ReadString()
				};
				int curveCount = (int) packet.ReadUnsignedVarInt();
				for (int j = 0; j < curveCount; j++) spline.Curve.Add(packet.ReadVector3());
				int progressCount = (int) packet.ReadUnsignedVarInt();
				for (int j = 0; j < progressCount; j++)
					spline.Progress.Add(new CameraSplineProgress { Value = packet.ReadFloat(), Time = packet.ReadFloat(), Ease = packet.ReadString() });
				int rotationCount = (int) packet.ReadUnsignedVarInt();
				for (int j = 0; j < rotationCount; j++)
					spline.Rotations.Add(new CameraSplineRotationOption { Value = packet.ReadVector3(), Time = packet.ReadFloat(), Ease = packet.ReadString() });
				result.Splines.Add(spline);
			}
			return result;
		}
	}
}
