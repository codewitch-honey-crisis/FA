using F;
using System;
using System.Collections.Generic;
using System.Text;
using LC;
namespace L
{
	sealed class Ast
	{
		#region Kinds
		public const int None = 0;
		public const int Lit = 1;
		public const int Set = 2;
		public const int NSet = 3;
		public const int Cls = 4;
		public const int Cat = 5;
		public const int Opt = 6;
		public const int Alt = 7;
		public const int Star = 8;
		public const int Plus = 9;
		public const int Rep = 10;
		public const int UCode = 11;
		public const int NUCode = 12;
		internal const int Dot = 13;
		#endregion Kinds

		public int Kind = None;
		public bool IsLazy = false;
		public Ast[] Exprs = null;
		
		public int Value = '\0';
		public int[] Ranges;
		public int Min = 0;
		public int Max = 0;
		static FA[] _ToFAs(Ast[] asts, int match = 0)
		{
			var result = new FA[asts.Length];
			for (var i = 0; i < result.Length; i++)
				result[i] = asts[i].ToFA(match);
			return result;
		}
		public FA ToFA(int match)
		{
			Ast ast = this;
			if (ast.IsLazy)
				throw new NotSupportedException("The AST node cannot be lazy");
			switch (ast.Kind)
			{
				case Ast.Alt:
					return FA.Or(_ToFAs(ast.Exprs, match), match);
				case Ast.Cat:
					if (1 == ast.Exprs.Length)
						return ast.Exprs[0].ToFA(match);
					return FA.Concat(_ToFAs(ast.Exprs, match), match);
				case Ast.Dot:
					return FA.Set(new int[] { 0, 0xd7ff, 0xe000, 0x10ffff }, match);
				case Ast.Lit:
					return FA.Literal(new int[] { ast.Value }, match);
				case Ast.NSet:
					var pairs = RangeUtility.ToPairs(ast.Ranges);
					RangeUtility.NormalizeRangeList(pairs);
					var pairl = new List<KeyValuePair<int, int>>(RangeUtility.NotRanges(pairs));
					return FA.Set(RangeUtility.FromPairs(pairl), match);
				case Ast.NUCode:
					pairs = RangeUtility.ToPairs(CharacterClasses.UnicodeCategories[ast.Value]);
					RangeUtility.NormalizeRangeList(pairs);
					pairl = new List<KeyValuePair<int, int>>(RangeUtility.NotRanges(pairs));
					return FA.Set(RangeUtility.FromPairs(pairl), match);
				case Ast.Opt:
					return FA.Optional(ast.Exprs[0].ToFA(match), match);
				case Ast.Plus:
					return FA.Repeat(ast.Exprs[0].ToFA(match), 1, 0, match);
				case Ast.Rep:
					return FA.Repeat(ast.Exprs[0].ToFA(match), ast.Min, ast.Max, match);
				case Ast.Set:
					return FA.Set(ast.Ranges, match);
				case Ast.Star:
					return FA.Repeat(ast.Exprs[0].ToFA(match), 0, 0, match);
				case Ast.UCode:
					return FA.Set(CharacterClasses.UnicodeCategories[ast.Value], match);
				default:
					throw new NotImplementedException();
			}
		}
		internal static Ast Parse(LexContext pc)
		{
			Ast result = null, next = null;
			int ich;
			pc.EnsureStarted();
			while (true)
			{
				switch (pc.Current)
				{
					case -1:
						return result;
					case '.':
						var dot = new Ast();
						dot.Kind = Ast.Dot;
						if (null == result)
							result = dot;
						else
						{
							if (Ast.Cat == result.Kind)
							{
								var exprs = new Ast[result.Exprs.Length + 1];
								Array.Copy(result.Exprs, 0, exprs, 0, result.Exprs.Length);
								exprs[exprs.Length - 1] = dot;
								result.Exprs = exprs;
							}
							else
							{
								var cat = new Ast();
								cat.Kind = Ast.Cat;
								cat.Exprs = new Ast[] { result, dot };
								result = cat;
							}
						}
						pc.Advance();
						result = _ParseModifier(result, pc);
						break;
					case '\\':

						pc.Advance();
						pc.Expecting();
						var isNot = false;
						switch (pc.Current)
						{
							case 'P':
								isNot = true;
								goto case 'p';
							case 'p':
								pc.Advance();
								pc.Expecting('{');
								var uc = new StringBuilder();
								int uli = pc.Line;
								int uco = pc.Column;
								long upo = pc.Position;
								while (-1 != pc.Advance() && '}' != pc.Current)
									uc.Append((char)pc.Current);
								pc.Expecting('}');
								pc.Advance();
								int uci = 0;
								switch(uc.ToString())
								{
									case "Pe":
										uci = 21;
										break;
									case "Pc":
										uci = 19;
										break;
									case "Cc":
										uci = 14;
										break;
									case "Sc":
										uci = 26;
										break;
									case "Pd":
										uci = 19;
										break;
									case "Nd":
										uci = 8;
										break;
									case "Me":
										uci = 7;
										break;
									case "Pf":
										uci = 23;
										break;
									case "Cf":
										uci = 15;
										break;
									case "Pi":
										uci = 22;
										break;
									case "Nl":
										uci = 9;
										break;
									case "Zl":
										uci = 12;
										break;
									case "Ll":
										uci = 1;
										break;
									case "Sm":
										uci = 25;
										break;
									case "Lm":
										uci = 3;
										break;
									case "Sk":
										uci = 27;
										break;
									case "Mn":
										uci = 5;
										break;
									case "Ps":
										uci = 20;
										break;
									case "Lo":
										uci = 4;
										break;
									case "Cn":
										uci = 29;
										break;
									case "No":
										uci = 10;
										break;
									case "Po":
										uci = 24;
										break;
									case "So":
										uci = 28;
										break;
									case "Zp":
										uci = 13;
										break;
									case "Co":
										uci = 17;
										break;
									case "Zs":
										uci = 11;
										break;
									case "Mc":
										uci = 6;
										break;
									case "Cs":
										uci = 16;
										break;
									case "Lt":
										uci = 2;
										break;
									case "Lu":
										uci = 0;
										break;
								}
								next = new Ast();
								next.Value = uci;
								next.Kind=isNot?Ast.NUCode:Ast.UCode;
								break;
							case 'd':
								next = new Ast();
								next.Kind = Ast.Set;
								next.Ranges = new int[] { '0', '9' };
								pc.Advance();
								break;
							case 'D':
								next = new Ast();
								next.Kind = Ast.NSet;
								next.Ranges = new int[] { '0', '9' };
								pc.Advance();
								break;
							
							case 's':
								next = new Ast();
								next.Kind = Ast.Set;
								next.Ranges = new int[] { '\t', '\t', ' ', ' ', '\r', '\r', '\n', '\n', '\f', '\f' };
								pc.Advance();
								break;
							case 'S':
								next = new Ast();
								next.Kind = Ast.NSet;
								next.Ranges = new int[] { '\t', '\t', ' ', ' ', '\r', '\r', '\n', '\n', '\f', '\f' };
								pc.Advance();
								break;
							case 'w':
								next = new Ast();
								next.Kind = Ast.Set;
								next.Ranges = new int[] { '_', '_', '0', '9', 'A', 'Z', 'a', 'z', };
								pc.Advance();
								break;
							case 'W':
								next = new Ast();
								next.Kind = Ast.NSet;
								next.Ranges = new int[] { '_', '_', '0', '9', 'A', 'Z', 'a', 'z', };
								pc.Advance();
								break;
							default:
								if (-1 != (ich = _ParseEscapePart(pc)))
								{
									next = new Ast();
									next.Kind = Ast.Lit;
									next.Value = ich;
								}
								else
								{
									pc.Expecting(); // throw an error
									return null; // doesn't execute
								}
								break;
						}
						next = _ParseModifier(next, pc);
						if (null != result)
						{
							if (Ast.Cat == result.Kind)
							{
								var exprs = new Ast[result.Exprs.Length + 1];
								Array.Copy(result.Exprs, 0, exprs, 0, result.Exprs.Length);
								exprs[exprs.Length - 1] = next;
								result.Exprs = exprs;
							}
							else
							{
								var cat = new Ast();
								cat.Kind = Ast.Cat;
								cat.Exprs = new Ast[] { result, next};
								result = cat;
							}
						}
						else
							result = next;
						break;
					case ')':
						return result;
					case '(':
						pc.Advance();
						pc.Expecting();
						next = Parse(pc);
						pc.Expecting(')');
						pc.Advance();
						next = _ParseModifier(next, pc);
						if (null == result)
							result = next;
						else
						{
							if (Ast.Cat == result.Kind)
							{
								var exprs = new Ast[result.Exprs.Length + 1];
								Array.Copy(result.Exprs, 0, exprs, 0, result.Exprs.Length);
								exprs[exprs.Length - 1] = next;
								result.Exprs = exprs;
							}
							else
							{
								var cat = new Ast();
								cat.Kind = Ast.Cat;
								cat.Exprs = new Ast[] { result, next };
								result = cat;
							}
						}
						break;
					case '|':
						if (-1 != pc.Advance())
						{
							next = Parse(pc);
							if (null!=result && Ast.Lit == result.Kind && Ast.Lit == next.Kind)
							{
								var set = new Ast();
								set.Kind = Set;
								set.Ranges = new int[] { result.Value, result.Value, next.Value, next.Value };
								result = set;
							}
							else if (null != result && Ast.Lit == result.Kind && Ast.Set == next.Kind)
							{
								var set = new Ast();
								set.Kind = Ast.Set;
								set.Ranges = new int[next.Ranges.Length + 2];
								set.Ranges[0] = result.Value;
								set.Ranges[1] = result.Value;
								Array.Copy(next.Ranges, 0, set.Ranges, 2, next.Ranges.Length);
								result = set;
							} else if(null!=result && Ast.Alt==result.Kind)
							{
								var exprs = new Ast[result.Exprs.Length + 1];
								Array.Copy(result.Exprs, 0, exprs, 0, result.Exprs.Length);
								exprs[exprs.Length - 1] = next;
								result.Exprs = exprs;
							}
							else { 
								var alt = new Ast();
								alt.Kind = Ast.Alt;
								if (null == next || next.Kind != Alt)
								{
									alt.Exprs = new Ast[] { result, next };
									result = alt;
								} else
								{
									var exprs = new Ast[1 + next.Exprs.Length];
									Array.Copy(next.Exprs, 0, exprs, 1, next.Exprs.Length);
									exprs[0] = result;
									alt.Exprs = exprs;
									result = alt;
								}
							}
						}
						else
						{
							var opt = new Ast();
							opt.Kind = Ast.Opt;
							opt.Exprs = new Ast[] { result };
							result = opt;
						}
						break;
					case '[':
						var seti = _ParseSet(pc);
						
						next = new Ast();
						next.Kind = (seti.Key)?NSet:Set;
						next.Ranges = seti.Value;
						next = _ParseModifier(next, pc);

						if (null == result)
							result = next;
						else
						{
							if (Ast.Cat == result.Kind)
							{
								var exprs = new Ast[result.Exprs.Length + 1];
								Array.Copy(result.Exprs, 0, exprs, 0, result.Exprs.Length);
								exprs[exprs.Length - 1] = next;
								result.Exprs = exprs;
							}
							else
							{
								var cat = new Ast();
								cat.Kind = Ast.Cat;
								cat.Exprs = new Ast[] { result, next };
								result = cat;
							}
							
						}
						break;
					default:
						ich = pc.Current;
						if(char.IsHighSurrogate((char)ich))
						{
							if (-1 == pc.Advance())
								throw new ExpectingException("Expecting low surrogate in Unicode stream", pc.Line, pc.Column, pc.Position, pc.FileOrUrl, "low-surrogate");
							ich = char.ConvertToUtf32((char)ich, (char)pc.Current);
						}
						next = new Ast();
						next.Kind = Ast.Lit;
						next.Value = ich;
						pc.Advance();
						next = _ParseModifier(next, pc);
						if (null == result)
							result = next;
						else
						{
							if (Ast.Cat == result.Kind)
							{
								var exprs = new Ast[result.Exprs.Length + 1];
								Array.Copy(result.Exprs, 0, exprs, 0, result.Exprs.Length);
								exprs[exprs.Length - 1] = next;
								result.Exprs = exprs;
							}
							else
							{
								var cat = new Ast();
								cat.Kind = Ast.Cat;
								cat.Exprs = new Ast[] { result, next };
								result = cat;
							}
						}
						break;
				}
			}
		}
		static KeyValuePair<bool, int[]> _ParseSet(LexContext pc)
		{
			var result = new List<int>();
			pc.EnsureStarted();
			pc.Expecting('[');
			pc.Advance();
			pc.Expecting();
			var isNot = false;
			if ('^' == pc.Current)
			{
				isNot = true;
				pc.Advance();
				pc.Expecting();
			}
			var firstRead = true;
			int firstChar = '\0';
			var readFirstChar = false;
			var wantRange = false;
			while (-1 != pc.Current && (firstRead || ']' != pc.Current))
			{
				if (!wantRange)
				{
					// can be a single char,
					// a range
					// or a named character class
					if ('[' == pc.Current) // named char class
					{
						pc.Advance();
						pc.Expecting();
						if (':' != pc.Current)
						{
							firstChar = '[';
							readFirstChar = true;
						}
						else
						{
							pc.Advance();
							pc.Expecting();
							var ll = pc.CaptureBuffer.Length;
							if (!pc.TryReadUntil(':', false))
								throw new ExpectingException("Expecting character class", pc.Line, pc.Column, pc.Position, pc.FileOrUrl);
							pc.Expecting(':');
							pc.Advance();
							pc.Expecting(']');
							pc.Advance();
							var cls = pc.GetCapture(ll);
							result.AddRange(Lex.GetCharacterClass(cls));
							readFirstChar = false;
							wantRange = false;
							firstRead = false;
							continue;
						}
					}
					if (!readFirstChar)
					{
						if (char.IsHighSurrogate((char)pc.Current))
						{
							var chh = (char)pc.Current;
							pc.Advance();
							pc.Expecting();
							firstChar = char.ConvertToUtf32(chh, (char)pc.Current);
							pc.Advance();
							pc.Expecting();
						}
						else if ('\\' == pc.Current)
						{
							pc.Advance();
							firstChar = _ParseRangeEscapePart(pc);
						}
						else
						{
							firstChar = pc.Current;
							pc.Advance();
							pc.Expecting();
						}
						readFirstChar = true;
						
					}
					else
					{
						if ('-' == pc.Current)
						{
							pc.Advance();
							pc.Expecting();
							wantRange = true;
						}
						else
						{
							result.Add(firstChar);
							result.Add(firstChar);
							readFirstChar = false;
						}
					}
					firstRead = false;
				}
				else
				{
					if ('\\' != pc.Current)
					{
						var ch = 0;
						if (char.IsHighSurrogate((char)pc.Current))
						{
							var chh = (char)pc.Current;
							pc.Advance();
							pc.Expecting();
							ch = char.ConvertToUtf32(chh, (char)pc.Current);
						}
						else
							ch = (char)pc.Current;
						pc.Advance();
						pc.Expecting();
						result.Add(firstChar);
						result.Add(ch);
					} else
					{
						result.Add(firstChar);
						pc.Advance();
						result.Add(_ParseRangeEscapePart(pc));
					}
					wantRange = false;
					readFirstChar = false;
				}

			}
			if (readFirstChar)
			{
				result.Add(firstChar);
				result.Add(firstChar);
				if (wantRange)
				{
					result.Add('-');
					result.Add('-');
				}
			}
			pc.Expecting(']');
			pc.Advance();
			return new KeyValuePair<bool, int[]>(isNot, result.ToArray());
		}
		static int[] _ParseRanges(LexContext pc)
		{
			pc.EnsureStarted();
			var result = new List<int>();
			int[] next = null;
			bool readDash = false;
			while (-1 != pc.Current && ']' != pc.Current)
			{
				switch (pc.Current)
				{
					case '[': // char class 
						if (null != next)
						{
							result.Add(next[0]);
							result.Add(next[1]);
							if (readDash)
							{
								result.Add('-');
								result.Add('-');
							}
						}
						pc.Advance();
						pc.Expecting(':');
						pc.Advance();
						var l = pc.CaptureBuffer.Length;
						var lin = pc.Line;
						var col = pc.Column;
						var pos = pc.Position;
						pc.TryReadUntil(':', false);
						var n = pc.GetCapture(l);
						pc.Advance();
						pc.Expecting(']');
						pc.Advance();
						int[] rngs;
						if (!CharacterClasses.Known.TryGetValue(n, out rngs))
						{
							var sa = new string[CharacterClasses.Known.Count];
							CharacterClasses.Known.Keys.CopyTo(sa, 0);
							throw new ExpectingException("Invalid character class " + n, lin, col, pos, pc.FileOrUrl,sa);
						}
						result.AddRange(rngs);
						readDash = false;
						next = null;
						break;
					case '\\':
						pc.Advance();
						pc.Expecting();
						switch (pc.Current)
						{
							case 'h':
								_ParseCharClassEscape(pc, "space", result, ref next, ref readDash);
								break;
							case 'd':
								_ParseCharClassEscape(pc, "digit", result, ref next, ref readDash);
								break;
							case 'D':
								_ParseCharClassEscape(pc, "^digit", result, ref next, ref readDash);
								break;
							case 'l':
								_ParseCharClassEscape(pc, "lower", result, ref next, ref readDash);
								break;
							case 's':
								_ParseCharClassEscape(pc, "space", result, ref next, ref readDash);
								break;
							case 'S':
								_ParseCharClassEscape(pc, "^space", result, ref next, ref readDash);
								break;
							case 'u':
								_ParseCharClassEscape(pc, "upper", result, ref next, ref readDash);
								break;
							case 'w':
								_ParseCharClassEscape(pc, "word", result, ref next, ref readDash);
								break;
							case 'W':
								_ParseCharClassEscape(pc, "^word", result, ref next, ref readDash);
								break;
							default:
								var ch = (char)_ParseRangeEscapePart(pc);
								if (null == next)
									next = new int[] { ch, ch };
								else if (readDash)
								{
									result.Add(next[0]);
									result.Add(ch);
									next = null;
									readDash = false;
								}
								else
								{
									result.AddRange(next);
									next = new int[] { ch, ch };
								}

								break;
						}

						break;
					case '-':
						pc.Advance();
						if (null == next)
						{
							next = new int[] { '-', '-' };
							readDash = false;
						}
						else
						{
							if (readDash)
								result.AddRange(next);
							
							readDash = true;
						}
						break;
					default:
						if (null == next)
						{
							next = new int[] { pc.Current, pc.Current };
						}
						else
						{
							if (readDash)
							{
								result.Add(next[0]);
								result.Add((char)pc.Current);
								next = null;
								readDash = false;
							}
							else
							{
								result.AddRange(next);
								next = new int[] { pc.Current, pc.Current };
							}
						}
						pc.Advance();
						break;
				}
			}
			if (null != next)
			{
				result.AddRange(next);
				if (readDash)
				{
					result.Add('-');
					result.Add('-');
				}
			}
			return result.ToArray();
		}
		
