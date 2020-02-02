using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.Design.Serialization;
using System.Globalization;
using System.IO;
using System.Text;

namespace Rolex
{
	/// <summary>
	/// This is an internal class that helps the code serializer know how to serialize DFA entries
	/// </summary>
	class NfaEntryConverter : TypeConverter
	{
		// we only need to convert to an InstanceDescriptor
		public override bool CanConvertTo(ITypeDescriptorContext context, Type destinationType)
		{
			if (typeof(InstanceDescriptor) == destinationType)
				return true;
			return base.CanConvertTo(context, destinationType);
		}
		// we return an InstanceDescriptor so the serializer can read it to figure out what code to generate
		public override object ConvertTo(ITypeDescriptorContext context, CultureInfo culture, object value, Type destinationType)
		{
			if (typeof(InstanceDescriptor) == destinationType)
			{
				// basically what we're doing is reporting that the constructor contains all the necessary
				// parameters for initializing an instance of this object in the specified state
				var nte = (NfaEntry)value;
				return new InstanceDescriptor(typeof(NfaEntry).GetConstructor(new Type[] { typeof(int), typeof(NfaTransitionEntry[]),typeof(int[]) }), new object[] { nte.AcceptSymbolId, nte.InputTransitions,nte.EpsilonTransitions });
			}
			return base.ConvertTo(context, culture, value, destinationType);
		}
	}

	/// <summary>
	/// This is an internal class that helps the code serializer serialize a NfaTransitionEntry
	/// </summary>
	class NfaTransitionEntryConverter : TypeConverter
	{
		// we only need to convert to an InstanceDescriptor
		public override bool CanConvertTo(ITypeDescriptorContext context, Type destinationType)
		{
			if (typeof(InstanceDescriptor) == destinationType)
				return true;
			return base.CanConvertTo(context, destinationType);
		}
		// report the constructor of the class so the serializer knows which call to serialize
		public override object ConvertTo(ITypeDescriptorContext context, CultureInfo culture, object value, Type destinationType)
		{
			if (typeof(InstanceDescriptor) == destinationType)
			{
				var dte = (NfaTransitionEntry)value;
				return new InstanceDescriptor(typeof(NfaTransitionEntry).GetConstructor(new Type[] { typeof(int[]), typeof(int) }), new object[] { dte.PackedRanges, dte.Destination });
			}
			return base.ConvertTo(context, culture, value, destinationType);
		}
	}
	[TypeConverter(typeof(NfaEntryConverter))]
	public struct NfaEntry
	{
		readonly public int AcceptSymbolId;
		readonly public NfaTransitionEntry[] InputTransitions;
		readonly public int[] EpsilonTransitions;
		public NfaEntry(int acceptSymbolId,NfaTransitionEntry[] inputTransitions, int[] epsilonTransitions)
		{
			AcceptSymbolId = acceptSymbolId;
			InputTransitions = inputTransitions;
			EpsilonTransitions = epsilonTransitions;
		}
	}
	[TypeConverter(typeof(NfaTransitionEntryConverter))]
	public struct NfaTransitionEntry
	{
		readonly public int[] PackedRanges;
		readonly public int Destination;
		public NfaTransitionEntry(int[] packedRanges,int destination)
		{
			PackedRanges = packedRanges;
			Destination = destination;
		}
	}
	public class PrototypeTokenizer : IEnumerable<Token>
	{
		public const int ErrorSymbol = -1;
		// our lexer
		NfaEntry[] _nfaTable;
		// our block ends (specified like comment<blockEnd="*/">="/*" in a rolex spec file)
		int[][] _blockEnds;
		// our node flags. Currently only used for the hidden attribute
		int[] _nodeFlags;
		// the input cursor. We can get this from a string, a char array, or some other source.
		IEnumerable<char> _input;
		public PrototypeTokenizer(NfaEntry[] nfaTable,int[][] blockEnds,int[] nodeFlags,IEnumerable<char> input)
		{
			_nfaTable = nfaTable;
			_blockEnds = blockEnds;
			_nodeFlags = nodeFlags;
			_input = input;
		}
		public IEnumerator<Token> GetEnumerator()
		{
			return new _PrototypeTokenizerEnumerator(_nfaTable, _blockEnds, _nodeFlags, _input.GetEnumerator());
		}

