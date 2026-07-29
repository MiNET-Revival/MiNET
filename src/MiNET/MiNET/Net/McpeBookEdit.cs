namespace MiNET.Net
{
	public enum BookEditAction
	{
		ReplacePage,
		AddPage,
		DeletePage,
		SwapPages,
		Sign
	}

	public partial class McpeBookEdit : Packet<McpeBookEdit>
	{
		public int inventorySlot;
		public BookEditAction action;
		public int pageNumber;
		public int secondaryPageNumber;
		public string text = string.Empty;
		public string photoName = string.Empty;
		public string title = string.Empty;
		public string author = string.Empty;
		public string xuid = string.Empty;

		partial void AfterEncode()
		{
			WriteSignedVarInt(inventorySlot);
			WriteUnsignedVarInt((uint) action);
			switch (action)
			{
				case BookEditAction.ReplacePage:
				case BookEditAction.AddPage:
					WriteSignedVarInt(pageNumber);
					Write(text);
					Write(photoName);
					break;
				case BookEditAction.DeletePage:
					WriteSignedVarInt(pageNumber);
					break;
				case BookEditAction.SwapPages:
					WriteSignedVarInt(pageNumber);
					WriteSignedVarInt(secondaryPageNumber);
					break;
				case BookEditAction.Sign:
					Write(title);
					Write(author);
					Write(xuid);
					break;
			}
		}

		partial void AfterDecode()
		{
			inventorySlot = ReadSignedVarInt();
			action = (BookEditAction) ReadUnsignedVarInt();
			switch (action)
			{
				case BookEditAction.ReplacePage:
				case BookEditAction.AddPage:
					pageNumber = ReadSignedVarInt();
					text = ReadString();
					photoName = ReadString();
					break;
				case BookEditAction.DeletePage:
					pageNumber = ReadSignedVarInt();
					break;
				case BookEditAction.SwapPages:
					pageNumber = ReadSignedVarInt();
					secondaryPageNumber = ReadSignedVarInt();
					break;
				case BookEditAction.Sign:
					title = ReadString();
					author = ReadString();
					xuid = ReadString();
					break;
			}
		}
	}
}
