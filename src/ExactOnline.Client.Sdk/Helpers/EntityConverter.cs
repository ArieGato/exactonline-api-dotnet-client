using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using ExactOnline.Client.Sdk.Controllers;
using ExactOnline.Client.Sdk.Exceptions;

namespace ExactOnline.Client.Sdk.Helpers;

/// <summary>
/// Convert entities from json to Exact Online object and vice versa
/// </summary>
public static class EntityConverter
{
	/// <summary>
	/// Convert single object to Dynamic object
	/// </summary>
	public static dynamic ConvertJsonToDynamicObject(string json)
	{
		try
		{
			var jsonNode = JsonNode.Parse(json)
			          ?? throw new IncorrectJsonException();

			return MutableJsonDynamic.FromNode(jsonNode);
		}
		catch
		{
			throw new IncorrectJsonException();
		}
	}

	/// <summary>
	/// Convert multiple objects to List of Dynamic objects
	/// </summary>
	public static List<dynamic> ConvertJsonToDynamicObjectList(string json)
	{
		try
		{
			var arr = JsonNode.Parse(json) as JsonArray
			          ?? throw new IncorrectJsonException();

			return arr
				.Where(n => n is not null)
				.Select(n => MutableJsonDynamic.FromNode(n!))
				.ToList();
		}
		catch (JsonException exception)
		{
			throw new IncorrectJsonException("Json is incorrect.", exception);
		}
	}

	/// <summary>
	/// Converts Dynamic Object to Json String
	/// </summary>
	/// <param name="obj">Dynamic Object to Convert</param>
	/// <returns>Json String</returns>
	public static string ConvertDynamicObjectToJson(dynamic obj) => JsonSerializer.Serialize(obj);

	/// <summary>
	/// Converts an Exact Online Object to Json
	/// </summary>
	/// <typeparam name="T">Type of Exact.Web.Api.Models</typeparam>
	/// <param name="entity">entity</param>
	/// <returns>Json String</returns>
	public static string ConvertObjectToJson<T>(T entity)
	{
		var options = GetJsonSerializerOptions();
		return JsonSerializer.Serialize(entity, options);
	}

	/// <summary>
	/// Converts an Object to Json for Updating
	/// The method creates Json using the original entity 
	/// and the current entity to create Json only for altered fields
	/// </summary>
	/// <typeparam name="T">Type of Exact.Web.Api.Models</typeparam>
	/// <param name="originalEntity">Original State of the Entity</param>
	/// <param name="entity">Current State of the Entity</param>
	/// <param name="getEntityControllerFunc">Delegate for entity controller</param>
	/// <returns>Json String</returns>
	public static string ConvertObjectToJson<T>(T originalEntity, T entity, Func<object, EntityController> getEntityControllerFunc)
	{
		var options = GetJsonSerializerOptions(originalEntity, getEntityControllerFunc);
		return JsonSerializer.Serialize(entity, options);
	}

	/// <summary>
	/// Convert Json to Exact Online Object
	/// </summary>
	/// <typeparam name="T">Type of Exact.Web.Api.Models</typeparam>
	/// <param name="json">Json String</param>
	/// <returns>Exact Online Object</returns>
	public static T? ConvertJsonToObject<T>(string? json)
		where T : notnull
	{
		try
		{
			var options = GetDeserializerOptions();
			return JsonSerializer.Deserialize<T>(json!, options);
		}
		catch (Exception exception)
		{
			throw new IncorrectJsonException("An exception occurred while converting JSON to object.", exception);
		}
	}

	/// <summary>
	/// Convert Json Array To Object List
	/// </summary>
	/// <typeparam name="T">Specifies the type</typeparam>
	/// <param name="json">Json Array</param>
	/// <returns>List of specified type</returns>
	public static List<T> ConvertJsonArrayToObjectList<T>(string? json)
	{
		try
		{
			if (string.IsNullOrEmpty(json))
			{
				return [];
			}

			var options = GetDeserializerOptions();
			return JsonSerializer.Deserialize<List<T>>(json!, options) ?? [];
		}
		catch (Exception)
		{
			throw new IncorrectJsonException("An error occurred while processing the json string. Possibly the result is too big. Please make a more specific query.");
		}
	}

	private static JsonSerializerOptions GetDeserializerOptions()
	{
		var options = new JsonSerializerOptions
		{
			DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.Never,
			WriteIndented = false
		};
		options.Converters.Add(new MicrosoftDateTimeConverter());
		return options;
	}

	private static JsonSerializerOptions GetJsonSerializerOptions()
	{
		var options = new JsonSerializerOptions
		{
			DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.Never,
			WriteIndented = false
		};
		options.Converters.Add(new ExactOnlineJsonConverter());
		return options;
	}

	private static JsonSerializerOptions GetJsonSerializerOptions<TEntity>(TEntity entity, Func<object, EntityController> getEntityControllerFunc)
	{
		var options = new JsonSerializerOptions
		{
			DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.Never,
			WriteIndented = false
		};
		options.Converters.Add(new ExactOnlineJsonConverter(entity, getEntityControllerFunc));
		return options;
	}
}
