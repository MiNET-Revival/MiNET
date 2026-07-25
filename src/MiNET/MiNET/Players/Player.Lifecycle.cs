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
	public partial class Player
	{
		private object _loginSyncLock = new object();

		public virtual void HandleMcpeLogin(McpeLogin message)
		{
			// Do nothing
		}

		public void Start(object o)
		{
			Stopwatch watch = new Stopwatch();
			watch.Restart();

			var serverInfo = Server.ConnectionInfo;

			try
			{
				Session = Server.SessionManager.CreateSession(this);

				lock (_disconnectSync)
				{
					if (!IsConnected) return;

					if (Level != null) return; // Already called this method.

					Level = Server.LevelManager.GetLevel(this, Dimension.Overworld.ToString());
				}

				if (Level == null)
				{
					Disconnect("No level assigned.");
					return;
				}

				OnPlayerJoining(new PlayerEventArgs(this));

				SpawnPosition = (PlayerLocation) (SpawnPosition ?? Level.SpawnPoint).Clone();
				KnownPosition = (PlayerLocation) SpawnPosition.Clone();

				// Check if the user already exist, that case bumpt the old one
				Level.RemoveDuplicatePlayers(Username, ClientId);

				Level.EntityManager.AddEntity(this);

				GameMode = Config.GetProperty("Player.GameMode", Level.GameMode);

				//
				// Start game - spawn sequence starts here
				//

				// Vanilla 1st player list here

				//Level.AddPlayer(this, false);

				SendSetTime();

				SendStartGame();

				SendItemRegistry();

				SetGameMode(GameMode);

				SendAvailableEntityIdentifiers();

				SendBiomeDefinitionList();

				BroadcastSetEntityData();

				if (ChunkRadius == -1) ChunkRadius = 5;

				SendChunkRadiusUpdate();

				//SendSetSpawnPosition();

				SendSetTime();

				SendSetDificulty();

				SendSetCommandsEnabled();

				SendAdventureSettings();

				SendGameRules();

				// Vanilla 2nd player list here

				Level.AddPlayer(this, false);

				SendUpdateAttributes();

				SendPlayerInventory();

				SendCreativeInventory();

				SendCraftingRecipes();

				SendAvailableCommands(); // Don't send this before StartGame!

				SendNetworkChunkPublisherUpdate();
				MiNetServer.FastThreadPool.QueueUserWorkItem(SendChunksForKnownPosition);
			}
			catch (Exception e)
			{
				Log.Error(e);
			}
			finally
			{
				Interlocked.Decrement(ref serverInfo.ConnectionsInConnectPhase);
			}

			LastUpdatedTime = DateTime.UtcNow;
			Log.InfoFormat("Login complete by: {0} from {2} in {1}ms", Username, watch.ElapsedMilliseconds, EndPoint);
		}

		public virtual void SendAvailableEntityIdentifiers()
		{
			var nbt = new Nbt
			{
				NbtFile = new NbtFile
				{
					Flavor = NbtFlavor.Bedrock,
					RootTag = new NbtCompound("") {EntityHelpers.GenerateEntityIdentifiers()}
				}
			};

			var pk = McpeAvailableEntityIdentifiers.CreateObject();
			pk.namedtag = nbt;
			SendPacket(pk);
		}

		public virtual void SendBiomeDefinitionList()
		{
			var stringList = new BiomeStringList();

			var pk = McpeBiomeDefinitionList.CreateObject();
			pk.biomeDefinitions = BiomeDefinitions.FromBiomes(BiomeUtils.IdBiomeMap.Values, stringList);
			pk.stringList = stringList;
			SendPacket(pk);
		}

		public bool EnableCommands { get; set; } = Config.GetProperty("EnableCommands", false);

		protected virtual void SendSetCommandsEnabled()
		{
			McpeSetCommandsEnabled enabled = McpeSetCommandsEnabled.CreateObject();
			enabled.enabled = EnableCommands;
			SendPacket(enabled);
		}

		protected virtual void SendAvailableCommands()
		{
			//return;
			//var settings = new JsonSerializerSettings();
			//settings.NullValueHandling = NullValueHandling.Ignore;
			//settings.DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate;
			//settings.MissingMemberHandling = MissingMemberHandling.Error;
			//settings.Formatting = Formatting.Indented;
			//settings.ContractResolver = new CamelCasePropertyNamesContractResolver();

			//var content = JsonConvert.SerializeObject(Server.PluginManager.Commands, settings);

			McpeAvailableCommands commands = McpeAvailableCommands.CreateObject();
			commands.CommandSet = Server.PluginManager.Commands;
			//commands.commands = content;
			//commands.unknown = "{}";
			SendPacket(commands);
		}

		public virtual void HandleMcpeCommandRequest(McpeCommandRequest message)
		{
			Log.Debug($"UUID: {message.unknownUuid}");

			var result = Server.PluginManager.HandleCommand(this, message.command);
			if (result is string)
			{
				string sRes = result as string;
				SendMessage(sRes);
			}

			//var jsonSerializerSettings = new JsonSerializerSettings
			//{
			//	PreserveReferencesHandling = PreserveReferencesHandling.None,
			//	Formatting = Formatting.Indented,
			//};

			//var commandJson = JsonConvert.DeserializeObject<dynamic>(message.commandInputJson);
			//Log.Debug($"CommandJson\n{JsonConvert.SerializeObject(commandJson, jsonSerializerSettings)}");
			//object result = Server.PluginManager.HandleCommand(this, message.commandName, message.commandOverload, commandJson);
			//if (result != null)
			//{
			//	var settings = new JsonSerializerSettings();
			//	settings.NullValueHandling = NullValueHandling.Ignore;
			//	settings.DefaultValueHandling = DefaultValueHandling.Include;
			//	settings.MissingMemberHandling = MissingMemberHandling.Error;
			//	settings.Formatting = Formatting.Indented;
			//	settings.StringEscapeHandling = StringEscapeHandling.EscapeNonAscii;
			//	settings.ContractResolver = new CamelCasePropertyNamesContractResolver();

			//	var content = JsonConvert.SerializeObject(result, settings);
			//	McpeCommandRequest commandResult = McpeCommandRequest.CreateObject();
			//	commandResult.commandName = message.commandName;
			//	commandResult.commandOverload = message.commandOverload;
			//	commandResult.isOutput = true;
			//	commandResult.clientId = NetworkHandler.GetNetworkNetworkIdentifier();
			//	commandResult.commandInputJson = "null\n";
			//	commandResult.commandOutputJson = content;
			//	commandResult.entityIdSelf = EntityId;
			//	SendPackage(commandResult);

			//	if (Log.IsDebugEnabled) Log.Debug($"NetworkId={commandResult.clientId}, Command Respone\n{Package.ToJson(commandResult)}\nJSON:\n{content}");
			//}
		}

		public virtual void InitializePlayer()
		{
			if (IsSpawned) return;

			// Send set health

			SendSetEntityData();

			SendPlayerStatus(3);

			//send time again
			SendSetTime();
			IsSpawned = true;

			var initializedPosition = _lastClientAuthInputPosition ?? SpawnPosition;
			SetPosition(initializedPosition);

			LastUpdatedTime = DateTime.UtcNow;
			_haveJoined = true;

			OnPlayerJoin(new PlayerEventArgs(this));
		}

		//public virtual void HandleMcpeRespawn()
		//{
		//	HandleMcpeRespawn(null);
		//}

		public virtual void HandleMcpeRespawn(McpeRespawn message)
		{
			if (message.state == (byte) McpeRespawn.RespawnState.ClientReady)
			{
				HealthManager.ResetHealth();

				HungerManager.ResetHunger();

				BroadcastSetEntityData();

				SendUpdateAttributes();

				SendSetSpawnPosition();

				SendAdventureSettings();

				SendPlayerInventory();

				CleanCache();

				ForcedSendChunk(SpawnPosition, false);

				// send teleport to spawn
				SetPosition(SpawnPosition);

				Level.SpawnToAll(this);

				IsSpawned = true;

				Log.InfoFormat("Respawn player {0} on level {1}", Username, Level.LevelId);

				SendSetTime();

				MiNetServer.FastThreadPool.QueueUserWorkItem(() => ForcedSendChunks());

				//SendPlayerStatus(3);

				var mcpeRespawn = McpeRespawn.CreateObject();
				mcpeRespawn.x = SpawnPosition.X;
				mcpeRespawn.y = SpawnPosition.Y;
				mcpeRespawn.z = SpawnPosition.Z;
				mcpeRespawn.state = (byte) McpeRespawn.RespawnState.Ready;
				mcpeRespawn.runtimeEntityId = EntityId;
				SendPacket(mcpeRespawn);

				////send time again
				//SendSetTime();
				//IsSpawned = true;
				//LastUpdatedTime = DateTime.UtcNow;
				//_haveJoined = true;
			}
			else
			{
				Log.Warn($"Unhandled respawn state = {message.state}");
			}
		}
	}
}
