using System.Text.Json;

namespace MatchaRunner.Utils
{
	public static class TextUtils
	{
		public static string ParseMatchaResponse(string response)
		{
			try
			{
				using JsonDocument doc = JsonDocument.Parse(response);
				JsonElement root = doc.RootElement;

				return root
					.GetProperty("outputs")[0]
					.GetProperty("outputs")[0]
					.GetProperty("results")
					.GetProperty("message")
					.GetProperty("text")
					.GetString() ?? "I received an empty response.";
			}
			catch
			{
				throw new Exception("An invalid response was received from Matcha.");
			}
		}
	}
}
