using FsCheck;
using FsCheck.Xunit;
using MatchaRunner.Services;
using Microsoft.Extensions.Logging.Abstractions;
using System.Net;
using Xunit;

namespace PIPScriptWriter.Tests;

/// <summary>
/// Property-based tests for JiraService functionality.
/// Feature: audio-attachment-jira-upload, Property 4: Ticket URL construction produces correctly formatted URLs
/// </summary>
public class JiraServicePropertyTests
{
	private const string BaseUrl = "https://connecture.atlassian.net/jira/polaris/projects/PIP/ideas/view/";

	/// <summary>
	/// Property 4a: For any valid ticket number and numeric issue ID,
	/// BuildTicketUrl produces a URL that starts with the expected base URL.
	///
	/// **Validates: Requirements 5.1, 5.2**
	/// </summary>
	[Property(MaxTest = 100)]
	public bool TicketUrlStartsWithExpectedBaseUrl(PositiveInt digitsPart, bool useFourDigits, PositiveInt issueIdPart)
	{
		string ticketNumber = GenerateTicketNumber(digitsPart.Get, useFourDigits);
		string issueId = issueIdPart.Get.ToString();

		string url = JiraService.BuildTicketUrl(ticketNumber, issueId);

		return url.StartsWith(BaseUrl);
	}

	/// <summary>
	/// Property 4b: For any valid ticket number and numeric issue ID,
	/// BuildTicketUrl produces a URL that contains the issue ID in the path.
	///
	/// **Validates: Requirements 5.1, 5.2**
	/// </summary>
	[Property(MaxTest = 100)]
	public bool TicketUrlContainsIssueIdInPath(PositiveInt digitsPart, bool useFourDigits, PositiveInt issueIdPart)
	{
		string ticketNumber = GenerateTicketNumber(digitsPart.Get, useFourDigits);
		string issueId = issueIdPart.Get.ToString();

		string url = JiraService.BuildTicketUrl(ticketNumber, issueId);

		// The issue ID should appear after the base URL path and before the query string
		string pathPortion = url.Split('?')[0];
		return pathPortion.EndsWith($"/{issueId}");
	}

	/// <summary>
	/// Property 4c: For any valid ticket number and numeric issue ID,
	/// BuildTicketUrl produces a URL with the correct query parameter.
	///
	/// **Validates: Requirements 5.1, 5.2**
	/// </summary>
	[Property(MaxTest = 100)]
	public bool TicketUrlContainsCorrectQueryParameter(PositiveInt digitsPart, bool useFourDigits, PositiveInt issueIdPart)
	{
		string ticketNumber = GenerateTicketNumber(digitsPart.Get, useFourDigits);
		string issueId = issueIdPart.Get.ToString();

		string url = JiraService.BuildTicketUrl(ticketNumber, issueId);

		string expectedQuery = $"?selectedIssue={ticketNumber}";
		return url.Contains(expectedQuery);
	}

	/// <summary>
	/// Property 4d: For any valid ticket number and numeric issue ID,
	/// BuildTicketUrl produces a URL that exactly matches the expected format.
	/// This is the strongest property — it verifies the complete URL structure.
	///
	/// **Validates: Requirements 5.1, 5.2**
	/// </summary>
	[Property(MaxTest = 100)]
	public bool TicketUrlMatchesExactExpectedFormat(PositiveInt digitsPart, bool useFourDigits, PositiveInt issueIdPart)
	{
		string ticketNumber = GenerateTicketNumber(digitsPart.Get, useFourDigits);
		string issueId = issueIdPart.Get.ToString();

		string url = JiraService.BuildTicketUrl(ticketNumber, issueId);

		string expectedUrl = $"https://connecture.atlassian.net/jira/polaris/projects/PIP/ideas/view/{issueId}?selectedIssue={ticketNumber}";
		return url == expectedUrl;
	}

