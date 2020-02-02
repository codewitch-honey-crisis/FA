using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Lexly
{
	partial class Tokenizer : object, IEnumerable<Token>
	{
		
		public const int DefaultTabWidth = 4;
		public const int ErrorSymbol = -1;
		private int[][] _program;
		private string[] _blockEnds;
		// our node flags. Currently only used for the hidden attribute
		private int[] _nodeFlags;

		private IEnumerable<char> _input;
		private int _tabWidth;
		private int _line;
		private int _column;
		private long _position;
		public Tokenizer(int[][] program,string[] blockEnds, int[] nodeFlags, IEnumerable<char> input) : 
			this(program,blockEnds,nodeFlags,input,1,0,0L,DefaultTabWidth)
		{

		}
		public Tokenizer(int[][] program, string[] blockEnds, int[] nodeFlags, IEnumerable<char> input,int line, int column, long position, int tabWidth)
		{
			_program = program;
			_blockEnds = blockEnds;
			_nodeFlags = nodeFlags;
			_input = input;
			_line = line;
			_column = column;
			_position = position;
			_tabWidth = tabWidth;
		}
		public IEnumerator<Token> GetEnumerator()
		{
			return new TokenizerEnumerator(_program,_blockEnds,_nodeFlags, _input.GetEnumerator(), _tabWidth, _line, _column, _position);
		}

		IEnumerator IEnumerable.GetEnumerator()
		{
			return GetEnumerator();
		}
		
	}
	internal class TokenizerEnumerator : object, IEnumerator<Token>
	{
		public const int ErrorSymbol = -1;
		private const int _EosSymbol = -2;
		#region Opcodes
		const int _Match = 1; // match symbol
		const int _Jmp = 2; // jmp addr1 {, addrN }
		const int _Switch = 3; // switch case inputranges:addr1 {, case inputranges:addrN } [, default: addr1 {, addrN } ]
		const int _Any = 4; // any
		const int _Char = 5; // char ch
		const int _Set = 6; // set packedRange1Left,packedRange1Right,packedRange2Left,packedRange2Right...
		const int _NSet = 7; // nset packedRange1Left,packedRange1Right,packedRange2Left,packedRange2Right...
		const int _UCode = 8; // ucode cat
		const int _NUCode = 9; // nucode cat
		const int _Save = 10; // save slot
		#endregion
		private const int _Disposed = -3;
		private const int _BeforeBegin = -2;
		private const int _EndOfInput = -1;
		private int[][] _program;
		private string[] _blockEnds;
		private int[] _nodeFlags; 
		private TokenizerFiber[] _currentFibers;
		private TokenizerFiber[] _nextFibers;
		private IEnumerator<char> _input;
		private StringBuilder _capture;
		private int _state = _BeforeBegin;
		private int _ch;
		private int _line;
		private int _column;
		private long _position;
		private int _oline;
		private int _ocolumn;
		private long _oposition;
		private string _value;
		private int _tabWidth;
		private Token _current;
		
		public TokenizerEnumerator(int[][] program,string[] blockEnds, int[] nodeFlags, IEnumerator<char> input, int tabWidth, int line,int column,long position)
		{
			_program = program;
			_blockEnds = blockEnds;
			_nodeFlags = nodeFlags;
			_input = input;
			_tabWidth = tabWidth;
			_line = line;
			_column = column;
			_position = position;
			_oline = line;
			_ocolumn = column;
			_oposition = position;
			_capture = new StringBuilder(64);
			_currentFibers = new TokenizerFiber[_program.Length];
			_nextFibers = new TokenizerFiber[_program.Length];
			_value = null;
			_ch = _BeforeBegin;
			_current.SymbolId = -1;
		}

		public Token Current 
		{ 
			get {
				if(0>_state)
				{
					if (_BeforeBegin == _state)
						throw new InvalidOperationException("The cursor is before the beginning of the input.");
					if (_EndOfInput == _state)
						throw new InvalidOperationException("The cursor is after the end of the input.");
					// _state == _Disposed
					throw new ObjectDisposedException("TokenizerEnumerator");
				}
				return _current;
			} 
		}
		object IEnumerator.Current { get { return Current; } }

		void IDisposable.Dispose()
		{
			if (_Disposed != _state)
			{
				_input.Dispose();
				_ch = _Disposed;
				_state = _Disposed;
			}

		}

		bool IEnumerator.MoveNext()
		{
			if (0 > _state)
			{
				if (_EndOfInput == _state)
					return false;
				if (_Disposed == _state)
					throw new ObjectDisposedException("TokenizerEnumerator");
				// _state = _BeforeBegin

			}
			if(_ch==_EndOfInput)
			{
				_state = _EndOfInput;
				return false;
			}
			_current = default(Token);
			_current.Line = _line;
			_current.Column = _column;
			_current.Position = _position;
			_current.Skipped = null;
			_capture.Clear();
			// lex the next input
			_current.SymbolId = _Lex();
			_current.Value = _value;
			// now look for hiddens and block ends
			var done = false;
			while (!done)
			{
				done = true;
				// if we're on a valid symbol
				if (ErrorSymbol < _current.SymbolId)
				{
					// get the block end for our symbol
					var be = _blockEnds[_current.SymbolId];
					// if it's valid
					if (null != be && 0 != be.Length)
					{
						// read until we find it or end of input
						if (!_TryReadUntilBlockEnd(be))
							_current.SymbolId = ErrorSymbol;
					}
					// node is hidden?
					if (ErrorSymbol < _current.SymbolId && 0 != (_nodeFlags[_current.SymbolId] & 1))
					{
						// update the cursor position and lex the next input, skipping this one
						done = false;
						if (_ch == _EndOfInput)
						{
							_state = _EndOfInput;
							return false;
						}
						_current.Line = _line;
						_current.Column = _column;
						_current.Position = _position;
						_capture.Clear();
						_current.SymbolId = _Lex();
					}
				}
			}
			// get what we captured
			_current.Value = _value;
			// return true if there's more to report
			return _EndOfInput != _state;
		}
		// moves to the next position, updates the state accordingly, and tracks the cursor position
		bool _MoveNextInput()
		{
			if (_ch == _EndOfInput)
				return false;
			if (_input.MoveNext())
			{
				if (_BeforeBegin != _state)
				{
					++_position;
					if ('\n' == _input.Current)
					{
						_column = 1;
						++_line;
					}
					else if ('\t' == _input.Current)
						_column += _tabWidth;
					else
						++_column;
				}
				else
				{
					// corner case for first move
					if ('\n' == _input.Current)
					{
						_column = 1;
						++_line;
					}
					else if ('\t' == _input.Current)
						_column += _tabWidth - 1;
				}
				_ch = _input.Current;
				_state = 0;
				return true;
			}
			_ch = _EndOfInput;
			return false;
		}
		// reads until the specified character, consuming it, returning false if it wasn't found
		bool _TryReadUntil(char character)
		{
			var ch = _input.Current;
			_capture.Append(ch);
			if (ch == character)
				return true;
			while (_MoveNextInput() && _input.Current != character)
				_capture.Append(_input.Current);
			if (_ch != _EndOfInput)
			{
				_capture.Append(_input.Current);
				return _input.Current == character;
			}
			return false;
		}
		// reads until the string is encountered, capturing it.
		bool
			_TryReadUntilBlockEnd(string blockEnd)
		{
			var ll = _capture.Length;
			while (_EndOfInput != _ch && _TryReadUntil(blockEnd[0]))
			{
				bool found = true;
				for (int i = 1; found && i < blockEnd.Length; ++i)
				{
					if (!_MoveNextInput() || _input.Current != blockEnd[i])
						found = false;
					else if (_EndOfInput != _ch)
						_capture.Append(_input.Current);
				}
				if (found)
				{
					_MoveNextInput();
					_value = _capture.ToString(ll, _capture.Length - ll);
					return true;
				}
			}

			return false;
		}
		void IEnumerator.Reset()
		{
			if (_BeforeBegin != _state)
			{
				if (_Disposed == _state)
					throw new ObjectDisposedException("TokenizerEnumerator");
				_input.Reset();
				_ch = _BeforeBegin;
				_line = _oline;
				_column = _ocolumn;
				_position = _oposition;
				_state = _BeforeBegin;
			}
		}
		static void _EnqueueFiber(ref int lcount, ref TokenizerFiber[] l, TokenizerFiber t, int sp)
		{
			// really shouldn't happen, but maybe it might
			if (l.Length <= lcount)
			{
				var newarr = new TokenizerFiber[l.Length * 2];
				Array.Copy(l, 0, newarr, 0, l.Length);
				l = newarr;
			}
			l[lcount] = t;
			++lcount;
			var pc = t.Program[t.Index];
			var op = pc[0];
			if(_Jmp==op)
			{
				for (var j = 1; j < pc.Length; ++j)
					_EnqueueFiber(ref lcount, ref l, new TokenizerFiber(t.Program, pc[j], t.Saved), sp);
				return;
			} 
			if(_Save==op) 
			{ 
				var slot = pc[1];
				var max = t.Saved.Length;
				if (slot > max)
					max = slot;
				var saved = new int[max];
				for (var i = 0; i < t.Saved.Length; ++i)
					saved[i] = t.Saved[i];
				saved[slot] = sp;
				_EnqueueFiber(ref lcount, ref l, new TokenizerFiber(t, t.Index + 1, saved), sp);
				return;
			}
		}
		static bool _InRanges(int[] pc, int ch)
		{
			var found = false;
			// go through all the ranges to see if we matched anything.
			for (var j = 1; j < pc.Length; ++j)
			{
				// grab our range from the packed ranges into first and last
				var first = pc[j];
				++j;
				var last = pc[j];
				// do a quick search through our ranges
				if (ch <= last)
				{
					if (first <= ch)
						found = true;
					// break
					j = pc.Length;
				}
			}
			return found;
		}
		int _Lex()
		{
			_capture.Clear();
			
			if(_state==_BeforeBegin)
				_MoveNextInput();
			
			int i;
			var match = -1;
			TokenizerFiber[] tmp;
			var currentFiberCount = 0;
			var nextFiberCount = 0;
			int[] pc;
			// position in input
			var sp = 0;
			int[] saved;
			int[] matched;
			saved = new int[2];
			_EnqueueFiber(ref currentFiberCount, ref _currentFibers, new TokenizerFiber(_program, 0, saved), 0);
			matched = null;
			var cur = -1;
			if (_EndOfInput != _ch)
			{
				var ch1 = (char)_ch;
				if (char.IsHighSurrogate(ch1))
				{
					if (!_MoveNextInput())
						throw new IOException(string.Format("Expecting low surrogate in unicode stream. The input source is corrupt or not valid Unicode at line {0}, column {1}, position {2}", _line, _column, _position));
					// compensate for doublewide char
					--_column;
					var ch2 = (char)_ch;
					cur = char.ConvertToUtf32(ch1, ch2);
				}
				else
					cur = ch1;

			}
			else
				cur = -1;

			while (0 < currentFiberCount)
			{
				bool passed = false;
				for (i = 0; i < currentFiberCount; ++i)
				{
					var t = _currentFibers[i];
					pc = t.Program[t.Index];
					saved = t.Saved;
					var op = pc[0];
					if (_Switch == op)
					{
						var idx = 1;
						while (idx < pc.Length && -2 < pc[idx])
						{
							if (_InRanges(pc, ref idx, cur))
							{
								while (-1 != pc[idx])
									++idx;

								++idx;
								passed = true;
								_EnqueueFiber(ref nextFiberCount, ref _nextFibers, new TokenizerFiber(t, pc[idx], saved), sp + 1);
								idx = pc.Length;
							}
							else
							{
								while (-1 != pc[idx])
									++idx;
								++idx;
							}
							++idx;
						}
						if (idx < pc.Length && -2 == pc[idx])
						{
							++idx;
							while (idx < pc.Length)
							{
								_EnqueueFiber(ref currentFiberCount, ref _currentFibers, new TokenizerFiber(t, pc[idx], saved), sp);
								++idx;
							}
						}
					}
					if (_Char==op)
					{
						if (cur == pc[1])
						{
							passed = true;
							_EnqueueFiber(ref nextFiberCount, ref _nextFibers, new TokenizerFiber(t, t.Index + 1, saved), sp + 1);
						}
					}
					else if(_Set==op)
					{
						if (_InRanges(pc, cur))
						{
							passed = true;
							_EnqueueFiber(ref nextFiberCount, ref _nextFibers, new TokenizerFiber(t, t.Index + 1, saved), sp + 1);
						}
					}
					else if (_NSet == op) 
					{
						if (!_InRanges(pc, cur) && _EndOfInput != _ch)
						{
							passed = true;
							_EnqueueFiber(ref nextFiberCount, ref _nextFibers, new TokenizerFiber(t, t.Index + 1, saved), sp + 1);
						}
					}
					else if (_UCode == op) 
					{
						var str = char.ConvertFromUtf32(cur);
						if ((int)char.GetUnicodeCategory(str, 0) == pc[1])
						{
							passed = true;
							_EnqueueFiber(ref nextFiberCount, ref _nextFibers, new TokenizerFiber(t, t.Index + 1, saved), sp + 1);
						}
					}
					else if (_NUCode == op) 
					{
						var str = char.ConvertFromUtf32(cur);
						if ((int)char.GetUnicodeCategory(str, 0) != pc[1] && _EndOfInput != _ch)
						{
							passed = true;
							_EnqueueFiber(ref nextFiberCount, ref _nextFibers, new TokenizerFiber(t, t.Index + 1, saved), sp + 1);
						}
					}
					else if (_Any == op) {
						if (_EndOfInput != _ch)
						{
							passed = true;
							_EnqueueFiber(ref nextFiberCount, ref _nextFibers, new TokenizerFiber(t, t.Index + 1, saved), sp + 1);
						}
					}
					else if (_Match == op)
					{
						matched = saved;
						match = pc[1];

						// break the for loop:
						i = currentFiberCount;
					}

				}
				if (passed)
				{
					_capture.Append(char.ConvertFromUtf32(cur));
					_MoveNextInput();
					if (_EndOfInput != _ch)
					{
						var ch1 = (char)_ch;
						if (char.IsHighSurrogate(ch1))
						{
							if (!_MoveNextInput())
								throw new IOException(string.Format("Expecting low surrogate in unicode stream. The input source is corrupt or not valid Unicode at line {0}, column {1}, position {2}", _line, _column, _position));
							// compensate for doublewide char
							--_column;
							++sp;
							var ch2 = (char)_ch;
							cur = char.ConvertToUtf32(ch1, ch2);
						}
						else
							cur = ch1;
					}
					else
						cur = -1;
					++sp;
				}
				tmp = _currentFibers;
				_currentFibers = _nextFibers;
				_nextFibers = tmp;
				currentFiberCount = nextFiberCount;
				nextFiberCount = 0;

			}

			if (null != matched)
			{
				var start = matched[0];
				// this is actually the point just past the end
				// of the match, but we can treat it as the length
				var len = matched[1];
				_value = _capture.ToString(start, len - start);
				return match;
			}
			return -1; // error symbol
		}
		static bool _InRanges(int[] pc, ref int index, int ch)
		{
			var found = false;
			// go through all the ranges to see if we matched anything.
			for (var j = index; j < pc.Length; ++j)
			{
				if (0 > pc[j])
				{
					index = j;
					return false;
				}
				// grab our range from the packed ranges into first and last
				var first = pc[j];
				++j;
				var last = pc[j];
				// do a quick search through our ranges
				if (ch <= last)
				{
					if (first <= ch)
						found = true;
					index = j;
					return found;
				}
			}
			index = pc.Length;
			return found;
		}
	}
	struct TokenizerFiber
	{
		public int[][] Program;
		public int Index;
		public int[] Saved;
		public TokenizerFiber(int[][] program, int index, int[] saved)
		{
			Program = program;
			Index = index;
			Saved = saved;
		}
		public TokenizerFiber(TokenizerFiber fiber, int index, int[] saved)
		{
			Program = fiber.Program;
			Index = index;
			Saved = saved;
		}
	}
}
