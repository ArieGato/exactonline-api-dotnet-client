using System;
using System.Collections.Specialized;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Web;
using OAuth2.Configuration;
using OAuth2.Infrastructure;
using RestSharp;

namespace OAuth2.Client;

/// <summary>
/// Base class for OAuth2 client implementation.
/// </summary>
public abstract class OAuth2Client : IClient
{
	private readonly IRequestFactory _factory;

	/// <summary>
	/// Client configuration object.
	/// </summary>
	public IClientConfiguration Configuration { get; private set; }

	/// <summary>
	/// Friendly name of provider (OAuth2 service).
	/// </summary>
	public abstract string Name { get; }

	/// <summary>
	/// State (any additional information that was provided by application and is posted back by service).
	/// </summary>
	public string? State { get; private set; }

	/// <summary>
	/// Access token returned by provider. Can be used for further calls of provider API.
	/// </summary>
	public string? AccessToken { get; private set; }

	/// <summary>
	/// Refresh token returned by provider. Can be used for further calls of provider API.
	/// </summary>
	public string? RefreshToken { get; private set; }

	/// <summary>
	/// Token type returned by provider. Can be used for further calls of provider API.
	/// </summary>
	public string? TokenType { get; private set; }

	/// <summary>
	/// Seconds till the token expires returned by provider. Can be used for further calls of provider API.
	/// </summary>
	public DateTime? ExpiresAt { get; private set; }

	private string? GrantType { get; set; }

	/// <summary>
	/// Initializes a new instance of the <see cref="OAuth2Client"/> class.
	/// </summary>
	/// <param name="factory">The factory.</param>
	/// <param name="configuration">The configuration.</param>
	/// <param name="accessToken"></param>
	/// <param name="refreshToken"></param>
	/// <param name="expiresAt"></param>
	protected OAuth2Client(IRequestFactory factory, IClientConfiguration configuration, string? accessToken = null, string? refreshToken = null, DateTime? expiresAt = null)
	{
		_factory = factory;
		Configuration = configuration;
		AccessToken = accessToken;
		RefreshToken = refreshToken;
		if (expiresAt.HasValue)
			ExpiresAt = expiresAt.Value;
	}

	/// <summary>
	/// Returns URI of service which should be called in order to start authentication process.
	/// This URI should be used for rendering login link.
	/// </summary>
	/// <param name="state">
	/// Any additional information that will be posted back by service.
	/// </param>
	/// <param name="ct"></param>
	public virtual Task<string> GetLoginLinkUriAsync(string? state = null, CancellationToken ct = default)
	{
		var client = _factory.CreateClient(AccessCodeServiceEndpoint);
		var request = _factory.CreateRequest(AccessCodeServiceEndpoint);
		if (string.IsNullOrEmpty(Configuration.Scope))
		{
			request.AddObject(new
			{
				response_type = "code",
				client_id = Configuration.ClientId,
				redirect_uri = Configuration.RedirectUri,
				state
			});
		}
		else
		{
			request.AddObject(new
			{
				response_type = "code",
				client_id = Configuration.ClientId,
				redirect_uri = Configuration.RedirectUri,
				scope = Configuration.Scope,
				state
			});
		}
		return Task.FromResult(client.BuildUri(request).ToString());
	}

	/// <summary>
	/// Issues query for access token and returns access token.
	/// </summary>
	/// <param name="parameters">Callback request payload (parameters).</param>
	/// <param name="ct">Optional cancellation token</param>
	public async Task<string> GetTokenAsync(NameValueCollection parameters, CancellationToken ct = default)
	{
		GrantType = "authorization_code";
		CheckErrorAndSetState(parameters);
		await QueryAccessTokenAsync(parameters, ct).ConfigureAwait(false);
		return AccessToken!;
	}

