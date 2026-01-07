using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using ExactOnline.Client.Sdk.Exceptions;

namespace ExactOnline.Client.Sdk.Helpers;

/// <summary>
/// Class for stripping unnecessary Json tags from API Response
/// </summary>
public static class ApiResponseCleaner
{
	/// <summary>
	/// Fetch Json Object (Json within ['d'] name/value pair) from response
	/// </summary>
	/// <param name="response"></param>
	/// <returns></returns>
	public static string GetJsonObject(string response)
	{
		var oldCulture = Thread.CurrentThread.CurrentCulture;
		Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;

		try
		{
			var root = JsonNode.Parse(response);
			if (root is null)
				throw new IncorrectJsonException("JSON is null.");

			if (root["d"] is not JsonObject dObj)
				throw new IncorrectJsonException("Property 'd' is missing or not an object.");

			return GetJsonFromObject(dObj);
		}
		catch (JsonException e)
		{
			throw new IncorrectJsonException(e.Message);
		}
		catch (Exception e)
		{
			throw new IncorrectJsonException(e.Message);
		}
		finally
		{
			Thread.CurrentThread.CurrentCulture = oldCulture;
		}
	}

	public static string? GetSkipToken(string response)
	{
		var oldCulture = Thread.CurrentThread.CurrentCulture;
		Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;

		string? token = null;

		try
		{
			JsonNode? root = JsonNode.Parse(response);

			// Equivalent to: jtoken["d"] is JObject dObject
			if (root?["d"] is JsonObject dObject)
			{
				// Equivalent to: dObject.ContainsKey("__next")
				if (dObject.TryGetPropertyValue("__next", out JsonNode? nextNode) && nextNode is not null)
				{
					var next = nextNode.ToString();

					// Skiptoken has format "$skiptoken=xyz" in the url and we want to extract xyz.
					var match = Regex.Match(next, @"\$skiptoken=([^&#]*)");

					token = match.Success ? match.Groups[1].Value : null;
				}
			}
		}
		catch (JsonException e)
		{
			throw new IncorrectJsonException(e.Message);
		}
		catch (Exception e)
		{
			throw new IncorrectJsonException(e.Message);
		}
		finally
		{
			Thread.CurrentThread.CurrentCulture = oldCulture;
		}

		return token;
	}

	public static string GetJsonArray(string response)
	{
		try
		{
			var root = JsonNode.Parse(response);
			var results = root?["d"] switch
			{
				JsonArray array => array,
				JsonObject dObject when dObject["results"] is JsonArray array => array,
				_ => throw new Exception("No ['d']['results'] token found in response")
			};

			return GetJsonFromArray(results);
		}
		catch (Exception e)
		{
			throw new IncorrectJsonException(e.Message);
		}
	}

	private static string GetJsonFromObject(JsonObject jsonObject)
	{
		var json = new System.Text.StringBuilder();
		json.Append('{');

		foreach (var entry in jsonObject)
		{
			var value = entry.Value;

			// Equivalent to: entry.Value is JValue
			if (value is JsonValue jsonValue)
			{
				json.Append('\"').Append(entry.Key).Append("\":");

				var clrValue = jsonValue.GetValue<object?>();
				if (clrValue == null)
				{
					json.Append("null");
				}
				else
				{
					json.Append(JsonSerializer.Serialize(clrValue));
				}

				json.Append(',');
			}
			else if (
				value is JsonObject subObject &&
				subObject.TryGetPropertyValue("results", out var resultsNode) &&
				resultsNode is JsonArray results
			)
			{
				var subJson = GetJsonFromArray(results);

				if (subJson.Length > 0)
				{
					json.Append('\"').Append(entry.Key).Append("\":");
					json.Append(subJson);
					json.Append(',');
				}
			}
		}

		// Remove trailing comma if present
		if (json.Length > 1 && json[json.Length - 1] == ',')
			json.Length--;

		json.Append('}');

		return json.ToString();
	}

	private static string GetJsonFromArray(JsonArray results)
	{
		var json = "[";

		if (results != null && results.Count > 0)
		{
			foreach (var entity in results)
			{
				if (entity is JsonObject obj)
				{
					json += GetJsonFromObject(obj) + ",";
				}
			}

			// Remove last comma
			if (json.EndsWith(","))
				json = json.Remove(json.Length - 1, 1);
		}

		json += "]";
		return json;
	}
}