	/// <summary>
	/// Property 4e: The ticket number in the generated URL matches the input exactly
	/// (no case changes, no trimming, no encoding).
	///
	/// **Validates: Requirements 5.1, 5.2**
	/// </summary>
	[Property(MaxTest = 100)]
	public bool TicketNumberInUrlMatchesInputExactly(PositiveInt digitsPart, bool useFourDigits, PositiveInt issueIdPart)
	{
		string ticketNumber = GenerateTicketNumber(digitsPart.Get, useFourDigits);
		string issueId = issueIdPart.Get.ToString();

		string url = JiraService.BuildTicketUrl(ticketNumber, issueId);

		// Extract the ticket number from the query parameter
		string queryPart = url.Split("?selectedIssue=")[1];
		return queryPart == ticketNumber;
	}

	/// <summary>
	/// Property 5: For any non-success HTTP status code (excluding 401/403 which have special handling),
	/// the resulting JiraUploadResult.ErrorMessage contains the numeric status code as a substring,
	/// and JiraUploadResult.StatusCode matches the returned status code.
	///
	/// **Validates: Requirements 6.1**
	/// </summary>
	[Property(MaxTest = 100)]
	public bool ErrorResponseContainsHttpStatusCode(PositiveInt statusCodeSeed)
	{
		// Map seed to non-success status codes excluding 401 and 403 (which use CannedResponses.JiraAuthError)
		int[] nonSuccessStatusCodes = [
			400, 402, 404, 405, 406, 407, 408, 409, 410,
			411, 412, 413, 414, 415, 416, 417, 422, 429,
			500, 501, 502, 503, 504, 505
		];
		int statusCode = nonSuccessStatusCodes[statusCodeSeed.Get % nonSuccessStatusCodes.Length];

		var handler = new MockHttpMessageHandler((HttpStatusCode)statusCode);
		var httpClient = new HttpClient(handler)
		{
			BaseAddress = new Uri("https://connecture.atlassian.net")
		};

		var factory = new MockHttpClientFactory(httpClient);
		var logger = NullLogger<JiraService>.Instance;
		var service = new JiraService(factory, logger);

		var result = service.UploadAttachmentAsync("PIP-123", new byte[] { 0x01, 0x02, 0x03 }, CancellationToken.None)
			.GetAwaiter().GetResult();

		bool errorMessageContainsStatusCode = result.ErrorMessage != null
			&& result.ErrorMessage.Contains(statusCode.ToString());
		bool statusCodeMatches = result.StatusCode == statusCode;
		bool isNotSuccess = !result.Success;

		return isNotSuccess && errorMessageContainsStatusCode && statusCodeMatches;
	}

	/// <summary>
	/// Property 5b: For 401 and 403 status codes, the StatusCode property is still set correctly
	/// even though the error message uses CannedResponses.JiraAuthError.
	///
	/// **Validates: Requirements 6.1**
	/// </summary>
	[Property(MaxTest = 100)]
	public bool AuthErrorResponsesHaveCorrectStatusCode(bool use401)
	{
		int statusCode = use401 ? 401 : 403;

		var handler = new MockHttpMessageHandler((HttpStatusCode)statusCode);
		var httpClient = new HttpClient(handler)
		{
			BaseAddress = new Uri("https://connecture.atlassian.net")
		};

		var factory = new MockHttpClientFactory(httpClient);
		var logger = NullLogger<JiraService>.Instance;
		var service = new JiraService(factory, logger);

		var result = service.UploadAttachmentAsync("PIP-456", new byte[] { 0x01, 0x02, 0x03 }, CancellationToken.None)
			.GetAwaiter().GetResult();

		bool statusCodeMatches = result.StatusCode == statusCode;
		bool isNotSuccess = !result.Success;
		bool hasErrorMessage = result.ErrorMessage != null;

		return isNotSuccess && statusCodeMatches && hasErrorMessage;
	}

