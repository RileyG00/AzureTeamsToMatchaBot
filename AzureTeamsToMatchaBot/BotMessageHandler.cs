using AdaptiveCards;
using MatchaRunner.Api.Outgoing;
using MatchaRunner.Configuration;
using MatchaRunner.Constants;
using MatchaRunner.Services;
using MatchaRunner.Storage;
using MatchaRunner.Utils;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Teams.Api;
using Microsoft.Teams.Api.Activities;
using Microsoft.Teams.Apps;
using OpenAI.Audio;
using System.ClientModel;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Text.Json;

[assembly: InternalsVisibleTo("AzureTeamsToMatchaBot.Tests")]

namespace MatchaRunner.Handlers
{
	public interface IBotMessageHandler
	{
		Task HandleMessageAsync(IContext<MessageActivity> context, CancellationToken cancellationToken);
		Task HandleFileConsentAsync(IContext<Microsoft.Teams.Api.Activities.Invokes.FileConsentActivity> context, CancellationToken cancellationToken);
	}

	public class BotMessageHandler : IBotMessageHandler
	{
		private readonly HttpClient _matchaHttpClient;
		private readonly Matcha _matchaSettings;
		private readonly ILogger<BotMessageHandler> _logger;
		private readonly IHttpClientFactory _httpClientFactory;
		private readonly IJiraService _jiraService;

		/// <summary>
		/// Temporary in-memory store for pending audio uploads.
		/// Key: unique file ID, Value: audio bytes.
		/// Entries are removed after upload or decline.
		/// </summary>
		internal static readonly ConcurrentDictionary<string, byte[]> PendingAudioUploads = new();


		public BotMessageHandler(IHttpClientFactory clientFactory, IOptions<Matcha> matchaOptions, ILogger<BotMessageHandler> logger, IJiraService jiraService)
		{
			this._matchaHttpClient = clientFactory.CreateClient(ApiNamedClients.Matcha);
			this._matchaSettings = matchaOptions.Value;
			this._logger = logger;
			this._httpClientFactory = clientFactory;
			this._jiraService = jiraService;
		}


		public async Task HandleMessageAsync(IContext<MessageActivity> context, CancellationToken cancellationToken)
		{

			try
			{
				await context.Typing("...", cancellationToken);

				string userText = context.Activity?.Text?.Trim() ?? string.Empty;

				if (CommandParser.IsAskingForHelp(userText))
				{
					MessageActivity helpActivity = new()
					{
						Attachments = new List<Attachment> { BuildHelpCard() }
					};
					await context.Send(helpActivity, cancellationToken);
					return;
				}

				if (CommandParser.IsAudioApproved(userText))
				{
					string? ticketNumber = CommandParser.ExtractTicketNumber(userText);
					TicketValidationResult validation = CommandParser.ValidateTicketNumber(ticketNumber);

					if (!validation.IsValid)
					{
						await context.Send(validation.ErrorMessage!);
						return;
					}

					string userId = context.Activity.From.AadObjectId;
					byte[]? audioBytes = UserAudioStore.GetAudio(userId);

					if (audioBytes == null)
					{
						await context.Send(CannedResponses.NoAudioFound);
						return;
					}

					await context.Send(CannedResponses.AudioApprovedProcessing);

					JiraUploadResult result = await _jiraService.UploadAttachmentAsync(ticketNumber!, audioBytes, cancellationToken);

					if (result.Success)
					{
						await context.Send($"Audio uploaded successfully! View ticket: {result.TicketUrl}");
						UserAudioStore.RemoveAudio(userId);
					}
					else
					{
						await context.Send(result.ErrorMessage!);
					}

					return;
				}

				bool isAudio = CommandParser.IsAskingForAudio(userText);

				if (isAudio)
				{
					await context.Send(CannedResponses.MessageReceivedAudio);
				}
				else
				{
					await context.Send(CannedResponses.MessageReceived);
				}


				MatchaFlowPayloadDto payloadDto = new()
				{
					InputValue = userText,
				};

				string payloadText = JsonSerializer.Serialize(payloadDto);

				HttpResponseMessage responseMessage = await this._matchaHttpClient.PostAsJsonAsync(
					this._matchaSettings.FlowId,
					payloadDto,
					CancellationToken.None // Azure Bots can only last 15 seconds. So we need to remove the kill switch from the API call.
				);

				string response = await responseMessage.Content.ReadAsStringAsync();

				string matchaResponse = TextUtils.ParseMatchaResponse(response);

				if (isAudio)
				{
					try
					{
						AudioClient client = new AudioClient("tts-1-hd", this._matchaSettings.OpenAIApiKey);

						ClientResult<BinaryData> ttsResponse = await client.GenerateSpeechAsync(
							matchaResponse,
							GeneratedSpeechVoice.Nova,
							null,
							CancellationToken.None
						);

						byte[] audioBytes = ttsResponse.Value.ToArray();

						if (audioBytes.Length == 0)
						{
							await context.Send("⚠️ Audio generation produced no data. Here is your script as text:\n\n" + matchaResponse, cancellationToken);
						}
						else
						{
							// Store audio for potential --audio-approved upload
							UserAudioStore.StoreAudio(context.Activity.From.AadObjectId, audioBytes);

							// Send the script text first
							if (CommandParser.IsAskingForAudioAndScript(userText))
							{ 
								await context.Send(matchaResponse, cancellationToken);
							}

							string dateTimeNow = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
							string fileName = string.Concat("PIP-Ticket-", dateTimeNow, ".mp3");

							// Store audio bytes and send file consent card
							Attachment consentAttachment = CreateFileConsentAttachment(audioBytes, fileName);

							MessageActivity consentActivity = new()
							{
								Attachments = new List<Attachment> { consentAttachment }
							};

							await context.Send(consentActivity, cancellationToken);
						}
					}
					catch (Exception ex)
					{
						_logger.LogError(ex, "TTS audio generation failed");
						await context.Send("⚠️ Audio generation failed. Here is your script as text:\n\n" + matchaResponse, cancellationToken);
					}
				}
				else
				{
					await context.Send(matchaResponse, cancellationToken);
				}


			}
			catch (Exception ex)
			{
				AdaptiveCard errorCard = new(new AdaptiveSchemaVersion(1, 4))
				{
					Body = new List<AdaptiveElement>
					{
						new AdaptiveTextBlock
						{
							Text = "⚠️ Error Processing Request",
							Weight = AdaptiveTextWeight.Bolder,
							Size = AdaptiveTextSize.Large,
							Color = AdaptiveTextColor.Attention
						},
						new AdaptiveTextBlock
						{
							Text = $"An unexpected error occurred: {ex.Message}",
							Wrap = true
						}
					}
				};

				string cardJsonString = errorCard.ToJson();
				JsonElement safeJsonContent = JsonDocument.Parse(cardJsonString).RootElement;

				MessageActivity errorActivity = new()
				{
					Attachments = new List<Attachment>
					{
						new Attachment
						{
							ContentType = ContentType.AdaptiveCard,
							Content = safeJsonContent
						}
					}
				};

				await context.Send(errorActivity, cancellationToken);
			}
		}

