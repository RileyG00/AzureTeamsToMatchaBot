using FsCheck;
using FsCheck.Xunit;
using MatchaRunner.Constants;
using MatchaRunner.Utils;
using Xunit;

namespace PIPScriptWriter.Tests;

/// <summary>
/// Property-based tests for CommandParser functionality.
/// Feature: audio-attachment-jira-upload, Property 1: Command parsing correctly identifies and extracts audio-approved requests
/// Feature: audio-attachment-jira-upload, Property 2: Ticket number validation accepts valid and rejects invalid formats
/// </summary>
public class CommandParserPropertyTests
{
	/// <summary>
	/// Property 1: Command parsing correctly identifies and extracts audio-approved requests.
	/// For any message containing --audio-approved with a valid ticket number (PIP-XXX or PIP-XXXX)
	/// at the start, IsAudioApproved returns true AND ExtractTicketNumber returns the correct ticket number.
	///
	/// **Validates: Requirements 1.1, 1.2**
	/// </summary>
	[Property(MaxTest = 100)]
	public bool AudioApprovedWithValidTicketIsIdentifiedAndExtracted(PositiveInt digitsPart, bool useFourDigits, NonEmptyString suffix)
	{
		// Generate a valid ticket number with 3 or 4 digits
		int number = useFourDigits
			? 1000 + (digitsPart.Get % 9000)   // 1000-9999 (4 digits)
			: 100 + (digitsPart.Get % 900);    // 100-999 (3 digits)

		string ticketNumber = $"PIP-{number}";
		string message = $"{ticketNumber} --audio-approved {suffix.Get}";

		bool isAudioApproved = CommandParser.IsAudioApproved(message);
		string? extractedTicket = CommandParser.ExtractTicketNumber(message);

		return isAudioApproved && extractedTicket == ticketNumber.ToUpper();
	}

	/// <summary>
	/// Property 1 (negative case): For messages NOT containing --audio-approved,
	/// IsAudioApproved returns false.
	///
	/// **Validates: Requirements 1.1, 1.2**
	/// </summary>
	[Property(MaxTest = 100)]
	public bool MessagesWithoutAudioApprovedFlagReturnFalse(NonEmptyString text)
	{
		// Remove any occurrence of --audio-approved from the generated string
		string message = text.Get
			.Replace("--audio-approved", "", StringComparison.OrdinalIgnoreCase);

		// Skip if the sanitization still somehow contains the flag
		if (message.Contains("--audio-approved", StringComparison.OrdinalIgnoreCase))
			return true;

		return !CommandParser.IsAudioApproved(message);
	}

	// ===================================================================
	// Property 2: Ticket number validation accepts valid and rejects invalid formats
	// ===================================================================

	/// <summary>
	/// Property 2a: For valid ticket numbers (PIP- followed by exactly 3 or 4 digits),
	/// ValidateTicketNumber returns IsValid = true and ErrorMessage = null.
	///
	/// **Validates: Requirements 1.3, 2.1, 2.2, 2.3**
	/// </summary>
	[Property(MaxTest = 100)]
	public bool ValidTicketNumbersAreAccepted(PositiveInt digitsPart, bool useFourDigits)
	{
		// Generate a valid ticket number with exactly 3 or 4 digits
		int number = useFourDigits
			? 1000 + (digitsPart.Get % 9000)   // 1000-9999 (4 digits)
			: 100 + (digitsPart.Get % 900);    // 100-999 (3 digits)

		string ticketNumber = $"PIP-{number}";

		TicketValidationResult result = CommandParser.ValidateTicketNumber(ticketNumber);

		return result.IsValid && result.ErrorMessage == null;
	}

	/// <summary>
	/// Property 2b: For null input, ValidateTicketNumber returns IsValid = false
	/// with InvalidTicketMissing error message.
	///
	/// **Validates: Requirements 1.3, 2.1, 2.2, 2.3**
	/// </summary>
	[Fact]
	public void NullTicketNumberReturnsInvalidTicketMissing()
	{
		TicketValidationResult result = CommandParser.ValidateTicketNumber(null);

		Assert.False(result.IsValid);
		Assert.Equal(CannedResponses.InvalidTicketMissing, result.ErrorMessage);
	}

	/// <summary>
	/// Property 2c: For strings not starting with "PIP-", ValidateTicketNumber returns
	/// IsValid = false with InvalidTicketNotPip error message.
	///
	/// **Validates: Requirements 1.3, 2.1, 2.2, 2.3**
	/// </summary>
	[Property(MaxTest = 100)]
	public bool NonPipPrefixReturnsInvalidTicketNotPip(NonEmptyString text)
	{
		string input = text.Get;

		// Ensure the string does NOT start with "PIP-" (case-insensitive)
		if (input.StartsWith("PIP-", StringComparison.OrdinalIgnoreCase))
			input = "NOTPIP-" + input;

		TicketValidationResult result = CommandParser.ValidateTicketNumber(input);

		return !result.IsValid && result.ErrorMessage == CannedResponses.InvalidTicketNotPip;
	}

	/// <summary>
	/// Property 2d: For strings starting with "PIP-" but with wrong digit count
	/// (not exactly 3 or 4 digits), ValidateTicketNumber returns IsValid = false
	/// with InvalidTicketDigits error message.
	///
	/// **Validates: Requirements 1.3, 2.1, 2.2, 2.3**
	/// </summary>
	[Property(MaxTest = 100)]
	public bool WrongDigitCountReturnsInvalidTicketDigits(PositiveInt digitsPart, byte digitCount)
	{
		// Generate digit counts that are NOT 3 or 4 (use 0, 1, 2, 5, 6, 7, etc.)
		int count = digitCount % 8; // 0-7
		if (count == 3 || count == 4)
			count = 5; // Force to an invalid count

		string digits;
		if (count == 0)
		{
			digits = "";
		}
		else
		{
			// Generate exactly 'count' digits
			digits = (digitsPart.Get % (int)Math.Pow(10, count))
				.ToString()
				.PadLeft(count, '1');
			// Ensure exactly the right number of digits
			digits = digits.Substring(0, Math.Min(digits.Length, count)).PadLeft(count, '1');
		}

		string ticketNumber = $"PIP-{digits}";

		TicketValidationResult result = CommandParser.ValidateTicketNumber(ticketNumber);

		return !result.IsValid && result.ErrorMessage == CannedResponses.InvalidTicketDigits;
	}
}
