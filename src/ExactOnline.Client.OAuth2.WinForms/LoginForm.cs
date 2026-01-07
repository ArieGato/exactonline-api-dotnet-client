using System.ComponentModel;
using System.Web;
using Microsoft.Web.WebView2.Core;

namespace ExactOnline.Client.OAuth2.WinForms;

public partial class LoginForm : Form
{
	private readonly Uri _redirectUri = default!;

	[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
	public Uri AuthorizationUri { get; private set; } = default!;

	public string? AuthorizationCode => HttpUtility.ParseQueryString(AuthorizationUri.Query)["code"];

	public LoginForm() => InitializeComponent();

	public LoginForm(Uri authorizationUri, Uri redirectUri)
		: this()
	{
		(AuthorizationUri, _redirectUri) = (authorizationUri, redirectUri);

		Load += LoginForm_Load;
	}

	private async void LoginForm_Load(object? sender, EventArgs e)
	{
		try
		{
			var userDataFolder = Path.Combine(Path.GetTempPath(), "ExactOnline.WebView2");

			var env = await CoreWebView2Environment.CreateAsync(
				browserExecutableFolder: null,
				userDataFolder: userDataFolder);
			await webView.EnsureCoreWebView2Async(env);
			webView.CoreWebView2.Navigate(AuthorizationUri.AbsoluteUri);
		}
		catch (Exception ex)
		{
			MessageBox.Show(ex.ToString(), "WebView2 init failed");
		}
	}

	private void WebView_NavigationCompleted(object sender, CoreWebView2NavigationCompletedEventArgs e)
	{
		if (IsRedirected(webView.Source))
		{
			AuthorizationUri = webView.Source;
			Hide();
		}
	}

	/// <summary>
	/// Tests whether two URLs are equal for purposes of detecting the conclusion of authorization.
	/// </summary>
	private bool IsRedirected(Uri uri)
	{
		var uriComponents = uri.GetComponents(UriComponents.SchemeAndServer | UriComponents.Path, UriFormat.Unescaped);
		var redirectUri = _redirectUri.ToString();
		if (uriComponents.EndsWith("/") && !redirectUri.EndsWith("/"))
		{
			redirectUri += "/";
		}

		return string.Equals(uriComponents, redirectUri, StringComparison.Ordinal);
	}
}
