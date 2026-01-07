using System.Collections;
using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using ExactOnline.Client.Models;
using ExactOnline.Client.Sdk.Controllers;

namespace ExactOnline.Client.Sdk.Helpers;

public class ExactOnlineJsonConverter : JsonConverter<object>
{
	private readonly Func<object, EntityController?>? _getEntityControllerFunc;
	private readonly bool _createUpdateJson;
	private readonly object? _originalEntity;

	public ExactOnlineJsonConverter() =>
		_createUpdateJson = false;

	public ExactOnlineJsonConverter(object? originalObject, Func<object, EntityController?>? getEntityControllerFunc)
	{
		_getEntityControllerFunc = getEntityControllerFunc;
		_originalEntity = originalObject;
		_createUpdateJson = true;
	}

	/// <summary>
	/// Indicates if an entity can be converted to Json
	/// </summary>
	/// <param name="typeToConvert">Type of the entity</param>
	/// <returns>True if the entity can be converted</returns>
	public override bool CanConvert(Type typeToConvert) =>
		typeToConvert.ToString().Contains("ExactOnline.Client.Models");

	public override object Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
		throw new NotImplementedException();

	/// <summary>
	/// Converts the object to Json
	/// </summary>
	public override void Write(Utf8JsonWriter writer, object? value, JsonSerializerOptions options)
	{
		if (value is null)
		{
			return;
		}

		var writeableFields = GetWriteableFields(value);
		var guidsToSkip = writeableFields.Where(x => x.GetValue(value) is Guid guid &&
		                                             guid == Guid.Empty).ToArray();

		// Remove the fields to skip from the writeable fields
		writeableFields = writeableFields.Except(writeableFields.Join(guidsToSkip, e => e.GetValue(value), m => m.GetValue(value), (e, m) => e)).ToArray();
		if (writeableFields.Length < 1)
		{
			return;
		}

		// Create Json 
		writer.WriteStartObject();
		foreach (var field in writeableFields)
		{
			var jsonPropertyAttribute = field.GetCustomAttribute<JsonPropertyNameAttribute>();
			var fieldName = jsonPropertyAttribute?.Name ?? field.Name;

			var fieldValue = field.GetValue(value);
			fieldValue = CheckDateFormat(fieldValue);

			if (fieldValue != null && fieldValue.GetType().IsGenericType && fieldValue is IEnumerable enumerable)
			{
				// Write property value for linked entities
				WriteLinkedEntities(writer, fieldName, enumerable, options);
			}
			else
			{
				// Write property value for normal key value pair
				writer.WritePropertyName(fieldName);
				JsonSerializer.Serialize(writer, fieldValue, fieldValue?.GetType() ?? typeof(object), options);
			}
		}
		writer.WriteEndObject();
	}

	private PropertyInfo[] GetWriteableFields(object value)
	{
		var writeableFields = value.GetType().GetProperties().Where(IsWriteField).ToArray();

		if (_createUpdateJson)
		{
			var updatedFields = GetUpdatedFields(writeableFields, value); // If Json for update: Get only updated fields
			writeableFields = (from f in writeableFields
						   join up in updatedFields on f.Name equals up.Name
						   select f).ToArray();
		}

		return writeableFields;
	}

	/// <summary>
	/// Returns if a field is writeable (is not a identifier and is not a TypeOfField.ReadOnly field)
	/// </summary>
	/// <param name="pi"></param>
	/// <returns></returns>
	private static bool IsWriteField(PropertyInfo pi) =>
		!pi.GetCustomAttributes().OfType<SDKFieldType>().Any(a => a.TypeOfField == FieldType.ReadOnly);

	private PropertyInfo[] GetUpdatedFields(PropertyInfo[] writeableFields, object value)
	{
		// Check if this is an object where only the json for updated fields have to be created
		writeableFields = value.GetType().GetProperties().Where(property => IsUpdatedField(value, property)).ToArray();
		return writeableFields;
	}

	/// <summary>
	/// Method for creating Json
	/// Indicates if the field is a field to create json for
	/// </summary>
	private bool IsUpdatedField(object objectToConvert, PropertyInfo pi)
	{
		Debug.Assert(_originalEntity is not null, "_originalEntity should never be null here");

		var returnValue = false;

		var originalValue = _originalEntity!.GetType().GetProperty(pi.Name)?.GetValue(_originalEntity) ?? "null";
		var currentValue = pi.GetValue(objectToConvert) ?? "null";

		if (currentValue is ICollection collection && currentValue.GetType() != typeof(byte[]) && _getEntityControllerFunc is not null)
		{
			foreach (var entity in collection)
			{
				var entityController = _getEntityControllerFunc(entity);
				if (entityController == null || entityController.IsUpdated(entity))
				{
					returnValue = true;
				}
			}
		}
		else
		{
			returnValue = !originalValue.Equals(currentValue);
		}

		return returnValue;
	}

	/// <summary>
	/// Check if datetime. If so, convert to EdmDate
	/// </summary>
	private static object? CheckDateFormat(object? fieldValue)
	{
		if (fieldValue is DateTime dateTime)
		{
			fieldValue = ConvertDateToEdmDate(dateTime);
		}
		return fieldValue;
	}

	/// <summary>
	/// Converts datetime to required format
	/// </summary>
	private static string ConvertDateToEdmDate(DateTime date) => $"{date:yyyy-MM-ddTHH:mm}";

	private void WriteLinkedEntities(Utf8JsonWriter writer, string fieldName, IEnumerable fieldValue, JsonSerializerOptions options)
	{
		var linkedEntities = fieldValue.Cast<object>().ToArray();
		if (linkedEntities.Length < 1)
		{
			return;
		}

		writer.WritePropertyName(fieldName);
		writer.WriteStartArray();
		foreach (var item in fieldValue)
		{
			JsonSerializer.Serialize(writer, item, item?.GetType() ?? typeof(object), GetCorrectOptions(item, options));
		}
		writer.WriteEndArray();
	}

	private JsonSerializerOptions GetCorrectOptions(object entity, JsonSerializerOptions baseOptions)
	{
		var options = new JsonSerializerOptions(baseOptions);

		// first remove existing ExactOnlineJsonConverter
		var converter = options.Converters.FirstOrDefault(c => c is ExactOnlineJsonConverter);
		options.Converters.Remove(converter);

		// add new ExactOnlineJsonConverter with correct parameters
		if (_createUpdateJson)
		{
			if (_getEntityControllerFunc!(entity) is { } entityController)
			{
				options.Converters.Add(new ExactOnlineJsonConverter(entityController.OriginalEntity, _getEntityControllerFunc));
			}
			else
			{
				var emptyEntity = Activator.CreateInstance(entity.GetType());
				options.Converters.Add(new ExactOnlineJsonConverter(emptyEntity, _getEntityControllerFunc));
			}
		}
		else
		{
			options.Converters.Add(new ExactOnlineJsonConverter());
		}

		return options;
	}
}
