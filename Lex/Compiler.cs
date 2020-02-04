using System;
using System.Collections.Generic;
using System.Text;
using F;
namespace L
{
	static class Compiler
	{
		#region Opcodes
		internal const int Match = 1; // match symbol
		internal const int Jmp = 2; // jmp addr1, addr2
		internal const int Switch = 3; // switch [ case <ranges>:<label> { , case <ranges>:<label> }] [, default: <label> {, <label> } ]
		internal const int Any = 4; // any
		internal const int Char = 5; // char ch
		internal const int Set = 6; // set packedRange1Left,packedRange1Right,packedRange2Left,packedRange2Right...
		internal const int NSet = 7; // nset packedRange1Left,packedRange1Right,packedRange2Left,packedRange2Right...
		internal const int UCode = 8; // ucode cat
		internal const int NUCode = 9; // nucode cat
		internal const int Save = 10; // save slot
		#endregion
		internal static List<int[]> Emit(Ast ast,int symbolId = -1)
		{
			var prog = new List<int[]>();
			EmitAstInnerPart(ast,prog);
			if (-1 != symbolId)
			{
				var match = new int[2];
				match[0] = Match;
				match[1] = symbolId;
				prog.Add(match);
			}
			return prog;
		}
		internal static void EmitPart(string literal, IList<int[]> prog)
		{
			for (var i = 0; i < literal.Length; ++i)
			{
				int ch = literal[i];
				if (char.IsHighSurrogate(literal[i]))
				{
					if (i == literal.Length - 1)
						throw new ArgumentException("The literal contains an incomplete unicode surrogate.", nameof(literal));
					ch = char.ConvertToUtf32(literal, i);
					++i;
				}
				var lit = new int[2];
				lit[0] = Char;
				lit[1] = ch;
				prog.Add(lit);
			}
		}
		internal static void EmitAstPart(Ast ast,IList<int[]> prog,int symbolId=0)
		{
			EmitAstInnerPart(ast, prog);
			var save = new int[] { Save, 1 };
			prog.Add(save);
			var match = new int[2];
			match[0] = Match;
			match[1] = symbolId;
			prog.Add(match);
		}
		internal static void EmitAstInnerPart(Ast ast, IList<int[]> prog)
		{
			
			int[] inst,jmp;
			switch(ast.Kind)
			{
				case Ast.Lit: // literal value
					// char <ast.Value>
					inst = new int[2];
					inst[0] = Char;
					inst[1] = ast.Value;
					prog.Add(inst);
					break;
				case Ast.Cat: // concatenation
					for(var i = 0;i<ast.Exprs.Length;i++)
						if(null!=ast.Exprs[i])
							EmitAstInnerPart(ast.Exprs[i],prog);
					break;
				case Ast.Dot: // dot/any
					inst = new int[1];
					inst[0] = Any;
					prog.Add(inst);
					break;
				case Ast.Alt: // alternation
					
					// be sure to handle the cases where one
					// of the children is null such as
					// in (foo|) or (|foo)
					var exprs = new List<Ast>(ast.Exprs.Length);
					var firstNull = -1;
					for (var i = 0; i < ast.Exprs.Length; i++)
					{
						var e = ast.Exprs[i];
						if (null == e)
						{
							if (0 > firstNull)
							{
								firstNull = i;
								exprs.Add(null);
							}
							continue;
						}
						exprs.Add(e);
					}
					ast.Exprs = exprs.ToArray();
					var jjmp = new int[ast.Exprs.Length + 1];
					jjmp[0] = Jmp;
					prog.Add(jjmp);
					var jmpfixes = new List<int>(ast.Exprs.Length - 1);
					for (var i = 0; i < ast.Exprs.Length; ++i)
					{
						var e = ast.Exprs[i];
						if (null != e)
						{
							jjmp[i + 1] = prog.Count;
							EmitAstInnerPart(e, prog);
							if (i == ast.Exprs.Length - 1)
								continue;
							if (i == ast.Exprs.Length - 2 && null == ast.Exprs[i + 1])
								continue;
							var j = new int[2];
							j[0] = Jmp;
							jmpfixes.Add(prog.Count);
							prog.Add(j);
						}
					}
					for (int ic = jmpfixes.Count, i = 0; i < ic; ++i)
					{
						var j = prog[jmpfixes[i]];
						j[1] = prog.Count;
					}
					if (-1 < firstNull)
					{
						jjmp[firstNull + 1] = prog.Count;
					}
					break;
					
				case Ast.NSet:
				case Ast.Set:
					// generate a set or nset instruction
					// with all the packed ranges
					// which we first sort to ensure they're 
					// all arranged from low to high
					// (n)set packedRange1Left, packedRange1Right, packedRange2Left, packedRange2Right...
					inst = new int[ast.Ranges.Length + 1];
					inst[0] = (ast.Kind==Ast.Set)?Set:NSet;
					SortRanges(ast.Ranges);
					Array.Copy(ast.Ranges, 0, inst, 1, ast.Ranges.Length);
					prog.Add(inst);
					break;
				case Ast.NUCode:
				case Ast.UCode:
					// generate a ucode or ncode instruction
					// with the given unicode category value
					// (n)ucode <ast.Value>
					inst = new int[2];
					inst[0] = (ast.Kind == Ast.UCode) ? UCode : NUCode;
					inst[1] = ast.Value;
					prog.Add(inst);
					break;
				case Ast.Opt:
					inst = new int[3];
					// we have to choose betweed Left or empty
					// jmp <pc>, <<next>>
					inst[0] = Jmp;
					prog.Add(inst);
					inst[1] = prog.Count;
					// emit 
					for (var i = 0; i < ast.Exprs.Length; i++)
						if (null != ast.Exprs[i])
							EmitAstInnerPart(ast.Exprs[i], prog);
					inst[2] = prog.Count;
					if (ast.IsLazy)
					{
						// non-greedy, swap jmp
						var t = inst[1];
						inst[1] = inst[2];
						inst[2] = t;
					}

					break;
				// the next two forward to Rep
				case Ast.Star:
					ast.Min = 0;
					ast.Max = 0;
					goto case Ast.Rep;
				case Ast.Plus:
					ast.Min = 1;
					ast.Max = 0;
					goto case Ast.Rep;
				case Ast.Rep:
					// TODO: There's an optimization opportunity
					// here wherein we can make the rep instruction
					// take min and max values, or make a condition
					// branch instruction take a loop count. We don't
					//
					// we need to generate a series of matches
					// based on the min and max values
					// this gets complicated
					if (ast.Min > 0 && ast.Max > 0 && ast.Min > ast.Max)
						throw new ArgumentOutOfRangeException("Max");
					
					int idx2;
					Ast opt;
					Ast rep;
					
					switch (ast.Min)
					{
						case -1:
						case 0:
							switch (ast.Max)
							{
								case -1: // kleene * ex: (foo)*
								case 0:
									var idx = prog.Count;
									inst = new int[3];
									inst[0] = Jmp;
									prog.Add(inst);
									idx2 = prog.Count;

									for (var i = 0; i < ast.Exprs.Length; i++)
										if (null != ast.Exprs[i])
											EmitAstInnerPart(ast.Exprs[i], prog);
									inst[1] = idx2;

									jmp = new int[2];
									jmp[0] = Jmp;
									jmp[1] = idx;
									prog.Add(jmp);
									inst[2] = prog.Count;
									if (ast.IsLazy)
									{   // non-greedy - swap jmp
										var t = inst[1];
										inst[1] = inst[2];
										inst[2] = t;
									}
									return;
								case 1: // opt ex: (foo)?
									opt = new Ast();
									opt.Kind = Ast.Opt;
									opt.Exprs = ast.Exprs;
									opt.IsLazy = ast.IsLazy;
									EmitAstInnerPart(opt,prog);
									return;
								default: // ex: (foo){,10}
									opt = new Ast();
									opt.Kind = Ast.Opt;
									opt.Exprs = ast.Exprs;
									opt.IsLazy = ast.IsLazy;
									EmitAstInnerPart(opt, prog);
									for (var i = 1; i < ast.Max; ++i)
									{
										EmitAstInnerPart(opt,prog);
									}
									return;
							}
						case 1:
							switch (ast.Max)
							{
								// plus ex: (foo)+
								case -1:
								case 0:
									idx2 = prog.Count;
									for (var i = 0; i < ast.Exprs.Length; i++)
										if (null != ast.Exprs[i])
											EmitAstInnerPart(ast.Exprs[i], prog);
									inst = new int[3];
									inst[0] = Jmp;
									prog.Add(inst);
									inst[1] = idx2;
									inst[2] = prog.Count;
									if (ast.IsLazy)
									{
										// non-greedy, swap jmp
										var t = inst[1];
										inst[1] = inst[2];
										inst[2] = t;
									}
									return;
								case 1:
									// no repeat ex: (foo)
									for (var i = 0; i < ast.Exprs.Length; i++)
										if (null != ast.Exprs[i])
											EmitAstInnerPart(ast.Exprs[i], prog);
									return;
								default:
									// repeat ex: (foo){1,10}
									rep = new Ast();
									rep.Min = 0;
									rep.Max = ast.Max -1;
									rep.IsLazy = ast.IsLazy;
									rep.Exprs = ast.Exprs;
									for (var i = 0; i < ast.Exprs.Length; i++)
										if (null != ast.Exprs[i])
											EmitAstInnerPart(ast.Exprs[i], prog);
									EmitAstInnerPart(rep, prog);
									return;
							}
						default: // bounded minum
							switch (ast.Max)
							{
								// repeat ex: (foo) {10,}
								case -1:
								case 0:
									for (var j = 0; j < ast.Min; ++j)
									{
										for (var i = 0; i < ast.Exprs.Length; i++)
											if (null != ast.Exprs[i])
												EmitAstInnerPart(ast.Exprs[i], prog);
									}
									rep = new Ast();
									rep.Kind = Ast.Star;
									rep.Exprs = ast.Exprs;
									rep.IsLazy = ast.IsLazy;
									EmitAstInnerPart(rep,prog);
									return;
								case 1: // invalid or handled prior
									// should never get here
									throw new NotImplementedException();
								default: // repeat ex: (foo){10,12}
									for (var j = 0; j < ast.Min; ++j)
									{
										for (var i = 0; i < ast.Exprs.Length; i++)
											if (null != ast.Exprs[i])
												EmitAstInnerPart(ast.Exprs[i], prog);
									}
									if (ast.Min== ast.Max)
										return;
									opt = new Ast();
									opt.Kind = Ast.Opt;
									opt.Exprs = ast.Exprs;
									opt.IsLazy = ast.IsLazy;
									rep = new Ast();
									rep.Kind = Ast.Rep;
									rep.Min = rep.Max = ast.Max - ast.Min;
									EmitAstInnerPart(rep, prog);
									return;

							}
					}
					// should never get here
					throw new NotImplementedException();

			}
		}

