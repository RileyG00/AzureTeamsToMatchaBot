using System.Text.Json;
using MatchaRunner.Handlers;
using Microsoft.Teams.Api;
using Xunit;

namespace PIPScriptWriter.Tests;

/// <summary>
/// Unit tests for CreateFileConsentAttachment helper method.
/// Feature: audio-attachment-response
/// </summary>
public class AudioAttachmentUnitTests
{
    /// <summary>
    /// Given known bytes, verify ContentType is the FileConsentCard content type.
    /// **Validates: Requirements 2.2**
    /// </summary>
    [Fact]
    public void CreateFileConsentAttachment_ReturnsCorrectContentType()
    {
        byte[] bytes = new byte[] { 0x49, 0x44, 0x33, 0x04, 0x00 };

        var attachment = BotMessageHandler.CreateFileConsentAttachment(bytes, "script-audio.mp3");

        Assert.Equal("application/vnd.microsoft.teams.card.file.consent", attachment.ContentType.ToString());

        // Clean up
        CleanupPendingUpload(attachment);
    }

    /// <summary>
    /// Given known bytes, verify the Content contains a FileConsentCard with correct sizeInBytes.
    /// **Validates: Requirements 2.1, 2.2**
    /// </summary>
    [Fact]
    public void CreateFileConsentAttachment_ContentHasCorrectSize()
    {
        byte[] bytes = new byte[] { 0xFF, 0xFB, 0x90, 0x00, 0x01, 0x02, 0x03 };

        var attachment = BotMessageHandler.CreateFileConsentAttachment(bytes, "script-audio.mp3");

        var content = Assert.IsType<JsonElement>(attachment.Content);
        Assert.True(content.TryGetProperty("sizeInBytes", out JsonElement sizeElement));
        Assert.Equal(bytes.Length, sizeElement.GetInt64());

        CleanupPendingUpload(attachment);
    }

    /// <summary>
    /// Given a filename "script-audio.mp3", verify Name is "script-audio.mp3".
    /// **Validates: Requirements 2.3**
    /// </summary>
    [Fact]
    public void CreateFileConsentAttachment_ReturnsCorrectName()
    {
        byte[] bytes = new byte[] { 0x01, 0x02, 0x03 };
        string fileName = "script-audio.mp3";

        var attachment = BotMessageHandler.CreateFileConsentAttachment(bytes, fileName);

        Assert.Equal(fileName, attachment.Name);

        CleanupPendingUpload(attachment);
    }

    /// <summary>
    /// Verify audio bytes are stored in PendingAudioUploads and can be retrieved by fileId.
    /// **Validates: Requirements 2.1, 2.4**
    /// </summary>
    [Fact]
    public void CreateFileConsentAttachment_StoresAudioBytesInPendingUploads()
    {
        byte[] bytes = new byte[] { 0xAB, 0xCD, 0xEF };

        var attachment = BotMessageHandler.CreateFileConsentAttachment(bytes, "test.mp3");

        var content = Assert.IsType<JsonElement>(attachment.Content);
        string? fileId = content.GetProperty("acceptContext").GetProperty("fileId").GetString();
        Assert.NotNull(fileId);

        Assert.True(BotMessageHandler.PendingAudioUploads.TryRemove(fileId!, out byte[]? storedBytes));
        Assert.Equal(bytes, storedBytes);
    }

    /// <summary>
    /// Verify the consent card has a description.
    /// **Validates: Requirements 2.3**
    /// </summary>
    [Fact]
    public void CreateFileConsentAttachment_HasDescription()
    {
        byte[] bytes = new byte[] { 0x01 };

        var attachment = BotMessageHandler.CreateFileConsentAttachment(bytes, "audio.mp3");

        var content = Assert.IsType<JsonElement>(attachment.Content);
        Assert.True(content.TryGetProperty("description", out JsonElement desc));
        Assert.False(string.IsNullOrEmpty(desc.GetString()));

        CleanupPendingUpload(attachment);
    }

    /// <summary>
    /// Edge case: large byte array works correctly.
    /// **Validates: Requirements 2.2, 2.3**
    /// </summary>
    [Fact]
    public void CreateFileConsentAttachment_WithLargeArray_WorksCorrectly()
    {
        byte[] bytes = new byte[50_000];
        new Random(42).NextBytes(bytes);

        var attachment = BotMessageHandler.CreateFileConsentAttachment(bytes, "large-audio.mp3");

        Assert.Equal("application/vnd.microsoft.teams.card.file.consent", attachment.ContentType.ToString());
        Assert.Equal("large-audio.mp3", attachment.Name);

        var content = Assert.IsType<JsonElement>(attachment.Content);
        Assert.Equal(bytes.Length, content.GetProperty("sizeInBytes").GetInt64());

        // Verify bytes stored correctly
        string? fileId = content.GetProperty("acceptContext").GetProperty("fileId").GetString();
        Assert.NotNull(fileId);
        Assert.True(BotMessageHandler.PendingAudioUploads.TryRemove(fileId!, out byte[]? storedBytes));
        Assert.Equal(bytes, storedBytes);
    }

    /// <summary>
    /// Helper to clean up PendingAudioUploads after a test.
    /// </summary>
    private static void CleanupPendingUpload(Attachment attachment)
    {
        if (attachment.Content is JsonElement content &&
            content.TryGetProperty("acceptContext", out JsonElement ctx) &&
            ctx.TryGetProperty("fileId", out JsonElement fid))
        {
            string? fileId = fid.GetString();
            if (fileId != null)
                BotMessageHandler.PendingAudioUploads.TryRemove(fileId, out _);
        }
    }
}
