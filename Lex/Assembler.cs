using LC;
using System;
using System.Collections.Generic;

namespace L
{
	static class Assembler
	{
		internal static List<Inst> Parse(LexContext l)
		{
			var result = new List<Inst>();
			while(-1!=l.Current && '}'!=l.Current)
			{
				result.Add(Inst.Parse(l));
				
			}
			return result;
		}
		internal static List<int[]> Emit(IList<Inst> instructions)
		{
			var ic = instructions.Count;
			var result = new List<int[]>(ic);
			var lmap = new Dictionary<string, int>();
			var pc = 0;
			var regm = new Dictionary<Inst, int[][]>();
			for(var i = 0;i<ic;++i)
			{
				var inst = instructions[i];
				if (inst.Opcode == Inst.Label)
				{
					if (lmap.ContainsKey(inst.Name))
						throw new InvalidProgramException("Duplicate label " + inst.Name + " found at line " + inst.Line.ToString());
					lmap.Add(inst.Name, pc);
				} else if(inst.Opcode==Inst.Regex)
				{
					var reg = new List<int[]>();
					Compiler.EmitPart(inst.Expr, reg);
					regm.Add(inst, reg.ToArray());
					pc += reg.Count;
				}
				else 
					++pc;
			}
			pc = 0;
			for(var i = 0;i<ic;++i)
			{
				int dst;
				var inst = instructions[i];
				int[] code = null;
				switch(inst.Opcode)
				{
					case Inst.Regex:
						Compiler.Fixup(regm[inst], pc);
						result.AddRange(regm[inst]);
						break;
					case Inst.Label:
						break;
					case Inst.Switch:
						var sw = new List<int>();
						if(0==inst.Cases.Length)
						{
							if (0 == inst.Labels.Length)
								break;
							sw.Add(Inst.Jmp);
						} else
							sw.Add(inst.Opcode);
						for(var k=0;k<inst.Cases.Length;k++)
						{
							var c = inst.Cases[k];
							sw.AddRange(c.Key);
							sw.Add(-1);
							var lbl = c.Value;
							if (!lmap.TryGetValue(lbl, out dst))
								throw new InvalidProgramException("Switch references undefined label " + inst.Name + " at line " + inst.Line.ToString());
							sw.Add(dst);
						}
						if (0 < inst.Cases.Length && (null!=inst.Labels && 0<inst.Labels.Length))
							sw.Add(-2);
						if (null != inst.Labels)
						{
							for (var j = 0; j < inst.Labels.Length; j++)
							{
								var lbl = inst.Labels[j];
								if (!lmap.TryGetValue(lbl, out dst))
									throw new InvalidProgramException("Switch references undefined label " + lbl + " at line " + inst.Line.ToString());
								sw.Add(dst);
							}
						}
						code = sw.ToArray();
						break;
					case Inst.Any:
						code = new int[1];
						code[0] = inst.Opcode;
						break ;
					case Inst.Char:
					case Inst.UCode:
					case Inst.NUCode:
					case Inst.Save:
					case Inst.Match:
						code = new int[2];
						code[0] = inst.Opcode;
						code[1] = inst.Value;
						break;
					case Inst.Set:
					case Inst.NSet:
						var set = new List<int>(inst.Ranges.Length+1);
						set.Add(inst.Opcode);
						Compiler.SortRanges(inst.Ranges);
						set.AddRange(inst.Ranges);
						code = set.ToArray();
						break;
					/*case Inst.Jmp:
						code = new int[2];
						code[0] = inst.Opcode;
						if (!lmap.TryGetValue(inst.Name, out dst))
							throw new InvalidProgramException("Jmp references undefined label " + inst.Name + " at line " + inst.Line.ToString());
						code[1] = dst;
						break;*/
					case Inst.Jmp:
						var jmp = new List<int>(inst.Labels.Length + 1);
						jmp.Add(inst.Opcode);
						for(var j = 0;j<inst.Labels.Length;j++)
						{
							var lbl = inst.Labels[j];
							if (!lmap.TryGetValue(lbl, out dst))
								throw new InvalidProgramException("Jmp references undefined label " + inst.Name + " at line " + inst.Line.ToString());
							jmp.Add(dst);
						}
						code = jmp.ToArray();
						break;
				}
				if(null!=code)
				{
					result.Add(code);
				}
				pc = result.Count;
			}
			return result;
		}
		
	}
	class Inst
	{
		#region Opcodes
		internal const int Regex = -2; // regex (expression) - a macro for a regular expression - will be replaced by the machine code represented by the the expression
		internal const int Label = -1; // label: (not a "real" instruction, just a marker we use to fill in addresses after the parse)
		internal const int Match = 1; // match symbol
		internal const int Jmp = 2; // jmp addr1 { , addrN }
		internal const int Switch = 3; // switch case <ranges>:label1 case <ranges>:label2 default:label3 {, labelN }
		internal const int Any = 4; // any
		internal const int Char = 5; // char ch
		internal const int Set = 6; // set packedRange1Left,packedRange1Right,packedRange2Left,packedRange2Right...
		internal const int NSet = 7; // nset packedRange1Left,packedRange1Right,packedRange2Left,packedRange2Right...
		internal const int UCode = 8; // ucode cat
		internal const int NUCode = 9; // nucode cat
		internal const int Save = 10; // save slot
		#endregion
		public int Opcode;
		// the meaning of these depends on the opcode
		public int[] Ranges;
		public string[] Labels;
		public KeyValuePair<int[],string>[] Cases;
		public int Value;
		public string Name;
		public int Line;
		public Ast Expr;
		internal static Inst Parse(LexContext input)
		{
			Inst result = new Inst();
			_SkipCommentsAndWhiteSpace(input);
			var l = input.Line;
			var c = input.Column;
			var p = input.Position;
			result.Line = l;
			var id = _ParseIdentifier(input);
			switch (id)
			{
				case "regex":
					_SkipWhiteSpace(input);
					result.Opcode = Regex;
					input.Expecting('(');
					input.Advance();
					result.Expr=Ast.Parse(input);
					input.Expecting(')');
					input.Advance();
					_SkipToNextInstruction(input);
					break;
				case "match":
					_SkipWhiteSpace(input);
					var ll = input.CaptureBuffer.Length;
					var neg = false;
					if('-'==input.Current)
					{
						neg = true;
						input.Advance();
					}
					if (!input.TryReadDigits())
						throw new ExpectingException("Illegal operand in match instruction. Expecting integer", input.Line, input.Column, input.Position, input.FileOrUrl, "integer");
					var i = int.Parse(input.GetCapture(ll));
					if (neg)
						i = -i;
					result.Opcode = Match;
					result.Value = i;
					_SkipToNextInstruction(input);
					break;
				/*case "jmp":
					_SkipWhiteSpace(input);
					var lbl = _ParseIdentifier(input);
					result.Opcode = Jmp;
					result.Name = lbl;
					_SkipToNextInstruction(input);
					break;*/
				case "jmp":
					_SkipWhiteSpace(input);
					result.Opcode = Jmp;
					result.Labels = _ParseLabels(input);
					_SkipToNextInstruction(input);
					break;
				case "switch":
					_SkipWhiteSpace(input);
					result.Opcode = Switch;
					_ParseCases(result,input);
					_SkipToNextInstruction(input);
					break;
				case "any":
					result.Opcode = Any;
					_SkipToNextInstruction(input);
					break;
				case "char":
					_SkipWhiteSpace(input);
					result.Opcode = Char;
					result.Value = _ParseChar(input);
					_SkipToNextInstruction(input);
					break;
				case "set":
					_SkipWhiteSpace(input);
					result.Opcode = Set;
					result.Ranges= _ParseRanges(input);
					_SkipToNextInstruction(input);
					break;
				case "nset":
					_SkipWhiteSpace(input);
					result.Opcode = NSet;
					result.Ranges = _ParseRanges(input);
					_SkipToNextInstruction(input);
					break;
				case "ucode":
					_SkipWhiteSpace(input);
					ll = input.CaptureBuffer.Length;
					if (!input.TryReadDigits())
						throw new ExpectingException("Illegal operand in ucode instruction. Expecting integer", input.Line, input.Column, input.Position, input.FileOrUrl, "integer");
					i = int.Parse(input.GetCapture(ll));
					result.Opcode = UCode;
					result.Value = i;
					_SkipToNextInstruction(input);
					break;
				case "nucode":
					_SkipWhiteSpace(input);
					ll = input.CaptureBuffer.Length;
					if (!input.TryReadDigits())
						throw new ExpectingException("Illegal operand in nucode instruction. Expecting integer", input.Line, input.Column, input.Position, input.FileOrUrl, "integer");
					i = int.Parse(input.GetCapture(ll));
					result.Opcode = NUCode;
					result.Value = i;
					_SkipToNextInstruction(input);
					break;
				case "save":
					_SkipWhiteSpace(input);
					ll = input.CaptureBuffer.Length;
					if (!input.TryReadDigits())
						throw new ExpectingException("Illegal operand in save instruction. Expecting integer", input.Line, input.Column, input.Position, input.FileOrUrl, "integer");
					i = int.Parse(input.GetCapture(ll));
					result.Opcode = Save;
					result.Value = i;
					_SkipToNextInstruction(input);
					break;
				default:
					if (':' != input.Current)
						throw new ExpectingException("Expecting instruction or label", l, c, p, input.FileOrUrl, "match", "jmp", "jmp", "any", "char", "set", "nset", "ucode", "nucode", "save", "label");
					input.Advance();
					result.Opcode = Label;
					result.Name = id;
					break;
			}
			_SkipCommentsAndWhiteSpace(input);
			return result;
		}
		static void _SkipWhiteSpace(LexContext l)
		{
			l.EnsureStarted();
			while (-1 != l.Current && '\n' != l.Current && char.IsWhiteSpace((char)l.Current))
				l.Advance();
		}
		static void _SkipToNextInstruction(LexContext l)
		{
			l.EnsureStarted();
			while(-1!=l.Current)
			{
				if (';' == l.Current)
				{
					_SkipCommentsAndWhiteSpace(l);
					return;
				}
				else if ('\n' == l.Current)
				{
					_SkipCommentsAndWhiteSpace(l);
					return;
				}
				else if (char.IsWhiteSpace((char)l.Current))
					l.Advance();
				else
					throw new ExpectingException("Unexpected token in input", l.Line, l.Column, l.Position, l.FileOrUrl, "newline", "comment");
			}
			
		}
		static void _SkipCommentsAndWhiteSpace(LexContext l)
		{
			l.TrySkipWhiteSpace();
			while (';' == l.Current)
			{
				l.TrySkipUntil('\n', true);
				l.TrySkipWhiteSpace();
			}
		}
		static string _ParseIdentifier(LexContext l)
		{
			l.EnsureStarted();
			var ll = l.CaptureBuffer.Length;
			if(-1!=l.Current && '_'==l.Current || char.IsLetter((char)l.Current))
			{
				l.Capture();
				l.Advance();
				while(-1!=l.Current && '_'==l.Current || char.IsLetterOrDigit((char)l.Current))
				{
					l.Capture();
					l.Advance();
				}
				return l.GetCapture(ll);
			}
			throw new ExpectingException("Expecting identifier", l.Line, l.Column, l.Position, "identifier");
		}
		static KeyValuePair<int,int> _ParseRange(LexContext l)
		{
			l.EnsureStarted();
			var first = _ParseChar(l);
			if ('.' !=l.Current)
			{
				_SkipWhiteSpace(l);
				l.Expecting(',', '\n',';',':', -1);
				_SkipWhiteSpace(l);
				return new KeyValuePair<int, int>(first, first);
			} 
			l.Advance();
			l.Expecting('.');
			l.Advance();
			var last = _ParseChar(l);
			_SkipWhiteSpace(l);
			l.Expecting(',', ';',':','\n', -1);
			_SkipWhiteSpace(l);
			return new KeyValuePair<int, int>(first, last);
		}
		static int[] _ParseRanges(LexContext l)
		{
			_SkipWhiteSpace(l);
			var result = new List<int>();
			while(-1!=l.Current && '\n'!=l.Current && ':'!=l.Current)
			{
				_SkipWhiteSpace(l);
				var kvp = _ParseRange(l);
				result.Add(kvp.Key);
				result.Add(kvp.Value);
				if (',' == l.Current)
					l.Advance();
			}
			result.Sort();
			return result.ToArray();
		}
		static void _ParseCases(Inst result, LexContext l)
		{
			var cases = new List<KeyValuePair<int[], string>>();
			while (-1 != l.Current && '\n' != l.Current && ';' != l.Current)
			{
				_SkipWhiteSpace(l);
				var line = l.Line;
				var column = l.Column;
				var position = l.Position;
				string s;
				if ("case" != (s = _ParseIdentifier(l)) && "default" != s)
					throw new ExpectingException("Expecting case or default", line, column, position, l.FileOrUrl, "case", "default");
				_SkipWhiteSpace(l);
				if ("case" == s)
				{
					var ranges = _ParseRanges(l);
					_SkipWhiteSpace(l);
					l.Expecting(':');
					l.Advance();
					l.Expecting();
					var dst = _ParseIdentifier(l);
					cases.Add(new KeyValuePair<int[], string>(ranges, dst));
					if(','==l.Current)
					{
						l.Advance();
					}
				}
				else // default
				{
					_SkipWhiteSpace(l);
					l.Expecting(':');
					l.Advance();
					l.Expecting();
					result.Labels = _ParseLabels(l);
					break;
				}
				_SkipWhiteSpace(l);
			}
			result.Cases = cases.ToArray();
			_SkipWhiteSpace(l);
		}
		static string[] _ParseLabels(LexContext l)
		{
			_SkipWhiteSpace(l);
			var result = new List<string>();
			while (-1 != l.Current && ';'!=l.Current && '\n' != l.Current)
			{
				_SkipWhiteSpace(l);
				var name = _ParseIdentifier(l);
				_SkipWhiteSpace(l);
				result.Add(name);
				if (',' == l.Current)
				{
					l.Advance();
					_SkipWhiteSpace(l);
				}
			}
			return result.ToArray();
		}
		static int _ParseChar(LexContext l)
		{
			var line = l.Line;
			var column = l.Column;
			var position = l.Position;
			l.EnsureStarted();
			l.Expecting('\"');
			l.Advance();
			var ll = l.CaptureBuffer.Length;
			if (!l.TryReadUntil('\"', '\\', false))
				throw new ExpectingException("Unterminated character literal", line, column, position, l.FileOrUrl, "\"");
			var s = l.GetCapture(ll);
			int result;
			if('\\'== s[0])
			{
				var e = s.GetEnumerator();
				e.MoveNext();
				result = char.ConvertToUtf32(_ParseEscapeChar(e, l), 0);
				l.Expecting('\"');
				l.Advance();
				return result;
			}
			result = char.ConvertToUtf32(s, 0);
			l.Expecting('\"');
			l.Advance();
			return result;
		}
		static string _ParseEscapeChar(IEnumerator<char> e, LexContext pc)
		{
			if (e.MoveNext())
			{
				switch (e.Current)
				{
					case 'r':
						e.MoveNext();
						return "\r";
					case 'n':
						e.MoveNext();
						return "\n";
					case 't':
						e.MoveNext();
						return "\t";
					case 'a':
						e.MoveNext();
						return "\a";
					case 'b':
						e.MoveNext();
						return "\b";
					case 'f':
						e.MoveNext();
						return "\f";
					case 'v':
						e.MoveNext();
						return "\v";
					case '0':
						e.MoveNext();
						return "\0";
					case '\\':
						e.MoveNext();
						return "\\";
					case '\'':
						e.MoveNext();
						return "\'";
					case '\"':
						e.MoveNext();
						return "\"";
					case 'u':
						var acc = 0L;
						if (!e.MoveNext())
							break;
						if (!_IsHexChar(e.Current))
							break;
						acc <<= 4;
						acc |= _FromHexChar(e.Current);
						if (!e.MoveNext())
							break;
						if (!_IsHexChar(e.Current))
							break;
						acc <<= 4;
						acc |= _FromHexChar(e.Current);
						if (!e.MoveNext())
							break;
						if (!_IsHexChar(e.Current))
							break;
						acc <<= 4;
						acc |= _FromHexChar(e.Current);
						if (!e.MoveNext())
							break;
						if (!_IsHexChar(e.Current))
							break;
						acc <<= 4;
						acc |= _FromHexChar(e.Current);
						e.MoveNext();
						return unchecked((char)acc).ToString();
					case 'x':
						acc = 0;
						if (!e.MoveNext())
							break;
						if (!_IsHexChar(e.Current))
							break;
						acc <<= 4;
						acc |= _FromHexChar(e.Current);
						if (e.MoveNext() && _IsHexChar(e.Current))
						{
							acc <<= 4;
							acc |= _FromHexChar(e.Current);
							if (e.MoveNext() && _IsHexChar(e.Current))
							{
								acc <<= 4;
								acc |= _FromHexChar(e.Current);
								if (e.MoveNext() && _IsHexChar(e.Current))
								{
									acc <<= 4;
									acc |= _FromHexChar(e.Current);
									e.MoveNext();
								}
							}
						}
						return unchecked((char)acc).ToString();
					case 'U':
						acc = 0;
						if (!e.MoveNext())
							break;
						if (!_IsHexChar(e.Current))
							break;
						acc <<= 4;
						acc |= _FromHexChar(e.Current);
						if (!e.MoveNext())
							break;
						if (!_IsHexChar(e.Current))
							break;
						acc <<= 4;
						acc |= _FromHexChar(e.Current);
						if (!e.MoveNext())
							break;
						if (!_IsHexChar(e.Current))
							break;
						acc <<= 4;
						acc |= _FromHexChar(e.Current);
						if (!e.MoveNext())
							break;
						if (!_IsHexChar(e.Current))
							break;
						acc <<= 4;
						acc |= _FromHexChar(e.Current);
						if (!e.MoveNext())
							break;
						if (!_IsHexChar(e.Current))
							break;
						acc <<= 4;
						acc |= _FromHexChar(e.Current);
						if (!e.MoveNext())
							break;
						if (!_IsHexChar(e.Current))
							break;
						acc <<= 4;
						acc |= _FromHexChar(e.Current);
						if (!e.MoveNext())
							break;
						if (!_IsHexChar(e.Current))
							break;
						acc <<= 4;
						acc |= _FromHexChar(e.Current);
						if (!e.MoveNext())
							break;
						if (!_IsHexChar(e.Current))
							break;
						acc <<= 4;
						acc |= _FromHexChar(e.Current);
						e.MoveNext();
						return char.ConvertFromUtf32(unchecked((int)acc));
					default:
						throw new NotSupportedException(string.Format("Unsupported escape sequence \\{0}", e.Current));
				}
			}
			throw new ExpectingException("Unterminated escape sequence",pc.Line,pc.Column,pc.Position,pc.FileOrUrl);
		}
		static bool _IsHexChar(char hex)
		{
			return (':' > hex && '/' < hex) ||
				('G' > hex && '@' < hex) ||
				('g' > hex && '`' < hex);
		}
		static byte _FromHexChar(char hex)
		{
			if (':' > hex && '/' < hex)
				return (byte)(hex - '0');
			if ('G' > hex && '@' < hex)
				return (byte)(hex - '7'); // 'A'-10
			if ('g' > hex && '`' < hex)
				return (byte)(hex - 'W'); // 'a'-10
			throw new ArgumentException("The value was not hex.", "hex");
		}
	}
	
}
