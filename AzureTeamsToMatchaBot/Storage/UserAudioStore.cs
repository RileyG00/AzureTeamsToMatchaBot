using System.Collections.Concurrent;

namespace MatchaRunner.Storage
{
	public static class UserAudioStore
	{
		private static readonly ConcurrentDictionary<string, byte[]> _store = new();

		/// <summary>
		/// Stores or replaces the audio bytes for a given user.
		/// </summary>
		public static void StoreAudio(string userId, byte[] audioBytes)
		{
			_store[userId] = audioBytes;
		}

		/// <summary>
		/// Retrieves the audio bytes for a given user, or null if none exists.
		/// </summary>
		public static byte[]? GetAudio(string userId)
		{
			return _store.TryGetValue(userId, out var audioBytes) ? audioBytes : null;
		}

		/// <summary>
		/// Removes the audio entry for a given user. Called after successful upload.
		/// </summary>
		public static bool RemoveAudio(string userId)
		{
			return _store.TryRemove(userId, out _);
		}
	}
}