		static void _ParseCharClassEscape(LexContext pc, string cls, List<int> result, ref int[] next, ref bool readDash)
		{
			if (null != next)
			{
				result.AddRange(next);
				if (readDash)
				{
					result.Add('-');
					result.Add('-');
				}
				result.Add('-');
				result.Add('-');
			}
			pc.Advance();
			int[] rngs;
			if (!CharacterClasses.Known.TryGetValue(cls, out rngs))
			{
				var sa = new string[CharacterClasses.Known.Count];
				CharacterClasses.Known.Keys.CopyTo(sa, 0);
				throw new ExpectingException("Invalid character class " + cls, pc.Line, pc.Column, pc.Position, pc.FileOrUrl, sa);
			}
			result.AddRange(rngs);
			next = null;
			readDash = false;
		}

		static Ast _ParseModifier(Ast expr, LexContext pc)
		{
			var line = pc.Line;
			var column = pc.Column;
			var position = pc.Position;
			switch (pc.Current)
			{
				case '*':
					var rep = new Ast();
					rep.Kind = Ast.Star;
					rep.Exprs = new Ast[] { expr };
					expr = rep;
					pc.Advance();
					if ('?' == pc.Current)
					{
						rep.IsLazy = true;
						pc.Advance();
					}
					break;
				case '+':
					rep = new Ast();
					rep.Kind = Ast.Plus;
					rep.Exprs = new Ast[] { expr };
					expr = rep;
					pc.Advance();
					if ('?' == pc.Current)
					{
						rep.IsLazy = true;
						pc.Advance();
					}
					break;
				case '?':
					var opt = new Ast();
					opt.Kind = Ast.Opt;
					opt.Exprs = new Ast[] { expr };
					expr = opt;
					pc.Advance();
					if ('?' == pc.Current)
					{
						opt.IsLazy = true;
						pc.Advance();
					}
					break;
				case '{':
					pc.Advance();
					pc.TrySkipWhiteSpace();
					pc.Expecting('0', '1', '2', '3', '4', '5', '6', '7', '8', '9', ',', '}');
					var min = -1;
					var max = -1;
					if (',' != pc.Current && '}' != pc.Current)
					{
						var l = pc.CaptureBuffer.Length;
						pc.TryReadDigits();
						min = int.Parse(pc.GetCapture(l));
						pc.TrySkipWhiteSpace();
					}
					if (',' == pc.Current)
					{
						pc.Advance();
						pc.TrySkipWhiteSpace();
						pc.Expecting('0', '1', '2', '3', '4', '5', '6', '7', '8', '9', '}');
						if ('}' != pc.Current)
						{
							var l = pc.CaptureBuffer.Length;
							pc.TryReadDigits();
							max = int.Parse(pc.GetCapture(l));
							pc.TrySkipWhiteSpace();
						}
					}
					else { max = min; }
					pc.Expecting('}');
					pc.Advance();
					rep = new Ast();
					rep.Exprs = new Ast[] { expr };
					rep.Kind = Ast.Rep;
					rep.Min = min;
					rep.Max = max;
					expr = rep;
					if ('?' == pc.Current)
					{
						rep.IsLazy = true;
						pc.Advance();
					}
					break;
			}
			return expr;
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
		static bool _IsHexChar(char hex)
		{
			if (':' > hex && '/' < hex)
				return true;
			if ('G' > hex && '@' < hex)
				return true;
			if ('g' > hex && '`' < hex)
				return true;
			return false;
		}
		// return type is either char or ranges. this is kind of a union return type.
		static int _ParseEscapePart(LexContext pc)
		{
			if (-1 == pc.Current) return -1;
			switch (pc.Current)
			{
				case 'f':
					pc.Advance();
					return '\f';
				case 'v':
					pc.Advance();
					return '\v';
				case 't':
					pc.Advance();
					return '\t';
				case 'n':
					pc.Advance();
					return '\n';
				case 'r':
					pc.Advance();
					return '\r';
				case 'x':
					if (-1 == pc.Advance() || !_IsHexChar((char)pc.Current))
						return 'x';
					byte b = _FromHexChar((char)pc.Current);
					if (-1 == pc.Advance() || !_IsHexChar((char)pc.Current))
						return unchecked((char)b);
					b <<= 4;
					b |= _FromHexChar((char)pc.Current);
					if (-1 == pc.Advance() || !_IsHexChar((char)pc.Current))
						return unchecked((char)b);
					b <<= 4;
					b |= _FromHexChar((char)pc.Current);
					if (-1 == pc.Advance() || !_IsHexChar((char)pc.Current))
						return unchecked((char)b);
					b <<= 4;
					b |= _FromHexChar((char)pc.Current);
					return unchecked((char)b);
				case 'u':
					if (-1 == pc.Advance())
						return 'u';
					ushort u = _FromHexChar((char)pc.Current);
					u <<= 4;
					if (-1 == pc.Advance())
						return unchecked((char)u);
					u |= _FromHexChar((char)pc.Current);
					u <<= 4;
					if (-1 == pc.Advance())
						return unchecked((char)u);
					u |= _FromHexChar((char)pc.Current);
					u <<= 4;
					if (-1 == pc.Advance())
						return unchecked((char)u);
					u |= _FromHexChar((char)pc.Current);
					return unchecked((char)u);
				default:
					int i = pc.Current;
					pc.Advance();
					if(char.IsHighSurrogate((char)i))
					{
						i = char.ConvertToUtf32((char)i, (char)pc.Current);
						pc.Advance();
					}
					return (char)i;
			}
		}
		static int _ParseRangeEscapePart(LexContext pc)
		{
			if (-1 == pc.Current)
				return -1;
			switch (pc.Current)
			{
				case 'f':
					pc.Advance();
					return '\f';
				case 'v':
					pc.Advance();
					return '\v';
				case 't':
					pc.Advance();
					return '\t';
				case 'n':
					pc.Advance();
					return '\n';
				case 'r':
					pc.Advance();
					return '\r';
				case 'x':
					if (-1 == pc.Advance() || !_IsHexChar((char)pc.Current))
						return 'x';
					byte b = _FromHexChar((char)pc.Current);
					if (-1 == pc.Advance() || !_IsHexChar((char)pc.Current))
						return unchecked((char)b);
					b <<= 4;
					b |= _FromHexChar((char)pc.Current);
					if (-1 == pc.Advance() || !_IsHexChar((char)pc.Current))
						return unchecked((char)b);
					b <<= 4;
					b |= _FromHexChar((char)pc.Current);
					if (-1 == pc.Advance() || !_IsHexChar((char)pc.Current))
						return unchecked((char)b);
					b <<= 4;
					b |= _FromHexChar((char)pc.Current);
					return unchecked((char)b);
				case 'u':
					if (-1 == pc.Advance())
						return 'u';
					ushort u = _FromHexChar((char)pc.Current);
					u <<= 4;
					if (-1 == pc.Advance())
						return unchecked((char)u);
					u |= _FromHexChar((char)pc.Current);
					u <<= 4;
					if (-1 == pc.Advance())
						return unchecked((char)u);
					u |= _FromHexChar((char)pc.Current);
					u <<= 4;
					if (-1 == pc.Advance())
						return unchecked((char)u);
					u |= _FromHexChar((char)pc.Current);
					return unchecked((char)u);
				default:
					int i = pc.Current;
					pc.Advance();
					if (char.IsHighSurrogate((char)i))
					{
						i = char.ConvertToUtf32((char)i, (char)pc.Current);
						pc.Advance();
					}
					return (char)i;
			}
		}
	}
}
