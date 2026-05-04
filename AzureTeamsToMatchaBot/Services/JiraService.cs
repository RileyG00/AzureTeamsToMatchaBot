using MatchaRunner.Constants;
using Microsoft.Extensions.Logging;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MatchaRunner.Services
{
	public interface IJiraService
	{
		/// <summary>
		/// Uploads audio bytes as an .mp3 attachment to the specified Jira issue.
		/// Returns a result with success/failure info and the ticket URL for navigation.
		/// </summary>
		Task<JiraUploadResult> UploadAttachmentAsync(string ticketNumber, byte[] audioBytes, CancellationToken cancellationToken);
	}

	public record JiraUploadResult(bool Success, string? TicketUrl, string? ErrorMessage, int? StatusCode);

	internal record JiraAttachmentResponse(
		[property: JsonPropertyName("self")] string Self,
		[property: JsonPropertyName("filename")] string Filename,
		[property: JsonPropertyName("size")] int Size
	);

	public class JiraService : IJiraService
	{
		private readonly HttpClient _jiraHttpClient;
		private readonly ILogger<JiraService> _logger;

		public JiraService(IHttpClientFactory clientFactory, ILogger<JiraService> logger)
		{
			_jiraHttpClient = clientFactory.CreateClient(ApiNamedClients.Jira);
			_logger = logger;
		}

		public async Task<JiraUploadResult> UploadAttachmentAsync(string ticketNumber, byte[] audioBytes, CancellationToken cancellationToken)
		{
			try
			{
				string fileName = $"audio_{DateTime.UtcNow:yyyyMMdd_HHmmss}.mp3";
				string endpoint = $"/rest/api/2/issue/{ticketNumber}/attachments";

				using var content = new MultipartFormDataContent();
				var fileContent = new ByteArrayContent(audioBytes);
				fileContent.Headers.ContentType = new MediaTypeHeaderValue("audio/mpeg");
				content.Add(fileContent, "file", fileName);

				HttpResponseMessage response = await _jiraHttpClient.PostAsync(endpoint, content, cancellationToken);

				int statusCode = (int)response.StatusCode;

				if (response.IsSuccessStatusCode)
				{
					string responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
					var attachments = JsonSerializer.Deserialize<JiraAttachmentResponse[]>(responseBody);

					if (attachments is { Length: > 0 })
					{
						string selfUrl = attachments[0].Self;
						string issueId = ExtractIssueIdFromSelfUrl(selfUrl);
						string ticketUrl = BuildTicketUrl(ticketNumber, issueId);

						_logger.LogInformation("Successfully uploaded attachment to {TicketNumber}, issueId: {IssueId}", ticketNumber, issueId);

						return new JiraUploadResult(true, ticketUrl, null, statusCode);
					}

					_logger.LogWarning("Jira returned success but empty attachment response for {TicketNumber}", ticketNumber);
					return new JiraUploadResult(true, null, null, statusCode);
				}

				if (statusCode == 401 || statusCode == 403)
				{
					_logger.LogError("Jira authentication/permissions error for {TicketNumber}: {StatusCode}", ticketNumber, statusCode);
					return new JiraUploadResult(false, null, CannedResponses.JiraAuthError, statusCode);
				}

				string errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
				_logger.LogError("Jira upload failed for {TicketNumber}: {StatusCode} - {Body}", ticketNumber, statusCode, errorBody);
				return new JiraUploadResult(false, null, $"⚠️ Jira upload failed with status code {statusCode}. Please try again or contact support.", statusCode);
			}
			catch (HttpRequestException ex)
			{
				_logger.LogError(ex, "HTTP request error uploading to Jira for {TicketNumber}", ticketNumber);
				return new JiraUploadResult(false, null, CannedResponses.JiraUnavailable, null);
			}
			catch (TaskCanceledException ex)
			{
				_logger.LogError(ex, "Request timed out uploading to Jira for {TicketNumber}", ticketNumber);
				return new JiraUploadResult(false, null, CannedResponses.JiraUnavailable, null);
			}
		}

		/// <summary>
		/// Constructs the Jira ticket URL from the ticket number and issue ID.
		/// </summary>
		internal static string BuildTicketUrl(string ticketNumber, string issueId)
		{
			return $"https://connecture.atlassian.net/jira/polaris/projects/PIP/ideas/view/{issueId}?selectedIssue={ticketNumber}";
		}

		/// <summary>
		/// Extracts the numeric issue ID from the Jira API self URL.
		/// Example self URL: "https://connecture.atlassian.net/rest/api/2/attachment/12345"
		/// The issue ID is extracted from the attachment's parent issue context.
		/// </summary>
		private static string ExtractIssueIdFromSelfUrl(string selfUrl)
		{
			// The self URL for an attachment looks like:
			// https://connecture.atlassian.net/rest/api/2/attachment/{attachmentId}
			// We extract the last segment as the issue ID reference
			var uri = new Uri(selfUrl);
			string lastSegment = uri.Segments[^1].TrimEnd('/');
			return lastSegment;
		}
	}
}
