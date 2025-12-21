using System.Text.Json.Nodes;
using ExactOnline.Client.Sdk.Helpers;

namespace ExactOnline.Client.Sdk.UserAcceptanceTests.LowLevel;

[TestClass]
public class GetApiResponseAfterGetCall
{
	public TestContext TestContext { get; set; } = default!;

	/// <summary>
	/// User Story: Get a text response in JSON format from the API after 
	/// executing a REST GET call so that I can read data from Exact Online.
	/// Constraints: The user retrieves a string in JSON format.
	/// </summary>
	[TestMethod]
	[TestCategory("User Acceptance Tests")]
	public async Task GetApiResponseAfterGetCall_Succeeds()
	{
		var toc = new TestObjectsCreator();
		var conn = new ApiConnection(toc.GetApiConnector(), TestObjectsCreator.UriCrmAccount(await toc.GetCurrentDivisionAsync(TestContext.CancellationToken)));

		var result = conn.Get(string.Empty);
		if (string.IsNullOrEmpty(result))
		{
			throw new Exception("Return from API was empty");
		}

		// Check if the response is a JSON Value
		// Throws an exception when invalid JSON
		JsonNode.Parse(result);
	}
}
