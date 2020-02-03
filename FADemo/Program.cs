using System;
using System.Collections.Generic;
using System.Text;
using F;

namespace FADemo
{
	class Program
	{
		static void Main(string[] args)
		{
			var kws = "abstract|as|ascending|async|await|base|bool|break|byte|case|catch|char|checked|class|const|continue|decimal|default|delegate|descending|do|double|dynamic|else|enum|equals|explicit|extern|event|false|finally|fixed| float |for|foreach| get | global |goto|if|implicit|int|interface|internal|is|lock|long|namespace|new|null|object|operator|out|override|params|partial|private|protected|public|readonly|ref|return|sbyte|sealed|set|short|sizeof|stackalloc|static|string|struct|switch|this|throw|true|try|typeof|uint|ulong|unchecked|unsafe|ushort|using|var|virtual|void|volatile|while|yield";
			// shorten this so our state graphs aren't so big:
			kws = "as|base|case";
			var lexa = new FA[]
			{
				FA.Parse(kws,0),
				FA.Parse("[A-Z_a-z][0-9A-Z_a-z]*", 1),
				FA.Parse(@"""([^""]|\\[^n])*""", 2),
				FA.Parse("[\r\n\t\v\f ]+", 3)

			};
			
			// build our lexer
			var nfa = FA.ToLexer(lexa);
			nfa.TrimNeutrals();
			Console.WriteLine("NFA has " + nfa.FillClosure().Count + " states");
			// minimize
			var dfa = nfa.ToDfa();
			dfa.TrimDuplicates();
			Console.WriteLine("DFA has " + dfa.FillClosure().Count + " states");

			var baseFn = @"..\..\lex_";
			var fn = baseFn + "nfa.jpg";
			Console.WriteLine("Rendering...");
			Console.WriteLine(fn);
			try
			{
				nfa.RenderToFile(fn);
			}
			catch
			{
				Console.WriteLine("Rendering aborted - GraphViz is not installed. Visit GraphViz.org to download.");
			}
			
			fn = baseFn + "dfa.jpg";
			Console.WriteLine(fn);
			try
			{
				dfa.RenderToFile(fn);
			}
			catch { }
			var text = "\"\\\"foo\\tbar\\\"\"";
			text = "\"base foo \\\"bar\\\" foobar  bar 123 baz -345 fubar 1foo *#( 0\"";
			Console.Write("Lex NFA " + text + ": ");
			var sb = new StringBuilder();
			// lex NFA
			Console.WriteLine(nfa.Lex(UnicodeUtility.ToUtf32(text).GetEnumerator(), sb));
			
			
			// build a simple symbol table so our ids match our NFA
			var symids = new int[lexa.Length];
			for (var i = 0; i < symids.Length; i++)
				symids[i] = i;
			var dfaTable = dfa.ToDfaStateTable(symids);
			Console.Write("Lex DFA " + text + ": ");
			Console.WriteLine(FA.Lex(dfaTable,UnicodeUtility.ToUtf32(text).GetEnumerator(), sb));
			var tokenizer = new Tokenizer(dfa, text);
			foreach (var token in tokenizer)
				Console.WriteLine("{0}: {1}", token.SymbolId, token.Value);
			return;
		}
	}
}