		public async Task HandleFileConsentAsync(IContext<Microsoft.Teams.Api.Activities.Invokes.FileConsentActivity> context, CancellationToken cancellationToken)
		{
			FileConsentCardResponse consentResponse = context.Activity.Value;

			string? fileId = null;

			// Extract the fileId from the consent context
			if (consentResponse.Context is JsonElement contextElement)
			{
				fileId = contextElement.GetProperty("fileId").GetString();
			}

			if (consentResponse.Action == Microsoft.Teams.Api.Action.Accept && consentResponse.UploadInfo != null)
			{
				// User accepted — upload the file
				if (fileId != null && PendingAudioUploads.TryRemove(fileId, out byte[]? audioBytes))
				{
					try
					{
						using var httpClient = _httpClientFactory.CreateClient();
						using var content = new ByteArrayContent(audioBytes);
						content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("audio/mpeg");
						content.Headers.ContentLength = audioBytes.Length;
						content.Headers.ContentRange = new System.Net.Http.Headers.ContentRangeHeaderValue(0, audioBytes.Length - 1, audioBytes.Length);

						HttpResponseMessage uploadResponse = await httpClient.PutAsync(
							consentResponse.UploadInfo.UploadUrl,
							content,
							cancellationToken
						);

						uploadResponse.EnsureSuccessStatusCode();

						// Send file info card to confirm the upload
						var fileInfoAttachment = new Attachment
						{
							ContentType = new ContentType("application/vnd.microsoft.teams.card.file.info"),
							ContentUrl = consentResponse.UploadInfo.ContentUrl,
							Name = consentResponse.UploadInfo.Name,
							Content = JsonDocument.Parse(JsonSerializer.Serialize(new
							{
								uniqueId = consentResponse.UploadInfo.UniqueId,
								fileType = consentResponse.UploadInfo.FileType
							})).RootElement
						};

						MessageActivity fileInfoActivity = new()
						{
							Attachments = new List<Attachment> { fileInfoAttachment }
						};

						await context.Send(fileInfoActivity, cancellationToken);
					}
					catch (Exception ex)
					{
						_logger.LogError(ex, "Failed to upload audio file to OneDrive");
						await context.Send("⚠️ Failed to upload the audio file.", cancellationToken);
					}
				}
				else
				{
					_logger.LogWarning("Audio bytes not found for file consent accept (fileId: {FileId})", fileId);
					await context.Send("⚠️ Audio data expired. Please try generating the audio again.", cancellationToken);
				}
			}
			else
			{
				// User declined — clean up stored bytes
				if (fileId != null)
				{
					PendingAudioUploads.TryRemove(fileId, out _);
				}
				await context.Send("File upload declined. The script text was already sent above.", cancellationToken);
			}
		}

