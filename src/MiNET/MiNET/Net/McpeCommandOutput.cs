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
// All portions of the code written by Niclas Olofsson are Copyright (c) 2014-2021 Niclas Olofsson.
// All Rights Reserved.
#endregion

using System;

namespace MiNET.Net
{
	public class CommandOutputMessage
	{
		public bool IsInternal { get; set; }
		public string MessageId { get; set; }
		public string[] Parameters { get; set; }

		/// <inheritdoc />
		public override string ToString()
		{
			switch (MessageId)
			{
				case "commands.generic.unknown":
					return $"Unknown command: {Parameters[0]}";
			}
			return $"{{MessageId={MessageId}, IsInternal={IsInternal}, Parameters={String.Join(',', Parameters)}}}";
		}
	}

	public enum CommandOutputType
	{
		Last = 1,
		Silent = 2,
		All = 3,
		DataSet = 4,
	}
	
	public partial class McpeCommandOutput
	{
		public CommandOriginData OriginData { get; set; }
		public CommandOutputType OutputType { get; set; }
		public uint SuccessCount { get; set; }
		public CommandOutputMessage[] Messages { get; set; }
		public string UnknownString { get; set; }

		partial void AfterEncode()
		{
			Write("player");
			Write(OriginData?.UUID ?? default);
			Write(OriginData?.RequestId ?? string.Empty);
			Write(OriginData?.EntityUniqueId ?? 0L);
			Write(OutputType switch
			{
				CommandOutputType.Last => "lastoutput",
				CommandOutputType.Silent => "silent",
				CommandOutputType.All => "alloutput",
				CommandOutputType.DataSet => "dataset",
				_ => "none"
			});
			Write(SuccessCount);
			WriteUnsignedVarInt((uint) (Messages?.Length ?? 0));
			if (Messages != null)
			{
				foreach (var message in Messages)
				{
					Write(message.MessageId ?? string.Empty);
					Write(message.IsInternal);
					WriteUnsignedVarInt((uint) (message.Parameters?.Length ?? 0));
					if (message.Parameters == null) continue;
					foreach (var parameter in message.Parameters) Write(parameter ?? string.Empty);
				}
			}

			bool hasData = UnknownString != null;
			Write(hasData);
			if (hasData) Write(UnknownString);
		}

		partial void AfterDecode()
		{
			OriginData = ReadOriginData();
			OutputType = ReadString() switch
			{
				"lastoutput" => CommandOutputType.Last,
				"silent" => CommandOutputType.Silent,
				"alloutput" => CommandOutputType.All,
				"dataset" => CommandOutputType.DataSet,
				_ => CommandOutputType.Last
			};
			SuccessCount = ReadUint();

			var messageCount = ReadUnsignedVarInt();
			Messages = new CommandOutputMessage[messageCount];

			for (int i = 0; i < Messages.Length; i++)
			{
				Messages[i] = ReadCommandOutputMessage();
			}

			if (ReadBool())
			{
				UnknownString = ReadString();
			}
		}

		private CommandOriginData ReadOriginData()
		{
			ReadString(); // Cereal origin discriminator, currently "player".
			var type = CommandOriginType.Player;
			var uuid = ReadUUID();
			var requestId = ReadString();
			var entityId = ReadLong();

			return new CommandOriginData(type, uuid, requestId, entityId);
		}
		
		private CommandOutputMessage ReadCommandOutputMessage()
		{
			CommandOutputMessage result = new CommandOutputMessage();
			result.MessageId = ReadString();
			result.IsInternal = ReadBool();

			var count = ReadUnsignedVarInt();
			result.Parameters = new string[count];

			for (int i = 0; i < result.Parameters.Length; i++)
			{
				result.Parameters[i] = ReadString();
			}

			return result;
		}
	}
}
