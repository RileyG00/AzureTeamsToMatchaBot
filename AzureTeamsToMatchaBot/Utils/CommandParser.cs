using System.Text.RegularExpressions;
using MatchaRunner.Constants;

namespace MatchaRunner.Utils
{
	public record TicketValidationResult(bool IsValid, string? ErrorMessage);

	public static class CommandParser
	{
		private static readonly Regex TicketNumberRegex = new(@"^(PIP-\d+)", RegexOptions.IgnoreCase);

		/// <summary>
		/// Returns true if the message contains the --audio-approved flag (case-insensitive).
		/// </summary>
		public static bool IsAudioApproved(string input) => input.ToLower().Contains("--audio-approved");

		/// <summary>
		/// Extracts the ticket number from the beginning of the message.
		/// Returns null if no ticket number is found at the start.
		/// </summary>
		public static string? ExtractTicketNumber(string input)
		{
			string trimmed = input.Trim();
			Match match = TicketNumberRegex.Match(trimmed);
			return match.Success ? match.Groups[1].Value.ToUpper() : null;
		}

		/// <summary>
		/// Validates that a ticket number matches PIP- followed by exactly 3 or 4 digits.
		/// Returns a TicketValidationResult with IsValid and ErrorMessage.
		/// </summary>
		public static TicketValidationResult ValidateTicketNumber(string? ticketNumber)
		{
			if (ticketNumber == null)
			{
				return new TicketValidationResult(false, CannedResponses.InvalidTicketMissing);
			}

			if (!ticketNumber.StartsWith("PIP-", StringComparison.OrdinalIgnoreCase))
			{
				return new TicketValidationResult(false, CannedResponses.InvalidTicketNotPip);
			}

			string digits = ticketNumber.Substring(4);
			if (!Regex.IsMatch(digits, @"^\d{3,4}$"))
			{
				return new TicketValidationResult(false, CannedResponses.InvalidTicketDigits);
			}

			return new TicketValidationResult(true, null);
		}

		/// <summary>
		/// Returns true if the message contains the --help flag (case-insensitive).
		/// </summary>
		public static bool IsAskingForHelp(string input) => input.ToLower().Contains("--help");

		/// <summary>
		/// Returns true if the message contains the --audio flag (case-insensitive).
		/// </summary>
		public static bool IsAskingForAudio(string input) => input.ToLower().Contains("--audio");

		/// <summary>
		/// Returns true if the message contains the --include-script flag (case-insensitive).
		/// </summary>
		public static bool IsAskingForAudioAndScript(string input) => input.ToLower().Contains("--include-script");
	}
}
