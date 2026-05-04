using FsCheck;
using FsCheck.Xunit;
using MatchaRunner.Storage;
using Xunit;

namespace PIPScriptWriter.Tests;

/// <summary>
/// Property-based tests for UserAudioStore functionality.
/// Feature: audio-attachment-jira-upload, Property 6: User-audio store round-trip preserves data and replaces on overwrite
/// </summary>
public class UserAudioStorePropertyTests
{
	/// <summary>
	/// Property 6a: Store then Get returns the exact same byte array (round-trip).
	/// For any user ID and byte array, storing audio and then retrieving it
	/// returns a byte-identical copy.
	///
	/// **Validates: Requirements 3.1, 9.1, 9.2, 9.3**
	/// </summary>
	[Property(MaxTest = 100)]
	public bool StoreAndGetReturnsIdenticalBytes(NonEmptyString userId, byte[] audioBytes)
	{
		// Use a unique key to avoid cross-test interference
		string key = $"roundtrip_{userId.Get}_{Guid.NewGuid()}";

		try
		{
			UserAudioStore.StoreAudio(key, audioBytes);
			byte[]? retrieved = UserAudioStore.GetAudio(key);

			return retrieved != null && retrieved.SequenceEqual(audioBytes);
		}
		finally
		{
			UserAudioStore.RemoveAudio(key);
		}
	}

	/// <summary>
	/// Property 6b: Storing a second array for the same user replaces the first (overwrite).
	/// For any user ID and two distinct byte arrays, GetAudio returns only the
	/// most recently stored array.
	///
	/// **Validates: Requirements 3.1, 9.1, 9.2, 9.3**
	/// </summary>
	[Property(MaxTest = 100)]
	public bool StoreOverwriteReturnsOnlyLatestBytes(NonEmptyString userId, byte[] firstAudio, byte[] secondAudio)
	{
		string key = $"overwrite_{userId.Get}_{Guid.NewGuid()}";

		try
		{
			UserAudioStore.StoreAudio(key, firstAudio);
			UserAudioStore.StoreAudio(key, secondAudio);
			byte[]? retrieved = UserAudioStore.GetAudio(key);

			return retrieved != null && retrieved.SequenceEqual(secondAudio);
		}
		finally
		{
			UserAudioStore.RemoveAudio(key);
		}
	}

	/// <summary>
	/// Property 6c: RemoveAudio returns true when entry exists, false when it doesn't.
	///
	/// **Validates: Requirements 3.1, 9.1, 9.2, 9.3**
	/// </summary>
	[Property(MaxTest = 100)]
	public bool RemoveReturnsTrueWhenExistsFalseWhenNot(NonEmptyString userId, byte[] audioBytes)
	{
		string key = $"remove_{userId.Get}_{Guid.NewGuid()}";

		UserAudioStore.StoreAudio(key, audioBytes);
		bool firstRemove = UserAudioStore.RemoveAudio(key);
		bool secondRemove = UserAudioStore.RemoveAudio(key);

		return firstRemove && !secondRemove;
	}

	/// <summary>
	/// Property 6d: GetAudio returns null for users that have no stored audio.
	///
	/// **Validates: Requirements 3.1, 9.1, 9.2, 9.3**
	/// </summary>
	[Property(MaxTest = 100)]
	public bool GetAudioReturnsNullForNonExistentUser(NonEmptyString userId)
	{
		string key = $"nonexistent_{userId.Get}_{Guid.NewGuid()}";

		byte[]? retrieved = UserAudioStore.GetAudio(key);

		return retrieved == null;
	}
}