		internal static void EmitFAPart(FA fa,IList<int[]> prog)
		{
			//fa = fa.ToDfa();
			//fa.TrimDuplicates();
			//fa = fa.ToGnfa();
			if (fa.IsNeutral)
			{
				foreach (var efa in fa.EpsilonTransitions)
				{
					fa = efa;
				}
			}
			var acc = fa.FillAcceptingStates(); 
			foreach(var afa in acc)
			{
				if (!afa.IsFinal)
				{
					var ffa = new FA(true, afa.AcceptSymbol);
					afa.EpsilonTransitions.Add(ffa);
					afa.IsAccepting = false;
				}
			}
			var rendered = new Dictionary<FA, int>();
			var swFixups = new Dictionary<FA, int>();
			var jmpFixups = new Dictionary<FA, int>();
			var l = new List<FA>();
			fa.FillClosure(l);
			for(int ic=l.Count,i=0;i<ic;++i)
			{
				var reused = false;
				var cfa = l[i];	
				if(!cfa.IsFinal)
				{
					rendered.Add(cfa, prog.Count);
				} else
				{
					
					foreach(var r in rendered)
					{
						if(r.Key.IsFinal)
						{
							if (r.Key.IsAccepting && cfa.AcceptSymbol == r.Key.AcceptSymbol)
							{


								// we can reuse this 
								rendered.Add(cfa, r.Value);
								reused = true;
								break;
							}
						}
					}
					if (!reused)
						rendered.Add(cfa, prog.Count);
				}
				
				if (!cfa.IsFinal)
				{
					int swfixup = prog.Count;
					prog.Add(null); // switch
					swFixups.Add(cfa, swfixup);
				} else
				{
#if DEBUG
					System.Diagnostics.Debug.Assert(cfa.IsAccepting);
#endif
					if (!reused)
					{
						prog.Add(new int[] { Save, 1 }); // save
						prog.Add(new int[] { Match, cfa.AcceptSymbol });
					}
				}
			}
			for(int ic=l.Count,i=0;i<ic;++i)
			{
				var cfa =  l[i];
				
				if (!cfa.IsFinal)
				{
					
					var sw = new List<int>();
					sw.Add(Switch);
					
					var rngGrps = cfa.FillInputTransitionRangesGroupedByState();
					foreach (var grp in rngGrps)
					{
						var dst = rendered[grp.Key];
						sw.AddRange(grp.Value);
						sw.Add(-1);
						sw.Add(dst);
					}
					if (1 < sw.Count)
					{
						if (0 < cfa.EpsilonTransitions.Count)
						{
							sw.Add(-2);
							foreach (var efa in cfa.EpsilonTransitions)
							{
								var dst = rendered[efa];
								sw.Add(dst);
							}
						}
					} else
					{
						// basically a NOP. Will get removed
						sw[0] = Jmp;
						sw.Add(swFixups[cfa] + 1);
					}
					prog[swFixups[cfa]] = sw.ToArray();
				}
				
				var jfi = -1;
				if (jmpFixups.TryGetValue(cfa, out jfi))
				{
					var jmp = new int[2];
					jmp[0] = Jmp;
					jmp[1] = prog.Count;
					prog[jfi] = jmp;
				}
				
			}
			
		}
		static void EmitAstInnerPart(FA fa,IDictionary<FA,int> rendered, IList<int[]> prog)
		{
			if (fa.IsFinal)
				return;
			int swfixup = prog.Count;
			var sw = new List<int>();
			sw.Add(Switch);
			prog.Add(null);
			foreach (var trns in fa.InputTransitions)
			{
				var dst = -1;
				if(!rendered.TryGetValue(trns.Value,out dst))
				{
					dst = prog.Count;
					rendered.Add(trns.Value, dst);
					EmitAstInnerPart(trns.Value, rendered, prog);

				}
				sw.Add(trns.Key.Key);
				sw.Add(trns.Key.Value);
				sw.Add(-1);
				sw.Add(dst);
			}
			if(0<fa.InputTransitions.Count && 0<fa.EpsilonTransitions.Count)
				sw.Add(-2);
			else if (0==fa.InputTransitions.Count)
				sw[0] = Jmp;
			foreach(var efa in fa.EpsilonTransitions)
			{
				int dst;
				if (!rendered.TryGetValue(efa, out dst))
				{
					dst = prog.Count;
					rendered.Add(efa, dst);
					EmitAstInnerPart(efa, rendered, prog);
				}
				sw.Add(dst);
			}
			prog[swfixup] = sw.ToArray();
		}
		static string _FmtLbl(int i)
		{
			return string.Format("L{0,4:000#}", i);
		}
		public static string ToString(IEnumerable<int[]> prog)
		{
			var sb = new StringBuilder();
			var i = 0;
			foreach(var inst in prog)
			{
				sb.Append(_FmtLbl(i));
				sb.Append(": ");
				sb.AppendLine(ToString(inst));
				++i;
			}
			return sb.ToString();
		}
		static string _ToStr(int ch)
		{
			return string.Concat('\"', _EscChar(ch), '\"');
		}
		static string _EscChar(int ch)
		{
			switch (ch)
			{
				case '.':
				case '/': // js expects this
				case '(':
				case ')':
				case '[':
				case ']':
				case '<': // flex expects this
				case '>':
				case '|':
				case ';': // flex expects this
				case '\'': // pck expects this
				case '\"':
				case '{':
				case '}':
				case '?':
				case '*':
				case '+':
				case '$':
				case '^':
				case '\\':
					return "\\"+char.ConvertFromUtf32(ch);
				case '\t':
					return "\\t";
				case '\n':
					return "\\n";
				case '\r':
					return "\\r";
				case '\0':
					return "\\0";
				case '\f':
					return "\\f";
				case '\v':
					return "\\v";
				case '\b':
					return "\\b";
				default:
					var s = char.ConvertFromUtf32(ch);
					if (!char.IsLetterOrDigit(s,0) && !char.IsSeparator(s,0) && !char.IsPunctuation(s,0) && !char.IsSymbol(s,0))
					{
						if (1 == s.Length)
							return string.Concat(@"\u", unchecked((ushort)ch).ToString("x4"));
						else
							return string.Concat(@"\U" + ch.ToString("x8"));
						

					}
					else
						return s;
			}
		}
		static int _AppendRanges(StringBuilder sb, int[] inst,int index)
		{
			var i = index;
			for (i = index; i < inst.Length - 1; i++)
			{
				if (-1 == inst[i])
					return i;
				if (index != i)
					sb.Append(", ");
				if (inst[i] == inst[i + 1])
					sb.Append(_ToStr(inst[i]));
				else
				{
					sb.Append(_ToStr(inst[i]));
					sb.Append("..");
					sb.Append(_ToStr(inst[i + 1]));
				}

				++i;
			}
			return i;
		}
		public static string ToString(int[] inst)
		{
			switch (inst[0])
			{
				case Jmp:
					var sb = new StringBuilder();
					sb.Append("jmp ");
					sb.Append(_FmtLbl(inst[1]));
					for (var i = 2; i < inst.Length; i++)
						sb.Append(", " + _FmtLbl(inst[i]));
					return sb.ToString();
				case Switch:
					sb = new StringBuilder();
					sb.Append("switch ");
					var j = 1;
					for(;j<inst.Length;)
					{
						if (-2 == inst[j])
							break;
						if (j != 1)
							sb.Append(", ");
						sb.Append("case ");
						j = _AppendRanges(sb, inst, j);
						++j;
						sb.Append(":");
						sb.Append(_FmtLbl(inst[j]));
						++j;
					}
					if(j<inst.Length && -2==inst[j])
					{
						sb.Append(", default:");
						var delim = "";
						for(++j;j<inst.Length;j++)
						{
							sb.Append(delim);
							sb.Append(_FmtLbl(inst[j]));
							delim = ", ";
						}
					}
					return sb.ToString();
				case Char:
					if (2==inst.Length)// for testing
						return "char " + _ToStr(inst[1]);
					else return "char";
				case UCode:
				case NUCode:
					return (UCode == inst[0] ? "ucode " : "nucode ") + inst[1];
				case Set:
				case NSet:
					sb = new StringBuilder();
					if (Set == inst[0])
						sb.Append("set ");
					else
						sb.Append("nset ");
					for(var i = 1;i<inst.Length-1;i++)
					{
						if (1 != i)
							sb.Append(", ");
						if (inst[i] == inst[i + 1])
							sb.Append(_ToStr(inst[i]));
						else
						{
							sb.Append(_ToStr(inst[i]));
							sb.Append("..");
							sb.Append(_ToStr(inst[i+1]));
						}
							
						++i;
					}
					return sb.ToString();
				case Any:
					return "any";
				case Match:
					return "match " + inst[1].ToString();
				case Save:
					return "save " + inst[1].ToString();
				default:
					throw new InvalidProgramException("The instruction is not valid");
			}
		}
		// hate taking object but have no choice
		// expressions can be KeyValuePair<int,object> where object is Ast or string or int[][] and int is the symbol id
		internal static int[][] EmitLexer(bool optimize, params KeyValuePair<int,object>[] parts)
		{
			var fragments = new List<int[][]>(parts.Length);
			var ordered = new List<object>(); // i wish C# had proper unions
			var i = 0;
			if (optimize)
			{
				var workingFA = new List<FA>();
				while (i < parts.Length)
				{
					while (i < parts.Length)
					{
						var ast = parts[i].Value as Ast;
						if(null==ast)
						{
							var str = parts[i].Value as string;
							if (null != str)
								ast = Ast.FromLiteral(str);
						}
						if (null != ast)
						{
							FA fa = null;
							try
							{
								fa = ast.ToFA(parts[i].Key);
							}
							// we can't do lazy expressions
							catch (NotSupportedException) { }
							if (null == fa)
							{
								if (0 < workingFA.Count)
								{
									ordered.Add(workingFA);
									workingFA = new List<FA>();
								}
								break;
							}
							workingFA.Add(fa);
						}
						else break;
						++i;
					}
					if (i == parts.Length)
					{
						if (0 < workingFA.Count)
						{
							ordered.Add(workingFA);
							workingFA = new List<FA>();
						}
					}
					while (i < parts.Length)
					{
						var ast = parts[i].Value as Ast;
						if (null == ast)
						{
							var str = parts[i].Value as string;
							if (null != str)
								ast = Ast.FromLiteral(str);
						}
						if (null != ast)
						{
							FA fa;
							try
							{
								fa = ast.ToFA(parts[i].Key);
								workingFA.Add(fa);
								++i;
								if (i == parts.Length)
									ordered.Add(workingFA);
								break;
							}
							catch { }
						}
						ordered.Add(parts[i]);
						++i;
					}
				}
				i = 0;
				for (int ic = ordered.Count; i < ic; ++i)
				{
					var l = ordered[i] as List<FA>;
					if (null != l)
					{
						var root = new FA();
						for (int jc = l.Count, j = 0; j < jc; ++j)
						{
							root.EpsilonTransitions.Add(l[j]);
						}
						root = root.ToDfa();
						root.TrimDuplicates();
						ordered[i] = root;
					} 
				}
			} else
			{
				for(i = 0;i<parts.Length;++i)
				{
					ordered.Add(parts[i]);
				}
			}
			i = 0;
			for(var ic = ordered.Count;i<ic;++i)
			{
				var l = new List<int[]>();
				var fa = ordered[i] as FA;
				if (null != fa)
				{
					EmitFAPart(fa, l);
				}
				else
				{
					
					
					if (ordered[i] is KeyValuePair<int,object>)
					{
						var kvp = (KeyValuePair<int,object>)ordered[i];
						var ast = kvp.Value as Ast;
						if(null!=ast)
						{
							EmitAstInnerPart(ast, l);
							var save = new int[] { Save, 1 };
							l.Add(save);
							var match = new int[2];
							match[0] = Match;
							match[1] = kvp.Key;
							l.Add(match);
						}
						else
						{
							var frag = kvp.Value as int[][];
							Fixup(frag, l.Count);
							l.AddRange(frag);
							// TODO: add check for linker attribute (somehow?)
							var save = new int[] { Save, 1 };
							l.Add(save);
							var match = new int[2];
							match[0] = Match;
							match[1] = kvp.Key;
							l.Add(match);
						}
						
					} 
				}
				fragments.Add(l.ToArray());
			}
			var result =  _EmitLexer(fragments);
			if(optimize)
			{
				// remove dead code
				var code = new List<int[]>(RemoveDeadCode(result));
				var pc = code[1];
				// remove initial jmp for error handling, if we can, replacing it with the switch's default
				// from the next line
				if(3==pc.Length && Jmp==pc[0])
				{
					if(2==pc[1] && result.Length-3==pc[2] && Switch == code[2][0])
					{
						pc = code[2];
						var idef = Array.IndexOf(pc, -2);
						if(0>idef)
						{
							var nsw = new int[pc.Length + 2];
							Array.Copy(pc, 0, nsw, 0, pc.Length);
							nsw[nsw.Length - 2] = -2;
							nsw[nsw.Length - 1] = result.Length-3;
							code[2] = nsw;
							code.RemoveAt(1);
							result = code.ToArray();
							Fixup(result, -1);
							
						}
					}
				}
			}
			return result;
		}

