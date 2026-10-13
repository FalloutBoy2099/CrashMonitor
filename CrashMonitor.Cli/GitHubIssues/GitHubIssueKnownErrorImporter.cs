namespace CrashMonitor.Cli.GitHubIssues
{
	using System;
	using System.Collections.Generic;
	using System.Globalization;
	using System.Linq;
	using System.Net.Http;
	using System.Net.Http.Headers;
	using System.Text.Json;
	using System.Text.RegularExpressions;
	using System.Threading;
	using System.Threading.Tasks;
	using CrashMonitor.Analyzer.KnownErrors;

	internal sealed class GitHubIssueKnownErrorImporter
	{
		private static readonly Regex InstructionPairRegex = new(
			@"(?is)\binstruction\s+(?:at\s+)?(?<instruction>(?:0x)?[0-9a-f]{8}).{0,160}?\b(?:referenced|read|write|execute|accessed)\b.{0,160}?(?<memory>(?:0x)?[0-9a-f]{8})",
			RegexOptions.Compiled);

		private static readonly Regex AddressRegex = new(
			@"(?<![0-9a-fA-F])(?:0x)?(?<address>[0-9a-fA-F]{8})(?![0-9a-fA-F])",
			RegexOptions.Compiled);

		private readonly HttpClient _client;

		public GitHubIssueKnownErrorImporter(HttpClient client)
		{
			_client = client;
		}

		public async Task<IReadOnlyList<KnownErrorEntry>> ImportAsync(GitHubIssueImportOptions options, CancellationToken cancellationToken)
		{
			var entries = new List<KnownErrorEntry>();
			var seen = new HashSet<String>(StringComparer.OrdinalIgnoreCase);

			for (var page = 1; page <= options.MaxPages; page++)
			{
				var uri = BuildUri(options.Repository, options.State, page);
				using var response = await _client.GetAsync(uri, cancellationToken);
				response.EnsureSuccessStatusCode();

				await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
				using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
				if (document.RootElement.GetArrayLength() == 0)
				{
					break;
				}

				foreach (var issue in ReadIssues(document.RootElement))
				{
					foreach (var entry in BuildEntries(issue, options))
					{
						var key = $"{entry.Reference}|{entry.Address}|{entry.ObservedMemoryAddress}";
						if (seen.Add(key))
						{
							entries.Add(entry);
						}
					}
				}
			}

			return entries
				.OrderBy(entry => entry.Address, StringComparer.OrdinalIgnoreCase)
				.ThenBy(entry => entry.Reference, StringComparer.OrdinalIgnoreCase)
				.ToArray();
		}

		private static String BuildUri(String repository, String state, Int32 page)
		{
			return $"https://api.github.com/repos/{repository}/issues?state={Uri.EscapeDataString(state)}&per_page=100&page={page}";
		}

		private static IEnumerable<GitHubIssue> ReadIssues(JsonElement root)
		{
			foreach (var item in root.EnumerateArray())
			{
				if (item.TryGetProperty("pull_request", out _))
				{
					continue;
				}

				yield return new GitHubIssue(
					item.GetProperty("number").GetInt32(),
					item.GetProperty("title").GetString() ?? "",
					item.TryGetProperty("body", out var body) ? body.GetString() ?? "" : "",
					item.GetProperty("html_url").GetString() ?? "");
			}
		}

		private static IEnumerable<KnownErrorEntry> BuildEntries(GitHubIssue issue, GitHubIssueImportOptions options)
		{
			var text = issue.Title + Environment.NewLine + issue.Body;
			var pairMatches = InstructionPairRegex.Matches(text);
			var index = 0;

			foreach (Match match in pairMatches)
			{
				var instructionAddress = NormalizeAddress(match.Groups["instruction"].Value);
				var memoryAddress = NormalizeAddress(match.Groups["memory"].Value);
				if (!ShouldInclude(instructionAddress, options))
				{
					continue;
				}

				yield return CreateEntry(issue, instructionAddress, memoryAddress, index++);
			}

			if (index > 0)
			{
				yield break;
			}

			foreach (var address in ExtractLikelyInstructionAddresses(text, options).Take(4))
			{
				yield return CreateEntry(issue, address, null, index++);
			}
		}

		private static IEnumerable<String> ExtractLikelyInstructionAddresses(String text, GitHubIssueImportOptions options)
		{
			return AddressRegex.Matches(text)
				.Select(match => NormalizeAddress(match.Groups["address"].Value))
				.Where(address => ShouldInclude(address, options))
				.Distinct(StringComparer.OrdinalIgnoreCase);
		}

		private static Boolean ShouldInclude(String address, GitHubIssueImportOptions options)
		{
			var value = UInt32.Parse(address[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
			if (!options.IncludeLowAddresses && value < options.MinAddress)
			{
				return false;
			}

			if (options.OnlyFalloutExe && GuessModule(address) != "fallout2.exe")
			{
				return false;
			}

			return value <= 0x7FFFFFFF;
		}

		private static KnownErrorEntry CreateEntry(GitHubIssue issue, String address, String? memoryAddress, Int32 index)
		{
			return new KnownErrorEntry
			{
				Id = $"github-crashmonitor-{issue.Number}-{index + 1}-{address[2..].ToLowerInvariant()}",
				Module = GuessModule(address),
				Address = address,
				Tolerance = 0,
				Description = $"Untriaged CrashMonitor issue #{issue.Number}: {NormalizeTitle(issue.Title)}",
				Cause = "Imported from a public CrashMonitor issue; cause not triaged yet.",
				Workaround = "Inspect the referenced issue, attached logs and any dump before promoting this entry to curated status.",
				Reference = issue.HtmlUrl,
				Status = "Untriaged",
				Source = "egornovivan/CrashMonitor GitHub issues",
				ObservedMemoryAddress = memoryAddress
			};
		}

		private static String NormalizeAddress(String value)
		{
			var clean = value.Trim();
			if (clean.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
			{
				clean = clean[2..];
			}

			var numeric = UInt32.Parse(clean, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
			return $"0x{numeric:X8}";
		}

		private static String? GuessModule(String address)
		{
			var value = UInt32.Parse(address[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
			if (value is >= 0x00400000 and < 0x00700000)
			{
				return "fallout2.exe";
			}

			return null;
		}

		private static String NormalizeTitle(String title)
		{
			var normalized = Regex.Replace(title.Trim(), @"\s+", " ");
			return normalized.Length <= 140 ? normalized : normalized[..137] + "...";
		}

		private sealed record GitHubIssue(Int32 Number, String Title, String Body, String HtmlUrl);
	}
}