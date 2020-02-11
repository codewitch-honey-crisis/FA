using System;
using System.Collections.Generic;

namespace F
{
#if FALIB
	public 
#endif
	sealed partial class FA
	{
		public readonly Dictionary<KeyValuePair<int,int>,FA> InputTransitions = new Dictionary<KeyValuePair<int,int>,FA>();
		public readonly HashSet<FA> EpsilonTransitions = new HashSet<FA>();
		public int AcceptSymbol = -1;
		public bool IsAccepting = false;
		public FA(bool isAccepting,int acceptSymbol=-1)
		{
			IsAccepting = isAccepting;
			AcceptSymbol = acceptSymbol;
		}
		public FA() : this(false)
		{

		}
		
		/// <summary>
		/// Moves from the specified state to a destination state in a DFA by moving along the specified input.
		/// </summary>
		/// <param name="dfaTable">The DFA state table to use</param>
		/// <param name="state">The current state id</param>
		/// <param name="input">The input to move on</param>
		/// <returns>The state id which the machine moved to or -1 if no state could be found.</returns>
		public static int Move(DfaEntry[] dfaTable, int state, int input)
		{
			// go through all the transitions
			for (var i = 0; i < dfaTable[state].Transitions.Length; i++)
			{
				var entry = dfaTable[state].Transitions[i];
				var found = false;
				// go through all the ranges to see if we matched anything.
				for (var j = 0; j < entry.PackedRanges.Length; j++)
				{
					var first = entry.PackedRanges[j];
					++j;
					var last = entry.PackedRanges[j];
					if (input > last) continue;
					if (first > input) break;
					found = true;
					break;
				}
				if (found)
				{
					// set the transition destination
					return entry.Destination;
				}
			}
			return -1;
		}
		/// <summary>
		/// Returns a DFA table that can be used to lex or match
		/// </summary>
		/// <param name="symbolTable">The symbol table to use, or null to just implicitly tag symbols with integer ids</param>
		/// <returns>A DFA table that can be used to efficiently match or lex input</returns>
		public DfaEntry[] ToDfaStateTable(IList<int> symbolTable = null,IProgress<FAProgress> progress=null)
		{
			// only convert to a DFA if we haven't already
			// ToDfa() already checks but it always copies
			// the state information so this performs better
			FA dfa = null;
			if (!IsDfa)
			{
				dfa = ToDfa(progress);
				dfa.TrimDuplicates(progress);
			}
			else
				dfa = this;
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
						if (0 > fa.AcceptSymbol)
							throw new InvalidOperationException("An accept symbol was never specified for state q" + jc.ToString());
						symbolLookup.Add(fa.AcceptSymbol, i);
						++i;
					}
				}
			}
			else // build the symbol lookup from the symbol table
				for (int ic = symbolTable.Count, i = 0; i < ic; ++i)
				{
					symbolLookup.Add(symbolTable[i], i);
				}

			// build the root array
			var result = new DfaEntry[closure.Count];
			for (var i = 0; i < result.Length; i++)
			{
				var fa = closure[i];
#if DEBUG
				if (fa.IsAccepting)
					System.Diagnostics.Debug.Assert(-1 < fa.AcceptSymbol, "Illegal accept symbol " + fa.AcceptSymbol.ToString() + " was found on state state q" + i.ToString());
#endif
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
#if DEBUG
				if (fa.IsAccepting && !symbolLookup.ContainsKey(fa.AcceptSymbol))
				{
					try
					{
						dfa.RenderToFile(@"dfastatetable_crashdump_dfa.jpg");
					}
					catch
					{

					}
					System.Diagnostics.Debug.Assert(false, "The symbol table did not contain an entry for state q" + i.ToString());
				}
#endif
				result[i] = new DfaEntry(
					fa.IsAccepting ? symbolLookup[fa.AcceptSymbol] : -1,
					trns);

			}
			return result;
		}
		
		public IDictionary<FA,int[]> FillInputTransitionRangesGroupedByState(IDictionary<FA,int[]> result = null)
		{
			var working = new Dictionary<FA, List<KeyValuePair<int,int>>>();
			foreach(var trns in InputTransitions)
			{
				List<KeyValuePair<int, int>> l;
				if(!working.TryGetValue(trns.Value,out l))
				{
					l = new List<KeyValuePair<int, int>>();
					working.Add(trns.Value, l);
				}
				l.Add(trns.Key);
			}
			if (null == result)
				result = new Dictionary<FA, int[]>();
			foreach(var item in working)
			{
				item.Value.Sort((x, y) => { var c = x.Key.CompareTo(y.Key); if (0 != c) return c; return x.Value.CompareTo(y.Value); });
				RangeUtility.NormalizeSortedRangeList(item.Value);
				result.Add(item.Key, RangeUtility.FromPairs(item.Value));
			}
			return result;
		}
		
		static bool _TryForwardNeutral(FA fa, out FA result)
		{
			if (!fa.IsNeutral)
			{
				result = fa;
				return false;
			}
			result = fa;
			foreach (var efa in fa.EpsilonTransitions)
			{
				result = efa;
				break;
			}
			return fa != result; // false if circular
		}
		static FA _ForwardNeutrals(FA fa)
		{
			if (null == fa)
				throw new ArgumentNullException(nameof(fa));
			var result = fa;

			while (_TryForwardNeutral(result, out result)) ;


			return result;
		}
		/// <summary>
		/// Converts the state machine to a Generalized NFA
		/// </summary>
		/// <param name="accept">The accept symbol</param>
		/// <returns>A new GNFA state machine that accepts the same language</returns>
		/// <remarks>A generalized NFA has a single start state and a single accept state that is final.</remarks>
		public FA ToGnfa(int accept=-1)
		{
			var fa = Clone();
			var accepting = fa.FillAcceptingStates();
			
			if(1<accepting.Count)
			{
				var newFinal = new FA(true, accept);
				foreach(var afa in accepting)
				{
					afa.IsAccepting = false;
					afa.EpsilonTransitions.Add(newFinal);
				}
			} else
			{
				foreach(var afa in accepting)
				{
					afa.AcceptSymbol = accept;
				}
			}
			// using the state removal method 
			// first convert to a GNFA
			var last = fa.FirstAcceptingState;
			if (!last.IsFinal)
			{
				// sometimes our last state isn't final,
				// so we have to extend the machine to have
				// a final last state
				last.IsAccepting = false;
				last.EpsilonTransitions.Add(new FA(true,accept));
			}

			if (!fa.IsNeutral) 
			{
				// add a neutral transition to the beginning
				var nfa = new FA();
				nfa.EpsilonTransitions.Add(fa);
				fa = nfa;
			}

			return fa;
		}
		/// <summary>
		/// Builds a simple lexer using the specified FA expressions
		/// </summary>
		/// <param name="expressions">The FSMs/expressions to compose the lexer from</param>
		/// <returns>An FSM suitable for lexing</returns>
		public static FA ToLexer(IEnumerable<FA> expressions)
		{
			var result = new FA();
			foreach (var expr in expressions)
				result.EpsilonTransitions.Add(expr);
			return result;
		}
		/// <summary>
		/// 
		/// </summary>
		/// <returns></returns>
		public ICollection<FA> FillAcceptingStates(ICollection<FA> result = null)
		{
			if (null == result)
				result = new HashSet<FA>();
			var closure = FillClosure();
			foreach (var fa in closure)
				if (fa.IsAccepting)
					result.Add(fa);
			return result;
		}
		public bool IsFinal {
			get {
				return 0 == InputTransitions.Count && 0 == EpsilonTransitions.Count;
			}
		}
		public bool IsNeutral {
			get {
				return !IsAccepting && 0 == InputTransitions.Count && 1 == EpsilonTransitions.Count;
			}
		}
		public void TrimNeutrals()
		{
			var cl = new List<FA>();
			FillClosure(cl);
			foreach (var s in cl)
			{
				var td = new List<KeyValuePair<KeyValuePair<int,int>, FA>>(s.InputTransitions.Count);
				
				foreach(var trns in s.InputTransitions)
				{
					var fa2 = _ForwardNeutrals(trns.Value);
					if (null == fa2)
						throw new InvalidProgramException("null in forward neutrals support code");
					td.Add(new KeyValuePair<KeyValuePair<int, int>, FA>(trns.Key, fa2));
				}
				s.InputTransitions.Clear();
				foreach (var trns in td)
				{
					s.InputTransitions.Add(trns.Key, trns.Value);
				}
				var el = new List<FA>(s.EpsilonTransitions.Count);
				foreach(var fa in s.EpsilonTransitions)
				{
					var fa2 = _ForwardNeutrals(fa);
					if (null == fa2)
						throw new InvalidProgramException("null in forward neutrals support code");
					el.Add(fa2);
				}
				var ec = el.Count;
				s.EpsilonTransitions.Clear();
				for (int j = 0; j < ec; ++j)
				{
					s.EpsilonTransitions.Add(el[j]);
				}
			}
		}
		public FA FirstAcceptingState {
			get {
				foreach(var fa in FillClosure())
				{
					if (fa.IsAccepting)
						return fa;
				}
				return null;
			}
		}
		public void AddInputTransition(KeyValuePair<int,int> range,FA dst)
		{
			foreach(var trns in InputTransitions)
			{
				if (RangeUtility.Intersects(trns.Key, range))
					throw new ArgumentException("There already is a transition to a different state on at least part of the specified input range");
			}
			InputTransitions.Add(range,dst);
		}
		public ICollection<FA> FillClosure(ICollection<FA> result = null)
		{
			if (null == result) result = new HashSet<FA>();
			if (result.Contains(this))
				return result;
			result.Add(this);
			foreach (var trns in InputTransitions)
				trns.Value.FillClosure(result);
			foreach (var fa in EpsilonTransitions)
				fa.FillClosure(result);
			return result;
		}
		public ICollection<FA> FillEpsilonClosure(ICollection<FA> result = null)
		{
			if (null == result) result = new HashSet<FA>();
			if (result.Contains(this))
				return result;
			result.Add(this);
			foreach (var fa in EpsilonTransitions)
				fa.FillEpsilonClosure(result);
			return result;
		}
		public static ICollection<FA> FillEpsilonClosure(IEnumerable<FA> states,ICollection<FA> result = null)
		{
			if (null == result) result = new HashSet<FA>();
			foreach (var fa in states)
				fa.FillEpsilonClosure(result);
			return result;
		}
		public FA Clone()
		{
			var closure = new List<FA>();
			FillClosure(closure);
			var nclosure = new FA[closure.Count];
			for (var i = 0; i < nclosure.Length; i++)
			{
				var fa = closure[i];
				nclosure[i] = new FA(fa.IsAccepting, fa.AcceptSymbol);
			}
			for(var i = 0;i<nclosure.Length;i++)
			{
				var fa = closure[i];
				var nfa = nclosure[i];
				foreach(var trns in fa.InputTransitions)
				{
					nfa.InputTransitions.Add(trns.Key, nclosure[closure.IndexOf(trns.Value)]);
				}
				foreach(var efa in fa.EpsilonTransitions)
				{
					nfa.EpsilonTransitions.Add(nclosure[closure.IndexOf(efa)]);
				}
			}
			return nclosure[0];
		}
		/// <summary>
		/// Returns a duplicate state machine, except one that only goes from this state to the state specified in <paramref name="to"/>. Any state that does not lead to that state is eliminated from the resulting graph.
		/// </summary>
		/// <param name="to">The state to track the path to</param>
		/// <returns>A new state machine that only goes from this state to the state indicated by <paramref name="to"/></returns>
		public FA ClonePathTo(FA to)
		{
			var closure = new List<FA>();
			FillClosure(closure);
			var nclosure = new FA[closure.Count];
			for (var i = 0; i < nclosure.Length; i++)
			{
				nclosure[i] = new FA(closure[i].IsAccepting, closure[i].AcceptSymbol);
			}
			for (var i = 0; i < nclosure.Length; i++)
			{
				var t = nclosure[i].InputTransitions;
				var e = nclosure[i].EpsilonTransitions;
				foreach (var trns in closure[i].InputTransitions)
				{
					if (trns.Value.FillClosure().Contains(to))
					{
						var id = closure.IndexOf(trns.Value);

						t.Add(trns.Key, nclosure[id]);
					}
				}
				foreach (var trns in closure[i].EpsilonTransitions)
				{
					if (trns.FillClosure().Contains(to))
					{
						var id = closure.IndexOf(trns);
						e.Add(nclosure[id]);
					}
				}
			}
			return nclosure[0];
		}
		/// <summary>
		/// Returns a duplicate state machine, except one that only goes from this state to any state specified in <paramref name="to"/>. Any state that does not lead to one of those states is eliminated from the resulting graph.
		/// </summary>
		/// <param name="to">The collection of destination states</param>
		/// <returns>A new state machine that only goes from this state to the states indicated by <paramref name="to"/></returns>
		public FA ClonePathToAny(IEnumerable<FA> to)
		{
			var closure = new List<FA>();
			FillClosure(closure);
			var nclosure = new FA[closure.Count];
			for (var i = 0; i < nclosure.Length; i++)
			{
				nclosure[i] = new FA(closure[i].IsAccepting, closure[i].AcceptSymbol);
			}
			for (var i = 0; i < nclosure.Length; i++)
			{
				var t = nclosure[i].InputTransitions;
				var e = nclosure[i].EpsilonTransitions;
				foreach (var trns in closure[i].InputTransitions)
				{
					if (_ContainsAny(trns.Value.FillClosure(), to))
					{
						var id = closure.IndexOf(trns.Value);

						t.Add(trns.Key, nclosure[id]);
					}
				}
				foreach (var trns in closure[i].EpsilonTransitions)
				{
					if (_ContainsAny(trns.FillClosure(), to))
					{
						var id = closure.IndexOf(trns);
						e.Add(nclosure[id]);
					}
				}
			}
			return nclosure[0];
		}

		static bool _ContainsAny(ICollection<FA> col, IEnumerable<FA> any)
		{
			foreach (var fa in any)
				if (col.Contains(fa))
					return true;
			return false;
		}
		public static FA Literal(IEnumerable<int> @string, int accept = -1)
		{
			var result = new FA();
			var current = result;
			foreach (var ch in @string)
			{
				current.IsAccepting = false;
				var fa = new FA(true, accept);
				current.AddInputTransition(new KeyValuePair<int,int>(ch, ch ), fa);
				current = fa;
			}
			return result;
		}

		public static FA Concat(IEnumerable<FA> exprs, int accept = -1)
		{
			FA result = null,left = null, right = null;
			foreach (var val in exprs)
			{
				if (null == val) continue;
				//Debug.Assert(null != val.FirstAcceptingState);
				var nval = val.Clone();
				//Debug.Assert(null != nval.FirstAcceptingState);
				if (null == left)
				{
					if (null == result)
						result = nval;
					left = nval;
					//Debug.Assert(null != left.FirstAcceptingState);
					continue;
				}
				if (null == right)
				{
					right = nval;
				}

				//Debug.Assert(null != left.FirstAcceptingState);
				nval = right.Clone();
				_Concat(left, nval);
				right = null;
				left = nval;

				//Debug.Assert(null != left.FirstAcceptingState);

			}
			if (null != right)
			{
				right.FirstAcceptingState.AcceptSymbol = accept;
			}
			else
			{
				result.FirstAcceptingState.AcceptSymbol = accept;
			}
			return result;
		}
		static void _Concat(FA lhs, FA rhs)
		{
			//Debug.Assert(lhs != rhs);
			var f = lhs.FirstAcceptingState;
			//Debug.Assert(null != rhs.FirstAcceptingState);
			f.IsAccepting = false;
			f.EpsilonTransitions.Add(rhs);
			//Debug.Assert(null!= lhs.FirstAcceptingState);

		}
		public static FA Set(int[] ranges, int accept = -1)
		{
			var result = new FA();
			var final = new FA(true, accept);
			var pairs = new List<KeyValuePair<int, int>>(RangeUtility.ToPairs(ranges));
			pairs.Sort((x, y) => { return x.Key.CompareTo(y.Key); });
			RangeUtility.NormalizeSortedRangeList(pairs);
			foreach(var pair in pairs)
				result.AddInputTransition(pair, final);
			return result;
		}
		public static FA Or(IEnumerable<FA> exprs, int accept = -1)
		{
			var result = new FA();
			var final = new FA(true, accept);
			foreach (var fa in exprs)
			{
				if (null != fa)
				{
					var nfa = fa.Clone();
					result.EpsilonTransitions.Add(nfa);
					var nffa = nfa.FirstAcceptingState;
					nffa.IsAccepting = false;
					nffa.EpsilonTransitions.Add(final);
				}
				else if (!result.EpsilonTransitions.Contains(final))
					result.EpsilonTransitions.Add(final);
			}
			return result;
		}
		public static FA Repeat(FA expr, int minOccurs = -1, int maxOccurs = -1, int accept = -1)
		{
			expr = expr.Clone();
			if (minOccurs > 0 && maxOccurs > 0 && minOccurs > maxOccurs)
				throw new ArgumentOutOfRangeException(nameof(maxOccurs));
			FA result;
			switch (minOccurs)
			{
				case -1:
				case 0:
					switch (maxOccurs)
					{
						case -1:
						case 0:
							//return Repeat(Optional(expr, accept),1,0,accept);
							result = new FA();
							var final = new FA(true, accept);
							final.EpsilonTransitions.Add(result);
							foreach (var afa in expr.FillAcceptingStates())
							{
								afa.IsAccepting = false;
								afa.EpsilonTransitions.Add(final);
							}
							result.EpsilonTransitions.Add(expr);
							result.EpsilonTransitions.Add(final);
							//Debug.Assert(null != result.FirstAcceptingState);
							return result;
						case 1:
							result = Optional(expr, accept);
							//Debug.Assert(null != result.FirstAcceptingState);
							return result;
						default:
							var l = new List<FA>();
							expr = Optional(expr);
							l.Add(expr);
							for (int i = 1; i < maxOccurs; ++i)
							{
								l.Add(expr.Clone());
							}
							result = Concat(l, accept);
							//Debug.Assert(null != result.FirstAcceptingState);
							return result;
					}
				case 1:
					switch (maxOccurs)
					{
						case -1:
						case 0:
							result = new FA();
							var final = new FA(true, accept);
							final.EpsilonTransitions.Add(result);
							foreach (var afa in expr.FillAcceptingStates())
							{
								afa.IsAccepting = false;
								afa.EpsilonTransitions.Add(final);
							}
							result.EpsilonTransitions.Add(expr);
							//Debug.Assert(null != result.FirstAcceptingState);
							return result;
						case 1:
							//Debug.Assert(null != expr.FirstAcceptingState);
							return expr;
						default:
							result = Concat(new FA[] { expr, Repeat(expr.Clone(), 0, maxOccurs - 1) }, accept);
							//Debug.Assert(null != result.FirstAcceptingState);
							return result;
					}
				default:
					switch (maxOccurs)
					{
						case -1:
						case 0:
							result = Concat(new FA[] { Repeat(expr, minOccurs, minOccurs, accept), Repeat(expr, 0, 0, accept) }, accept);
							//Debug.Assert(null != result.FirstAcceptingState);
							return result;
						case 1:
							throw new ArgumentOutOfRangeException(nameof(maxOccurs));
						default:
							if (minOccurs == maxOccurs)
							{
								var l = new List<FA>();
								l.Add(expr);
								//Debug.Assert(null != expr.FirstAcceptingState);
								for (int i = 1; i < minOccurs; ++i)
								{
									var e = expr.Clone();
									//Debug.Assert(null != e.FirstAcceptingState);
									l.Add(e);
								}
								result = Concat(l, accept);
								//Debug.Assert(null != result.FirstAcceptingState);
								return result;
							}
							result = Concat(new FA[] { Repeat(expr.Clone(), minOccurs, minOccurs, accept), Repeat(Optional(expr.Clone()), maxOccurs - minOccurs, maxOccurs - minOccurs, accept) }, accept);
							//Debug.Assert(null != result.FirstAcceptingState);
							return result;


					}
			}
			// should never get here
			throw new NotImplementedException();
		}
		public static FA Optional(FA expr, int accept = -1)
		{
			var result = expr.Clone();
			var f = result.FirstAcceptingState;
			f.AcceptSymbol = accept;
			result.EpsilonTransitions.Add(f);
			return result;
		}
		public static FA CaseInsensitive(FA expr,int accept = -1)
		{
			var result = expr.Clone();
			var closure = new List<FA>();
			result.FillClosure(closure);
			for(int ic = closure.Count, i = 0;i<ic;++i)
			{
				var fa = closure[i];
				var t = new List<KeyValuePair<KeyValuePair<int, int>, FA>>(fa.InputTransitions);
				fa.InputTransitions.Clear();
				foreach(var trns in t)
				{
					var f = char.ConvertFromUtf32(trns.Key.Key);
					var l = char.ConvertFromUtf32(trns.Key.Value);
					if(char.IsLower(f,0))
					{
						if (!char.IsLower(l, 0))
							throw new NotSupportedException("Attempt to make an invalid range case insensitive");
						fa.InputTransitions.Add(trns.Key, trns.Value);
						f = f.ToUpperInvariant();
						l = l.ToUpperInvariant();
						fa.InputTransitions.Add(new KeyValuePair<int, int>(char.ConvertToUtf32(f,0), char.ConvertToUtf32(l,0)), trns.Value);

					} else if(char.IsUpper(f,0))
					{
						if (!char.IsUpper(l, 0))
							throw new NotSupportedException("Attempt to make an invalid range case insensitive");
						fa.InputTransitions.Add(trns.Key, trns.Value);
						f = f.ToLowerInvariant();
						l = l.ToLowerInvariant();
						fa.InputTransitions.Add(new KeyValuePair<int, int>(char.ConvertToUtf32(f, 0), char.ConvertToUtf32(l, 0)), trns.Value);
					} else
					{
						fa.InputTransitions.Add(trns.Key, trns.Value);
					}
				}
			}
			return result;
		}
	
		/// <summary>
		/// Indicates whether or not this state machine is a DFA
		/// </summary>
		public bool IsDfa {
			get {
				// just check if any of our states have
				// epsilon transitions
				foreach (var fa in FillClosure())
					if (0 != fa.EpsilonTransitions.Count)
						return false;
				return true;
			}
		}
		/// <summary>
		/// Fills a collection with the result of moving each of the specified <paramref name="states"/> on the specified input.
		/// </summary>
		/// <param name="states">The states to examine</param>
		/// <param name="input">The input to use</param>
		/// <param name="result">The states that are now entered as a result of the move</param>
		/// <returns><paramref name="result"/> or a new collection if it wasn't specified.</returns>
		public static ICollection<FA> FillMove(IEnumerable<FA> states, int input, ICollection<FA> result = null)
		{
			var inputs = new KeyValuePair<int,int>( input, input );
			if (null == result) result = new List<FA>();
			foreach (var fa in states)
			{
				// examine each of the states reachable from this state on no input
				foreach (var efa in fa.FillEpsilonClosure())
				{
					// see if this state has this input in its transitions
					foreach(var trns in efa.InputTransitions)
						if(RangeUtility.Intersects(trns.Key,inputs))
							if(!result.Contains(trns.Value))
								foreach(var ofa in trns.Value.FillEpsilonClosure())
									if (!result.Contains(ofa)) // if it does, add it if it's not already there
										result.Add(ofa);	
				}
			}
			return result;
		}
		/// <summary>
		/// Indicates whether this state is a duplicate of another state.
		/// </summary>
		/// <param name="rhs">The state to compare with</param>
		/// <returns>True if the states are duplicates (one can be removed without changing the language of the machine)</returns>
		public bool IsDuplicate(FA rhs)
		{
			if(null != rhs && IsAccepting == rhs.IsAccepting &&
				_SetComparer.Default.Equals(EpsilonTransitions, rhs.EpsilonTransitions) &&
				_SetComparer.Default.Equals(InputTransitions, rhs.InputTransitions))
			{
				if (!IsAccepting || AcceptSymbol == rhs.AcceptSymbol)
					return true;
			}
			return false;
		}
		/// <summary>
		/// Fills a dictionary of duplicates by state for any duplicates found in the state graph
		/// </summary>
		/// <param name="result">The resulting dictionary to be filled.</param>
		/// <returns>The resulting dictionary of duplicates</returns>
		public IDictionary<FA, ICollection<FA>> FillDuplicatesGroupedByState(IDictionary<FA, ICollection<FA>> result = null)
		{
			if (null == result)
				result = new Dictionary<FA, ICollection<FA>>();
			var closure = new List<FA>();
			FillClosure(closure);
			var cl = closure;
			int c = cl.Count;
			for (int i = 0; i < c; i++)
			{
				var s = cl[i];
				for (int j = i + 1; j < c; j++)
				{
					var cmp = cl[j];
					if (s.IsDuplicate(cmp))
					{
						ICollection<FA> col = new List<FA>();
						if (!result.ContainsKey(s))
							result.Add(s, col);
						else
							col = result[s];
						if (!col.Contains(cmp))
							col.Add(cmp);
					}
				}
			}
			return result;
		}
		/// <summary>
		/// Trims duplicate states from the graph
		/// </summary>
		public void TrimDuplicates(IProgress<FAProgress> progress=null)
		{
			var closure = new List<FA>();
			FillClosure(closure);
			var lclosure = closure;
			var dups = new Dictionary<FA, ICollection<FA>>();
			int oc = 0;
			int c = -1;
			var k = 0;
			// we may have to run this multiple times to remove all references
			while (c < oc)
			{
				c = lclosure.Count;
				lclosure[0].FillDuplicatesGroupedByState(dups);
				
				if (0 < dups.Count)
				{
					if (null != progress)
					{
						progress.Report(new FAProgress(FAStatus.TrimDuplicates, k));
					}
					// for each pair of duplicates basically we replace all references to the first
					// with references to the latter, thus eliminating the duplicate state:
					foreach (KeyValuePair<FA, ICollection<FA>> de in dups)
					{
						var replacement = de.Key;
						var targets = de.Value;
						for (int i = 0; i < c; ++i)
						{
							var s = lclosure[i];

							var repls = new List<KeyValuePair<FA, FA>>();
							var td = new List<KeyValuePair<KeyValuePair<int, int>, FA>>(s.InputTransitions);
							s.InputTransitions.Clear();
							foreach (var trns in td)
							{
								if (targets.Contains(trns.Value))
								{
									s.InputTransitions.Add(trns.Key, replacement);
								}
								else
									s.InputTransitions.Add(trns.Key, trns.Value);
							}
							
							int lc = s.EpsilonTransitions.Count;
							var epsl = new List<FA>(s.EpsilonTransitions);
							s.EpsilonTransitions.Clear();
							for (int j = 0; j < lc; ++j)
							{
								var e = epsl[j];
								if (targets.Contains(e))
									e = replacement;
								if (!s.EpsilonTransitions.Contains(e))
									s.EpsilonTransitions.Add(e);		
							}
						}
					}
					dups.Clear();
					++k;
				}
				else
				{
					++k;
					break;
				}
					
				oc = c;
				var f = lclosure[0];
				lclosure = new List<FA>();
				f.FillClosure(lclosure);
				c = lclosure.Count;
				
			}
			
		}
