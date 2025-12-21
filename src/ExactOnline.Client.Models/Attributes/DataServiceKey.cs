namespace ExactOnline.Client.Models;

public sealed class DataServiceKey : Attribute
{
	public DataServiceKey(string? dataServiceKey) =>
		DataServiceKeyName = dataServiceKey;

	[JsonPropertyName("dataServiceKey")]
	public string? DataServiceKeyName { get; set; }
}
