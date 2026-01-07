namespace ExactOnline.Client.Models;

public class SupportsSync
{
	/// <summary>Timestamp for use with the sync api</summary>
	[JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
	public long Timestamp { get; set; }
}
