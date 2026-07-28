using System.Numerics;

namespace MiNET.Net
{
	public sealed class DebugMarkerData
	{
		public string Text { get; set; }
		public Vector3 Position { get; set; }
		public uint Color { get; set; }
		public ulong DurationMillis { get; set; }
	}

	public partial class McpeClientboundDebugRenderer : Packet<McpeClientboundDebugRenderer>
	{
		public const string ClearDebugMarkers = "cleardebugmarkers";
		public const string AddDebugMarkerCube = "adddebugmarkercube";

		public string type;
		public DebugMarkerData marker;

		partial void AfterEncode()
		{
			Write(type);
			Write(marker != null);
			if (marker == null) return;
			Write(marker.Text);
			Write(marker.Position);
			Write(marker.Color);
			Write(marker.DurationMillis);
		}

		partial void AfterDecode()
		{
			type = ReadString();
			if (!ReadBool()) return;
			marker = new DebugMarkerData
			{
				Text = ReadString(),
				Position = ReadVector3(),
				Color = ReadUint(),
				DurationMillis = ReadUlong()
			};
		}
	}
}
