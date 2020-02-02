#region Usings
using System;
using System.Diagnostics;
using System.IO;
using LC;
using L;
#endregion

namespace LexlyDemo
{
	using TestTokenizer = ExampleTokenizer;
	class Program
	{
		static void Main(string[] args)
		{
			var text = "foo 123 bar";

			using (var sr = new StreamReader(@"..\..\Program.cs"))
				text = sr.ReadToEnd();
			
			Console.WriteLine("Lex: " + text);

			var tokenizer = new TestTokenizer(text); // generated from Example.lx
			
			Console.WriteLine("Disassembly:");
			Console.WriteLine(Lex.Disassemble(TestTokenizer.Program));
			Console.WriteLine();
			
			foreach (var tok in tokenizer)
			{
				Console.WriteLine("{0}: {1}", tok.SymbolId, tok.Value);
			}
			
			Stopwatch sw = new Stopwatch();
			const int ITER = 100;
			for (var i = 0; i < ITER; ++i)
			{
				var lc = LexContext.Create(text);
				while (LexContext.EndOfInput != lc.Current)
				{
					lc.ClearCapture();
					sw.Start();
					var acc = Lex.Run(TestTokenizer.Program, lc);
					sw.Stop();
				}
			}
			
			Console.WriteLine("Lexed in " + sw.ElapsedMilliseconds / (float)ITER + " msec");
		}
	}
}