	private static string GenerateTicketNumber(int digitsPart, bool useFourDigits)
	{
		int number = useFourDigits
			? 1000 + (digitsPart % 9000)   // 1000-9999 (4 digits)
			: 100 + (digitsPart % 900);    // 100-999 (3 digits)

		return $"PIP-{number}";
	}

	/// <summary>
	/// Property 3a: For any valid ticket number and non-empty byte array,
	/// the constructed HTTP request targets the correct Jira attachment endpoint.
	///
	/// **Validates: Requirements 4.2**
	/// </summary>
	[Property(MaxTest = 100)]
	public bool JiraRequestTargetsCorrectEndpoint(PositiveInt digitsPart, bool useFourDigits, PositiveInt byteSeed)
	{
		string ticketNumber = GenerateTicketNumber(digitsPart.Get, useFourDigits);
		byte[] audioBytes = GenerateNonEmptyBytes(byteSeed.Get);

		var handler = new CapturingHttpMessageHandler();
		var httpClient = new HttpClient(handler)
		{
			BaseAddress = new Uri("https://connecture.atlassian.net")
		};

		var factory = new MockHttpClientFactory(httpClient);
		var logger = NullLogger<JiraService>.Instance;
		var service = new JiraService(factory, logger);

		service.UploadAttachmentAsync(ticketNumber, audioBytes, CancellationToken.None)
			.GetAwaiter().GetResult();

		string expectedPath = $"/rest/api/2/issue/{ticketNumber}/attachments";
		return handler.CapturedRequestUri != null
			&& handler.CapturedRequestUri.AbsolutePath == expectedPath;
	}

	/// <summary>
	/// Property 3b: For any valid ticket number and non-empty byte array,
	/// the constructed HTTP request uses multipart/form-data content type.
	///
	/// **Validates: Requirements 4.2**
	/// </summary>
	[Property(MaxTest = 100)]
	public bool JiraRequestUsesMultipartFormData(PositiveInt digitsPart, bool useFourDigits, PositiveInt byteSeed)
	{
		string ticketNumber = GenerateTicketNumber(digitsPart.Get, useFourDigits);
		byte[] audioBytes = GenerateNonEmptyBytes(byteSeed.Get);

		var handler = new CapturingHttpMessageHandler();
		var httpClient = new HttpClient(handler)
		{
			BaseAddress = new Uri("https://connecture.atlassian.net")
		};

		var factory = new MockHttpClientFactory(httpClient);
		var logger = NullLogger<JiraService>.Instance;
		var service = new JiraService(factory, logger);

		service.UploadAttachmentAsync(ticketNumber, audioBytes, CancellationToken.None)
			.GetAwaiter().GetResult();

		return handler.CapturedContentTypeMediaType == "multipart/form-data";
	}

	/// <summary>
	/// Property 3c: For any valid ticket number and non-empty byte array,
	/// the multipart content contains a file with .mp3 extension.
	///
	/// **Validates: Requirements 4.2**
	/// </summary>
	[Property(MaxTest = 100)]
	public bool JiraRequestContainsMp3File(PositiveInt digitsPart, bool useFourDigits, PositiveInt byteSeed)
	{
		string ticketNumber = GenerateTicketNumber(digitsPart.Get, useFourDigits);
		byte[] audioBytes = GenerateNonEmptyBytes(byteSeed.Get);

		var handler = new CapturingHttpMessageHandler();
		var httpClient = new HttpClient(handler)
		{
			BaseAddress = new Uri("https://connecture.atlassian.net")
		};

		var factory = new MockHttpClientFactory(httpClient);
		var logger = NullLogger<JiraService>.Instance;
		var service = new JiraService(factory, logger);

		service.UploadAttachmentAsync(ticketNumber, audioBytes, CancellationToken.None)
			.GetAwaiter().GetResult();

		return handler.CapturedFileName != null
			&& handler.CapturedFileName.EndsWith(".mp3");
	}

