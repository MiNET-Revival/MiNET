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
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Numerics;
using System.Threading;
using fNbt;
using log4net;
using MiNET.BlockEntities;
using MiNET.Blocks;
using MiNET.Blocks.States;
using MiNET.Crafting;
using MiNET.Effects;
using MiNET.Entities;
using MiNET.Entities.Passive;
using MiNET.Entities.World;
using MiNET.Inventories;
using MiNET.Items;
using MiNET.Net;
using MiNET.Particles;
using MiNET.UI;
using MiNET.Utils;
using MiNET.Utils.Metadata;
using MiNET.Utils.Nbt;
using MiNET.Utils.Skins;
using MiNET.Utils.Vectors;
using MiNET.Worlds;
using Newtonsoft.Json;

namespace MiNET.Players
{
	public partial class Player : Entity, IMcpeMessageHandler
	{
		private static readonly ILog Log = LogManager.GetLogger(typeof(Player));

		private static readonly UUID WorldTemplateId = new UUID(Guid.NewGuid().ToByteArray());

		private MiNetServer Server { get; set; }
		public IPEndPoint EndPoint { get; private set; }
		public INetworkHandler NetworkHandler { get; set; }

		private Dictionary<ChunkCoordinates, McpeWrapper> _chunksUsed = new Dictionary<ChunkCoordinates, McpeWrapper>();
		private ChunkCoordinates _currentChunkPosition = new ChunkCoordinates(int.MaxValue);

		private IInventory _openInventory;

		public PlayerInventory Inventory { get; set; }
		public ItemStackInventoryManager ItemStackInventoryManager { get; set; }

		public PlayerLocation SpawnPosition { get; set; }
		public bool IsSleeping { get; set; } = false;

		public int MaxViewDistance { get; set; } = 22;
		public int MoveRenderDistance { get; set; } = 1;

		public GameMode GameMode { get; set; }
		public bool UseCreativeInventory { get; set; } = true;
		public bool IsConnected { get; set; }
		public CertificateData CertificateData { get; set; }
		public string Username { get; set; }
		public string DisplayName { get; set; }
		public long ClientId { get; set; }
		public UUID ClientUuid { get; set; }
		public string ServerAddress { get; set; }
		public PlayerInfo PlayerInfo { get; set; }

		public Skin Skin { get; set; }

		public float MovementSpeed { get; set; } = 0.1f;
		public ConcurrentDictionary<EffectType, Effect> Effects { get; set; } = new ConcurrentDictionary<EffectType, Effect>();

		public HungerManager HungerManager { get; set; }
		public ExperienceManager ExperienceManager { get; set; }

		public bool IsFalling { get; set; }
		public bool IsFlyingHorizontally { get; set; }

		public Entity LastAttackTarget { get; set; }

		public List<Popup> Popups { get; set; } = new List<Popup>();

		public Session Session { get; set; }

		public DamageCalculator DamageCalculator { get; set; } = new DamageCalculator();


		public Player(MiNetServer server, IPEndPoint endPoint) : base(EntityType.None, null)
		{
			Server = server;
			EndPoint = endPoint;

			Inventory = new PlayerInventory(this);
			HungerManager = new HungerManager(this);
			ExperienceManager = new ExperienceManager(this);
			ItemStackInventoryManager = new ItemStackInventoryManager(this);

			IsSpawned = false;
			IsConnected = endPoint != null; // Can't connect if there is no endpoint

			Width = 0.6f;
			Length = Width;
			Height = 1.80;

			HideNameTag = false;
			IsAlwaysShowName = true;
			CanClimb = true;
			HasCollision = true;
			IsAffectedByGravity = true;
			NoAi = false;
		}

	}

	public class PlayerEventArgs : EventArgs
	{
		public Player Player { get; }
		public Level Level { get; }

		public PlayerEventArgs(Player player)
		{
			Player = player;
			Level = player?.Level;
		}
	}
}
