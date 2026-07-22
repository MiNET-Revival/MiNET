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
// All portions of the code written by Niclas Olofsson are Copyright (c) 2014-2020 Niclas Olofsson.
// All Rights Reserved.

#endregion

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using System.Transactions;
using fNbt;
using log4net;
using MiNET.BlockEntities;
using MiNET.Blocks;
using MiNET.Entities;
using MiNET.Entities.Hostile;
using MiNET.Entities.Passive;
using MiNET.Entities.World;
using MiNET.Inventories;
using MiNET.Items;
using MiNET.Net;
using MiNET.Net.RakNet;
using MiNET.Sounds;
using MiNET.Utils;
using MiNET.Utils.Diagnostics;
using MiNET.Utils.IO;
using MiNET.Utils.Nbt;
using MiNET.Utils.Vectors;
using MiNET.Worlds.Anvil;

namespace MiNET.Worlds
{
	public partial class Level
	{
		public virtual void BroadcastTitle(string text, TitleType type = TitleType.Title, int fadeIn = 6, int fadeOut = 6, int stayTime = 20, Player sender = null, Player[] sendList = null)
		{
			var mcpeSetTitle = McpeSetTitle.CreateObject();
			mcpeSetTitle.fadeInTime = fadeIn;
			mcpeSetTitle.stayTime = stayTime;
			mcpeSetTitle.fadeOutTime = fadeOut;
			mcpeSetTitle.type = (int) type;
			mcpeSetTitle.text = text;

			RelayBroadcast(sender, sendList, mcpeSetTitle);
		}

		public virtual void BroadcastMessage(string text, MessageType type = MessageType.Chat, Player sender = null, Player[] sendList = null, bool needsTranslation = false, string[] parameters = null)
		{
			if (type == MessageType.Chat || type == MessageType.Raw)
			{
				foreach (var line in text.Split(new string[] {"\n", Environment.NewLine}, StringSplitOptions.RemoveEmptyEntries))
				{
					McpeText message = McpeText.CreateObject();
					message.type = (byte) type;
					message.source = sender == null ? "" : sender.Username;
					message.message = line;
					message.needsTranslation = needsTranslation;
					message.parameters = parameters;
					RelayBroadcast(sendList, message);
				}
			}
			else
			{
				McpeText message = McpeText.CreateObject();
				message.type = (byte) type;
				message.source = sender == null ? "" : sender.Username;
				message.message = text;
				message.needsTranslation = needsTranslation;
				message.parameters = parameters;
				RelayBroadcast(sendList, message);
			}
		}
	}
}