	/// <summary>
	/// Property 3d: For any valid ticket number and non-empty byte array,
	/// the file content bytes in the multipart request match the input audio bytes exactly.
	///
	/// **Validates: Requirements 4.2**
	/// </summary>
	[Property(MaxTest = 100)]
	public bool JiraRequestContainsExactAudioBytes(PositiveInt digitsPart, bool useFourDigits, PositiveInt byteSeed)
	{
		string ticketNumber = GenerateTicketNumber(digitsPart.Get, useFourDigits);
		byte[] audioBytes = GenerateNonEmptyBytes(byteSeed.Get);

		var handler = new CapturingHttpMessageHandler();
		var httpClient = new HttpClient(handler)
		{
			BaseAddress = new Uri("https://connecture.atlassian.net")
		};

		var factory = new MockHttpClientFactory(httpClient);
		var logger = NullLogger<JiraService>.Instance;
		var service = new JiraService(factory, logger);

		service.UploadAttachmentAsync(ticketNumber, audioBytes, CancellationToken.None)
			.GetAwaiter().GetResult();

		return handler.CapturedFileBytes != null
			&& handler.CapturedFileBytes.SequenceEqual(audioBytes);
	}

	private static byte[] GenerateNonEmptyBytes(int seed)
	{
		// Generate 1-64 bytes deterministically from the seed
		int length = (Math.Abs(seed) % 64) + 1;
		byte[] bytes = new byte[length];
		var rng = new Random(seed);
		rng.NextBytes(bytes);
		return bytes;
	}

	/// <summary>
	/// Mock HttpMessageHandler that returns a configured status code.
	/// </summary>
	private class MockHttpMessageHandler : HttpMessageHandler
	{
		private readonly HttpStatusCode _statusCode;

		public MockHttpMessageHandler(HttpStatusCode statusCode)
		{
			_statusCode = statusCode;
		}

		protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
		{
			var response = new HttpResponseMessage(_statusCode)
			{
				Content = new StringContent($"{{\"errorMessages\":[\"Error occurred\"],\"errors\":{{}}}}")
			};
			return Task.FromResult(response);
		}
	}

	/// <summary>
	/// Capturing HttpMessageHandler that reads and stores request details during SendAsync,
	/// before the content gets disposed. Returns a mock success response.
	/// Used for Property 3 tests to verify request construction.
	/// </summary>
	private class CapturingHttpMessageHandler : HttpMessageHandler
	{
		public Uri? CapturedRequestUri { get; private set; }
		public string? CapturedContentTypeMediaType { get; private set; }
		public string? CapturedFileName { get; private set; }
		public byte[]? CapturedFileBytes { get; private set; }

		protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
		{
			CapturedRequestUri = request.RequestUri;

			if (request.Content is MultipartFormDataContent multipartContent)
			{
				CapturedContentTypeMediaType = request.Content.Headers.ContentType?.MediaType;

				foreach (var part in multipartContent)
				{
					var disposition = part.Headers.ContentDisposition;
					if (disposition?.FileName != null)
					{
						CapturedFileName = disposition.FileName.Trim('"');
						CapturedFileBytes = await part.ReadAsByteArrayAsync(cancellationToken);
						break;
					}
				}
			}

			var response = new HttpResponseMessage(HttpStatusCode.OK)
			{
				Content = new StringContent("[{\"self\":\"https://connecture.atlassian.net/rest/api/2/attachment/12345\",\"filename\":\"audio.mp3\",\"size\":100}]")
			};
			return response;
		}
	}

	/// <summary>
	/// Mock IHttpClientFactory that returns a pre-configured HttpClient.
	/// </summary>
	private class MockHttpClientFactory : IHttpClientFactory
	{
		private readonly HttpClient _httpClient;

		public MockHttpClientFactory(HttpClient httpClient)
		{
			_httpClient = httpClient;
		}

		public HttpClient CreateClient(string name)
		{
			return _httpClient;
		}
	}
}