	public async Task<string> GetCurrentTokenAsync(string? refreshToken = null, bool forceUpdate = false, CancellationToken ct = default)
	{
		if (!forceUpdate && ExpiresAt != default && DateTime.Now < ExpiresAt && !string.IsNullOrEmpty(AccessToken))
		{
			return AccessToken!;
		}

		NameValueCollection parameters = [];
		if (!string.IsNullOrEmpty(refreshToken))
		{
			parameters.Add("refresh_token", refreshToken);
		}
		else if (!string.IsNullOrEmpty(RefreshToken))
		{
			parameters.Add("refresh_token", RefreshToken);
		}

		if (parameters.Count > 0)
		{
			GrantType = "refresh_token";
			await QueryAccessTokenAsync(parameters, ct).ConfigureAwait(false);
			return AccessToken!;
		}
		throw new Exception("Token never fetched and refresh token not provided.");
	}

	/// <summary>
	/// Defines URI of service which issues access code.
	/// </summary>
	protected abstract Endpoint AccessCodeServiceEndpoint { get; }

	/// <summary>
	/// Defines URI of service which issues access token.
	/// </summary>
	protected abstract Endpoint AccessTokenServiceEndpoint { get; }

	/// <summary>
	/// Defines URI of service which allows to obtain information about user 
	/// who is currently logged in.
	/// </summary>
	protected abstract Endpoint UserInfoServiceEndpoint { get; }

	private void CheckErrorAndSetState(NameValueCollection parameters)
	{
		const string errorFieldName = "error";
		var error = parameters[errorFieldName];
		if (!string.IsNullOrWhiteSpace(error))
		{
			throw new UnexpectedResponseException(errorFieldName);
		}

		State = parameters["state"];
	}

	/// <summary>
	/// Issues query for access token and parses response.
	/// </summary>
	/// <param name="parameters">Callback request payload (parameters).</param>
	/// <param name="ct">Optional cancellation token</param>
	private async Task QueryAccessTokenAsync(NameValueCollection parameters, CancellationToken ct = default)
	{
		var client = _factory.CreateClient(AccessTokenServiceEndpoint);
		var request = _factory.CreateRequest(AccessTokenServiceEndpoint, Method.Post);

		BeforeGetAccessToken(new BeforeAfterRequestArgs
		{
			Client = client,
			Request = request,
			Parameters = parameters,
			Configuration = Configuration
		});

		var response = await client.ExecuteAndVerifyAsync(request, ct).ConfigureAwait(false);

		if (response?.Content == null)
			throw new UnexpectedResponseException("Response content is null.");

		var tokenResponse = JsonSerializer.Deserialize<TokenResponse>(response.Content);
		AccessToken = tokenResponse?.AccessToken;
		RefreshToken = tokenResponse?.RefreshToken;
		TokenType = tokenResponse?.TokenType;
		ExpiresAt = DateTime.Now.AddSeconds(tokenResponse?.ExpiresIn ?? 0 - 5); // subtract 5 seconds to be sure the token isn't expired before the next call is executed

		OnAfterTokensChanged();
	}

	protected virtual void OnAfterTokensChanged()
	{
	}

	protected virtual void BeforeGetAccessToken(BeforeAfterRequestArgs args)
	{
		if (GrantType == "refresh_token")
		{
			args.Request.AddObject(new
			{
				refresh_token = args.Parameters.GetOrThrowUnexpectedResponse("refresh_token"),
				client_id = Configuration.ClientId,
				client_secret = Configuration.ClientSecret,
				grant_type = GrantType
			});
		}
		else
		{
			args.Request.AddObject(new
			{
				code = args.Parameters.GetOrThrowUnexpectedResponse("code"),
				client_id = Configuration.ClientId,
				client_secret = Configuration.ClientSecret,
				redirect_uri = Configuration.RedirectUri,
				grant_type = GrantType
			});
		}
	}
}

public class TokenResponse
{
	[JsonPropertyName("access_token")]
	public string? AccessToken { get; set; }
	[JsonPropertyName("refresh_token")]
	public string? RefreshToken { get; set; }
	[JsonPropertyName("expires_in")]
	public int ExpiresIn { get; set; }
	[JsonPropertyName("token_type")]
	public string? TokenType { get; set; }
}
