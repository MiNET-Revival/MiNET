using System.Collections.Generic;
using System.Numerics;
using MiNET.Utils.Vectors;

namespace MiNET.Net;

public partial class McpePlayerAuthInput : Packet<McpePlayerAuthInput>
{
	/// <summary>
	///		Pitch and Yaw hold the rotation that the player reports it has.
	/// </summary>
	public float Pitch;

	/// <summary>
	/// Pitch and Yaw hold the rotation that the player reports it has.
	/// </summary>
	public float Yaw;

	/// <summary>
	///		Pitch and Yaw hold the rotation that the player reports it has.
	/// </summary>
	public float HeadYaw;

	/// <summary>
	///		 Position holds the position that the player reports it has.
	/// </summary>
	public Vector3 Position;

	/// <summary>
	///		MoveVector is a Vec2 that specifies the direction in which the player moved, as a combination of X/Z
	///		values which are created using the WASD/controller stick state.
	/// </summary>
	public Vector2 MoveVector;

	/// <summary>
	///		InputData is a combination of bit flags that together specify the way the player moved last tick.
	/// </summary>
	public AuthInputFlags InputFlags;

	/// <summary>
	///  InputMode specifies the way that the client inputs data to the screen.
	/// </summary>
	public PlayerInputMode InputMode;

	/// <summary>
	/// PlayMode specifies the way that the player is playing.
	/// </summary>
	public PlayerPlayMode PlayMode;

	/// <summary>
	///		InteractionModel is a constant representing the interaction model the player is using.
	/// </summary>
	public PlayerInteractionModel InteractionModel;

	public Vector2 InteractRotation;

	/// <summary>
	///		GazeDirection is the direction in which the player is gazing, when the PlayMode is PlayModeReality: In other words, when the player is playing in virtual reality.
	/// </summary>
	public Vector3 GazeDirection;

	/// <summary>
	///		Tick is the server tick at which the packet was sent. It is used in relation to CorrectPlayerMovePrediction.
	/// </summary>
	public long Tick;

	/// <summary>
	///		Delta was the delta between the old and the new position.
	/// </summary>
	public Vector3 Delta;

	public Transaction ItemInteraction;

	public ItemStackRequests ItemStackRequests;

	public PlayerAuthInputVehicleInfo VehicleInfo;

	public PlayerBlockActions BlockActions = new PlayerBlockActions();

	public Vector2 AnalogMoveVector;

	public Vector3 CameraOrientation;

	public Vector2 RawMoveVector;

	partial void AfterDecode()
	{
		Pitch = ReadFloat();
		Yaw = ReadFloat();
		Position = ReadVector3();
		MoveVector = ReadVector2();
		HeadYaw = ReadFloat();
		InputFlags = (AuthInputFlags) ReadUnsignedVarLong();
		InputMode = (PlayerInputMode) ReadUnsignedVarInt();
		PlayMode = (PlayerPlayMode) ReadUnsignedVarInt();
		InteractionModel = (PlayerInteractionModel) ReadSignedVarInt();
		InteractRotation = ReadVector2();

		if (PlayMode == PlayerPlayMode.VR)
		{
			GazeDirection = ReadVector3();
		}

		Tick = ReadUnsignedVarLong();
		Delta = ReadVector3();

		if ((InputFlags & AuthInputFlags.PerformItemInteraction) != 0)
		{
			ItemInteraction = ReadPlayerAuthInputItemInteraction();
		}

		if ((InputFlags & AuthInputFlags.PerformItemStackRequest) != 0)
		{
			ItemStackRequests = new ItemStackRequests { ItemStackActionList.Read(this) };
		}

		if ((InputFlags & AuthInputFlags.PerformBlockActions) != 0)
		{
			BlockActions = PlayerBlockActions.Read(this);
		}

		if ((InputFlags & AuthInputFlags.InClientPredictedVehicle) != 0)
		{
			VehicleInfo = PlayerAuthInputVehicleInfo.Read(this);
		}

		AnalogMoveVector = ReadVector2();
		CameraOrientation = ReadVector3();
		RawMoveVector = ReadVector2();
	}

	partial void AfterEncode()
	{
		Write(Pitch);
		Write(Yaw);
		Write(Position);
		Write(MoveVector);
		Write(HeadYaw);
		WriteUnsignedVarLong((long) InputFlags);
		WriteUnsignedVarInt((uint) InputMode);
		WriteUnsignedVarInt((uint) PlayMode);
		WriteSignedVarInt((int) InteractionModel);
		Write(InteractRotation);

		if (PlayMode == PlayerPlayMode.VR)
		{
			Write(GazeDirection);
		}

		WriteUnsignedVarLong(Tick);
		Write(Delta);

		if ((InputFlags & AuthInputFlags.PerformItemInteraction) != 0)
		{
			Write(ItemInteraction);
		}

		if ((InputFlags & AuthInputFlags.PerformItemStackRequest) != 0)
		{
			if (ItemStackRequests == null || ItemStackRequests.Count == 0)
			{
				Write(new ItemStackActionList());
			}
			else
			{
				Write(ItemStackRequests[0]);
			}
		}

		if ((InputFlags & AuthInputFlags.PerformBlockActions) != 0)
		{
			Write(BlockActions);
		}

		if ((InputFlags & AuthInputFlags.InClientPredictedVehicle) != 0)
		{
			Write(VehicleInfo);
		}

		Write(AnalogMoveVector);
		Write(CameraOrientation);
		Write(RawMoveVector);
	}

