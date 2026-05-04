using System.Text.Json.Serialization;

namespace MatchaRunner.Api.Outgoing
{
	public class MatchaFlowPayloadDto
	{
		[JsonPropertyName("output_type")]
		public string OutputType { get; init; } = "text";

		[JsonPropertyName("input_type")]
		public string InputType { get; init; } = "chat";

		[JsonPropertyName("input_value")]
		public string InputValue { get; init; } = string.Empty;

		[JsonPropertyName("session_id")]
		public string SessionId { get; init; } = Guid.NewGuid().ToString();
	}
}
