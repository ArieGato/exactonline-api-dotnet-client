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
			JsonNode? root = JsonNode.Parse(response);
			if (root is null)
				throw new IncorrectJsonException("JSON is null.");

			// Equivalent to jtoken["d"] as JObject
			JsonObject? dObj = root["d"] as JsonObject;
			if (dObj is null)
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
		var oldCulture = Thread.CurrentThread.CurrentCulture;
		Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;

		try
		{
			JsonNode? root = JsonNode.Parse(response);

			JsonArray? results = null;

			// Equivalent to: if (jtoken["d"] is JObject dObject && dObject["results"] is JArray resultsArray)
			if (root?["d"] is JsonObject dObject && dObject["results"] is JsonArray resultsArray)
			{
				results = resultsArray;
			}
			// Equivalent to: else if (jtoken["d"] is JArray dArray)
			else if (root?["d"] is JsonArray dArray)
			{
				results = dArray;
			}
			else
			{
				throw new Exception("No ['d']['results'] token found in response");
			}

			return GetJsonFromArray(results);
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

	private static string GetJsonFromObject(JsonObject jsonObject)
	{
		var json = "{";

		foreach (var entry in jsonObject)
		{
			JsonNode? value = entry.Value;

			// Equivalent to: entry.Value is JValue
			if (value is JsonValue jsonValue)
			{
				json += "\"" + entry.Key + "\":";

				object? clrValue = jsonValue.GetValue<object?>();

				if (clrValue == null)
				{
					json += "null";
				}
				else
				{
					json += JsonSerializer.Serialize(clrValue);
				}

				json += ",";
			}
			// Equivalent to:
			// entry.Value is JObject subcollection
			// && subcollection.ContainsKey("results")
			// && subcollection["results"] is JArray
			else if (
				value is JsonObject subObject &&
				subObject.TryGetPropertyValue("results", out JsonNode? resultsNode) &&
				resultsNode is JsonArray results
			)
			{
				var subjson = GetJsonFromArray(results);

				if (subjson.Length > 0)
				{
					json += "\"" + entry.Key + "\":";
					json += subjson;
					json += ",";
				}
			}
		}

		// Remove trailing comma if present
		if (json.EndsWith(","))
			json = json.Remove(json.Length - 1, 1);

		json += "}";

		return json;
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
