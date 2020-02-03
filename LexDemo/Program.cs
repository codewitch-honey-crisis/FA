using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using CU;
using L;
using LC;
namespace LexDemo
{
	class Program
	{
		static void Main()
		{
			var test = "fubar bar 123 1foo bar -243 0 baz 83";
			Console.WriteLine("Lex: " + test);
			var prog = Lex.CompileLexerRegex(false,
					@"[A-Z_a-z][A-Z_a-z0-9]*", // id
					@"0|(\-?[1-9][0-9]*)", // int
					@"( |\t|\r|\n|\v|\f)" // space
				);
			Console.WriteLine("Unoptimized dump:");
			Console.WriteLine(Lex.Disassemble(prog));
			Console.WriteLine();

			var progOpt = Lex.CompileLexerRegex(true,
					@"[A-Z_a-z][A-Z_a-z0-9]*", // id
					@"0|(\-?[1-9][0-9]*)", // int
					@"( |\t|\r|\n|\v|\f)" // space
				);

			Console.WriteLine("Optimized dump:");
			Console.WriteLine(Lex.Disassemble(progOpt));
			Console.WriteLine();
			var progDfa = Lex.AssembleFrom(@"..\..\dfa.lasm");
			Console.WriteLine("DFA dump:");
			Console.WriteLine(Lex.Disassemble(progDfa));
			Console.WriteLine();
			var result = -1;
			var count = 0f ;
			var maxFiberCount = 0;
			var avgCharPasses = 0f;
			LexContext lc = LexContext.Create(test);
			while (LexContext.EndOfInput!=lc.Current)
			{
				var stats = Lex.RunWithLoggingAndStatistics(prog, lc, TextWriter.Null, out result);
				maxFiberCount = stats.MaxFiberCount;
				if (stats.AverageCharacterPasses > avgCharPasses)
					avgCharPasses = stats.AverageCharacterPasses;

				++count;
			}
			Console.WriteLine("NFA ran with "+maxFiberCount+" max fibers and " + avgCharPasses+ " average char passes");
			count = 0f;
			maxFiberCount = 0;
			avgCharPasses = 0f;
			count = 0;
			lc = LexContext.Create(test);
			while (LexContext.EndOfInput != lc.Current)
			{
				var stats = Lex.RunWithLoggingAndStatistics(progOpt, lc, TextWriter.Null, out result);
				maxFiberCount = stats.MaxFiberCount;
				if (stats.AverageCharacterPasses > avgCharPasses)
					avgCharPasses = stats.AverageCharacterPasses;

				++count;
			}
			Console.WriteLine("NFA+DFA (optimized) ran with " +  maxFiberCount+ " max fibers and " + avgCharPasses + " average char passes");
			count = 0;
			maxFiberCount = 0;
			avgCharPasses = 0f;
			lc = LexContext.Create(test);
			while (LexContext.EndOfInput != lc.Current)
			{
				var stats = Lex.RunWithLoggingAndStatistics(progDfa, lc, TextWriter.Null, out result);
				maxFiberCount = stats.MaxFiberCount;
				if (stats.AverageCharacterPasses > avgCharPasses)
					avgCharPasses = stats.AverageCharacterPasses;
				
				++count;
			}
			Console.WriteLine("DFA ran with " + maxFiberCount + " max fibers and " + avgCharPasses+ " average char passes");

			for (var i = 0; i < 5; ++i)
				test = string.Concat(test, test);

			for (var i = 0; i < 10; ++i)
			{
				Console.WriteLine("Pass #" + (i + 1));
				Console.Write("NFA: ");
				_Perf(prog, test);
				Console.WriteLine();
				Console.Write("NFA+DFA (optimized): ");
				_Perf(progOpt, test);
				Console.WriteLine();
				Console.Write("DFA: ");
				_Perf(progDfa, test);
				Console.WriteLine();
				Console.WriteLine();
			}
			Console.WriteLine();
			_RunLexer();
		}
		static void _Perf(int[][] prog,string test)
		{
			var sw = PrecisionDateTime.IsAvailable?null:new Stopwatch();
			DateTime utcStart;
			DateTime utcEnd;
			TimeSpan elapsed=TimeSpan.Zero;
			const int ITER = 100;
			ConsoleUtility.WriteProgressBar(0,false);
			for (var i = 0; i < ITER; ++i)
			{
				if(0==((i+1) %10))
					ConsoleUtility.WriteProgressBar(i, true);
				var lc = LexContext.Create(test);
				while (LexContext.EndOfInput != lc.Current)
				{
					lc.ClearCapture();
					if (!PrecisionDateTime.IsAvailable)
					{
						sw.Start();
						var acc = Lex.Run(prog, lc);
						sw.Stop();
						elapsed += sw.Elapsed;
					} else
					{
						utcStart = PrecisionDateTime.UtcNow;
						var acc = Lex.Run(prog, lc);
						utcEnd = PrecisionDateTime.UtcNow;
						elapsed += (utcEnd - utcStart);
					}
				}
			}
			ConsoleUtility.EraseProgressBar();
			Console.WriteLine("Lexed in " + elapsed.TotalMilliseconds / (float)ITER + " msec");
		}

		
		static void _RunLexer()
		{

			// compile a lexer
			var prog = Lex.CompileLexerRegex(true,
				@"[A-Z_a-z][A-Z_a-z0-9]*", // id
				@"0|(\-?[1-9][0-9]*)", // int
				@"( |\t|\r|\n|\v|\f)" // space
			);
			
			// dump the program to the console
			Console.WriteLine(Lex.Disassemble(prog));

			// our test data - 14 tokens. 29 length
			var text = "fubar bar 123 1foo bar -243 0";
			Console.WriteLine("Lex: " + text);

			// spin up a lexer context
			// see: https://www.codeproject.com/Articles/5256794/LexContext-A-streamlined-cursor-over-a-text-input
			var lc = LexContext.Create(text);
			
			// while more input to be read
			while(LexContext.EndOfInput!=lc.Current)
			{
				// clear any current captured data
				lc.ClearCapture();
				int acc;
				var stat = Lex.RunWithLoggingAndStatistics(prog, lc, Console.Error, out acc);
				// lex our next input and dump it
				Console.WriteLine("{0}: \"{1}\"", acc, lc.GetCapture());
			}
		}
	}
}