		IEnumerator IEnumerable.GetEnumerator()
		{
			return GetEnumerator();
		}
		private class _PrototypeTokenizerEnumerator : IEnumerator<Token>
		{
			// our error symbol. Always -1
			public const int ErrorSymbol = -1;
			// our end of stream symbol - returned by _Lex() and used internally but not reported
			private const int _EosSymbol = -2;
			// our disposed state indicator
			private const int _Disposed = -4;
			// the state indicates the cursor is before the beginning (initial state)
			private const int _BeforeBegin = -3;
			// the state indicates the cursor is after the end
			private const int _AfterEnd = -2;
			// the state indicates that the inner input enumeration has finished (we still have one more token to report)
			private const int _InnerFinished = -1;
			// indicates we're currently enumerating. We spend most of our time and effort in this state
			private const int _Enumerating = 0;
			// indicates the tab width, used for updating the Column property when we encounter a tab
			private const int _TabWidth = 4;
			NfaEntry[] _nfaTable;
			int[][] _blockEnds;
			int[] _nodeFlags;
			IEnumerator<char> _input;
			// current UTF-32 input value
			int _inputCurrent;
			StringBuilder _buffer;
			int _state;
			int _line;
			int _column;
			long _position;
			Token _current;
			public _PrototypeTokenizerEnumerator(NfaEntry[] nfaTable, int[][] blockEnds, int[] nodeFlags, IEnumerator<char> input)
			{
				_nfaTable = nfaTable;
				_blockEnds = blockEnds;
				_nodeFlags = nodeFlags;
				_input = input;
				_state = _BeforeBegin;
				_buffer = new StringBuilder();
				_line = 1;
				_column = 1;
				_position = 0;
			}

