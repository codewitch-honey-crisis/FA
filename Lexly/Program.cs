#define OPTIMIZE
using System;
using System.CodeDom;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using CD;
using L;
using LC;
namespace Lexly
{
	public class Program
	{
		static readonly string CodeBase = _GetCodeBase();
		static readonly string FileName = Path.GetFileName(CodeBase);
		static readonly string Name = _GetName();
		static int Main(string[] args)
		{
			return Run(args, Console.In, Console.Out, Console.Error);
		}

		public static int Run(string[] args, TextReader stdin, TextWriter stdout, TextWriter stderr)
		{
			// our return code
			var result = 0;
			// app parameters
			string inputfile = null;
			string outputfile = null;
			string name = null;
			string codelanguage = null;
			string codenamespace = null;
			bool noshared = false;
			bool ifstale = false;
			bool dump = false;
			string graphfile = null;
			// our working variables
			TextReader input = null;
			TextWriter output = null;
			try
			{
				if (0 == args.Length)
				{
					_PrintUsage(stderr);
					result = -1;
				}
				else if (args[0].StartsWith("/"))
				{
					throw new ArgumentException("Missing input file.");
				}
				else
				{
					// process the command line args
					inputfile = args[0];
					for (var i = 1; i < args.Length; ++i)
					{
						switch (args[i])
						{
							case "/output":
								if (args.Length - 1 == i) // check if we're at the end
									throw new ArgumentException(string.Format("The parameter \"{0}\" is missing an argument", args[i].Substring(1)));
								++i; // advance 
								outputfile = args[i];
								break;
							case "/name":
								if (args.Length - 1 == i) // check if we're at the end
									throw new ArgumentException(string.Format("The parameter \"{0}\" is missing an argument", args[i].Substring(1)));
								++i; // advance 
								name = args[i];
								break;
							case "/language":
								if (args.Length - 1 == i) // check if we're at the end
									throw new ArgumentException(string.Format("The parameter \"{0}\" is missing an argument", args[i].Substring(1)));
								++i; // advance 
								codelanguage = args[i];
								break;
							case "/namespace":
								if (args.Length - 1 == i) // check if we're at the end
									throw new ArgumentException(string.Format("The parameter \"{0}\" is missing an argument", args[i].Substring(1)));
								++i; // advance 
								codenamespace = args[i];
								break;
							case "/graph":
								if (args.Length - 1 == i) // check if we're at the end
									throw new ArgumentException(string.Format("The parameter \"{0}\" is missing an argument", args[i].Substring(1)));
								++i; // advance 
								graphfile= args[i];
								break;
							case "/noshared":
								noshared = true;
								break;
							case "/ifstale":
								ifstale = true;
								break;
							case "/dump":
								dump = true;
								break;
							default:
								throw new ArgumentException(string.Format("Unknown switch {0}", args[i]));
						}
					}
					// now build it
					if (string.IsNullOrEmpty(name))
					{
						// default we want it to be named after the code file
						// otherwise we'll use inputfile
						if (null != outputfile)
							name = Path.GetFileNameWithoutExtension(outputfile);
						else
							name = Path.GetFileNameWithoutExtension(inputfile);
					}
					if (string.IsNullOrEmpty(codelanguage))
					{
						if (!string.IsNullOrEmpty(outputfile))
						{
							codelanguage = Path.GetExtension(outputfile);
							if (codelanguage.StartsWith("."))
								codelanguage = codelanguage.Substring(1);
						}
						if (string.IsNullOrEmpty(codelanguage))
							codelanguage = "cs";
					}
					var stale = true;
					if (ifstale && null != outputfile)
					{
						stale = _IsStale(inputfile, outputfile);
						if (!stale)
							stale = _IsStale(CodeBase, outputfile);
					}
					if (!stale)
					{
						stderr.WriteLine("{0} skipped building {1} because it was not stale.", Name, outputfile);
					}
					else
					{
						if (null != outputfile)
							stderr.Write("{0} is building file: {1}", Name, outputfile);
						else
							stderr.Write("{0} is building tokenizer.", Name);
						input = new StreamReader(inputfile);
						var rules = _ParseRules(input);
						input.Close();
						input = null;
						
						_FillRuleIds(rules);
						
						var ccu = new CodeCompileUnit();
						var cns = new CodeNamespace();
						if (!string.IsNullOrEmpty(codenamespace))
							cns.Name = codenamespace;
						ccu.Namespaces.Add(cns);
						var program = _BuildLexer(rules,dump);
						var symbolTable = _BuildSymbolTable(rules);
						var blockEnds = _BuildBlockEnds(rules);
						var nodeFlags = _BuildNodeFlags(rules);
						if (!noshared)
						{
							// import our Export/Token.cs into the library
							_ImportCompileUnit(Deslanged.Token, cns);
							// import our Export/Tokenizer.cs into the library
							_ImportCompileUnit(Deslanged.Tokenizer, cns);
							
						}
						var origName = "Lexly.";
						CodeTypeDeclaration td = null;
						var ccd = Deslanged.TokenizerTemplate;
						if (null == td) 
						{
							td = ccd.Namespaces[1].Types[0];
							origName += td.Name;
							td.Name = name;
							var f = CodeDomUtility.GetByName("Program", td.Members) as CodeMemberField;
							f.InitExpression = CodeDomUtility.Literal(program);
							f = CodeDomUtility.GetByName("BlockEnds", td.Members) as CodeMemberField;
							f.InitExpression = CodeDomUtility.Literal(blockEnds);
							f = CodeDomUtility.GetByName("NodeFlags", td.Members) as CodeMemberField;
							f.InitExpression = CodeDomUtility.Literal(nodeFlags);
							_GenerateSymbolConstants(td, symbolTable);
						}
						CodeDomVisitor.Visit(ccd, (ctx) => {
							var tr = ctx.Target as CodeTypeReference;
							if (null != tr && 0 == string.Compare(origName, tr.BaseType, StringComparison.InvariantCulture))
								tr.BaseType = name;
						});
						cns.Types.Add(td);
						
						var hasColNS = false;
						foreach (CodeNamespaceImport nsi in cns.Imports)
						{
							if (0 == string.Compare(nsi.Namespace, "System.Collections.Generic", StringComparison.InvariantCulture))
							{
								hasColNS = true;
								break;
							}
						}
						if (!hasColNS)
							cns.Imports.Add(new CodeNamespaceImport("System.Collections.Generic"));
						stderr.WriteLine();
						var prov = CodeDomProvider.CreateProvider(codelanguage);
						var opts = new CodeGeneratorOptions();
						opts.BlankLinesBetweenMembers = false;
						opts.VerbatimOrder = true;
						if (null == outputfile)
							output = stdout;
						else
						{
							// open the file and truncate it if necessary
							var stm = File.Open(outputfile, FileMode.Create);
							stm.SetLength(0);
							output = new StreamWriter(stm);
						}
						prov.GenerateCodeFromCompileUnit(ccu, output, opts);
					}
				}
			}
			// we don't like to catch in debug mode
#if !DEBUG
		    catch(Exception ex)
			{
				result = _ReportError(ex,stderr);
			}
#endif
			finally
			{
				// close the input file if necessary
				if (null != input)
					input.Close();
				// close the output file if necessary
				if (null != outputfile && null != output)
					output.Close();
			}
			return result;
		}
		static bool _IsStale(string inputfile, string outputfile)
		{
			var result = true;
			// File.Exists doesn't always work right
			try
			{
				if (File.GetLastWriteTimeUtc(outputfile) >= File.GetLastWriteTimeUtc(inputfile))
					result = false;
			}
			catch { }
			return result;
		}
		private static void _ImportCompileUnit(CodeCompileUnit fromCcu, CodeNamespace dst)
		{
			CD.CodeDomVisitor.Visit(fromCcu, (ctx) => {
				var ctr = ctx.Target as CodeTypeReference;
				if (null != ctr)
				{
					if (ctr.BaseType.StartsWith("Lexly."))
						ctr.BaseType = ctr.BaseType.Substring(6);
				}
			});
			// import all the usings and all the types
			foreach (CodeNamespace ns in fromCcu.Namespaces)
			{
				foreach (CodeNamespaceImport nsi in ns.Imports)
				{
					var found = false;
					foreach (CodeNamespaceImport nsicmp in dst.Imports)
					{
						if (0 == string.Compare(nsicmp.Namespace, nsi.Namespace, StringComparison.InvariantCulture))
						{
							found = true;
							break;
						}
					}
					if (!found)
						dst.Imports.Add(nsi);
				}
				foreach (CodeTypeDeclaration type in ns.Types)
				{
					type.CustomAttributes.Add(_GeneratedCodeAttribute);
					dst.Types.Add(type);
				}
			}
		}
		static readonly CodeAttributeDeclaration _GeneratedCodeAttribute
			= new CodeAttributeDeclaration(CodeDomUtility.Type(typeof(GeneratedCodeAttribute)), new CodeAttributeArgument(CodeDomUtility.Literal("Lexly")), new CodeAttributeArgument(CodeDomUtility.Literal(Assembly.GetExecutingAssembly().GetName().Version.ToString())));
		static void _GenerateSymbolConstants(CodeTypeDeclaration target, IList<string> symbolTable)
		{
			// generate symbol constants
			for (int ic = symbolTable.Count, i = 0; i < ic; ++i)
			{
				var symbol = symbolTable[i];
				if (null != symbol)
				{
					var s = _MakeSafeName(symbol);
					s = _MakeUniqueMember(target, s);
					var constField = CodeDomUtility.Field(typeof(int), s, MemberAttributes.Const | MemberAttributes.Public, CodeDomUtility.Literal(i));
					target.Members.Add(constField);
				}
			}
		}
		static string _MakeSafeName(string name)
		{
			var sb = new StringBuilder();
			if (char.IsDigit(name[0]))
				sb.Append('_');
			for (var i = 0; i < name.Length; ++i)
			{
				var ch = name[i];
				if ('_' == ch || char.IsLetterOrDigit(ch))
					sb.Append(ch);
				else
					sb.Append('_');
			}
			return sb.ToString();
		}
		static string _MakeUniqueMember(CodeTypeDeclaration decl, string name)
		{
			var seen = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);
			for (int ic = decl.Members.Count, i = 0; i < ic; i++)
				seen.Add(decl.Members[i].Name);
			var result = name;
			var suffix = 2;
			while (seen.Contains(result))
			{
				result = string.Concat(name, suffix.ToString());
				++suffix;
			}
			return result;
		}
		static void _FillRuleIds(IList<_LexRule> rules)
		{
			var ids = new HashSet<int>();
			for (int ic = rules.Count, i = 0; i < ic; ++i)
			{
				var rule = rules[i];
				if (int.MinValue != rule.Id && !ids.Add(rule.Id))
					throw new InvalidOperationException(string.Format("The input file has a rule with a duplicate id at line {0}, column {1}, position {2}", rule.Line, rule.Column, rule.Position));
			}
			var lastId = 0;
			for (int ic = rules.Count, i = 0; i < ic; ++i)
			{
				var rule = rules[i];
				if (int.MinValue == rule.Id)
				{
					rule.Id = lastId;
					ids.Add(lastId);
					while (ids.Contains(lastId))
						++lastId;
				}
				else
				{
					lastId = rule.Id;
					while (ids.Contains(lastId))
						++lastId;
				}
			}
		}
		static string[] _BuildBlockEnds(IList<_LexRule> rules)
		{
			int max = int.MinValue;
			for (int ic = rules.Count, i = 0; i < ic; ++i)
			{
				var rule = rules[i];
				if (rule.Id > max)
					max = rule.Id;
			}
			var result = new string[max + 1];
			for (int ic = rules.Count, i = 0; i < ic; ++i)
			{
				var rule = rules[i];
				var be = _GetAttr(rule, "blockEnd") as string;
				if (!string.IsNullOrEmpty(be))
				{
					result[rule.Id] = be;
				}
			}
			return result;
		}
		static int[] _BuildNodeFlags(IList<_LexRule> rules)
		{
			int max = int.MinValue;
			for (int ic = rules.Count, i = 0; i < ic; ++i)
			{
				var rule = rules[i];
				if (rule.Id > max)
					max = rule.Id;
			}
			var result = new int[max + 1];
			for (int ic = rules.Count, i = 0; i < ic; ++i)
			{
				var rule = rules[i];
				var hidden = _GetAttr(rule, "hidden");
				if ((hidden is bool) && (bool)hidden)
					result[rule.Id] = 1;
			}
			return result;
		}
		static object _GetAttr(_LexRule rule, string name, object @default = null)
		{
			var attrs = rule.Attributes;
			if (null != attrs)
			{
				for (var i = 0; i < attrs.Length; i++)
				{
					var attr = attrs[i];
					if (0 == string.Compare(attr.Key, name))
						return attr.Value;
				}
			}
			return @default;
		}
		static string[] _BuildSymbolTable(IList<_LexRule> rules)
		{
			int max = int.MinValue;
			for (int ic = rules.Count, i = 0; i < ic; ++i)
			{
				var rule = rules[i];
				if (rule.Id > max)
					max = rule.Id;
			}
			var result = new string[max + 1];
			for (int ic = rules.Count, i = 0; i < ic; ++i)
			{
				var rule = rules[i];
				result[rule.Id] = rule.Symbol;
			}
			return result;
		}
		static IList<_LexRule> _ParseRules(TextReader inp)
		{
			var result = new List<_LexRule>();
			var pc = LexContext.CreateFrom(inp);
			pc.EnsureStarted();
			while (-1 != pc.Current)
			{
				pc.TrySkipCCommentsAndWhiteSpace();
				if (-1 == pc.Current)
					break;
				pc.ClearCapture();
				var l = pc.Line;
				var c = pc.Column;
				var p = pc.Position;
				var rule = new _LexRule();
				rule.Line = l;
				rule.Column = c;
				rule.Position = p;
				if (!pc.TryReadCIdentifier())
					throw new ExpectingException(string.Format("Identifier expected at line {0}, column {1}, position {2}", l, c, p), l, c, p, "identifier");
				rule.Symbol = pc.GetCapture();
				rule.Id = int.MinValue;
				pc.ClearCapture();
				pc.TrySkipCCommentsAndWhiteSpace();
				pc.Expecting('<', '=','{');
				if ('<' == pc.Current)
				{
					pc.Advance();
					pc.Expecting();
					var attrs = new List<KeyValuePair<string, object>>();
					while (-1 != pc.Current && '>' != pc.Current)
					{
						pc.TrySkipCCommentsAndWhiteSpace();
						pc.ClearCapture();
						l = pc.Line;
						c = pc.Column;
						p = pc.Position;
						if (!pc.TryReadCIdentifier())
							throw new ExpectingException(string.Format("Identifier expected at line {0}, column {1}, position {2}", l, c, p), l, c, p, "identifier");
						var aname = pc.GetCapture();
						pc.TrySkipCCommentsAndWhiteSpace();
						pc.Expecting('=', '>', ',');
						if ('=' == pc.Current)
						{
							pc.Advance();
							pc.TrySkipCCommentsAndWhiteSpace();
							l = pc.Line;
							c = pc.Column;
							p = pc.Position;
							var value = pc.ParseJsonValue();
							attrs.Add(new KeyValuePair<string, object>(aname, value));
							if (0 == string.Compare("id", aname) && (value is double))
							{
								rule.Id = (int)((double)value);
								if (0 > rule.Id)
									throw new ExpectingException(string.Format("Expecting a non-negative integer at line {0}, column {1}, position {2}", l, c, p), l, c, p, "nonNegativeInteger");
							}
						}
						else
						{ // boolean true
							attrs.Add(new KeyValuePair<string, object>(aname, true));
						}
						pc.TrySkipCCommentsAndWhiteSpace();
						pc.Expecting(',', '>');
						if (',' == pc.Current)
							pc.Advance();
					}
					pc.Expecting('>');
					pc.Advance();
					rule.Attributes = attrs.ToArray();
					pc.TrySkipCCommentsAndWhiteSpace();
				}
				pc.Expecting('=','{');
				var isAsm = '{' == pc.Current;
				
				pc.Advance();
				if (!isAsm)
				{
					pc.TrySkipCCommentsAndWhiteSpace();
					pc.Expecting('\'', '\"');
					if ('\'' == pc.Current)
					{
						pc.Advance();
						pc.ClearCapture();
						pc.TryReadUntil('\'', '\\', false);
						pc.Expecting('\'');
						var pc2 = LexContext.Create(pc.GetCapture());
						pc2.EnsureStarted();
						pc2.SetLocation(pc.Line, pc.Column, pc.Position, pc.FileOrUrl);
						rule.Regex = pc.GetCapture();
						// we compile it twice essentially but once for dumping
						// and pre-validation
						rule.Part = Lex.CompileRegexPart(pc2,
							false

							) ;
						pc.Advance();
					}
					else
					{
						var str = pc.ParseJsonString();
						rule.Part = Lex.CompileLiteralPart(str);
						rule.Literal = str;
					}
				} else
				{
					var ll = pc.CaptureBuffer.Length;
					rule.Part=Lex.Assemble(pc);
					
					pc.Expecting('}');
					pc.Advance();
				}
				result.Add(rule);
			}
			if (0 == result.Count)
				throw new ExpectingException("Expecting lexer rules, but the document was empty", 0, 0, 0, "rule");
			return result;

		}
		static int[][] _BuildLexer(IList<_LexRule> rules,bool dump)
		{
			if(dump)
			{
				Console.WriteLine();
			}
			var parts = new KeyValuePair<int,int[][]>[rules.Count];
			for (var i = 0; i < parts.Length; ++i)
			{
				var id = rules[i].Id;
				var rule = rules[i];
				parts[i] = new KeyValuePair<int, int[][]>(id, rule.Part);
				if (dump)
				{
					Console.Error.WriteLine("Disassembly of "+rule.Symbol+":");
					Console.Error.WriteLine(Lex.Disassemble(rule.Part));
					Console.Error.WriteLine();
				}
			}
#if !OPTIMIZE
			var prog = Lex.LinkLexerParts(
				false
				, parts);
			if (dump)
			{
				Console.Error.WriteLine("Disassembly of unoptimized lexer:");
				Console.Error.WriteLine(Lex.Disassemble(prog));
				Console.Error.WriteLine();
			}
#else
			var exprs = new List<object>();
			for(int ic=rules.Count,i = 0;i<ic;++i)
			{
				var r = rules[i];
				if (null != r.Literal)
				{
					exprs.Add(r.Literal);
				}
				else if (null != r.Regex)
				{
					exprs.Add(Ast.Parse(LexContext.Create(r.Regex)));
				}
				else
					exprs.Add(r.Part);
			}
			var prog = Lex.CompileLexer(true, exprs.ToArray());
			if (dump)
			{
				Console.Error.WriteLine("Disassembly of optimized lexer:");
				Console.Error.WriteLine(Lex.Disassemble(prog));
				Console.Error.WriteLine();
			}
#endif
			return prog;
		}

