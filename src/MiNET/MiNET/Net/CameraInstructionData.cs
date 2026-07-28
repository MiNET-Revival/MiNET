using System;
using System.Collections.Generic;
using System.Numerics;

namespace MiNET.Net
{
	public enum CameraEaseType : byte
	{
		Linear,
		Spring,
		InQuad,
		OutQuad,
		InOutQuad,
		InCubic,
		OutCubic,
		InOutCubic,
		InQuart,
		OutQuart,
		InOutQuart,
		InQuint,
		OutQuint,
		InOutQuint,
		InSine,
		OutSine,
		InOutSine,
		InExpo,
		OutExpo,
		InOutExpo,
		InCirc,
		OutCirc,
		InOutCirc,
		InBounce,
		OutBounce,
		InOutBounce,
		InBack,
		OutBack,
		InOutBack,
		InElastic,
		OutElastic,
		InOutElastic
	}

	public sealed class CameraSetInstruction
	{
		public int RuntimeId { get; set; }
		public CameraEase? Ease { get; set; }
		public Vector3? Position { get; set; }
		public Vector2? Rotation { get; set; }
		public Vector3? Facing { get; set; }
		public Vector2? ViewOffset { get; set; }
		public Vector3? EntityOffset { get; set; }
		public bool? Default { get; set; }
		public bool RemoveIgnoreStartingValues { get; set; }
	}

	public readonly record struct CameraEase(CameraEaseType Type, float Duration);
	public readonly record struct CameraFade(float FadeInDuration, float WaitDuration, float FadeOutDuration, Vector3 Color);
	public readonly record struct CameraTarget(Vector3? Offset, long EntityUniqueId);
	public readonly record struct CameraFieldOfView(float FieldOfView, float EaseTime, CameraEaseType EaseType, bool Clear);
	public readonly record struct CameraSplineRotation(Vector3 Value, float Time);

	public enum CameraSplineEaseType : byte
	{
		CatmullRom,
		Linear
	}

	public sealed class CameraSplineInstruction
	{
		public float TotalTime { get; set; }
		public CameraSplineEaseType EaseType { get; set; }
		public List<Vector3> Curve { get; } = new();
		public List<Vector2> ProgressKeyframes { get; } = new();
		public List<CameraSplineRotation> RotationOptions { get; } = new();
	}

	public sealed class CameraInstructionData : IPacketDataObject
	{
		public CameraSetInstruction Set { get; set; }
		public bool? Clear { get; set; }
		public CameraFade? Fade { get; set; }
		public CameraTarget? Target { get; set; }
		public bool? RemoveTarget { get; set; }
		public CameraFieldOfView? FieldOfView { get; set; }
		public CameraSplineInstruction Spline { get; set; }
		public long? AttachToEntity { get; set; }
		public bool? DetachFromEntity { get; set; }

		public void Write(Packet packet)
		{
			WriteOptional(packet, Set, value => WriteSet(packet, value));
			WriteOptional(packet, Clear, packet.Write);
			WriteOptional(packet, Fade, value => WriteFade(packet, value));
			WriteOptional(packet, Target, value => WriteTarget(packet, value));
			WriteOptional(packet, RemoveTarget, packet.Write);
			WriteOptional(packet, FieldOfView, value => WriteFieldOfView(packet, value));
			WriteOptional(packet, Spline, value => WriteSpline(packet, value));
			WriteOptional(packet, AttachToEntity, packet.Write);
			WriteOptional(packet, DetachFromEntity, packet.Write);
		}

		public static CameraInstructionData Read(Packet packet)
		{
			return new CameraInstructionData
			{
				Set = ReadOptionalReference(packet, () => ReadSet(packet)),
				Clear = ReadOptional(packet, packet.ReadBool),
				Fade = ReadOptional(packet, () => ReadFade(packet)),
				Target = ReadOptional(packet, () => ReadTarget(packet)),
				RemoveTarget = ReadOptional(packet, packet.ReadBool),
				FieldOfView = ReadOptional(packet, () => ReadFieldOfView(packet)),
				Spline = ReadOptionalReference(packet, () => ReadSpline(packet)),
				AttachToEntity = ReadOptional(packet, packet.ReadLong),
				DetachFromEntity = ReadOptional(packet, packet.ReadBool)
			};
		}

