using System.Collections.Generic;

namespace MiNET.Net
{
	public sealed class CameraAimAssistPriority
	{
		public string Name { get; set; }
		public int Priority { get; set; }
	}

	public sealed class CameraAimAssistCategory
	{
		public string Name { get; set; }
		public List<CameraAimAssistPriority> EntityPriorities { get; set; } = new();
		public List<CameraAimAssistPriority> BlockPriorities { get; set; } = new();
		public List<CameraAimAssistPriority> BlockTagPriorities { get; set; } = new();
		public int? EntityDefaultPriority { get; set; }
		public int? BlockDefaultPriority { get; set; }
	}

	public sealed class CameraAimAssistItemSettings
	{
		public string ItemId { get; set; }
		public string Category { get; set; }
	}

	public sealed class CameraAimAssistPreset
	{
		public string Identifier { get; set; }
		public List<string> BlockExclusionList { get; set; } = new();
		public List<string> EntityExclusionList { get; set; } = new();
		public List<string> BlockTagExclusionList { get; set; } = new();
		public List<string> LiquidTargetingList { get; set; } = new();
		public List<CameraAimAssistItemSettings> ItemSettings { get; set; } = new();
		public string DefaultItemSettings { get; set; }
		public string HandSettings { get; set; }
	}

	public enum CameraAimAssistPresetsOperation : byte
	{
		Set = 0,
		Add = 1,
		Remove = 2
	}

	public partial class McpeCameraAimAssistPresets : Packet<McpeCameraAimAssistPresets>
	{
		public List<CameraAimAssistCategory> categories = new();
		public List<CameraAimAssistPreset> presets = new();
		public CameraAimAssistPresetsOperation operation;

		partial void AfterEncode()
		{
			WriteUnsignedVarInt((uint) categories.Count);
			foreach (CameraAimAssistCategory category in categories) WriteCategory(category);
			WriteUnsignedVarInt((uint) presets.Count);
			foreach (CameraAimAssistPreset preset in presets) WritePreset(preset);
			Write((byte) operation);
		}

		partial void AfterDecode()
		{
			int categoryCount = (int) ReadUnsignedVarInt();
			categories = new List<CameraAimAssistCategory>(categoryCount);
			for (int i = 0; i < categoryCount; i++) categories.Add(ReadCategory());
			int presetCount = (int) ReadUnsignedVarInt();
			presets = new List<CameraAimAssistPreset>(presetCount);
			for (int i = 0; i < presetCount; i++) presets.Add(ReadPreset());
			operation = (CameraAimAssistPresetsOperation) ReadByte();
		}

		private void WriteCategory(CameraAimAssistCategory value)
		{
			Write(value.Name);
			WritePriorities(value.EntityPriorities);
			WritePriorities(value.BlockPriorities);
			WritePriorities(value.BlockTagPriorities);
			WriteOptionalInt(value.EntityDefaultPriority);
			WriteOptionalInt(value.BlockDefaultPriority);
		}

		private CameraAimAssistCategory ReadCategory() => new()
		{
			Name = ReadString(),
			EntityPriorities = ReadPriorities(),
			BlockPriorities = ReadPriorities(),
			BlockTagPriorities = ReadPriorities(),
			EntityDefaultPriority = ReadOptionalInt(),
			BlockDefaultPriority = ReadOptionalInt()
		};

		private void WritePreset(CameraAimAssistPreset value)
		{
			Write(value.Identifier);
			WriteStrings(value.BlockExclusionList);
			WriteStrings(value.EntityExclusionList);
			WriteStrings(value.BlockTagExclusionList);
			WriteStrings(value.LiquidTargetingList);
			WriteUnsignedVarInt((uint) value.ItemSettings.Count);
			foreach (CameraAimAssistItemSettings setting in value.ItemSettings)
			{
				Write(setting.ItemId);
				Write(setting.Category);
			}
			WriteOptionalString(value.DefaultItemSettings);
			WriteOptionalString(value.HandSettings);
		}

		private CameraAimAssistPreset ReadPreset()
		{
			var value = new CameraAimAssistPreset
			{
				Identifier = ReadString(),
				BlockExclusionList = ReadStringList(),
				EntityExclusionList = ReadStringList(),
				BlockTagExclusionList = ReadStringList(),
				LiquidTargetingList = ReadStringList()
			};
			int count = (int) ReadUnsignedVarInt();
			for (int i = 0; i < count; i++)
			{
				value.ItemSettings.Add(new CameraAimAssistItemSettings { ItemId = ReadString(), Category = ReadString() });
			}
			value.DefaultItemSettings = ReadOptionalString();
			value.HandSettings = ReadOptionalString();
			return value;
		}

		private void WritePriorities(List<CameraAimAssistPriority> values)
		{
			WriteUnsignedVarInt((uint) values.Count);
			foreach (CameraAimAssistPriority value in values)
			{
				Write(value.Name);
				Write(value.Priority);
			}
		}

		private List<CameraAimAssistPriority> ReadPriorities()
		{
			int count = (int) ReadUnsignedVarInt();
			var values = new List<CameraAimAssistPriority>(count);
			for (int i = 0; i < count; i++) values.Add(new CameraAimAssistPriority { Name = ReadString(), Priority = ReadInt() });
			return values;
		}

		private void WriteStrings(List<string> values)
		{
			WriteUnsignedVarInt((uint) values.Count);
			foreach (string value in values) Write(value);
		}

		private List<string> ReadStringList()
		{
			int count = (int) ReadUnsignedVarInt();
			var values = new List<string>(count);
			for (int i = 0; i < count; i++) values.Add(ReadString());
			return values;
		}

		private void WriteOptionalInt(int? value)
		{
			Write(value.HasValue);
			if (value.HasValue) Write(value.Value);
		}

		private int? ReadOptionalInt() => ReadBool() ? ReadInt() : null;

		private void WriteOptionalString(string value)
		{
			Write(value != null);
			if (value != null) Write(value);
		}

		private string ReadOptionalString() => ReadBool() ? ReadString() : null;
	}
}
