#region Usings
using System;
using System.Diagnostics;
using System.IO;
using LC;
using L;
#endregion
/* Build Evenets
"$(SolutionDir)Lexly.exe" "$(ProjectDir)Example.lx" /output "$(ProjectDir)ExampleTokenizer.cs" /namespace LexlyDemo
"$(SolutionDir)Lexly.exe" "$(ProjectDir)Slang.lx" /output "$(ProjectDir)SlangTokenizer.cs" /namespace LexlyDemo /noshared
*/
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
				// we don't want errors or whitespace but we don't know the symbol
				// id for whitespace because you can switch tokenizers around
				// so we check tok.Value instead
				if(-1!=tok.SymbolId && !string.IsNullOrWhiteSpace(tok.Value))
					Console.WriteLine("{0}: {1}", tok.SymbolId, tok.Value);
			}
			
			Stopwatch sw = new Stopwatch();
			const int ITER = 1000;
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
