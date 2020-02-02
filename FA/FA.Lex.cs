using System;
using System.Collections.Generic;
using System.Text;

namespace F
{
	partial class FA
	{
		public static int Lex(DfaEntry[] dfaTable,IEnumerator<int> input,StringBuilder capture)
		{
			var state = 0;
			while (input.MoveNext())
			{
				var next = FA.Move(dfaTable, state, input.Current);
				if (-1 == next)
				{
					return dfaTable[state].AcceptSymbolId;
				}
				capture.Append(char.ConvertFromUtf32(input.Current));
				state = next;
			}
			return dfaTable[state].AcceptSymbolId;
		}
		public int Lex(IEnumerator<int> input,StringBuilder capture)
		{
			var states = FillEpsilonClosure();
			while(input.MoveNext())
			{
				var next = FA.FillMove(states, input.Current);
				if(0==next.Count)
				{
					foreach(var state in states)
					{
						if (state.IsAccepting)
							return state.AcceptSymbol;
					}
					return -1;
				}
				capture.Append(char.ConvertFromUtf32(input.Current));
				states = next;
			}
			foreach (var state in states)
			{
				if (state.IsAccepting)
					return state.AcceptSymbol;
			}
			return -1;
		}
	}
}
