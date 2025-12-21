using System.Collections;
using System.Dynamic;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace ExactOnline.Client.Sdk.Helpers;

public sealed class MutableJsonDynamic : DynamicObject, IEnumerable
{
	private static readonly Regex Regex =
		new(@"^/Date\((\d+)([+-]\d{4})?\)/$", RegexOptions.Compiled);

	private JsonNode _node;

	private MutableJsonDynamic(JsonNode node)
	{
		_node = node ?? throw new ArgumentNullException(nameof(node));
	}

	public static dynamic FromNode(JsonNode node) => new MutableJsonDynamic(node);

	public override bool TryGetMember(GetMemberBinder binder, out object? result)
	{
		if (_node is JsonObject obj && obj.TryGetPropertyValue(binder.Name, out var value))
		{
			result = Wrap(value);
			return true;
		}

		result = null;
		return true; // match common "dynamic JSON" behavior (missing => null)
	}

	public override bool TrySetMember(SetMemberBinder binder, object value)
	{
		EnsureObject();
		((JsonObject)_node!)[binder.Name] = ToJsonNode(value);
		return true;
	}

	public override bool TryGetIndex(GetIndexBinder binder, object[] indexes, out object? result)
	{
		result = null;

		if (indexes.Length != 1)
			return false;

		if (_node is JsonObject obj && indexes[0] is string key)
		{
			obj.TryGetPropertyValue(key, out var value);
			result = Wrap(value);
			return true;
		}

		if (_node is JsonArray arr && indexes[0] is int i)
		{
			if (i < 0 || i >= arr.Count)
			{
				result = null;
				return true;
			}

			result = Wrap(arr[i]);
			return true;
		}

		return false;
	}

	public override bool TrySetIndex(SetIndexBinder binder, object[] indexes, object value)
	{
		if (indexes.Length != 1)
			return false;

		if (_node is JsonObject obj && indexes[0] is string key)
		{
			obj[key] = ToJsonNode(value);
			return true;
		}

		if (_node is JsonArray arr && indexes[0] is int i)
		{
			if (i < 0) return false;

			// Expand array if needed (optional behavior, but usually convenient)
			while (arr.Count <= i)
				arr.Add(null);

			arr[i] = ToJsonNode(value);
			return true;
		}

		return false;
	}

	public override bool TryConvert(ConvertBinder binder, out object result)
	{
		// Convert primitive nodes into CLR types when assigned to concrete types
		var val = UnwrapPrimitive(_node);

		if (val is null)
		{
			result = null;
			return !binder.Type.IsValueType || Nullable.GetUnderlyingType(binder.Type) != null;
		}

		if (binder.Type.IsInstanceOfType(val))
		{
			result = val;
			return true;
		}

		try
		{
			result = Convert.ChangeType(val, binder.Type, CultureInfo.InvariantCulture);
			return true;
		}
		catch
		{
			result = null;
			return false;
		}
	}

	public override string ToString() => _node?.ToJsonString(new JsonSerializerOptions { WriteIndented = false });

	public string ToJson(bool indented = true)
	{
		var jsonSerializerOptions = new JsonSerializerOptions { WriteIndented = indented };
		jsonSerializerOptions.Converters.Add(new MicrosoftDateTimeConverter());
		return _node?.ToJsonString(jsonSerializerOptions) ?? "null";
	}

	private void EnsureObject()
	{
		if (_node is JsonObject)
			return;

		// If current node is not an object, replace it.
		_node = new JsonObject();
	}

	private static object? Wrap(JsonNode? node)
	{
		if (node is null) return null;

		// primitives become CLR values
		var primitive = UnwrapPrimitive(node);
		if (node is JsonValue)
			return primitive;

		// objects/arrays stay dynamic wrappers so writes propagate
		return new MutableJsonDynamic(node);
	}

	private static object? UnwrapPrimitive(JsonNode? node)
	{
		if (node is not JsonValue v) return null;

		if (v.TryGetValue<string>(out var s))
		{
			var match = Regex.Match(s);
			if (!match.Success)
				return s;

			var ms = long.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
			return DateTimeOffset
				.FromUnixTimeMilliseconds(ms)
				.UtcDateTime.ToString(CultureInfo.InvariantCulture);
		}

		if (v.TryGetValue<bool>(out var b)) return b;
		if (v.TryGetValue<long>(out var l)) return l.ToString(CultureInfo.InvariantCulture);
		if (v.TryGetValue<double>(out var d)) return d.ToString(CultureInfo.InvariantCulture);

		// fallback
		return v.ToJsonString();
	}

	private static JsonNode? ToJsonNode(object? value)
	{
		return value switch
		{
			null => null,
			MutableJsonDynamic mjd => mjd._node,
			JsonNode node => node,
			JsonElement element => JsonNode.Parse(element.GetRawText()),
			_ => JsonValue.Create(value)
		};
	}

	public IEnumerator GetEnumerator()
	{
		return _node switch
		{
			JsonArray arr => arr.Select(Wrap).GetEnumerator(),
			JsonObject obj => obj.Select(kvp => new KeyValuePair<string, object?>(kvp.Key, Wrap(kvp.Value)))
				.GetEnumerator(),
			_ => Enumerable.Empty<object>().GetEnumerator()
		};
	}
}
