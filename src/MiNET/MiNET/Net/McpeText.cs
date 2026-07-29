#region LICENSE

// The contents of this file are subject to the Common Public Attribution
// License Version 1.0. (the "License"); you may not use this file except in
// compliance with the License. You may obtain a copy of the License at
// https://github.com/NiclasOlofsson/MiNET/blob/master/LICENSE. 
// The License is based on the Mozilla Public License Version 1.1, but Sections 14 
// and 15 have been added to cover use of software over a computer network and 
// provide for limited attribution for the Original Developer. In addition, Exhibit A has 
// been modified to be consistent with Exhibit B.
// 
// Software distributed under the License is distributed on an "AS IS" basis,
// WITHOUT WARRANTY OF ANY KIND, either express or implied. See the License for
// the specific language governing rights and limitations under the License.
// 
// The Original Code is MiNET.
// 
// The Original Developer is the Initial Developer.  The Initial Developer of
// the Original Code is Niclas Olofsson.
// 
// All portions of the code written by Niclas Olofsson are Copyright (c) 2014-2018 Niclas Olofsson. 
// All Rights Reserved.

#endregion

namespace MiNET.Net
{
	public partial class McpeText : Packet<McpeText>
	{
		public byte type;
		public bool needsTranslation; // = null
		public string source; // = null;
		public string message; // = null;
		public string xuid; // = null
		public string platformChatId; // = null
		public string[] parameters; // = null
		public string filteredMessage; // = null

		partial void AfterEncode()
		{
			Write(needsTranslation);
			ChatTypes chatType = (ChatTypes) type;
			switch (chatType)
			{
				case ChatTypes.Chat:
				case ChatTypes.Whisper:
				case ChatTypes.Announcement:
					Write((byte) 1);
					Write("chat");
					Write("whisper");
					Write("announcement");
					Write(type);
					Write(source);
					Write(message);
					break;
				case ChatTypes.Raw:
				case ChatTypes.Tip:
				case ChatTypes.System:
				case ChatTypes.Json:
				case ChatTypes.Jsonwhisper:
				case ChatTypes.Jsonannouncement:
					Write((byte) 0);
					Write("raw");
					Write("tip");
					Write("systemMessage");
					Write("textObjectWhisper");
					Write("textObjectAnnouncement");
					Write("textObject");
					Write(type);
					Write(message);
					break;
				case ChatTypes.Popup:
				case ChatTypes.Translation:
				case ChatTypes.Jukeboxpopup:
					Write((byte) 2);
					Write("translate");
					Write("popup");
					Write("jukeboxPopup");
					Write(type);
					Write(message);
					if (parameters == null)
					{
						WriteUnsignedVarInt(0);
					}
					else
					{
						WriteUnsignedVarInt((uint) parameters.Length);
						foreach (var parameter in parameters)
						{
							Write(parameter);
						}
					}
					break;
			}

			Write(xuid);
			Write(platformChatId);
			bool hasFilteredMessage = !string.IsNullOrEmpty(filteredMessage);
			Write(hasFilteredMessage);
			if (hasFilteredMessage) Write(filteredMessage);
		}

		public override void Reset()
		{
			type = 0;
			source = null;
			message = null;

			base.Reset();
		}

		partial void AfterDecode()
		{
			needsTranslation = ReadBool();
			byte category = ReadByte();
			switch (category)
			{
				case 0:
					for (int i = 0; i < 6; i++) ReadString();
					type = ReadByte();
					message = ReadString();
					break;
				case 1:
					for (int i = 0; i < 3; i++) ReadString();
					type = ReadByte();
					source = ReadString();
					message = ReadString();
					break;
				case 2:
					for (int i = 0; i < 3; i++) ReadString();
					type = ReadByte();
					message = ReadString();
					parameters = new string[ReadUnsignedVarInt()];
					for (var i = 0; i < parameters.Length; ++i)
					{
						parameters[i] = ReadString();
					}
					break;
				default:
					throw new System.IO.InvalidDataException($"Unknown text message category {category}");
			}

			xuid = ReadString();
			platformChatId = ReadString();
			filteredMessage = ReadBool() ? ReadString() : null;
		}
	}
}
