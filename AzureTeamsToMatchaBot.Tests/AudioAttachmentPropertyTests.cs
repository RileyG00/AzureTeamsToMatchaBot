using System.Text.Json;
using FsCheck;
using FsCheck.Xunit;
using MatchaRunner.Handlers;

namespace PIPScriptWriter.Tests;

/// <summary>
/// Property-based tests for audio attachment functionality.
/// Feature: audio-attachment-response
/// </summary>
public class AudioAttachmentPropertyTests
{
    /// <summary>
    /// Property 1: Base64 encoding round-trip preserves audio data.
    /// For any byte array of length 1 to 100,000, base64-encoding it and then
    /// decoding the result produces a byte array identical to the original input.
    /// 
    /// **Validates: Requirements 2.1**
    /// </summary>
    [Property(MaxTest = 100)]
    public bool Base64EncodingRoundTripPreservesAudioData(byte[] bytes)
    {
        if (bytes == null || bytes.Length == 0)
            return true; // Skip trivial cases

        string base64 = Convert.ToBase64String(bytes);
        byte[] decoded = Convert.FromBase64String(base64);
        return decoded.SequenceEqual(bytes);
    }

    /// <summary>
    /// Property 2: FileConsentCard attachment is well-formed and stores audio bytes.
    /// For any non-empty byte array, the Attachment returned by CreateFileConsentAttachment
    /// has ContentType "application/vnd.microsoft.teams.card.file.consent", a Name ending
    /// in ".mp3", and the audio bytes are stored in PendingAudioUploads with a valid fileId
    /// that can be extracted from the card's acceptContext.
    /// 
    /// **Validates: Requirements 2.2, 2.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public bool FileConsentAttachmentIsWellFormedAndStoresBytes(byte[] bytes)
    {
        if (bytes == null || bytes.Length == 0)
            return true;

        // Clear pending uploads to avoid cross-test pollution
        var attachment = BotMessageHandler.CreateFileConsentAttachment(bytes, "test.mp3");

        // Assert ContentType is FileConsentCard
        if (attachment.ContentType.ToString() != "application/vnd.microsoft.teams.card.file.consent")
            return false;

        // Assert Name ends with .mp3
        if (attachment.Name == null || !attachment.Name.EndsWith(".mp3"))
            return false;

        // Assert Content contains a valid FileConsentCard with sizeInBytes matching
        if (attachment.Content is not JsonElement content)
            return false;

        if (!content.TryGetProperty("sizeInBytes", out JsonElement sizeElement))
            return false;

        if (sizeElement.GetInt64() != bytes.Length)
            return false;

        // Extract fileId from acceptContext and verify bytes are stored
        if (!content.TryGetProperty("acceptContext", out JsonElement acceptCtx))
            return false;

        string? fileId = acceptCtx.GetProperty("fileId").GetString();
        if (fileId == null)
            return false;

        if (!BotMessageHandler.PendingAudioUploads.TryRemove(fileId, out byte[]? storedBytes))
            return false;

        return storedBytes.SequenceEqual(bytes);
    }
}
