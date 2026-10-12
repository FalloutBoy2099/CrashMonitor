namespace CrashMonitor.Cli
{
	using System;
	using System.Collections.Generic;
	using System.Globalization;

	internal sealed class CliOptions
	{
		private readonly Dictionary<String, String> _values;

		private CliOptions(Dictionary<String, String> values)
		{
			_values = values;
		}

		public static CliOptions Parse(String[] args)
		{
			var values = new Dictionary<String, String>(StringComparer.OrdinalIgnoreCase);

			for (var index = 0; index < args.Length; index++)
			{
				var item = args[index];
				if (!item.StartsWith("--", StringComparison.Ordinal))
				{
					continue;
				}

				var name = item[2..];
				if (String.IsNullOrWhiteSpace(name))
				{
					continue;
				}

				if (index + 1 >= args.Length || args[index + 1].StartsWith("--", StringComparison.Ordinal))
				{
					values[name] = "true";
					continue;
				}

				values[name] = args[index + 1];
				index++;
			}

			return new CliOptions(values);
		}

		public String? Get(String name)
		{
			return _values.TryGetValue(name, out var value) ? value : null;
		}

		public Boolean TryGetInt32(String name, out Int32 value)
		{
			return Int32.TryParse(Get(name), NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
		}
	}
}