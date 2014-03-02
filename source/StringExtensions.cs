using System;
using Microsoft.Practices.EnterpriseLibrary.Logging;

namespace MetaFeedConsole {
	internal static class StringExtensions {
		internal static void ConsoleWrite(this string text, ConsoleColor foregroundColor) {
			ConsoleWrite(text, foregroundColor, ConsoleColor.Black, false, true);
		}

		internal static void ConsoleWrite(this string text, ConsoleColor foregroundColor, bool writeToLog) {
			ConsoleWrite(text, foregroundColor, ConsoleColor.Black, false, writeToLog);
		}

		internal static void ConsoleWrite(this string text, ConsoleColor foregroundColor, ConsoleColor backgroundColor,
			bool writeToLog) {
			ConsoleWrite(text, foregroundColor, backgroundColor, false, writeToLog);
		}

		internal static void ConsoleWriteLine(this string text, ConsoleColor foregroundColor, ConsoleColor backgroundColor,
			bool writeToLog) {
			ConsoleWrite(text, foregroundColor, backgroundColor, true, writeToLog);
		}

		internal static void ConsoleWriteLine(this string text, ConsoleColor foregroundColor, bool WriteToLog) {
			ConsoleWrite(text, foregroundColor, ConsoleColor.Black, true, WriteToLog);
		}

		internal static void ConsoleWriteLine(this string text, ConsoleColor foregroundColor) {
			ConsoleWrite(text, foregroundColor, ConsoleColor.Black, true, true);
		}

		internal static void ConsoleWrite(this string text, ConsoleColor foregroundColor, ConsoleColor backgroundColor,
			bool writeLine, bool writeToLog) {
			Console.BackgroundColor = backgroundColor;
			Console.ForegroundColor = foregroundColor;
			if (true == writeLine) {
				Console.WriteLine(text);
			}
			else {
				Console.Write(text);
			}
			if (true == writeToLog) {
				Logger.Write(text);
			}
		}
	}
}