namespace MatchaRunner.Constants
{
	public static class CannedResponses
	{
		public const string Help = "Hi! I am here you help you craft a script for your PIPs. Reference me, and just enter your PIP number or the Jira link to your PIP. If you want the audio to be automatically generated for you, you can include `--audio` in your message. If you want the audio and the script returned to you, you can include `--audio --include-script` in your message.";
		public const string MessageReceived = "Hi! I got your message. I'm a bit slow, so please give me 30-60 seconds to return your script to you. I will add chat message when I am done, error or not.";
		public const string MessageReceivedAudio = "Hi! I got your message. Since you are requesting an audio file, this could take a couple minutes to process. Please be patient. I will add chat message when I am done, error or not.";
	}
}