#region _SetComparer
		// this class provides a series of comparers for various FA operations
		// these are primarily used during duplicate checking and in the powerset 
		// construction
		// see: https://www.codeproject.com/Articles/5251448/Implementing-Value-Equality-in-Csharp
		private sealed class _SetComparer :
			IEqualityComparer<IList<FA>>,
			IEqualityComparer<ICollection<FA>>,
			IEqualityComparer<ISet<FA>>,
			IEqualityComparer<IDictionary<KeyValuePair<int,int>, FA>>
		{
			// unordered comparison
			public bool Equals(ISet<FA> lhs, ISet<FA> rhs)
			{
				if (ReferenceEquals(lhs, rhs))
					return true;
				else if (ReferenceEquals(null, lhs) || ReferenceEquals(null, rhs))
					return false;
				return lhs.SetEquals(rhs);
			}
			// unordered comparison
			public bool Equals(IList<FA> lhs, IList<FA> rhs)
			{
				if (ReferenceEquals(lhs, rhs))
					return true;
				else if (ReferenceEquals(null, lhs) || ReferenceEquals(null, rhs))
					return false;
				if (lhs.Count != rhs.Count)
					return false;
				using (var xe = lhs.GetEnumerator())
				using (var ye = rhs.GetEnumerator())
					while (xe.MoveNext() && ye.MoveNext())
						if (!rhs.Contains(xe.Current) || !lhs.Contains(ye.Current))
							return false;
				return true;
			}
			// unordered comparison
			public bool Equals(ICollection<FA> lhs, ICollection<FA> rhs)
			{
				if (ReferenceEquals(lhs, rhs))
					return true;
				else if (ReferenceEquals(null, lhs) || ReferenceEquals(null, rhs))
					return false;
				if (lhs.Count != rhs.Count)
					return false;
				using (var xe = lhs.GetEnumerator())
				using (var ye = rhs.GetEnumerator())
					while (xe.MoveNext() && ye.MoveNext())
						if (!rhs.Contains(xe.Current) || !lhs.Contains(ye.Current))
							return false;
				return true;
			}
			public bool Equals(IDictionary<KeyValuePair<int,int>, FA> lhs, IDictionary<KeyValuePair<int,int>, FA> rhs)
			{
				if (ReferenceEquals(lhs, rhs))
					return true;
				else if (ReferenceEquals(null, lhs) || ReferenceEquals(null, rhs))
					return false;
				if (lhs.Count != rhs.Count)
					return false;
				using (var xe = lhs.GetEnumerator())
				using (var ye = rhs.GetEnumerator())
					while (xe.MoveNext() && ye.MoveNext())
						if (!rhs.Contains(xe.Current) || !lhs.Contains(ye.Current))
							return false;
				return true;
			}
			public bool Equals(IDictionary<FA, ICollection<KeyValuePair<int,int>>> lhs, IDictionary<FA, ICollection<KeyValuePair<int,int>>> rhs)
			{
				if (ReferenceEquals(lhs, rhs))
					return true;
				else if (ReferenceEquals(null, lhs) || ReferenceEquals(null, rhs))
					return false;
				if (lhs.Count != rhs.Count)
					return false;
				foreach (var trns in lhs)
				{
					ICollection<KeyValuePair<int,int>> col;
					if (!rhs.TryGetValue(trns.Key, out col))
						return false;
					using (var xe = trns.Value.GetEnumerator())
					using (var ye = col.GetEnumerator())
						while (xe.MoveNext() && ye.MoveNext())
							if (!col.Contains(xe.Current) || !trns.Value.Contains(ye.Current))
								return false;
				}

				return true;
			}
			public static bool _EqualsInput(ICollection<KeyValuePair<int,int>> lhs, ICollection<KeyValuePair<int,int>> rhs)
			{
				if (ReferenceEquals(lhs, rhs))
					return true;
				else if (ReferenceEquals(null, lhs) || ReferenceEquals(null, rhs))
					return false;
				if (lhs.Count != rhs.Count)
					return false;
				using (var xe = lhs.GetEnumerator())
				using (var ye = rhs.GetEnumerator())
					while (xe.MoveNext() && ye.MoveNext())
						if (!rhs.Contains(xe.Current) || !lhs.Contains(ye.Current))
							return false;
				return true;
			}
			public int GetHashCode(IList<FA> lhs)
			{
				var result = 0;
				for (int ic = lhs.Count, i = 0; i < ic; ++i)
				{
					var fa = lhs[i];
					if (null != fa)
						result ^= fa.GetHashCode();
				}
				return result;
			}
			public int GetHashCode(ISet<FA> lhs)
			{
				var result = 0;
				foreach (var fa in lhs)
					if (null != fa)
						result ^= fa.GetHashCode();
				return result;
			}
			public int GetHashCode(ICollection<FA> lhs)
			{
				var result = 0;
				foreach (var fa in lhs)
					if (null != fa)
						result ^= fa.GetHashCode();
				return result;
			}
			public int GetHashCode(IDictionary<KeyValuePair<int,int>, FA> lhs)
			{
				var result = 0;
				foreach (var kvp in lhs)
					result ^= kvp.GetHashCode();
				return result;
			}
			public static readonly _SetComparer Default = new _SetComparer();
		}
#endregion

	}
}