	private Transaction ReadPlayerAuthInputItemInteraction()
	{
		var requestId = ReadSignedVarInt();
		var requestRecords = new List<RequestRecord>();

		if (requestId != 0)
		{
			var recordsCount = ReadLength();
			for (var i = 0; i < recordsCount; i++)
			{
				requestRecords.Add(RequestRecord.Read(this));
			}
		}

		var records = new List<TransactionRecord>();
		var recordCount = ReadLength();
		for (var i = 0; i < recordCount; i++)
		{
			records.Add(TransactionRecord.Read(this));
		}

		var transaction = ItemUseTransaction.ReadData(this);
		transaction.RequestId = requestId;
		transaction.RequestRecords = requestRecords;
		transaction.TransactionRecords = records;
		return transaction;
	}

	/// <inheritdoc />
	public override void Reset()
	{
		base.Reset();
		Pitch = Yaw = HeadYaw = 0f;
		MoveVector = Vector2.Zero;
		Position = Vector3.Zero;
		InputFlags = 0;
		InputMode = PlayerInputMode.Mouse;
		PlayMode = PlayerPlayMode.Normal;
		InteractionModel = PlayerInteractionModel.Touch;
		InteractRotation = Vector2.Zero;
		Tick = 0;
		Delta = Vector3.Zero;
		ItemInteraction = null;
		ItemStackRequests = null;
		VehicleInfo = null;
		BlockActions.Clear();
		AnalogMoveVector = Vector2.Zero;
		CameraOrientation = Vector3.Zero;
		RawMoveVector = Vector2.Zero;
	}

	public enum PlayerPlayMode
	{
		Normal = 0,
		Teaser = 1,
		Screen = 2,
		Viewer = 3,
		VR = 4,
		Placement = 5,
		LivingRoom = 6,
		ExitLevel = 7,
		ExitLevelLivingRoom = 8,
		NumModes = 9
	}

	public enum PlayerInputMode
	{
		Unknown = 0,
		Mouse = 1,
		Touch = 2,
		GamePad = 3,
		MotionController = 4
	}

	public enum PlayerInteractionModel
	{
		Touch = 0,
		Crosshair = 1,
		Classic = 2
	}
}

public class PlayerBlockActions : List<PlayerBlockAction>, IPacketDataObject
{
	public void Write(Packet packet)
	{
		packet.WriteSignedVarInt(Count);

		foreach (var action in this)
		{
			action.Write(packet);
		}
	}

	public static PlayerBlockActions Read(Packet packet)
	{
		var actions = new PlayerBlockActions();
		var count = packet.ReadSignedVarInt();

		for (var i = 0; i < count; i++)
		{
			actions.Add(PlayerBlockAction.Read(packet));
		}

		return actions;
	}
}

public class PlayerBlockAction : IPacketDataObject
{
	public PlayerAction Action { get; set; }
	public BlockCoordinates Coordinates { get; set; }
	public int Face { get; set; }
	public bool HasBlockPosition { get; set; }

	public void Write(Packet packet)
	{
		packet.WriteSignedVarInt((int) Action);

		if (HasBlockPosition)
		{
			WriteSignedBlockCoordinates(packet, Coordinates);
			packet.WriteSignedVarInt(Face);
		}
	}

	public static PlayerBlockAction Read(Packet packet)
	{
		var action = new PlayerBlockAction
		{
			Action = (PlayerAction) packet.ReadSignedVarInt()
		};

		switch (action.Action)
		{
			case PlayerAction.StartBreak:
			case PlayerAction.AbortBreak:
			case PlayerAction.Breaking:
			case PlayerAction.PredictDestroyBlock:
			case PlayerAction.ContinueDestroyBlock:
				action.Coordinates = ReadSignedBlockCoordinates(packet);
				action.Face = packet.ReadSignedVarInt();
				action.HasBlockPosition = true;
				break;
		}

		return action;
	}

	private static BlockCoordinates ReadSignedBlockCoordinates(Packet packet)
	{
		return new BlockCoordinates(packet.ReadSignedVarInt(), packet.ReadSignedVarInt(), packet.ReadSignedVarInt());
	}

	private static void WriteSignedBlockCoordinates(Packet packet, BlockCoordinates coordinates)
	{
		packet.WriteSignedVarInt(coordinates.X);
		packet.WriteSignedVarInt(coordinates.Y);
		packet.WriteSignedVarInt(coordinates.Z);
	}
}