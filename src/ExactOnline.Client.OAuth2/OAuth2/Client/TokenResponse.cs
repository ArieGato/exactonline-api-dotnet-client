using System.Text.Json.Serialization;

namespace OAuth2.Client;

public class TokenResponse
{
	[JsonPropertyName("access_token")]
	public string? AccessToken { get; set; }
	[JsonPropertyName("refresh_token")]
	public string? RefreshToken { get; set; }
	[JsonPropertyName("expires_in")]
	public string ExpiresIn { get; set; }
	[JsonPropertyName("token_type")]
	public string? TokenType { get; set; }
}
