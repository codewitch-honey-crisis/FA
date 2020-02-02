using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace F
{
	/// <summary>
	/// The tokenizer breaks input into lexical units that can be fed to a parser. Abstractly, they are essentially
	/// a series of regular expressions, each one tagged to a symbol. As the input is scanned, the tokenizer reports
	/// the symbol for each matched chunk, along with the matching value and location information. It's a regex runner.
	/// </summary>
	/// <remarks>The heavy lifting here is done by the <see cref="TokenEnumerator"/> class. This just provides a for-each interface over the tokenization process.</remarks>
#if FLIB
		public
#endif
	class Tokenizer : IEnumerable<Token>
	{
		FA _lexer;
		IEnumerable<char> _input;
		bool _reportEndToken;
		public Tokenizer(FA lexer,IEnumerable<char> input,bool reportEndToken=false)
		{
			_lexer = lexer;
			_input = input;
			_reportEndToken = reportEndToken;
		}
		public IEnumerator<Token> GetEnumerator()
			=> new TokenEnumerator(_lexer, _input, _reportEndToken);
		
		IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
	}
	/// <summary>
	/// The token enumerator is the core of the lexing engine. It uses a composite FA macine to match text against one of several "regular expression" patterns.
	/// </summary>
	class TokenEnumerator : IEnumerator<Token>
	{
		// our underlying input enumerator - works on strings or char arrays
		IEnumerator<char> _input;
		// location information
		long _position;
		int _line;
		int _column;
		bool _reportEndToken;
		// an integer we use so we can tell if the enumeration is started or running, or past the end.
		int _state;
		// this holds the current token we're on.
		Token _token;
		// this holds the initial NFA states we're in so we can quickly return here.
		// it's the starting point for all matches. It comes from _lexer.FillEpsilonClosure()
		ICollection<FA> _initialStates;

		// the lexer is a composite "regular expression" with tagged symbols for each one.
		FA _lexer;
		// this holds our current value
		StringBuilder _buffer;
		public TokenEnumerator(FA lexer,IEnumerable<char> @string, bool reportEndToken)
		{
			_lexer = lexer;
			_input = @string.GetEnumerator();
			_buffer = new StringBuilder();
			_reportEndToken = reportEndToken;
			_initialStates = _lexer.FillEpsilonClosure();
			Reset(); // Reset is used here to initialize the rest of the values
		}

		public Token Current { get { return _token; } }
		object IEnumerator.Current => Current;

		public void Dispose()
		{
			_state = -3;
			_input.Dispose();
		}
		public bool MoveNext()
		{
			switch(_state)
			{
				case -3:
					throw new ObjectDisposedException(GetType().FullName);
				case -2:
					if(-2!=_token.SymbolId)
					{
						_state = -2;
						goto case 0;
					}
					return false;
				case -1:
				case 0:
					_token = new Token();
					// store our current location before we advance
					_token.Column = _column;
					_token.Line = _line;
					_token.Position = _position;
					// this is where the real work happens:
					_token.SymbolId = _Lex();
					if (-2 == _token.SymbolId && !_reportEndToken)
					{
						_state = -2; // end. _Lex won't set it for this
						return false;
					}
					// store our value and length from the lex
					_token.Value = _buffer.ToString();
					_token.Length = _buffer.Length;
					return true;
				default:
					return false;
			}
			
		}
		/// <summary>
		/// This is where the work happens
		/// </summary>
		/// <returns>The symbol that was matched. members _state _line,_column,_position,_buffer and _input are also modified.</returns>
		int _Lex()
		{
			int acc;
			var states = _initialStates;
			_buffer.Clear();
			switch (_state)
			{
				case -1: // initial
					if (!_MoveNextInput())
					{
						_state = -2;
						acc = _GetAcceptingSymbol(states);
						if (-1<acc)
							return acc;
						else
							return -1; // "#ERROR";
					}
					_state = 0; // running
					break;
				case -2: // end of stream
					return -2; // "#EOS";
			}
			// Here's where we run most of the match. FillMove runs one interation of the NFA state machine.
			// We match until we can't match anymore (greedy matching) and then report the symbol of the last 
			// match we found, or an error ("#ERROR") if we couldn't find one.
			while (true)
			{
				var next = FA.FillMove(states, _input.Current);
				if (0 == next.Count) // couldn't find any states
					break;
				_buffer.Append(_input.Current);

				states = next;
				if (!_MoveNextInput())
				{
					// end of stream
					_state = -2;
					acc = _GetAcceptingSymbol(states);
					if (-1< acc) // do we accept?
						return acc;
					else
						return -1;// "#ERROR";
				}
			}
			acc = _GetAcceptingSymbol(states);
			if (-1< acc) // do we accept?
				return acc;
			else
			{
				// handle the error condition
				_buffer.Append(_input.Current);
				if (!_MoveNextInput())
					_state = -2;
				return -1; // "#ERROR";
			}
		}
		/// <summary>
		/// Advances the input, and tracks location information
		/// </summary>
		/// <returns>True if the underlying MoveNext returned true, otherwise false.</returns>
		bool _MoveNextInput()
		{
			if (_input.MoveNext())
			{
				if (-1 != _state)
				{
					++_position;
					if ('\n' == _input.Current)
					{
						_column = 1;
						++_line;
					}
					else
						++_column;
				}
				return true;
			}
			else if (0==_state)
			{
				++_position;
				++_column;
			}
			return false;
		}
		/// <summary>
		/// Finds if any of our states has an accept symbol and if so, returns it
		/// </summary>
		/// <param name="states">The states to check</param>
		/// <returns>The first symbol found or -1 if none were found</returns>
		static int _GetAcceptingSymbol(IEnumerable<FA> states)
		{
			foreach (var fa in states)
				if (fa.IsAccepting)
					return fa.AcceptSymbol;
			return -1;
		}
		public void Reset()
		{
			_input.Reset();
			_state = -1;
			_line = 1;
			_column = 1;
			_position = 0;
		}
	}
}
