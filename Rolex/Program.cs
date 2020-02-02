using CD;
using LC;
using F;
using System;
using System.CodeDom;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace Rolex
{
	public class Program
	{
		static readonly string CodeBase = _GetCodeBase();
		static readonly string Filename = Path.GetFileName(CodeBase);
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
			string codeclass = null;
			string codelanguage = null;
			string codenamespace = null;
			string nfagraph = null;
			string dfagraph = null;
			bool ignorecase = false;
			bool prototype = false;
			bool noshared = false;
			bool ifstale = false;
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
						switch (args[i].ToLowerInvariant())
						{
							case "/output":
								if (args.Length - 1 == i) // check if we're at the end
									throw new ArgumentException(string.Format("The parameter \"{0}\" is missing an argument", args[i].Substring(1)));
								++i; // advance 
								outputfile = args[i];
								break;
							case "/class":
								if (args.Length - 1 == i) // check if we're at the end
									throw new ArgumentException(string.Format("The parameter \"{0}\" is missing an argument", args[i].Substring(1)));
								++i; // advance 
								codeclass = args[i];
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
							case "/nfagraph":
								if (args.Length - 1 == i) // check if we're at the end
									throw new ArgumentException(string.Format("The parameter \"{0}\" is missing an argument", args[i].Substring(1)));
								++i; // advance 
								nfagraph = args[i];
								break;
							case "/dfagraph":
								if (args.Length - 1 == i) // check if we're at the end
									throw new ArgumentException(string.Format("The parameter \"{0}\" is missing an argument", args[i].Substring(1)));
								++i; // advance 
								dfagraph = args[i];
								break;
							case "/ignorecase":
								ignorecase = true;
								break;
							case "/prototype":
								prototype = true;
								break;
							case "/noshared":
								noshared = true;
								break;
							case "/ifstale":
								ifstale = true;
								break;


							default:
								throw new ArgumentException(string.Format("Unknown switch {0}", args[i]));
						}
					}
					// now build it
					if (string.IsNullOrEmpty(codeclass))
					{
						// default we want it to be named after the code file
						// otherwise we'll use inputfile
						if (null != outputfile)
							codeclass = Path.GetFileNameWithoutExtension(outputfile);
						else
							codeclass = Path.GetFileNameWithoutExtension(inputfile);
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
						var rules = new List<LexRule>();
						string line;
						while(null!=(line=input.ReadLine()))
						{
							var lc = LexContext.Create(line);
							lc.TrySkipCCommentsAndWhiteSpace();
							if(-1!=lc.Current)
								rules.Add(LexRule.Parse(lc));
						}
						input.Close();
						input = null;
						LexRule.FillRuleIds(rules);

						var ccu = new CodeCompileUnit();
						var cns = new CodeNamespace();
						if (!string.IsNullOrEmpty(codenamespace))
							cns.Name = codenamespace;
						ccu.Namespaces.Add(cns);
						var fa = _BuildLexer(rules, ignorecase,inputfile);
						var symbolTable = _BuildSymbolTable(rules);
						var symids = new int[symbolTable.Length];
						for (var i = 0; i < symbolTable.Length; ++i)
							symids[i] = i;
						var blockEnds = _BuildBlockEnds(rules);
						var nodeFlags = _BuildNodeFlags(rules);
						if (null != nfagraph)
						{
							fa.RenderToFile(nfagraph);
						}
						if (!prototype)
						{
							fa = fa.ToDfa(new ConsoleProgress(stderr));
							//fa.TrimDuplicates(new _ConsoleProgress(stderr));
							if (null != dfagraph)
							{
								fa.RenderToFile(dfagraph);
							}
						}
						else
						{
							fa.TrimNeutrals();
						}
						DfaEntry[] dfaTable = null;
						NfaEntry[] nfaTable = null;
						if (!prototype)
						{
							fa = fa.ToDfa(new ConsoleProgress(stderr));
							fa.TrimDuplicates(new ConsoleProgress(stderr));
							dfaTable = _ToDfaStateTable(fa,symids);
						}
							
						else
						{
							nfaTable = _ToNfaStateTable(fa, symids);
						}
						if (!noshared && !prototype)
						{
							// import our Export/Token.cs into the library
							_ImportCompileUnit(Deslanged.Token, cns);

							// import our Export/TableTokenizer.cs into the library
							_ImportCompileUnit(Deslanged.TableTokenizer, cns);

						}
						var origName = "Rolex.";
						CodeTypeDeclaration td = null;

						if (prototype)
						{
							ccu.ReferencedAssemblies.Add(typeof(Program).Assembly.GetName().FullName);
							td = Deslanged.PrototypeTokenizerTemplate.Namespaces[1].Types[0];
							origName += td.Name;
							td.Name = codeclass;
							CodeGenerator.GenerateSymbolConstants(td, symbolTable);
						}
						else
						{
							/*CodeDomVisitor.Visit(Shared.TableTokenizerTemplate, (ctx) => {
								td = ctx.Target as CodeTypeDeclaration;
								if (null != td)
								{
									System.Diagnostics.Debugger.Break();
									if (td.Name.EndsWith("Template"))
									{
										origName += td.Name;
										td.Name = name;
										var f = CodeDomUtility.GetByName("DfaTable", td.Members) as CodeMemberField;
										f.InitExpression = CodeGenerator.GenerateDfaTableInitializer(dfaTable);
										f = CodeDomUtility.GetByName("NodeFlags", td.Members) as CodeMemberField;
										f.InitExpression = CodeDomUtility.Literal(nodeFlags);
										f = CodeDomUtility.GetByName("BlockEnds", td.Members) as CodeMemberField;
										f.InitExpression = CodeDomUtility.Literal(blockEnds);
										CodeGenerator.GenerateSymbolConstants(td, symbolTable);
										ctx.Cancel = true;
									}
								}
							});*/
							if (null == td) // for some reason the above fails in devstudio DTE *sometimes* so do this
							{
								td = Deslanged.TableTokenizerTemplate.Namespaces[1].Types[0];
								origName += td.Name;
								td.Name = codeclass;
								CodeGenerator.GenerateSymbolConstants(td, symbolTable);
							}
						}
						CodeDomVisitor.Visit(td, (ctx) =>
						{
							var tr = ctx.Target as CodeTypeReference;
							if (null != tr)
							{
								if (0 == string.Compare(origName, tr.BaseType, StringComparison.InvariantCulture))
									tr.BaseType = codeclass;
							}

						});
						CodeMemberField f = null;
						if (prototype)
						{
							f = CodeDomUtility.GetByName("NfaTable", td.Members) as CodeMemberField;
							f.InitExpression = CodeGenerator.GenerateNfaTableInitializer(nfaTable);
						}
						else
						{
							f = CodeDomUtility.GetByName("DfaTable", td.Members) as CodeMemberField;
							f.InitExpression = CodeGenerator.GenerateDfaTableInitializer(dfaTable);

						}

						f = CodeDomUtility.GetByName("NodeFlags", td.Members) as CodeMemberField;
						f.InitExpression = CodeDomUtility.Literal(nodeFlags);
						f = CodeDomUtility.GetByName("BlockEnds", td.Members) as CodeMemberField;
						f.InitExpression = CodeDomUtility.Literal(blockEnds);

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
						if (prototype)
							cns.Imports.Add(new CodeNamespaceImport("Rolex"));

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
			catch (Exception ex)
			{
				result = _ReportError(ex, stderr);
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
			CD.CodeDomVisitor.Visit(fromCcu, (ctx) =>
			{
				var ctr = ctx.Target as CodeTypeReference;
				if (null != ctr)
				{
					if (ctr.BaseType.StartsWith("Rolex."))
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
					type.CustomAttributes.Add(CodeGenerator.GeneratedCodeAttribute);
					dst.Types.Add(type);
				}
			}
		}

		// do our error handling here (release builds)
		static int _ReportError(Exception ex, TextWriter stderr)
		{
			//_PrintUsage(stderr);
			stderr.WriteLine("Error: {0}", ex.Message);
			return -1;
		}
		static void _PrintUsage(TextWriter w)
		{
			w.Write("Usage: "+Filename + " ");
			w.WriteLine("<inputFile>");
			w.WriteLine();
			w.WriteLine(Name + " generates a lexer/scanner/tokenizer in the target .NET language");
			w.WriteLine();
			w.WriteLine("	<inputFile>		The input lexer specification");
			w.WriteLine();
		}
		static string _GetCodeBase()
		{
			try
			{
				return Assembly.GetExecutingAssembly().GetModules()[0].FullyQualifiedName;
			}
			catch
			{
				return Path.Combine(Environment.CurrentDirectory,"rolex.exe");
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
			return Path.GetFileNameWithoutExtension(Filename);
		}
		static string[] _BuildSymbolTable(IList<LexRule> rules)
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
		static int[][] _BuildBlockEnds(IList<LexRule> rules)
		{
			int max = int.MinValue;
			for (int ic = rules.Count, i = 0; i < ic; ++i)
			{
				var rule = rules[i];
				if (rule.Id > max)
					max = rule.Id;
			}
			var result = new int[max + 1][];
			for (int ic = rules.Count, i = 0; i < ic; ++i)
			{
				var rule = rules[i];
				var be = rule.GetAttribute("blockEnd") as string;
				if (!string.IsNullOrEmpty(be))
				{
					result[rule.Id] = new List<int>(UnicodeUtility.ToUtf32(be)).ToArray();
				}
			}
			return result;
		}
		static int[] _BuildNodeFlags(IList<LexRule> rules)
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
				var hidden = rule.GetAttribute("hidden");
				if ((hidden is bool) && (bool)hidden)
					result[rule.Id] = 1;
			}
			return result;
		}
		static FA _BuildLexer(IList<LexRule> rules, bool ignoreCase,string inputFile)
		{
			var exprs = new FA[rules.Count];
			var result = new FA();
			for (var i = 0; i < exprs.Length; ++i)
			{
				var rule = rules[i];

				var fa = FA.Parse(rule.Expression.Substring(1, rule.Expression.Length - 2), rule.Id, rule.ExpressionLine, rule.ExpressionColumn, rule.ExpressionPosition, inputFile);
				if (!ignoreCase)
				{
					var ic = (bool)rule.GetAttribute("ignoreCase", false);
					if (ic)
						fa = FA.CaseInsensitive(fa, rule.Id);
				}
				else
				{
					var ic = (bool)rule.GetAttribute("ignoreCase", true);
					if (ic)
						fa = FA.CaseInsensitive(fa, rule.Id);
				}
				result.EpsilonTransitions.Add(fa);
			}
			result.TrimNeutrals();
			if (result.IsNeutral)
			{
				foreach (var efa in result.EpsilonTransitions)
					result = efa;
			}
			return result;
		}
		static DfaEntry[] _ToDfaStateTable(FA dfa,IList<int> symbolTable = null)
		{
			var closure = new List<FA>();
			dfa.FillClosure(closure);
			var symbolLookup = new Dictionary<int, int>();
			// if we don't have a symbol table, build 
			// the symbol lookup from the states.
			if (null == symbolTable)
			{
				// go through each state, looking for accept symbols
				// and then add them to the new symbol table is we
				// haven't already
				var i = 0;
				for (int jc = closure.Count, j = 0; j < jc; ++j)
				{
					var fa = closure[j];
					if (fa.IsAccepting && !symbolLookup.ContainsKey(fa.AcceptSymbol))
					{
						symbolLookup.Add(fa.AcceptSymbol, i);
						++i;
					}
				}
			}
			else // build the symbol lookup from the symbol table
				for (int ic = symbolTable.Count, i = 0; i < ic; ++i)
					symbolLookup.Add(symbolTable[i], i);

			// build the root array
			var result = new DfaEntry[closure.Count];
			for (var i = 0; i < result.Length; i++)
			{
				var fa = closure[i];
				// get all the transition ranges for each destination state
				var trgs = fa.FillInputTransitionRangesGroupedByState();
				// make a new transition entry array for our DFA state table
				var trns = new DfaTransitionEntry[trgs.Count];
				var j = 0;
				// for each transition range
				foreach (var trg in trgs)
				{
					// add the transition entry using
					// the packed ranges from CharRange
					trns[j] = new DfaTransitionEntry(
						trg.Value,
						closure.IndexOf(trg.Key));

					++j;
				}
				// now add the state entry for the state above
				result[i] = new DfaEntry(
					fa.IsAccepting ? symbolLookup[fa.AcceptSymbol] : -1, trns);

			}
			return result;
		}
		static NfaEntry[] _ToNfaStateTable(FA fa, IList<int> symbolTable = null)
		{
			var closure = new List<FA>();
			fa.FillClosure(closure);
			var symbolLookup = new Dictionary<int, int>();
			// if we don't have a symbol table, build 
			// the symbol lookup from the states.
			if (null == symbolTable)
			{
				// go through each state, looking for accept symbols
				// and then add them to the new symbol table is we
				// haven't already
				var i = 0;
				for (int jc = closure.Count, j = 0; j < jc; ++j)
				{
					var ffa = closure[j];
					if (ffa.IsAccepting && !symbolLookup.ContainsKey(ffa.AcceptSymbol))
					{
						symbolLookup.Add(ffa.AcceptSymbol, i);
						++i;
					}
				}
			}
			else // build the symbol lookup from the symbol table
				for (int ic = symbolTable.Count, i = 0; i < ic; ++i)
					symbolLookup.Add(symbolTable[i], i);

			// build the root array
			var result = new NfaEntry[closure.Count];
			for (var i = 0; i < result.Length; i++)
			{
				var ffa = closure[i];
				// get all the transition ranges for each destination state
				var trgs = ffa.FillInputTransitionRangesGroupedByState();
				// make a new transition entry array for our DFA state table
				var trns = new NfaTransitionEntry[trgs.Count];
				var j = 0;
				// for each transition range
				foreach (var trg in trgs)
				{
					// add the transition entry using
					// the packed ranges from CharRange
					trns[j] = new NfaTransitionEntry(
						trg.Value,
						closure.IndexOf(trg.Key));

					++j;
				}
				j = 0;
				var eps = new int[ffa.EpsilonTransitions.Count];
				foreach (var efa in ffa.EpsilonTransitions)
				{
					eps[j] = closure.IndexOf(efa);
					++j;
				}
				// now add the state entry for the state above
				result[i] = new NfaEntry(
				  ffa.IsAccepting ? symbolLookup[ffa.AcceptSymbol] : -1,
					trns,
					eps);

			}
			return result;
		}
	}

	class ConsoleProgress : IProgress<FAProgress>
	{
		TextWriter _stderr;
		public ConsoleProgress(TextWriter stderr)
		{
			_stderr = stderr;
		}
		public void Report(FAProgress progress)
		{
			_stderr.Write(".");
			_stderr.Flush();
		}
	}

}
