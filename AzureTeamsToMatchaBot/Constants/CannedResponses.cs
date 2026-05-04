namespace MatchaRunner.Constants
{
	public static class CannedResponses
	{
		public const string Help = "Hi! I am here you help you craft a script for your PIPs. Reference me, and just enter your PIP number or the Jira link to your PIP. If you want the audio to be automatically generated for you, you can include `--audio` in your message. If you want the audio and the script returned to you, you can include `--audio --include-script` in your message.";
		public const string MessageReceived = "Hi! I got your message. I'm a bit slow, so please give me 30-60 seconds to return your script to you. I will add chat message when I am done, error or not.";
		public const string MessageReceivedAudio = "Hi! I got your message. Since you are requesting an audio file, this could take a couple minutes to process. Please be patient. I will add chat message when I am done, error or not.";

		// Audio-approved responses
		public const string AudioApprovedProcessing = "Uploading your audio file to Jira. This may take a moment...";
		public const string NoAudioFound = "⚠️ No audio file found for your session. Please generate an audio file first using the `--audio` command, then try `--audio-approved` again.";
		public const string InvalidTicketMissing = "⚠️ Your message must start with a valid PIP ticket number (e.g., `PIP-629 --audio-approved`).";
		public const string InvalidTicketNotPip = "⚠️ Audio upload is only supported for PIP tickets. The ticket number must start with `PIP-`.";
		public const string InvalidTicketDigits = "⚠️ Invalid ticket number format. The ticket number must be `PIP-` followed by 3 or 4 digits (e.g., `PIP-629` or `PIP-2815`).";
		public const string JiraUnavailable = "⚠️ The Jira service is currently unavailable. Please try again later.";
		public const string JiraAuthError = "⚠️ There is an authentication or permissions issue with the Jira integration. Please contact your administrator.";
	}
}