		// do our error handling here (release builds)
		static int _ReportError(Exception ex, TextWriter stderr)
		{
			_PrintUsage(stderr);
			stderr.WriteLine("Error: {0}", ex.Message);
			return -1;
		}
		static void _PrintUsage(TextWriter stderr)
		{
			var t = stderr;
			// write the name of our app. this actually uses the 
			// name of the executable so it will always be correct
			// even if the executable file was renamed.
			t.WriteLine("{0} generates a lexer/tokenizer", Name);
			t.WriteLine();
			t.Write(FileName);
			t.WriteLine(" <inputfile> [/output <outputfile>] [/name <name>]");
			t.WriteLine("   [/namespace <codenamespace>] [/language <codelanguage>]");
			t.WriteLine("   [/noshared] [/ifstale]");
			t.WriteLine();
			t.WriteLine("   <inputfile>      The input file to use.");
			t.WriteLine("   <outputfile>     The output file to use - default stdout.");
			t.WriteLine("   <name>           The name to use - default taken from <inputfile>.");
			t.WriteLine("   <codelanguage>   The code language - default based on output file - default C#");
			t.WriteLine("   <codenamepace>   The code namespace");
			t.WriteLine("   <noshared>       Do not generate the shared dependency code");
			t.WriteLine("   <ifstale>        Do not generate unless <outputfile> is older than <inputfile>.");
			t.WriteLine();
			t.WriteLine("Any other switch displays this screen and exits.");
			t.WriteLine();
		}
		static string _GetCodeBase()
		{
			try
			{
				return Assembly.GetExecutingAssembly().GetModules()[0].FullyQualifiedName;
			}
			catch
			{
				return "lexly.exe";
			}
		}
		static string _GetName()
		{
			try
			{
				foreach (var attr in Assembly.GetExecutingAssembly().CustomAttributes)
				{
					if (typeof(AssemblyTitleAttribute) == attr.AttributeType)
					{
						return attr.ConstructorArguments[0].Value as string;
					}
				}
			}
			catch { }
			return Path.GetFileNameWithoutExtension(FileName);
		}
		

	}
	
	// used to hold the results of reading the input document
	class _LexRule
	{
		public int Id;
		public string Symbol;
		public KeyValuePair<string, object>[] Attributes;
		public int[][] Part;
		public int Line;
		public int Column;
		public long Position;
		public string Regex;
		public string Literal;
	}
	
}
