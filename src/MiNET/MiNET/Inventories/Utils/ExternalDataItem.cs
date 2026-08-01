using System.ComponentModel;
using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace MiNET.Inventories
{
	[JsonConverter(typeof(ExternalDataItemConverter))]
	public class ExternalDataItem
	{
		[JsonProperty("name")]
		public string Id { get; set; }

		[JsonProperty("meta")]
		public short Metadata { get; set; }

		[JsonProperty("block_states")]
		public byte[] BlockStates { get; set; }

		[JsonProperty("nbt")]
		public byte[] ExtraData { get; set; }

		[JsonProperty("tag")]
		public string Tag { get; set; }

		[DefaultValue(1)]
		[JsonProperty("count", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
		public int Count { get; set; }
	}

	public sealed class ExternalDataItemConverter : JsonConverter
	{
		public override bool CanWrite => false;

		public override bool CanConvert(Type objectType) => objectType == typeof(ExternalDataItem);

		public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
		{
			JToken token = JToken.Load(reader);
			if (token.Type == JTokenType.Null)
			{
				return null;
			}

			if (token.Type == JTokenType.String)
			{
				return new ExternalDataItem { Id = token.Value<string>(), Count = 1 };
			}

			var data = new ExternalDataItem { Count = 1 };
			using JsonReader objectReader = token.CreateReader();
			serializer.Populate(objectReader, data);
			return data;
		}

		public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer) =>
			throw new NotSupportedException();
	}
}
