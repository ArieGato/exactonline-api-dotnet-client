using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace ExactOnline.Client.Sdk.Helpers;

public sealed class MicrosoftDateTimeConverter : JsonConverter<DateTime>
{
	private static readonly Regex Regex =
		new(@"^/Date\((\d+)([+-]\d{4})?\)/$", RegexOptions.Compiled);

	public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
	{
		var s = reader.GetString();

		if (DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dateTime))
		{
			return dateTime;
		}

		var match = Regex.Match(s ?? "");

		if (!match.Success)
			throw new JsonException("Invalid Microsoft date format.");

		var ms = long.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
		return DateTimeOffset.FromUnixTimeMilliseconds(ms).UtcDateTime;
	}

	public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
	{
		var ms = new DateTimeOffset(value).ToUnixTimeMilliseconds();
		writer.WriteStringValue($"/Date({ms})/");
	}
}
