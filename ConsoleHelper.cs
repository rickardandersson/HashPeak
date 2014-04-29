using System;
using Mono.Options;

namespace RA.HashPeak
{
	class ConsoleHelper
	{
		// Shows --help
		public static void ShowHelp(OptionSet os)
		{
			Console.WriteLine("Usage: HashPeak [OPTIONS]" + Environment.NewLine + Environment.NewLine + "Options:");
			os.WriteOptionDescriptions(Console.Out);
			Console.WriteLine();
			Environment.Exit(0);
		}

		// Writes specified message to console using specified color
		public static void Write(string message, ConsoleColor color)
		{
			var oldColor = Console.ForegroundColor;

			Console.ForegroundColor = color;
			Console.Write(message);
			Console.ForegroundColor = oldColor;
		}

		// Displays specified status and message and terminates application
		public static void Exit(string status, string message)
		{
			Console.ForegroundColor = ConsoleColor.Red;
			Console.Error.Write(status);
			Console.ForegroundColor = ConsoleColor.Gray;

			if (message != null)
				Console.Error.WriteLine(" - " + message + Environment.NewLine);
			else
				Console.Error.WriteLine(Environment.NewLine);

			Environment.Exit(1);
		}

		// Displays specified message and terminates application
		public static void Exit(string message)
		{
			Console.Error.WriteLine(message);

			Environment.Exit(1);
		}
	}
}
