namespace MatchaRunner.Configuration
{
	public sealed record Matcha
	{
		public string BaseUrl { get; set; } = string.Empty;
		public string FlowId { get; set; } = string.Empty;
		public string DesignStudioApiKey { get; set; } = string.Empty;
		public string OpenAIApiKey { get; set; } = string.Empty;
	}
}
