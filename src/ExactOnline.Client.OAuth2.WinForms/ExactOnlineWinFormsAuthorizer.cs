namespace ExactOnline.Client.OAuth2.WinForms;

public class ExactOnlineWinFormsAuthorizer(string clientId, string clientSecret, Uri callbackUrl, string baseUrl = "https://start.exactonline.be", string? accessToken = null, string? refreshToken = null, DateTime? expiresAt = null)
	: ExactOnlineAuthorizer(clientId, clientSecret, callbackUrl, baseUrl, accessToken, refreshToken, expiresAt)
{
	public override async Task<string> GetAccessTokenAsync(CancellationToken ct)
	{
		if (await IsAuthorizationNeededAsync(ct))
		{
			var authorizationUri = await GetLoginLinkUriAsync(ct: ct);

			string? code = null;
			Exception? uiError = null;

			var t = new Thread(() =>
			{
				try
				{
					using var loginDialog = new LoginForm(new(authorizationUri), new(Configuration.RedirectUri));

					// Show the modal dialog on this STA thread
					loginDialog.ShowDialog();

					code = loginDialog.AuthorizationCode;
				}
				catch (Exception ex)
				{
					uiError = ex;
				}
			});

			t.SetApartmentState(ApartmentState.STA);
			t.Start();
			t.Join();

			if (uiError != null)
				throw new AggregateException(uiError);

			if (!string.IsNullOrWhiteSpace(code))
			{
				await ProcessAuthorizationAsync(code, ct);
			}
		}

		return await base.GetAccessTokenAsync(ct);
	}
}
