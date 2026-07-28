namespace MiNET.Net
{
	public partial class McpePlayerVideoCapture : Packet<McpePlayerVideoCapture>
	{
		public bool recording;
		public uint frameRate;
		public string filePrefix;

		partial void AfterEncode()
		{
			Write(recording);
			if (!recording) return;
			Write(frameRate);
			Write(filePrefix);
		}

		partial void AfterDecode()
		{
			recording = ReadBool();
			if (!recording) return;
			frameRate = ReadUint();
			filePrefix = ReadString();
		}
	}
}