		private static void WriteSet(Packet packet, CameraSetInstruction value)
		{
			packet.Write(value.RuntimeId);
			WriteOptional(packet, value.Ease, ease =>
			{
				packet.Write((byte) ease.Type);
				packet.Write(ease.Duration);
			});
			WriteOptional(packet, value.Position, packet.Write);
			WriteOptional(packet, value.Rotation, packet.Write);
			WriteOptional(packet, value.Facing, packet.Write);
			WriteOptional(packet, value.ViewOffset, packet.Write);
			WriteOptional(packet, value.EntityOffset, packet.Write);
			WriteOptional(packet, value.Default, packet.Write);
			packet.Write(value.RemoveIgnoreStartingValues);
		}

		private static CameraSetInstruction ReadSet(Packet packet)
		{
			return new CameraSetInstruction
			{
				RuntimeId = packet.ReadInt(),
				Ease = ReadOptional(packet, () => new CameraEase((CameraEaseType) packet.ReadByte(), packet.ReadFloat())),
				Position = ReadOptional(packet, packet.ReadVector3),
				Rotation = ReadOptional(packet, packet.ReadVector2),
				Facing = ReadOptional(packet, packet.ReadVector3),
				ViewOffset = ReadOptional(packet, packet.ReadVector2),
				EntityOffset = ReadOptional(packet, packet.ReadVector3),
				Default = ReadOptional(packet, packet.ReadBool),
				RemoveIgnoreStartingValues = packet.ReadBool()
			};
		}

		private static void WriteFade(Packet packet, CameraFade value)
		{
			packet.Write(value.FadeInDuration);
			packet.Write(value.WaitDuration);
			packet.Write(value.FadeOutDuration);
			packet.Write(value.Color);
		}

		private static CameraFade ReadFade(Packet packet) =>
			new(packet.ReadFloat(), packet.ReadFloat(), packet.ReadFloat(), packet.ReadVector3());

		private static void WriteTarget(Packet packet, CameraTarget value)
		{
			WriteOptional(packet, value.Offset, packet.Write);
			packet.Write(value.EntityUniqueId);
		}

		private static CameraTarget ReadTarget(Packet packet) =>
			new(ReadOptional(packet, packet.ReadVector3), packet.ReadLong());

		private static void WriteFieldOfView(Packet packet, CameraFieldOfView value)
		{
			packet.Write(value.FieldOfView);
			packet.Write(value.EaseTime);
			packet.Write((byte) value.EaseType);
			packet.Write(value.Clear);
		}

		private static CameraFieldOfView ReadFieldOfView(Packet packet) =>
			new(packet.ReadFloat(), packet.ReadFloat(), (CameraEaseType) packet.ReadByte(), packet.ReadBool());

		private static void WriteSpline(Packet packet, CameraSplineInstruction value)
		{
			packet.Write(value.TotalTime);
			packet.Write((byte) value.EaseType);
			WriteArray(packet, value.Curve, packet.Write);
			WriteArray(packet, value.ProgressKeyframes, packet.Write);
			WriteArray(packet, value.RotationOptions, option =>
			{
				packet.Write(option.Value);
				packet.Write(option.Time);
			});
		}

		private static CameraSplineInstruction ReadSpline(Packet packet)
		{
			var value = new CameraSplineInstruction
			{
				TotalTime = packet.ReadFloat(),
				EaseType = (CameraSplineEaseType) packet.ReadByte()
			};
			ReadArray(packet, value.Curve, packet.ReadVector3);
			ReadArray(packet, value.ProgressKeyframes, packet.ReadVector2);
			ReadArray(packet, value.RotationOptions,
				() => new CameraSplineRotation(packet.ReadVector3(), packet.ReadFloat()));
			return value;
		}

		private static void WriteOptional<T>(Packet packet, T? value, Action<T> writer) where T : struct
		{
			packet.Write(value.HasValue);
			if (value.HasValue) writer(value.Value);
		}

		private static void WriteOptional<T>(Packet packet, T value, Action<T> writer) where T : class
		{
			packet.Write(value != null);
			if (value != null) writer(value);
		}

		private static T? ReadOptional<T>(Packet packet, Func<T> reader) where T : struct =>
			packet.ReadBool() ? reader() : null;

		private static T ReadOptionalReference<T>(Packet packet, Func<T> reader) where T : class =>
			packet.ReadBool() ? reader() : null;

		private static void WriteArray<T>(Packet packet, ICollection<T> values, Action<T> writer)
		{
			packet.WriteUnsignedVarInt((uint) values.Count);
			foreach (var value in values) writer(value);
		}

		private static void ReadArray<T>(Packet packet, ICollection<T> values, Func<T> reader)
		{
			var count = packet.ReadUnsignedVarInt();
			for (var i = 0u; i < count; i++) values.Add(reader());
		}
	}
}