			public Token Current {
				get {
					// if we're not enumerating, find out what's going on
					if (_Enumerating > _state)
					{
						// check which state we're in, and throw accordingly
						if (_BeforeBegin == _state)
							throw new InvalidOperationException("The cursor is before the start of the enumeration");
						if (_AfterEnd == _state)
							throw new InvalidOperationException("The cursor is after the end of the enumeration");
						if (_Disposed == _state)
							_CheckDisposed(); // always throws
						// if we got here, the state is fine
					}
					return _current;
				}
			}
			object IEnumerator.Current { get { return Current; } }
			// moves to the next position, updates the state accordingly, and tracks the cursor position
			bool _MoveNextInput()
			{
				if (_input.MoveNext())
				{
					_inputCurrent = _input.Current;
					if (char.IsHighSurrogate(_input.Current))
					{
						if (!_input.MoveNext())
							throw new IOException("Unexpected end of input while looking for Unicode low surrogate.");
						++_position;
						_inputCurrent = char.ConvertToUtf32((char)_inputCurrent, _input.Current);
					}
					if (_BeforeBegin != _state)
					{

						++_position;
						if ('\n' == _inputCurrent)
						{
							_column = 1;
							++_line;
						}
						else if ('\t' == _inputCurrent)
							_column += _TabWidth;
						else
							++_column;
					}
					else
					{
						// compensate because initial move
						// shouldn't advance
						// corner case for first move
						if ('\n' == _inputCurrent)
						{
							_column = 1;
							++_line;
						}
						else if ('\t' == _inputCurrent)
							_column = _TabWidth - 1;
						else
							--_column;
					}
					return true;
				}
				_inputCurrent = -1;
				_state = _InnerFinished;
				return false;
			}
			public bool MoveNext()
			{
				// if we're not enumerating
				if (_Enumerating > _state)
				{
					_CheckDisposed();
					if (_AfterEnd == _state)
						return false;
					// we're okay if we got here
				}
				_current = default(Token);
				_current.Line = _line;
				_current.Column = _column;
				_current.Position = _position;
				_current.Skipped = null;
				_buffer.Clear();
				// lex the next input
				_current.SymbolId = _Lex();
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
							_current.Line = _line;
							_current.Column = _column;
							_current.Position = _position;
							_current.Skipped = null;
							_buffer.Clear();
							_current.SymbolId = _Lex();
						}
					}
				}
				// get what we captured
				_current.Value = _buffer.ToString();
				// update our state if we hit the end
				if (_EosSymbol == _current.SymbolId)
					_state = _AfterEnd;
				// return true if there's more to report
				return _AfterEnd != _state;
			}
			// reads until the specified character, consuming it, returning false if it wasn't found
			bool _TryReadUntil(int character)
			{
				var ch = _inputCurrent;
				_buffer.Append(char.ConvertFromUtf32(ch));
				if (ch == character)
					return true;
				while (_MoveNextInput() && _inputCurrent != character)
					_buffer.Append(char.ConvertFromUtf32(_inputCurrent));
				if (_state != _InnerFinished)
				{
					_buffer.Append(char.ConvertFromUtf32(_inputCurrent));
					return _input.Current == character;
				}
				return false;
			}
			// reads until the string is encountered, capturing it.
			bool _TryReadUntilBlockEnd(int[] blockEnd)
			{
				while (_InnerFinished != _state && _TryReadUntil(blockEnd[0]))
				{
					bool found = true;
					for (int i = 1; found && i < blockEnd.Length; ++i)
					{
						if (!_MoveNextInput() || _inputCurrent != blockEnd[i])
							found = false;
						else if (_InnerFinished != _state)
							_buffer.Append(char.ConvertFromUtf32(_inputCurrent));
					}
					if (found)
					{
						_MoveNextInput();
						return true;
					}
				}

				return false;
			}
			public void Reset()
			{
				_CheckDisposed();
				// don't reset if we're already before the beginning
				if (_BeforeBegin != _state)
					_input.Reset();
				// put our state back to the initial and reset the cursor position
				_state = _BeforeBegin;
				_line = 1;
				_column = 1;
				_position = 0L;
			}

			#region IDisposable Support
			void _CheckDisposed()
			{
				if (_state == _Disposed)
					throw new ObjectDisposedException(GetType().Name);
			}
			void _Dispose()
			{
				if (_Disposed!=_state)
				{

					_input.Dispose();
					_input = null;
					_state = _Disposed;
				}
			}
			~_PrototypeTokenizerEnumerator()
			{
				_Dispose();
			}

			public void Dispose()
			{
				_Dispose();
				GC.SuppressFinalize(this);
			}
			#endregion

			void FillEClosure(int state,ICollection<int> result)
			{
				if (result.Contains(state)) return;
				result.Add(state);
				var nfe = _nfaTable[state];
				for (var i = 0; i < nfe.EpsilonTransitions.Length; i++)
					FillEClosure(nfe.EpsilonTransitions[i], result);
			}
			int _GetAnyAccept(ICollection<int> states)
			{
				foreach (var state in states)
				{
					var a = _nfaTable[state].AcceptSymbolId;
					if (-1 != a) return a;
				}
				return -1;
			}
			int _Lex()
			{
				// our accepting symbol id
				int acceptSymbolId;
				// the DFA state we're currently in (start at zero)
				var nfaStates = new HashSet<int>();
				FillEClosure(0, nfaStates);
				// corner case for beginning
				if (_BeforeBegin == _state)
				{
					if (!_MoveNextInput()) // empty input.
					{
						// if we're on an accepting state, return that
						// otherwise, error
						acceptSymbolId = _GetAnyAccept(nfaStates);
						if (-1 != acceptSymbolId)
							return acceptSymbolId;
						else
							return ErrorSymbol;
					}
					// we're enumerating now
					_state = _Enumerating;
				}
				else if (_InnerFinished == _state || _AfterEnd == _state)
				{
					// if we're at the end just return the end symbol
					return _EosSymbol;
				}
				// Here's where we run most of the match. we run one interation of the DFA state machine.
				// We match until we can't match anymore (greedy matching) and then report the symbol of the last 
				// match we found, or an error ("#ERROR") if we couldn't find one.
				var done = false;
				while (!done)
				{
					var nextNfaStates = new HashSet<int>();
					foreach (var nfaState in nfaStates)
					{
						// go through all the transitions
						for (var i = 0; i < _nfaTable[nfaState].InputTransitions.Length; i++)
						{
							var entry = _nfaTable[nfaState].InputTransitions[i];
							var found = false;
							// go through all the ranges to see if we matched anything.
							for (var j = 0; j < entry.PackedRanges.Length; j++)
							{
								var ch = _inputCurrent;
								// grab our range from the packed ranges into first and last
								var first = entry.PackedRanges[j];
								++j;
								var last = entry.PackedRanges[j];
								// do a quick search through our ranges
								if (ch <= last)
								{
									if (first <= ch)
										found = true;
									j = int.MaxValue - 1; // break
								}
							}
							if (found)
							{
								// set the transition destination
								FillEClosure(entry.Destination, nextNfaStates);
								
							}
						}
					}
					if (0<nextNfaStates.Count) // found a valid transition
					{
						// capture our character
						_buffer.Append(char.ConvertFromUtf32(_inputCurrent));
						// and iterate to our next state
						nfaStates = nextNfaStates;
						if (!_MoveNextInput())
						{
							// end of stream, if we're on an accepting state,
							// return that, just like we do on empty string
							// if we're not, then we error, just like before
							acceptSymbolId = _GetAnyAccept(nfaStates);
							if (-1 != acceptSymbolId) // do we accept?
								return acceptSymbolId;
							else
								return ErrorSymbol;
						}
					}
					else
						done = true; // no valid transition, we can exit the loop
				}
				// once again, if the state we're on is accepting, return that
				// otherwise, error, almost as before with one minor exception
				acceptSymbolId = _GetAnyAccept(nfaStates);
				if (-1 != acceptSymbolId)
				{
					return acceptSymbolId;
				}
				else
				{
					// handle the error condition
					// we have to capture the input 
					// here and then advance or the 
					// machine will never halt
					_buffer.Append(char.ConvertFromUtf32(_inputCurrent));
					_MoveNextInput();
					return ErrorSymbol;
				}
			}
		}
	}
}