		/// <summary>
		/// Builds an AdaptiveCard attachment listing all available bot commands
		/// with descriptions and usage examples.
		/// </summary>
		internal static Attachment BuildHelpCard()
		{
			AdaptiveCard card = new(new AdaptiveSchemaVersion(1, 4))
			{
				Body = new List<AdaptiveElement>
				{
					new AdaptiveTextBlock
					{
						Text = "📋 Available Commands",
						Weight = AdaptiveTextWeight.Bolder,
						Size = AdaptiveTextSize.Large
					},
					new AdaptiveTextBlock
					{
						Text = "Here are the commands you can use with this bot:",
						Wrap = true,
						Spacing = AdaptiveSpacing.Small
					},
					new AdaptiveContainer
					{
						Separator = true,
						Spacing = AdaptiveSpacing.Medium,
						Items = new List<AdaptiveElement>
						{
							new AdaptiveTextBlock
							{
								Text = "`--audio`",
								Weight = AdaptiveTextWeight.Bolder,
								Spacing = AdaptiveSpacing.Small
							},
							new AdaptiveTextBlock
							{
								Text = "Generate an audio file from your PIP script.",
								Wrap = true,
								Spacing = AdaptiveSpacing.None
							},
							new AdaptiveTextBlock
							{
								Text = "Usage: `PIP-629 --audio`",
								IsSubtle = true,
								Spacing = AdaptiveSpacing.None
							}
						}
					},
					new AdaptiveContainer
					{
						Separator = true,
						Spacing = AdaptiveSpacing.Medium,
						Items = new List<AdaptiveElement>
						{
							new AdaptiveTextBlock
							{
								Text = "`--audio --include-script`",
								Weight = AdaptiveTextWeight.Bolder,
								Spacing = AdaptiveSpacing.Small
							},
							new AdaptiveTextBlock
							{
								Text = "Generate audio and also return the script text.",
								Wrap = true,
								Spacing = AdaptiveSpacing.None
							},
							new AdaptiveTextBlock
							{
								Text = "Usage: `PIP-629 --audio --include-script`",
								IsSubtle = true,
								Spacing = AdaptiveSpacing.None
							}
						}
					},
					new AdaptiveContainer
					{
						Separator = true,
						Spacing = AdaptiveSpacing.Medium,
						Items = new List<AdaptiveElement>
						{
							new AdaptiveTextBlock
							{
								Text = "`--audio-approved`",
								Weight = AdaptiveTextWeight.Bolder,
								Spacing = AdaptiveSpacing.Small
							},
							new AdaptiveTextBlock
							{
								Text = "Upload your last generated audio file to the specified Jira ticket.",
								Wrap = true,
								Spacing = AdaptiveSpacing.None
							},
							new AdaptiveTextBlock
							{
								Text = "Usage: `PIP-629 --audio-approved`",
								IsSubtle = true,
								Spacing = AdaptiveSpacing.None
							}
						}
					},
					new AdaptiveContainer
					{
						Separator = true,
						Spacing = AdaptiveSpacing.Medium,
						Items = new List<AdaptiveElement>
						{
							new AdaptiveTextBlock
							{
								Text = "`--help`",
								Weight = AdaptiveTextWeight.Bolder,
								Spacing = AdaptiveSpacing.Small
							},
							new AdaptiveTextBlock
							{
								Text = "Show this help message.",
								Wrap = true,
								Spacing = AdaptiveSpacing.None
							},
							new AdaptiveTextBlock
							{
								Text = "Usage: `--help`",
								IsSubtle = true,
								Spacing = AdaptiveSpacing.None
							}
						}
					}
				}
			};

			string cardJson = card.ToJson();
			JsonElement cardContent = JsonDocument.Parse(cardJson).RootElement;

			return new Attachment
			{
				ContentType = ContentType.AdaptiveCard,
				Content = cardContent
			};
		}

		/// <summary>
		/// Creates a FileConsentCard attachment that asks the user for permission
		/// to upload the audio file to their OneDrive. Stores the audio bytes
		/// in PendingAudioUploads keyed by a unique file ID.
		/// </summary>
		internal static Attachment CreateFileConsentAttachment(byte[] audioBytes, string fileName)
		{
			string fileId = Guid.NewGuid().ToString();
			PendingAudioUploads[fileId] = audioBytes;

			var consentCard = new FileConsentCard
			{
				Description = "Here is the audio version of your script. Accept to save it to your OneDrive.",
				SizeInBytes = audioBytes.Length,
				AcceptContext = JsonDocument.Parse(JsonSerializer.Serialize(new { fileId })).RootElement,
				DeclineContext = JsonDocument.Parse(JsonSerializer.Serialize(new { fileId })).RootElement
			};

			string cardJson = JsonSerializer.Serialize(consentCard);
			JsonElement cardContent = JsonDocument.Parse(cardJson).RootElement;

			return new Attachment
			{
				ContentType = new ContentType("application/vnd.microsoft.teams.card.file.consent"),
				Content = cardContent,
				Name = fileName
			};
		}
	}
}
