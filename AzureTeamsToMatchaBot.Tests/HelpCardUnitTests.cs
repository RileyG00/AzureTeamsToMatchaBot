using System.Text.Json;
using MatchaRunner.Handlers;
using Microsoft.Teams.Api;
using Xunit;

namespace PIPScriptWriter.Tests;

/// <summary>
/// Unit tests for BuildHelpCard helper method.
/// Feature: audio-attachment-jira-upload
/// </summary>
public class HelpCardUnitTests
{
	/// <summary>
	/// Verify the help card attachment uses AdaptiveCard content type.
	/// **Validates: Requirements 9 (help AC 2)**
	/// </summary>
	[Fact]
	public void BuildHelpCard_ReturnsAdaptiveCardContentType()
	{
		var attachment = BotMessageHandler.BuildHelpCard();

		Assert.Equal(ContentType.AdaptiveCard, attachment.ContentType);
	}

	/// <summary>
	/// Verify the help card JSON content contains the --audio-approved command.
	/// **Validates: Requirements 9 (help AC 1)**
	/// </summary>
	[Fact]
	public void BuildHelpCard_ContainsAudioApprovedCommand()
	{
		var attachment = BotMessageHandler.BuildHelpCard();

		var content = Assert.IsType<JsonElement>(attachment.Content);
		string cardJson = content.GetRawText();

		Assert.Contains("--audio-approved", cardJson);
	}

	/// <summary>
	/// Verify the help card contains all four commands: --audio, --audio --include-script, --audio-approved, --help.
	/// **Validates: Requirements 9 (help AC 1, 2)**
	/// </summary>
	[Fact]
	public void BuildHelpCard_ContainsAllCommands()
	{
		var attachment = BotMessageHandler.BuildHelpCard();

		var content = Assert.IsType<JsonElement>(attachment.Content);
		string cardJson = content.GetRawText();

		Assert.Contains("--audio", cardJson);
		Assert.Contains("--audio --include-script", cardJson);
		Assert.Contains("--audio-approved", cardJson);
		Assert.Contains("--help", cardJson);
	}
}