		internal static IList<int[]> RemoveDeadCode(IList<int[]> prog)
		{
			
			var done = false;
			while(!done)
			{
				done = true;
				var toRemove = -1;
				for(var i = 0;i<prog.Count;++i)
				{
					var pc = prog[i];
					// remove L0001: jmp L0002
					if(Jmp==pc[0] && i+1==pc[1] && 2==pc.Length)
					{
						toRemove = i;
						break;
					}
				}
				if(-1!=toRemove)
				{
					done = false;
					var newProg = new List<int[]>(prog.Count-1);
					for(var i = 0;i<toRemove;++i)
					{
						var inst = prog[i];
						switch(inst[0])
						{
							case Switch:
								var inDef = false;
								for (var j = 0; j < inst.Length; j++)
								{
									if (inDef)
									{
										if(inst[j]>toRemove)
											--inst[j];
									}
									else
									{
										if (-1 == inst[j])
										{
											++j;
											if (inst[j] > toRemove)
												--inst[j];
										}
										else if (-2 == inst[j])
											inDef = true;
									}
								}
								break;
							case Jmp:
								for (var j = 1; j < inst.Length; j++)
									if (inst[j] > toRemove)
										--inst[j];
								break;
						}
						newProg.Add(prog[i]);
					}
					var progNext = new List<int[]>(prog.Count - toRemove - 1);
					for(var i = toRemove+1;i<prog.Count;i++)
					{
						progNext.Add(prog[i]);
					}
					var pna = progNext.ToArray();
					Fixup(pna, -1);
					newProg.AddRange(pna);
					prog = newProg.ToArray();
				}
			}
			return prog;
		}
		internal static int[][] EmitLexer(IEnumerable<KeyValuePair<int,int[][]>> parts)
		{
			var l = new List<KeyValuePair<int, int[][]>>(parts);
			var prog = new List<int[]>();
			int[] match, save;
			// save 0
			save = new int[2];
			save[0] = Save;
			save[1] = 0;
			prog.Add(save);

			// generate the primary jmp instruction
			var jmp = new int[l.Count+ 2];
			jmp[0] = Compiler.Jmp;
			prog.Add(jmp);
			// for each expressions, render a save 0
			// followed by the the instructions
			// followed by save 1, and then match <i>
			for (int ic=l.Count,i = 0; i < ic; ++i)
			{
				jmp[i + 1] = prog.Count;
				
				// expr
				Fixup(l[i].Value, prog.Count);
				prog.AddRange(l[i].Value);
				// save 1
				save = new int[2];
				save[0] = Save;
				save[1] = 1;
				prog.Add(save);
				// match <l[i].Key>
				match = new int[2];
				match[0] = Match;
				match[1] = l[i].Key;
				prog.Add(match);
			}
			// generate the error condition
			// handling
			jmp[jmp.Length - 1] = prog.Count;
			// any
			var any = new int[1];
			any[0] = Any;
			prog.Add(any);
			// save 1
			save = new int[2];
			save[0] =Save;
			save[1] = 1;
			prog.Add(save);
			// match -1
			match = new int[2];
			match[0] = Match;
			match[1] = -1;
			prog.Add(match);
			
			return prog.ToArray();
		}
		internal static int[][] _EmitLexer(IEnumerable<int[][]> frags)
		{
			var l = new List<int[][]>(frags);
			var prog = new List<int[]>();
			int[] match, save;
			// save 0
			save = new int[2];
			save[0] = Save;
			save[1] = 0;
			prog.Add(save);

			// generate the primary jmp instruction
			var jmp = new int[l.Count + 2];
			jmp[0] = Compiler.Jmp;
			prog.Add(jmp);
			// for each expressions, render a save 0
			// followed by the the instructions
			// followed by save 1, and then match <i>
			for (int ic = l.Count, i = 0; i < ic; ++i)
			{
				jmp[i + 1] = prog.Count;

				// expr
				Fixup(l[i], prog.Count);
				prog.AddRange(l[i]);
			}
			// generate the error condition
			// handling
			jmp[jmp.Length - 1] = prog.Count;
			// any
			var any = new int[1];
			any[0] = Any;
			prog.Add(any);
			// save 1
			save = new int[2];
			save[0] = Save;
			save[1] = 1;
			prog.Add(save);
			// match -1
			match = new int[2];
			match[0] = Match;
			match[1] = -1;
			prog.Add(match);

			return prog.ToArray();
		}
		internal static void SortRanges(int[] ranges)
		{
			var result = new List<KeyValuePair<int, int>>(ranges.Length / 2);
			for (var i = 0; i < ranges.Length - 1; ++i)
			{
				var ch = ranges[i];
				++i;
				result.Add(new KeyValuePair<int, int>(ch, ranges[i]));
			}
			result.Sort((x, y) => { return x.Key.CompareTo(y.Key); });
			for (int ic = result.Count, i = 0; i < ic; ++i)
			{
				var j = i * 2;
				var kvp = result[i];
				ranges[j] = kvp.Key;
				ranges[j + 1] = kvp.Value;
			}
		}
		static int[] _GetFirsts(int[][] part,int index)
		{
			if (part.Length <= index)
				return new int[0];
			int idx;
			List<int> resl;
			int[] result;
			var pc = part[index];
			switch(pc[0])
			{
				case Char:
					return new int[] { pc[1], pc[1] };
				case Set:
					result = new int[pc.Length - 1];
					Array.Copy(pc, 1, result, 0, result.Length);
					return result;
				case NSet:
					result = new int[pc.Length - 1];
					Array.Copy(pc, 1, result, 0, result.Length);
					return RangeUtility.FromPairs(new List<KeyValuePair<int,int>>(RangeUtility.NotRanges(RangeUtility.ToPairs(result))));
				case Any:
					return new int[] { 0, 0x10ffff };
				case UCode:
					result = CharacterClasses.UnicodeCategories[pc[1]];
					return result;
				case NUCode:
					result = CharacterClasses.UnicodeCategories[pc[1]];
					Array.Copy(pc, 1, result, 0, result.Length);
					return RangeUtility.FromPairs(new List<KeyValuePair<int, int>>(RangeUtility.NotRanges(RangeUtility.ToPairs(result))));
				case Switch:
					resl = new List<int>();
					idx = 1;
					while(pc.Length>idx && -2!=pc[idx])
					{
						if (-1 == pc[idx])
						{
							idx += 2;
							continue;
						}
						resl.Add(pc[idx]);
					}
					if(pc.Length>idx && -2==pc[idx])
					{
						++idx;
						while (pc.Length > idx)
						{
							resl.AddRange(_GetFirsts(part, pc[idx]));
							++idx;
						}
					}
					return resl.ToArray();
				case Jmp:
					resl = new List<int>();
					idx = 1;
					while (pc.Length > idx)
					{
						resl.AddRange(_GetFirsts(part, pc[idx]));
						++idx;
					}
					return resl.ToArray();
				case Match:
					return new int[0];
				case Save:
					return _GetFirsts(part, index + 1);
			}
			// should never get here
			throw new NotImplementedException();
		}
		internal static void Fixup(int[][] program, int offset)
		{
			for(var i = 0;i<program.Length;i++)
			{
				var inst = program[i];
				var op = inst[0];
				switch(op)
				{
					case Switch:
						var inDef = false;
						for(var j = 0;j<inst.Length;j++)
						{
							if (inDef)
							{
								inst[j] += offset;
							}
							else
							{
								if (-1 == inst[j])
								{
									++j;
									inst[j] += offset;
								}
								else if (-2 == inst[j])
									inDef = true;
							}
						}
					break;
					case Jmp:
						for (var j = 1; j < inst.Length; j++)
							inst[j] += offset;
						break;
				}
			}
		}
	}
}
