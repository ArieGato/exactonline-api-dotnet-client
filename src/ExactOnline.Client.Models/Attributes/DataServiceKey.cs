namespace ExactOnline.Client.Models;

public sealed class DataServiceKey : Attribute
{
	public DataServiceKey(string dataServiceKey) =>
		DataServiceKeyName = dataServiceKey;

	public DataServiceKey(string dataServiceKey, string dataServiceKey2) =>
		DataServiceKeyName = dataServiceKey;

	public DataServiceKey(string dataServiceKey, string dataServiceKey2, string dataServiceKey3) =>
		DataServiceKeyName = dataServiceKey;

	[JsonPropertyName("dataServiceKey")]
	public string DataServiceKeyName { get; set; }
}
