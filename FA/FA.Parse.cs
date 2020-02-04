#define MINIMIZE
using LC;
using System;
using System.Collections.Generic;
using System.Text;

namespace F
{
	partial class FA
	{
		public static FA Parse(IEnumerable<char> input,int accept=-1,int line=1,int column=1,long position=0,string fileOrUrl=null)
		{
			var lc = LexContext.Create(input);
			lc.EnsureStarted();
			lc.SetLocation(line, column, position, fileOrUrl);
			return Parse(lc, accept);
		}
		internal static FA Parse(LexContext pc,int accept = -1)
		{
			
			FA result = null, next = null;
			int ich;
			pc.EnsureStarted();
			while (true)
			{
				switch (pc.Current)
				{
					case -1:
#if MINIMIZE
						result = result.ToDfa();
						result.TrimDuplicates();
#endif
						return result;
					case '.':
						var dot = FA.Set(new int[] { 0, 0x10ffff },accept);
						if (null == result)
							result = dot;
						else
						{
							result = FA.Concat(new FA[] { result, dot },accept);
						}
						pc.Advance();
						result = _ParseModifier(result, pc,accept);
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
								switch (uc.ToString())
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
								if(isNot)
								{
									next = FA.Set(CharacterClasses.UnicodeCategories[uci], accept);
								} else
									next = FA.Set(CharacterClasses.NotUnicodeCategories[uci], accept);
								break;
							case 'd':
								next = FA.Set(CharacterClasses.digit, accept);
								pc.Advance();
								break;
							case 'D':
								next = FA.Set(RangeUtility.NotRanges(CharacterClasses.digit), accept);
								pc.Advance();
								break;

							case 's':
								next = FA.Set(CharacterClasses.space,accept);
								pc.Advance();
								break;
							case 'S':
								next = FA.Set(RangeUtility.NotRanges(CharacterClasses.space),accept);
								pc.Advance();
								break;
							case 'w':
								next = FA.Set(CharacterClasses.word,accept);
								pc.Advance();
								break;
							case 'W':
								next = FA.Set(RangeUtility.NotRanges(CharacterClasses.word),accept);
								pc.Advance();
								break;
							default:
								if (-1 != (ich = _ParseEscapePart(pc)))
								{
									next = FA.Literal(new int[] { ich },accept);
									
								}
								else
								{
									pc.Expecting(); // throw an error
									return null; // doesn't execute
								}
								break;
						}
						next = _ParseModifier(next, pc,accept);
						if (null != result)
						{
							result = FA.Concat(new FA[] { result, next },accept);
						}
						else
							result = next;
						break;
					case ')':
#if MINIMIZE
						result = result.ToDfa();
						result.TrimDuplicates();
#endif
						return result;
					case '(':
						pc.Advance();
						pc.Expecting();
						next = Parse(pc,accept);
						pc.Expecting(')');
						pc.Advance();
						next = _ParseModifier(next, pc,accept);
						if (null == result)
							result = next;
						else
						{
							result = FA.Concat(new FA[] { result, next }, accept);
						}
						break;
					case '|':
						if (-1 != pc.Advance())
						{
							next = Parse(pc,accept);
							result = FA.Or(new FA[] { result, next }, accept);
						}
						else
						{
							result = FA.Optional(result, accept);
						}
						break;
					case '[':
						var seti = _ParseSet(pc);
						var set = seti.Value;
						if(seti.Key)
							set = RangeUtility.NotRanges(set);
						next = FA.Set(set, accept);
						next = _ParseModifier(next, pc,accept);

						if (null == result)
							result = next;
						else
						{
							result = FA.Concat(new FA[] { result, next }, accept);

						}
						break;
					default:
						ich = pc.Current;
						if (char.IsHighSurrogate((char)ich))
						{
							if (-1 == pc.Advance())
								throw new ExpectingException("Expecting low surrogate in Unicode stream", pc.Line, pc.Column, pc.Position, pc.FileOrUrl, "low-surrogate");
							ich = char.ConvertToUtf32((char)ich, (char)pc.Current);
						}
						next = FA.Literal(new int[] { ich }, accept);
						pc.Advance();
						next = _ParseModifier(next, pc,accept);
						if (null == result)
							result = next;
						else
						{
							result = FA.Concat(new FA[] { result, next }, accept);
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
							int[] ranges;
							if (!CharacterClasses.Known.TryGetValue(cls, out ranges))
								throw new ExpectingException("Unknown character class \"" + cls + "\" specified", pc.Line, pc.Column, pc.Position, pc.FileOrUrl);
							result.AddRange(ranges);
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
					}
					else
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
							throw new ExpectingException("Invalid character class " + n, lin, col, pos, pc.FileOrUrl, sa);
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

		static FA _ParseModifier(FA expr, LexContext pc,int accept)
		{
			var line = pc.Line;
			var column = pc.Column;
			var position = pc.Position;
			switch (pc.Current)
			{
				case '*':
					expr = Repeat(expr,0,0,accept);
					pc.Advance();
					break;
				case '+':
					expr = Repeat(expr, 1, 0, accept);
					pc.Advance();
					break;
				case '?':
					expr = Optional(expr, accept);
					pc.Advance();
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
					expr = Repeat(expr, min, max, accept);
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
					if (char.IsHighSurrogate((char)i))
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
