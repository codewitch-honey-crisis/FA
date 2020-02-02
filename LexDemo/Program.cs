using System;
using System.Diagnostics;
using System.IO;
using CU;
using L;
using LC;
namespace LexDemo
{
	class Program
	{
		static void Main2()
		{
			var id = @"[A-Z_a-z][A-Z_a-z0-9]*";
			var @int = @"0|(\-?[1-9][0-9]*)";
			var space = @"( |\t|\r|\n|\v|\f)";
			Lex.RenderOptimizedExecutionGraph(id, @"..\..\id_nfa.jpg");
			Lex.RenderOptimizedExecutionGraph(@int, @"..\..\int_nfa.jpg");
			Lex.RenderOptimizedExecutionGraph(@space, @"..\..\space_nfa.jpg");
			var prog = Lex.CompileLexerRegex(true,
				 id, // id
				 @int, // int
				 space // space
			 );
			prog = Lex.CompileRegexPart(@int);
			prog = Lex.FinalizePart(prog);
			Console.WriteLine(Lex.Disassemble(prog));
			var result = 0;
			Lex.RunWithLoggingAndStatistics(prog, LexContext.Create("123"), Console.Out, out result);
			Console.WriteLine(result);
		}
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
				maxFiberCount += stats.MaxFiberCount;
				if (stats.AverageCharacterPasses > avgCharPasses)
					avgCharPasses = stats.AverageCharacterPasses;

				++count;
			}
			Console.WriteLine("NFA ran with "+maxFiberCount/count + " average fibers and " + avgCharPasses+ " average char passes");
			count = 0f;
			maxFiberCount = 0;
			avgCharPasses = 0f;
			count = 0;
			lc = LexContext.Create(test);
			while (LexContext.EndOfInput != lc.Current)
			{
				var stats = Lex.RunWithLoggingAndStatistics(progOpt, lc, TextWriter.Null, out result);
				maxFiberCount += stats.MaxFiberCount;
				if (stats.AverageCharacterPasses > avgCharPasses)
					avgCharPasses = stats.AverageCharacterPasses;

				++count;
			}
			Console.WriteLine("NFA+DFA (optimized) ran with " + maxFiberCount/count + " average fibers and " + avgCharPasses + " average char passes");
			count = 0;
			maxFiberCount = 0;
			avgCharPasses = 0f;
			lc = LexContext.Create(test);
			while (LexContext.EndOfInput != lc.Current)
			{
				var stats = Lex.RunWithLoggingAndStatistics(progDfa, lc, TextWriter.Null, out result);
				maxFiberCount += stats.MaxFiberCount;
				if (stats.AverageCharacterPasses > avgCharPasses)
					avgCharPasses = stats.AverageCharacterPasses;
				
				++count;
			}
			Console.WriteLine("DFA ran with " + maxFiberCount/count + " average fibers and " + avgCharPasses+ " average char passes");

			for (var i = 0; i < 5; ++i)
				test = string.Concat(test, test);

			for (var i = 0; i < 10; ++i)
			{
				Console.WriteLine("Pass #" + (i + 1));
				Console.Write("NFA: ");
				Perf(prog, test);
				Console.Write("NFA+DFA (optimized): ");
				Perf(progOpt, test);
				
				Console.Write("DFA: ");
				Perf(progDfa, test);
			}
		}
		static void Perf(int[][] prog,string test)
		{
			var sw = new Stopwatch();
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
					sw.Start();
					var acc = Lex.Run(prog, lc);
					sw.Stop();
				}
			}
			ConsoleUtility.EraseProgressBar();
			Console.WriteLine("Lexed in " + sw.ElapsedMilliseconds / (float)ITER + " msec");
		}
		static void Test()
		{
			var test = "switch case \"a\":L0001, case \"b\":L0002, default: L0004\r\n" +
				"L0001: char \"b\"\r\n" +
				"L0002: char \"c\"\r\n" +
				"L0003: match 1\r\n" +
				"L0004: any\r\n" +
				"L0005: match -1\r\n";
			
			var prog = Lex.AssembleFrom(@"..\..\int.lasm");
			
			//Console.WriteLine(Lex.Disassemble(prog));
			var lc = LexContext.Create("1000");
			//Console.WriteLine("{0}: {1}",Lex.Run(prog,lc),lc.GetCapture());
			//
			//"((\\(['\\"abfnrtv0]|[0-7]{3}|x[0-9A-Fa-f]{2}|u[0-9A-Fa-f]{4}|U[0-9A-Fa-f]{8}))|[^\\"])*"
			test = @"""((\\(['\\""abfnrtv0]|[0-7]{3}|x[0-9A-Fa-f]{2}|u[0-9A-Fa-f]{4}|U[0-9A-Fa-f]{8}))|[^\\""])*""";
			//Lex.RenderGraph(LexContext.Create(test),@"..\..\string_nfa.jpg");
			prog = Lex.CompileRegexPart(test);
			prog = Lex.FinalizePart(prog);
			Console.WriteLine(Lex.Disassemble(prog));
			test = "\"\\\"\\tHello World!\\\"\"";
			lc = LexContext.Create(test);
			var result = 0;
			Lex.RunWithLoggingAndStatistics(prog, lc, Console.Error, out result);
			if (-1 != result)
			{
				Console.Write("Matched " + test + ": ");
				Console.WriteLine(lc.GetCapture());
			}
			else
			{
				Console.Write("Matched " + test + ": ");
				Console.WriteLine("False - failed at position " + lc.Position);
			}
			return;
			
			_RunLexer();
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
				// lex our next input and dump it
				Console.WriteLine("{0}: \"{1}\"", Lex.Run(prog, lc), lc.GetCapture());
			}
			var sw = new Stopwatch();
			const int ITER = 1000;
			for(var i = 0;i<ITER;++i)
			{
				lc = LexContext.Create(text);
				while (LexContext.EndOfInput != lc.Current)
				{
					lc.ClearCapture();
					sw.Start();
					var acc = Lex.Run(prog, lc);
					sw.Stop();
				}
			}
			Console.WriteLine("Lexed in " + sw.ElapsedMilliseconds / (float)ITER + " msec");
		}
	}
}
