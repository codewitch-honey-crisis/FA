using System;
using System.Collections.Generic;
using F;

namespace FADemo
{
	class Program
	{
		static void Main(string[] args)
		{
			var kws = "abstract|as|ascending|async|await|base|bool|break|byte|case|catch|char|checked|class|const|continue|decimal|default|delegate|descending|do|double|dynamic|else|enum|equals|explicit|extern|event|false|finally|fixed| float |for|foreach| get | global |goto|if|implicit|int|interface|internal|is|lock|long|namespace|new|null|object|operator|out|override|params|partial|private|protected|public|readonly|ref|return|sbyte|sealed|set|short|sizeof|stackalloc|static|string|struct|switch|this|throw|true|try|typeof|uint|ulong|unchecked|unsafe|ushort|using|var|virtual|void|volatile|while|yield";
			kws = "as|base|case";
			var kw = FA.Parse(kws,0);
			var id = FA.Parse("[A-Z_a-z][0-9A-Z_a-z]*",1);
			var str = FA.Parse(@"""([^""]|\\[^n])*""",2);
			var ws = FA.Parse("[\r\n\t\v\f ]+", 3);
			str.TrimNeutrals();
			str.RenderToFile(@"..\..\string_nfa.jpg");
			var tmp = str.ToDfa();
			tmp.TrimDuplicates();
			tmp.RenderToFile(@"..\..\string_dfa.jpg");
			//var str = FA.Parse(@"""(\\([\\""\'abfnrtv0]|[0-7]{3}|x[0-9A-Fa-f]{2}|u[0-9A-Fa-f]{4}|U[0-9A-Fa-f]{8})|[^""])*""",2);
			var fa = new FA();
			fa.EpsilonTransitions.Add(kw);
			fa.EpsilonTransitions.Add(id);
			fa.EpsilonTransitions.Add(str);
			fa.EpsilonTransitions.Add(ws);
			var baseFn = @"..\..\lex_";
			var fn = baseFn + "nfa.jpg";
			Console.WriteLine("Rendering...");
			Console.WriteLine(fn);
			fa.RenderToFile(@"..\..\lex_nfa.jpg");
			fn = baseFn + "dfa.jpg";
			Console.WriteLine(fn);
			//fa = str;
			fa = fa.ToDfa();
			fa.TrimDuplicates();
			fa.RenderToFile(@"..\..\lex_dfa.jpg");
			var dfaTable = fa.ToDfaStateTable();
			var text = "\"\\\"foo\\tbar\\\"\"";
			text = "\"base foo \\\"bar\\\" foobar  bar 123 baz -345 fubar 1foo *#( 0\"";
			Console.Write("Lex " + text + " (first move): ");
			Console.Write(FA.FillMove(fa.FillEpsilonClosure(), '\"').Count);
			Console.WriteLine(FA.Move(dfaTable,0,'\"'));
			var tokenizer = new Tokenizer(fa, text);
			foreach (var token in tokenizer)
				Console.WriteLine("{0}: {1}", token.SymbolId, token.Value);
			return;
		}
	}
}
