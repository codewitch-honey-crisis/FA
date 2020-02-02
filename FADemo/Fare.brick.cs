using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections;
using System.Runtime.CompilerServices;
using System.IO;
using System.Threading;
using System.Diagnostics;
namespace Fare{/// <summary>
/// Finite-state automaton with regular expression operations.
/// <p>
/// Class invariants:
/// <ul>
/// <li>
/// An automaton is either represented explicitly (with State and Transition} objects)
/// or with a singleton string (see Singleton property ExpandSingleton() method) in case the
/// automaton is known to accept exactly one string. (Implicitly, all states and transitions of
/// an automaton are reachable from its initial state.)
/// </li>
/// <li>
/// Automata are always reduced (see method Rreduce()) and have no transitions to dead states
/// (see RemoveDeadTransitions() method).
/// </li>
/// <li>
/// If an automaton is non deterministic, then IsDeterministic property returns false (but the
/// converse is not required).
/// </li>
/// <li>
/// Automata provided as input to operations are generally assumed to be disjoint.
/// </li>
/// </ul>
/// </p>
/// If the states or transitions are manipulated manually, the RestoreInvariant() method and
/// SetDeterministic(bool) methods should be used afterwards to restore representation invariants
/// that are assumed by the built-in automata operations.
/// </summary>
public class Automaton{/// <summary>
/// Minimize using Huffman's O(n<sup>2</sup>) algorithm.
///   This is the standard text-book algorithm.
/// </summary>
public const int MinimizeHuffman=0;/// <summary>
/// Minimize using Brzozowski's O(2<sup>n</sup>) algorithm. 
///   This algorithm uses the reverse-determinize-reverse-determinize trick, which has a bad
///   worst-case behavior but often works very well in practice even better than Hopcroft's!).
/// </summary>
public const int MinimizeBrzozowski=1;/// <summary>
/// Minimize using Hopcroft's O(n log n) algorithm.
///   This is regarded as one of the most generally efficient algorithms that exist.
/// </summary>
public const int MinimizeHopcroft=2;/// <summary>
/// Selects whether operations may modify the input automata (default: <code>false</code>).
/// </summary>
private static bool allowMutation;/// <summary>
/// Minimize always flag.
/// </summary>
private static bool minimizeAlways;/// <summary>
/// The hash code.
/// </summary>
private int hashCode;/// <summary>
/// The initial.
/// </summary>
private State initial;/// <summary>
/// Initializes a new instance of the <see cref="Automaton"/> class that accepts the empty 
///   language. Using this constructor, automata can be constructed manually from 
///   <see cref="State"/> and <see cref="Transition"/> objects.
/// </summary>
public Automaton(){this.Initial=new State();this.IsDeterministic=true;this.Singleton=null;}/// <summary>
/// Gets the minimization algorithm (default: 
/// <code>
/// MINIMIZE_HOPCROFT
/// </code>
/// ).
/// </summary>
public static int Minimization{get{return Automaton.MinimizeHopcroft;}}/// <summary>
/// Gets or sets a value indicating whether operations may modify the input automata (default:
///   <code>
/// false
/// </code>
/// ).
/// </summary>
/// <value>
/// <c>true</c> if [allow mutation]; otherwise, <c>false</c>.
/// </value>
public static bool AllowMutation{get;set;}/// <summary>
/// Gets or sets a value indicating whether this automaton is definitely deterministic (i.e.,
///   there are no choices for any run, but a run may crash).
/// </summary>
/// <value>
/// <c>true</c> then this automaton is definitely deterministic (i.e., there are no 
///   choices for any run, but a run may crash)., <c>false</c>.
/// </value>
public bool IsDeterministic{get;set;}/// <summary>
/// Gets or sets the initial state of this automaton.
/// </summary>
/// <value>
/// The initial state of this automaton.
/// </value>
public State Initial{get{this.ExpandSingleton();return this.initial;}set{this.Singleton=null;this.initial=value;}}/// <summary>
/// Gets or sets the singleton string for this automaton. An automaton that accepts exactly one
///  string <i>may</i> be represented in singleton mode. In that case, this method may be 
/// used to obtain the string.
/// </summary>
/// <value>The singleton string, null if this automaton is not in singleton mode.</value>
public string Singleton{get;set;}/// <summary>
/// Gets or sets a value indicating whether this instance is singleton.
/// </summary>
/// <value>
/// <c>true</c> if this instance is singleton; otherwise, <c>false</c>.
/// </value>
public bool IsSingleton{get{return this.Singleton!=null;}}/// <summary>
/// Gets or sets a value indicating whether this instance is debug.
/// </summary>
/// <value>
/// <c>true</c> if this instance is debug; otherwise, <c>false</c>.
/// </value>
public bool IsDebug{get;set;}/// <summary>
/// Gets or sets a value indicating whether IsEmpty.
/// </summary>
public bool IsEmpty{get;set;}/// <summary>
/// Gets the number of states in this automaton.
/// </summary>
/// Returns the number of states in this automaton.
public int NumberOfStates{get{if(this.IsSingleton){return this.Singleton.Length+1;}return this.GetStates().Count;}}/// <summary>
/// Gets the number of transitions in this automaton. This number is counted
///   as the total number of edges, where one edge may be a character interval.
/// </summary>
public int NumberOfTransitions{get{if(this.IsSingleton){return this.Singleton.Length;}return this.GetStates().Sum(s=>s.Transitions.Count);}}public static
 Transition[][]GetSortedTransitions(HashSet<State>states){Automaton.SetStateNumbers(states);var transitions=new Transition[states.Count][];foreach(State
 s in states){transitions[s.Number]=s.GetSortedTransitions(false).ToArray();}return transitions;}public static Automaton MakeChar(char c){return BasicAutomata.MakeChar(c);
}public static Automaton MakeCharSet(string set){return BasicAutomata.MakeCharSet(set);}public static Automaton MakeString(string s){return BasicAutomata.MakeString(s);
}public static Automaton Minimize(Automaton a){a.Minimize();return a;}/// <summary>
/// Sets or resets allow mutate flag. If this flag is set, then all automata operations
/// may modify automata given as input; otherwise, operations will always leave input
/// automata languages unmodified. By default, the flag is not set.
/// </summary>
/// <param name="flag">if set to <c>true</c> then all automata operations may modify 
/// automata given as input; otherwise, operations will always leave input automata 
/// languages unmodified..</param>
/// <returns>The previous value of the flag.</returns>
public static bool SetAllowMutate(bool flag){bool b=allowMutation;allowMutation=flag;return b;}/// <summary>
/// Sets or resets minimize always flag. If this flag is set, then {@link #minimize()} 
/// will automatically be invoked after all operations that otherwise may produce 
/// non-minimal automata. By default, the flag is not set.
/// </summary>
/// <param name="flag">The flag if true, the flag is set.</param>
public static void SetMinimizeAlways(bool flag){minimizeAlways=flag;}/// <summary>
/// Assigns consecutive numbers to the given states.
/// </summary>
/// <param name="states">The states.</param>
public static void SetStateNumbers(IEnumerable<State>states){int number=0;foreach(State s in states){s.Number=number++;}}/// <inheritdoc />
public override int GetHashCode(){if(this.hashCode==0){this.Minimize();}return this.hashCode;}public void AddEpsilons(ICollection<StatePair>pairs){BasicOperations.AddEpsilons(this,
pairs);}/// <summary>
/// The check minimize always.
/// </summary>
public void CheckMinimizeAlways(){if(minimizeAlways){this.Minimize();}}/// <summary>
/// The clear hash code.
/// </summary>
public void ClearHashCode(){this.hashCode=0;}/// <summary>
/// Creates a shallow copy of the current Automaton.
/// </summary>
/// <returns>
/// A shallow copy of the current Automaton.
/// </returns>
public Automaton Clone(){var a=(Automaton)this.MemberwiseClone();if(!this.IsSingleton){HashSet<State>states=this.GetStates();var d=states.ToDictionary(s
=>s,s=>new State());foreach(State s in states){State p;if(!d.TryGetValue(s,out p)){continue;}p.Accept=s.Accept;if(s==this.Initial){a.Initial=p;}foreach
(Transition t in s.Transitions){State to;d.TryGetValue(t.To,out to);p.Transitions.Add(new Transition(t.Min,t.Max,to));}}}return a;}/// <summary>
/// A clone of this automaton, expands if singleton.
/// </summary>
/// <returns>
/// Returns a clone of this automaton, expands if singleton.
/// </returns>
public Automaton CloneExpanded(){Automaton a=this.Clone();a.ExpandSingleton();return a;}/// <summary>
/// A clone of this automaton unless 
/// <code>
/// allowMutation
/// </code>
/// is set, expands if singleton.
/// </summary>
/// <returns>
/// Returns a clone of this automaton unless 
/// <code>
/// allowMutation
/// </code>
/// is set, expands if singleton.
/// </returns>
public Automaton CloneExpandedIfRequired(){if(Automaton.AllowMutation){this.ExpandSingleton();return this;}return this.CloneExpanded();}/// <summary>
/// Returns a clone of this automaton, or this automaton itself if <code>allow_mutation</code>
/// flag is set.
/// </summary>
/// <returns>A clone of this automaton, or this automaton itself if <code>allow_mutation</code>
/// flag is set.</returns>
public Automaton CloneIfRequired(){if(allowMutation){return this;}return this.Clone();}public Automaton Complement(){return BasicOperations.Complement(this);
}public Automaton Concatenate(Automaton a){return BasicOperations.Concatenate(this,a);}public Automaton Union(Automaton a){return BasicOperations.Union(new
 Automaton[]{this,a});}public void Determinize(){BasicOperations.Determinize(this);}/// <summary>
/// Expands singleton representation to normal representation.
/// Does nothing if not in singleton representation.
/// </summary>
public void ExpandSingleton(){if(this.IsSingleton){var p=new State();initial=p;foreach(char t in this.Singleton){var q=new State();p.Transitions.Add(new
 Transition(t,q));p=q;}p.Accept=true;this.IsDeterministic=true;this.Singleton=null;}}/// <summary>
/// The set of reachable accept states.
/// </summary>
/// <returns>Returns the set of reachable accept states.</returns>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design","CA1024:UsePropertiesWhereAppropriate",Justification="This is not executing immediately nor returns the same value each time it is invoked.")]
public HashSet<State>GetAcceptStates(){this.ExpandSingleton();var accepts=new HashSet<State>();var visited=new HashSet<State>();var worklist=new LinkedList<State>();
worklist.AddLast(this.Initial);visited.Add(this.Initial);while(worklist.Count>0){State s=worklist.RemoveAndReturnFirst();if(s.Accept){accepts.Add(s);}
foreach(Transition t in s.Transitions){ if(t.To==null){continue;}if(!visited.Contains(t.To)){visited.Add(t.To);worklist.AddLast(t.To);}}}return accepts;
}/// <summary>
/// Returns the set of live states. A state is "live" if an accept state is reachable from it.
/// </summary>
/// <returns></returns>
public HashSet<State>GetLiveStates(){this.ExpandSingleton();return this.GetLiveStates(this.GetStates());}/// <summary>
/// The sorted array of all interval start points.
/// </summary>
/// <returns>Returns sorted array of all interval start points.</returns>
public char[]GetStartPoints(){var pointSet=new HashSet<char>();foreach(State s in this.GetStates()){pointSet.Add(char.MinValue);foreach(Transition t in
 s.Transitions){pointSet.Add(t.Min);if(t.Max<char.MaxValue){pointSet.Add((char)(t.Max+1));}}}var points=new char[pointSet.Count];int n=0;foreach(char m
 in pointSet){points[n++]=m;}Array.Sort(points);return points;}/// <summary>
/// Gets the set of states that are reachable from the initial state.
/// </summary>
/// <returns>
/// The set of states that are reachable from the initial state.
/// </returns>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design","CA1024:UsePropertiesWhereAppropriate",Justification="This is not executing immediately nor returns the same value each time it is invoked.")]
public HashSet<State>GetStates(){this.ExpandSingleton();HashSet<State>visited;if(this.IsDebug){visited=new HashSet<State>();}else{visited=new HashSet<State>();
}var worklist=new LinkedList<State>();worklist.AddLast(this.Initial);visited.Add(this.Initial);while(worklist.Count>0){State s=worklist.RemoveAndReturnFirst();
if(s==null){continue;}HashSet<Transition>tr=this.IsDebug?new HashSet<Transition>(s.GetSortedTransitions(false)):new HashSet<Transition>(s.Transitions);
foreach(Transition t in tr){if(!visited.Contains(t.To)){visited.Add(t.To);worklist.AddLast(t.To);}}}return visited;}public Automaton Intersection(Automaton
 a){return BasicOperations.Intersection(this,a);}public bool IsEmptyString(){return BasicOperations.IsEmptyString(this);}/// <summary>
/// The minimize.
/// </summary>
public void Minimize(){MinimizationOperations.Minimize(this);}public Automaton Optional(){return BasicOperations.Optional(this);}/// <summary>
/// Recomputes the hash code.
///   The automaton must be minimal when this operation is performed.
/// </summary>
public void RecomputeHashCode(){this.hashCode=(this.NumberOfStates*3)+(this.NumberOfTransitions*2);if(hashCode==0){hashCode=1;}}/// <summary>
/// Reduces this automaton.
/// An automaton is "reduced" by combining overlapping and adjacent edge intervals with same 
/// destination.
/// </summary>
public void Reduce(){if(this.IsSingleton){return;}HashSet<State>states=this.GetStates();Automaton.SetStateNumbers(states);foreach(State s in states){IList<Transition>
st=s.GetSortedTransitions(true);s.ResetTransitions();State p=null;int min=-1,max=-1;foreach(Transition t in st){if(p==t.To){if(t.Min<=max+1){if(t.Max>
max){max=t.Max;}}else{if(p!=null){s.Transitions.Add(new Transition((char)min,(char)max,p));}min=t.Min;max=t.Max;}}else{if(p!=null){s.Transitions.Add(new
 Transition((char)min,(char)max,p));}p=t.To;min=t.Min;max=t.Max;}}if(p!=null){s.Transitions.Add(new Transition((char)min,(char)max,p));}}this.ClearHashCode();
}/// <summary>
/// Removes transitions to dead states and calls Reduce() and ClearHashCode().
/// (A state is "dead" if no accept state is reachable from it).
/// </summary>
public void RemoveDeadTransitions(){this.ClearHashCode();if(this.IsSingleton){return;} var states=new HashSet<State>(this.GetStates().Where(state=>state
!=null));var live=this.GetLiveStates(states);foreach(State s in states){var st=s.Transitions;s.ResetTransitions();foreach(Transition t in st){ if(t.To
==null){continue;}if(live.Contains(t.To)){s.Transitions.Add(t);}}}this.Reduce();}public Automaton Repeat(int min,int max){return BasicOperations.Repeat(this,
min,max);}public Automaton Repeat(){return BasicOperations.Repeat(this);}public Automaton Repeat(int min){return BasicOperations.Repeat(this,min);}public
 bool Run(string s){return BasicOperations.Run(this,s);}/// <summary>
/// Adds transitions to explicit crash state to ensure that transition function is total.
/// </summary>
public void Totalize(){var s=new State();s.Transitions.Add(new Transition(char.MinValue,char.MaxValue,s));foreach(State p in this.GetStates()){int maxi
=char.MinValue;foreach(Transition t in p.GetSortedTransitions(false)){if(t.Min>maxi){p.Transitions.Add(new Transition((char)maxi,(char)(t.Min-1),s));}
if(t.Max+1>maxi){maxi=t.Max+1;}}if(maxi<=char.MaxValue){p.Transitions.Add(new Transition((char)maxi,char.MaxValue,s));}}}private HashSet<State>GetLiveStates(HashSet<State>
states){var dictionary=states.ToDictionary(s=>s,s=>new HashSet<State>());foreach(State s in states){foreach(Transition t in s.Transitions){ if(t.To==null)
{continue;}dictionary[t.To].Add(s);}}var comparer=new StateEqualityComparer();var live=new HashSet<State>(this.GetAcceptStates(),comparer);var worklist
=new LinkedList<State>(live);while(worklist.Count>0){State s=worklist.RemoveAndReturnFirst();foreach(State p in dictionary[s]){if(!live.Contains(p)){live.Add(p);
worklist.AddLast(p);}}}return live;}}}namespace Fare{public static class BasicAutomata{/// <summary>
/// Returns a new (deterministic) automaton that accepts any single character.
/// </summary>
/// <returns>A new (deterministic) automaton that accepts any single character.</returns>
public static Automaton MakeAnyChar(){return BasicAutomata.MakeCharRange(char.MinValue,char.MaxValue);}/// <summary>
/// Returns a new (deterministic) automaton that accepts all strings.
/// </summary>
/// <returns>
/// A new (deterministic) automaton that accepts all strings.
/// </returns>
public static Automaton MakeAnyString(){var state=new State();state.Accept=true;state.Transitions.Add(new Transition(char.MinValue,char.MaxValue,state));
var a=new Automaton();a.Initial=state;a.IsDeterministic=true;return a;}/// <summary>
/// Returns a new (deterministic) automaton that accepts a single character of the given value.
/// </summary>
/// <param name="c">The c.</param>
/// <returns>A new (deterministic) automaton that accepts a single character of the given value.</returns>
public static Automaton MakeChar(char c){var a=new Automaton();a.Singleton=c.ToString();a.IsDeterministic=true;return a;}/// <summary>
/// Returns a new (deterministic) automaton that accepts a single char whose value is in the
/// given interval (including both end points).
/// </summary>
/// <param name="min">The min.</param>
/// <param name="max">The max.</param>
/// <returns>
/// A new (deterministic) automaton that accepts a single char whose value is in the
/// given interval (including both end points).
/// </returns>
public static Automaton MakeCharRange(char min,char max){if(min==max){return BasicAutomata.MakeChar(min);}var a=new Automaton();var s1=new State();var
 s2=new State();a.Initial=s1;s2.Accept=true;if(min<=max){s1.Transitions.Add(new Transition(min,max,s2));}a.IsDeterministic=true;return a;}/// <summary>
/// Returns a new (deterministic) automaton with the empty language.
/// </summary>
/// <returns>
/// A new (deterministic) automaton with the empty language.
/// </returns>
public static Automaton MakeEmpty(){var a=new Automaton();var s=new State();a.Initial=s;a.IsDeterministic=true;return a;}/// <summary>
/// Returns a new (deterministic) automaton that accepts only the empty string.
/// </summary>
/// <returns>
/// A new (deterministic) automaton that accepts only the empty string.
/// </returns>
public static Automaton MakeEmptyString(){var a=new Automaton();a.Singleton=string.Empty;a.IsDeterministic=true;return a;}/// <summary>
/// Returns a new automaton that accepts strings representing decimal non-negative integers in
/// the given interval.
/// </summary>
/// <param name="min">The minimum value of interval.</param>
/// <param name="max">The maximum value of inverval (both end points are included in the 
/// interval).</param>
/// <param name="digits">If f >0, use fixed number of digits (strings must be prefixed by 0's 
/// to obtain the right length) otherwise, the number of digits is not fixed.</param>
/// <returns>A new automaton that accepts strings representing decimal non-negative integers 
/// in the given interval.</returns>
public static Automaton MakeInterval(int min,int max,int digits){var a=new Automaton();string x=Convert.ToString(min);string y=Convert.ToString(max);if
(min>max||(digits>0&&y.Length>digits)){throw new ArgumentException();}int d=digits>0?digits:y.Length;var bx=new StringBuilder();for(int i=x.Length;i<d;
i++){bx.Append('0');}bx.Append(x);x=bx.ToString();var by=new StringBuilder();for(int i=y.Length;i<d;i++){by.Append('0');}by.Append(y);y=by.ToString();
ICollection<State>initials=new List<State>();a.Initial=BasicAutomata.Between(x,y,0,initials,digits<=0);if(digits<=0){List<StatePair>pairs=(from p in initials
 where a.Initial!=p select new StatePair(a.Initial,p)).ToList();a.AddEpsilons(pairs);a.Initial.AddTransition(new Transition('0',a.Initial));a.IsDeterministic
=false;}else{a.IsDeterministic=true;}a.CheckMinimizeAlways();return a;}/// <summary>
/// Returns a new (deterministic) automaton that accepts the single given string.
/// </summary>
/// <param name="s">The string.</param>
/// <returns>A new (deterministic) automaton that accepts the single given string.</returns>
public static Automaton MakeString(string s){var a=new Automaton();a.Singleton=s;a.IsDeterministic=true;return a;}/// <summary>
/// Constructs sub-automaton corresponding to decimal numbers of length x.Substring(n).Length.
/// </summary>
/// <param name="x">The x.</param>
/// <param name="n">The n.</param>
/// <returns></returns>
private static State AnyOfRightLength(string x,int n){var s=new State();if(x.Length==n){s.Accept=true;}else{s.AddTransition(new Transition('0','9',AnyOfRightLength(x,
n+1)));}return s;}/// <summary>
/// Constructs sub-automaton corresponding to decimal numbers of value at least x.Substring(n)
/// and length x.Substring(n).Length.
/// </summary>
/// <param name="x">The x.</param>
/// <param name="n">The n.</param>
/// <param name="initials">The initials.</param>
/// <param name="zeros">if set to <c>true</c> [zeros].</param>
/// <returns></returns>
private static State AtLeast(string x,int n,ICollection<State>initials,bool zeros){var s=new State();if(x.Length==n){s.Accept=true;}else{if(zeros){initials.Add(s);
}char c=x[n];s.AddTransition(new Transition(c,AtLeast(x,n+1,initials,zeros&&c=='0')));if(c<'9'){s.AddTransition(new Transition((char)(c+1),'9',AnyOfRightLength(x,
n+1)));}}return s;}/// <summary>
/// Constructs sub-automaton corresponding to decimal numbers of value at most x.Substring(n)
/// and length x.Substring(n).Length.
/// </summary>
/// <param name="x">The x.</param>
/// <param name="n">The n.</param>
/// <returns></returns>
private static State AtMost(string x,int n){var s=new State();if(x.Length==n){s.Accept=true;}else{char c=x[n];s.AddTransition(new Transition(c,AtMost(x,
(char)n+1)));if(c>'0'){s.AddTransition(new Transition('0',(char)(c-1),AnyOfRightLength(x,n+1)));}}return s;}/// <summary>
/// Constructs sub-automaton corresponding to decimal numbers of value between x.Substring(n)
/// and y.Substring(n) and of length x.Substring(n).Length (which must be equal to 
/// y.Substring(n).Length).
/// </summary>
/// <param name="x">The x.</param>
/// <param name="y">The y.</param>
/// <param name="n">The n.</param>
/// <param name="initials">The initials.</param>
/// <param name="zeros">if set to <c>true</c> [zeros].</param>
/// <returns></returns>
private static State Between(string x,string y,int n,ICollection<State>initials,bool zeros){var s=new State();if(x.Length==n){s.Accept=true;}else{if(zeros)
{initials.Add(s);}char cx=x[n];char cy=y[n];if(cx==cy){s.AddTransition(new Transition(cx,Between(x,y,n+1,initials,zeros&&cx=='0')));}else{ s.AddTransition(new
 Transition(cx,BasicAutomata.AtLeast(x,n+1,initials,zeros&&cx=='0')));s.AddTransition(new Transition(cy,BasicAutomata.AtMost(y,n+1)));if(cx+1<cy){s.AddTransition(new
 Transition((char)(cx+1),(char)(cy-1),BasicAutomata.AnyOfRightLength(x,n+1)));}}}return s;}/// <summary>
/// Returns a new (deterministic) automaton that accepts a single character in the given set.
/// </summary>
/// <param name="set">The set.</param>
/// <returns></returns>
public static Automaton MakeCharSet(string set){if(set.Length==1){return MakeChar(set[0]);}var a=new Automaton();var s1=new State();var s2=new State();
a.Initial=s1;s2.Accept=true;foreach(char t in set){s1.Transitions.Add(new Transition(t,s2));}a.IsDeterministic=true;a.Reduce();return a;}/// <summary>
/// Returns a new (deterministic and minimal) automaton that accepts the union of the given
/// set of strings. The input character sequences are internally sorted in-place, so the 
/// input array is modified. @see StringUnionOperations.
/// </summary>
/// <param name="strings">The strings.</param>
/// <returns></returns>
public static Automaton MakeStringUnion(params char[][]strings){if(strings.Length==0){return MakeEmpty();}Array.Sort(strings,StringUnionOperations.LexicographicOrderComparer);
var a=new Automaton();a.Initial=StringUnionOperations.Build(strings);a.IsDeterministic=true;a.Reduce();a.RecomputeHashCode();return a;}/// <summary>
/// Constructs automaton that accept strings representing nonnegative integer that are not 
/// larger than the given value.
/// </summary>
/// <param name="n">The n string representation of maximum value.</param>
/// <returns></returns>
public static Automaton MakeMaxInteger(String n){int i=0;while(i<n.Length&&n[i]=='0'){i++;}var b=new StringBuilder();b.Append("0*(0|");if(i<n.Length){
b.Append("[0-9]{1,"+(n.Length-i-1)+"}|");}MaxInteger(n.Substring(i),0,b);b.Append(")");return Automaton.Minimize((new RegExp(b.ToString())).ToAutomaton());
}private static void MaxInteger(String n,int i,StringBuilder b){b.Append('(');if(i<n.Length){char c=n[i];if(c!='0'){b.Append("[0-"+(char)(c-1)+"][0-9]{"
+(n.Length-i-1)+"}|");}b.Append(c);MaxInteger(n,i+1,b);}b.Append(')');}/// <summary>
/// Constructs automaton that accept strings representing nonnegative integers that are not
/// less that the given value.
/// </summary>
/// <param name="n">The n string representation of minimum value.</param>
/// <returns></returns>
public static Automaton MakeMinInteger(String n){int i=0;while(i+1<n.Length&&n[i]=='0'){i++;}var b=new StringBuilder();b.Append("0*");MinInteger(n.Substring(i),
0,b);b.Append("[0-9]*");return Automaton.Minimize((new RegExp(b.ToString())).ToAutomaton());}private static void MinInteger(String n,int i,StringBuilder
 b){b.Append('(');if(i<n.Length){char c=n[i];if(c!='9'){b.Append("["+(char)(c+1)+"-9][0-9]{"+(n.Length-i-1)+"}|");}b.Append(c);MinInteger(n,i+1,b);}b.Append(')');
}/// <summary>
/// Constructs automaton that accept strings representing decimal numbers that can be 
/// written with at most the given number of digits. Surrounding whitespace is permitted.
/// </summary>
/// <param name="i">The i max number of necessary digits.</param>
/// <returns></returns>
public static Automaton MakeTotalDigits(int i){return Automaton.Minimize(new RegExp("[ \t\n\r]*[-+]?0*([0-9]{0,"+i+"}|((([0-9]\\.*){0,"+i+"})&@\\.@)0*)[ \t\n\r]*")
.ToAutomaton());}/// <summary>
/// Constructs automaton that accept strings representing decimal numbers that can be 
/// written with at most the given number of digits in the fraction part. Surrounding
/// whitespace is permitted.
/// </summary>
/// <param name="i">The i max number of necessary fraction digits.</param>
/// <returns></returns>
public static Automaton MakeFractionDigits(int i){return Automaton.Minimize(new RegExp("[ \t\n\r]*[-+]?[0-9]+(\\.[0-9]{0,"+i+"}0*)?[ \t\n\r]*").ToAutomaton());
}/// <summary>
/// Constructs automaton that accept strings representing the given integer. Surrounding 
/// whitespace is permitted.
/// </summary>
/// <param name="value">The value string representation of integer.</param>
/// <returns></returns>
public static Automaton MakeIntegerValue(String value){bool minus=false;int i=0;while(i<value.Length){char c=value[i];if(c=='-'){minus=true;}if(c>='1'
&&c<='9'){break;}i++;}var b=new StringBuilder();b.Append(value.Substring(i));if(b.Length==0){b.Append("0");}Automaton s=minus?Automaton.MakeChar('-'):
Automaton.MakeChar('+').Optional();Automaton ws=Datatypes.WhitespaceAutomaton;return Automaton.Minimize(ws.Concatenate(s.Concatenate(Automaton.MakeChar('0').Repeat())
.Concatenate(Automaton.MakeString(b.ToString()))).Concatenate(ws));}/// <summary>
/// Constructs automaton that accept strings representing the given decimal number.
/// Surrounding whitespace is permitted.
/// </summary>
/// <param name="value">The value string representation of decimal number.</param>
/// <returns></returns>
public static Automaton MakeDecimalValue(String value){bool minus=false;int i=0;while(i<value.Length){char c=value[i];if(c=='-'){minus=true;}if((c>='1'
&&c<='9')||c=='.'){break;}i++;}var b1=new StringBuilder();var b2=new StringBuilder();int p=value.IndexOf('.',i);if(p==-1){b1.Append(value.Substring(i));
}else{b1.Append(value.Substring(i,p-i));i=value.Length-1;while(i>p){char c=value[i];if(c>='1'&&c<='9'){break;}i--;}b2.Append(value.Substring(p+1,i+1-(p
+1)));}if(b1.Length==0){b1.Append("0");}Automaton s=minus?Automaton.MakeChar('-'):Automaton.MakeChar('+').Optional();Automaton d;if(b2.Length==0){d=Automaton.MakeChar('.').Concatenate(Automaton.MakeChar('0').Repeat(1)).Optional();
}else{d=Automaton.MakeChar('.').Concatenate(Automaton.MakeString(b2.ToString())).Concatenate(Automaton.MakeChar('0').Repeat());}Automaton ws=Datatypes.WhitespaceAutomaton;
return Automaton.Minimize(ws.Concatenate(s.Concatenate(Automaton.MakeChar('0').Repeat()).Concatenate(Automaton.MakeString(b1.ToString())).Concatenate(d))
.Concatenate(ws));}/// <summary>
/// Constructs deterministic automaton that matches strings that contain the given substring.
/// </summary>
/// <param name="s">The s.</param>
/// <returns></returns>
public static Automaton MakeStringMatcher(String s){var a=new Automaton();var states=new State[s.Length+1];states[0]=a.Initial;for(int i=0;i<s.Length;
i++){states[i+1]=new State();}State f=states[s.Length];f.Accept=true;f.Transitions.Add(new Transition(Char.MinValue,Char.MaxValue,f));for(int i=0;i<s.Length;
i++){var done=new HashSet<char?>();char c=s[i];states[i].Transitions.Add(new Transition(c,states[i+1]));done.Add(c);for(int j=i;j>=1;j--){char d=s[j-1];
if(!done.Contains(d)&&s.Substring(0,j-1).Equals(s.Substring(i-j+1,i-(i-j+1)))){states[i].Transitions.Add(new Transition(d,states[j]));done.Add(d);}}var
 da=new char[done.Count];int h=0;foreach(char w in done){da[h++]=w;}Array.Sort(da);int from=Char.MinValue;int k=0;while(from<=Char.MaxValue){while(k<da.Length
&&da[k]==from){k++;from++;}if(from<=Char.MaxValue){int to=Char.MaxValue;if(k<da.Length){to=da[k]-1;k++;}states[i].Transitions.Add(new Transition((char)from,
(char)to,states[0]));from=to+2;}}}a.IsDeterministic=true;return a;}}}namespace Fare{public static class BasicOperations{/// <summary>
/// Adds epsilon transitions to the given automaton. This method adds extra character interval
/// transitions that are equivalent to the given set of epsilon transitions.
/// </summary>
/// <param name="a">The automaton.</param>
/// <param name="pairs">A collection of <see cref="StatePair"/> objects representing pairs of
/// source/destination states where epsilon transitions should be added.</param>
public static void AddEpsilons(Automaton a,ICollection<StatePair>pairs){a.ExpandSingleton();var forward=new Dictionary<State,HashSet<State>>();var back
=new Dictionary<State,HashSet<State>>();foreach(StatePair p in pairs){HashSet<State>to=forward[p.FirstState];if(to==null){to=new HashSet<State>();forward.Add(p.FirstState,
to);}to.Add(p.SecondState);HashSet<State>from=back[p.SecondState];if(from==null){from=new HashSet<State>();back.Add(p.SecondState,from);}from.Add(p.FirstState);
}var worklist=new LinkedList<StatePair>(pairs);var workset=new HashSet<StatePair>(pairs);while(worklist.Count!=0){StatePair p=worklist.RemoveAndReturnFirst();
workset.Remove(p);HashSet<State>to=forward[p.SecondState];HashSet<State>from=back[p.FirstState];if(to!=null){foreach(State s in to){var pp=new StatePair(p.FirstState,
s);if(!pairs.Contains(pp)){pairs.Add(pp);forward[p.FirstState].Add(s);back[s].Add(p.FirstState);worklist.AddLast(pp);workset.Add(pp);if(from!=null){foreach
(State q in from){var qq=new StatePair(q,p.FirstState);if(!workset.Contains(qq)){worklist.AddLast(qq);workset.Add(qq);}}}}}}} foreach(StatePair p in pairs)
{p.FirstState.AddEpsilon(p.SecondState);}a.IsDeterministic=false;a.ClearHashCode();a.CheckMinimizeAlways();}/// <summary>
/// Returns an automaton that accepts the union of the languages of the given automata.
/// </summary>
/// <param name="automatons">The l.</param>
/// <returns>
/// An automaton that accepts the union of the languages of the given automata.
/// </returns>
/// <remarks>
/// Complexity: linear in number of states.
/// </remarks>
public static Automaton Union(IList<Automaton>automatons){var ids=new HashSet<int>();foreach(Automaton a in automatons){ids.Add(RuntimeHelpers.GetHashCode(a));
}bool hasAliases=ids.Count!=automatons.Count;var s=new State();foreach(Automaton b in automatons){if(b.IsEmpty){continue;}Automaton bb=b;bb=hasAliases
?bb.CloneExpanded():bb.CloneExpandedIfRequired();s.AddEpsilon(bb.Initial);}var automaton=new Automaton();automaton.Initial=s;automaton.IsDeterministic
=false;automaton.ClearHashCode();automaton.CheckMinimizeAlways();return automaton;}/// <summary>
/// Returns a (deterministic) automaton that accepts the complement of the language of the 
/// given automaton.
/// </summary>
/// <param name="a">The automaton.</param>
/// <returns>A (deterministic) automaton that accepts the complement of the language of the 
/// given automaton.</returns>
/// <remarks>
/// Complexity: linear in number of states (if already deterministic).
/// </remarks>
public static Automaton Complement(Automaton a){a=a.CloneExpandedIfRequired();a.Determinize();a.Totalize();foreach(State p in a.GetStates()){p.Accept=
!p.Accept;}a.RemoveDeadTransitions();return a;}public static Automaton Concatenate(Automaton a1,Automaton a2){if(a1.IsSingleton&&a2.IsSingleton){return
 BasicAutomata.MakeString(a1.Singleton+a2.Singleton);}if(BasicOperations.IsEmpty(a1)||BasicOperations.IsEmpty(a2)){return BasicAutomata.MakeEmpty();}bool
 deterministic=a1.IsSingleton&&a2.IsDeterministic;if(a1==a2){a1=a1.CloneExpanded();a2=a2.CloneExpanded();}else{a1=a1.CloneExpandedIfRequired();a2=a2.CloneExpandedIfRequired();
}foreach(State s in a1.GetAcceptStates()){s.Accept=false;s.AddEpsilon(a2.Initial);}a1.IsDeterministic=deterministic;a1.ClearHashCode();a1.CheckMinimizeAlways();
return a1;}public static Automaton Concatenate(IList<Automaton>l){if(l.Count==0){return BasicAutomata.MakeEmptyString();}bool allSingleton=l.All(a=>a.IsSingleton);
if(allSingleton){var b=new StringBuilder();foreach(Automaton a in l){b.Append(a.Singleton);}return BasicAutomata.MakeString(b.ToString());}else{if(l.Any(a
=>a.IsEmpty)){return BasicAutomata.MakeEmpty();}var ids=new HashSet<int>();foreach(Automaton a in l){ids.Add(RuntimeHelpers.GetHashCode(a));}bool hasAliases
=ids.Count!=l.Count;Automaton b=l[0];b=hasAliases?b.CloneExpanded():b.CloneExpandedIfRequired();var ac=b.GetAcceptStates();bool first=true;foreach(Automaton
 a in l){if(first){first=false;}else{if(a.IsEmptyString()){continue;}Automaton aa=a;aa=hasAliases?aa.CloneExpanded():aa.CloneExpandedIfRequired();HashSet<State>
ns=aa.GetAcceptStates();foreach(State s in ac){s.Accept=false;s.AddEpsilon(aa.Initial);if(s.Accept){ns.Add(s);}}ac=ns;}}b.IsDeterministic=false;b.ClearHashCode();
b.CheckMinimizeAlways();return b;}}/// <summary>
/// Determinizes the specified automaton.
/// </summary>
/// <remarks>
/// Complexity: exponential in number of states.
/// </remarks>
/// <param name="a">The automaton.</param>
public static void Determinize(Automaton a){if(a.IsDeterministic||a.IsSingleton){return;}var initialset=new HashSet<State>();initialset.Add(a.Initial);
BasicOperations.Determinize(a,initialset.ToList());}/// <summary>
/// Determinizes the given automaton using the given set of initial states.
/// </summary>
/// <param name="a">The automaton.</param>
/// <param name="initialset">The initial states.</param>
public static void Determinize(Automaton a,List<State>initialset){char[]points=a.GetStartPoints();var comparer=new ListEqualityComparer<State>(); var sets
=new Dictionary<List<State>,List<State>>(comparer);var worklist=new LinkedList<List<State>>();var newstate=new Dictionary<List<State>,State>(comparer);
sets.Add(initialset,initialset);worklist.AddLast(initialset);a.Initial=new State();newstate.Add(initialset,a.Initial);while(worklist.Count>0){List<State>
s=worklist.RemoveAndReturnFirst();State r;newstate.TryGetValue(s,out r);foreach(State q in s){if(q.Accept){r.Accept=true;break;}}for(int n=0;n<points.Length;
n++){var set=new HashSet<State>();foreach(State c in s)foreach(Transition t in c.Transitions)if(t.Min<=points[n]&&points[n]<=t.Max)set.Add(t.To);var p
=set.ToList();if(!sets.ContainsKey(p)){sets.Add(p,p);worklist.AddLast(p);System.Console.WriteLine("Automaton - add state");newstate.Add(p,new State());
}State q;newstate.TryGetValue(p,out q);char min=points[n];char max;if(n+1<points.Length){max=(char)(points[n+1]-1);}else{max=char.MaxValue;} r.Transitions.Add(new
 Transition(min,max,q));}}a.IsDeterministic=true;a.RemoveDeadTransitions();return;}/// <summary>
/// Determines whether the given automaton accepts no strings.
/// </summary>
/// <param name="a">The automaton.</param>
/// <returns>
///   <c>true</c> if the given automaton accepts no strings; otherwise, <c>false</c>.
/// </returns>
public static bool IsEmpty(Automaton a){if(a.IsSingleton){return false;}return!a.Initial.Accept&&a.Initial.Transitions.Count==0;}/// <summary>
/// Determines whether the given automaton accepts the empty string and nothing else.
/// </summary>
/// <param name="a">The automaton.</param>
/// <returns>
///   <c>true</c> if the given automaton accepts the empty string and nothing else; otherwise,
/// <c>false</c>.
/// </returns>
public static bool IsEmptyString(Automaton a){if(a.IsSingleton){return a.Singleton.Length==0;}return a.Initial.Accept&&a.Initial.Transitions.Count==0;
}/// <summary>
/// Returns an automaton that accepts the intersection of the languages of the given automata.
/// Never modifies the input automata languages.
/// </summary>
/// <param name="a1">The a1.</param>
/// <param name="a2">The a2.</param>
/// <returns></returns>
public static Automaton Intersection(Automaton a1,Automaton a2){if(a1.IsSingleton){if(a2.Run(a1.Singleton)){return a1.CloneIfRequired();}return BasicAutomata.MakeEmpty();
}if(a2.IsSingleton){if(a1.Run(a2.Singleton)){return a2.CloneIfRequired();}return BasicAutomata.MakeEmpty();}if(a1==a2){return a1.CloneIfRequired();}Transition[][]
transitions1=Automaton.GetSortedTransitions(a1.GetStates());Transition[][]transitions2=Automaton.GetSortedTransitions(a2.GetStates());var c=new Automaton();
var worklist=new LinkedList<StatePair>();var newstates=new Dictionary<StatePair,StatePair>();var p=new StatePair(c.Initial,a1.Initial,a2.Initial);worklist.AddLast(p);
newstates.Add(p,p);while(worklist.Count>0){p=worklist.RemoveAndReturnFirst();p.S.Accept=p.FirstState.Accept&&p.SecondState.Accept;Transition[]t1=transitions1[p.FirstState.Number];
Transition[]t2=transitions2[p.SecondState.Number];for(int n1=0,b2=0;n1<t1.Length;n1++){while(b2<t2.Length&&t2[b2].Max<t1[n1].Min){b2++;}for(int n2=b2;
n2<t2.Length&&t1[n1].Max>=t2[n2].Min;n2++){if(t2[n2].Max>=t1[n1].Min){var q=new StatePair(t1[n1].To,t2[n2].To);StatePair r;newstates.TryGetValue(q,out
 r);if(r==null){q.S=new State();worklist.AddLast(q);newstates.Add(q,q);r=q;}char min=t1[n1].Min>t2[n2].Min?t1[n1].Min:t2[n2].Min;char max=t1[n1].Max<t2[n2].Max
?t1[n1].Max:t2[n2].Max;p.S.Transitions.Add(new Transition(min,max,r.S));}}}}c.IsDeterministic=a1.IsDeterministic&&a2.IsDeterministic;c.RemoveDeadTransitions();
c.CheckMinimizeAlways();return c;}/// <summary>
/// Returns an automaton that accepts the union of the empty string and the language of the 
/// given automaton.
/// </summary>
/// <param name="a">The automaton.</param>
/// <remarks>
/// Complexity: linear in number of states.
/// </remarks>
/// <returns>An automaton that accepts the union of the empty string and the language of the 
/// given automaton.</returns>
public static Automaton Optional(Automaton a){a=a.CloneExpandedIfRequired();var s=new State();s.AddEpsilon(a.Initial);s.Accept=true;a.Initial=s;a.IsDeterministic
=false;a.ClearHashCode();a.CheckMinimizeAlways();return a;}/// <summary>
/// Accepts the Kleene star (zero or more concatenated repetitions) of the language of the
/// given automaton. Never modifies the input automaton language.
/// </summary>
/// <param name="a">The automaton.</param>
/// <returns>
/// An automaton that accepts the Kleene star (zero or more concatenated repetitions)
/// of the language of the given automaton. Never modifies the input automaton language.
/// </returns>
/// <remarks>
/// Complexity: linear in number of states.
/// </remarks>
public static Automaton Repeat(Automaton a){a=a.CloneExpanded();var s=new State();s.Accept=true;s.AddEpsilon(a.Initial);foreach(State p in a.GetAcceptStates())
{p.AddEpsilon(s);}a.Initial=s;a.IsDeterministic=false;a.ClearHashCode();a.CheckMinimizeAlways();return a;}/// <summary>
/// Accepts <code>min</code> or more concatenated repetitions of the language of the given 
/// automaton.
/// </summary>
/// <param name="a">The automaton.</param>
/// <param name="min">The minimum concatenated repetitions of the language of the given 
/// automaton.</param>
/// <returns>Returns an automaton that accepts <code>min</code> or more concatenated 
/// repetitions of the language of the given automaton.
/// </returns>
/// <remarks>
/// Complexity: linear in number of states and in <code>min</code>.
/// </remarks>
public static Automaton Repeat(Automaton a,int min){if(min==0){return BasicOperations.Repeat(a);}var@as=new List<Automaton>();while(min-->0){@as.Add(a);
}@as.Add(BasicOperations.Repeat(a));return BasicOperations.Concatenate(@as);}/// <summary>
/// Accepts between <code>min</code> and <code>max</code> (including both) concatenated
/// repetitions of the language of the given automaton.
/// </summary>
/// <param name="a">The automaton.</param>
/// <param name="min">The minimum concatenated repetitions of the language of the given
/// automaton.</param>
/// <param name="max">The maximum concatenated repetitions of the language of the given
/// automaton.</param>
/// <returns>
/// Returns an automaton that accepts between <code>min</code> and <code>max</code>
/// (including both) concatenated repetitions of the language of the given automaton.
/// </returns>
/// <remarks>
/// Complexity: linear in number of states and in <code>min</code> and <code>max</code>.
/// </remarks>
public static Automaton Repeat(Automaton a,int min,int max){if(min>max){return BasicAutomata.MakeEmpty();}max-=min;a.ExpandSingleton();Automaton b;if(min
==0){b=BasicAutomata.MakeEmptyString();}else if(min==1){b=a.Clone();}else{var@as=new List<Automaton>();while(min-->0){@as.Add(a);}b=BasicOperations.Concatenate(@as);
}if(max>0){Automaton d=a.Clone();while(--max>0){Automaton c=a.Clone();foreach(State p in c.GetAcceptStates()){p.AddEpsilon(d.Initial);}d=c;}foreach(State
 p in b.GetAcceptStates()){p.AddEpsilon(d.Initial);}b.IsDeterministic=false;b.ClearHashCode();b.CheckMinimizeAlways();}return b;}/// <summary>
/// Returns true if the given string is accepted by the automaton.
/// </summary>
/// <param name="a">The automaton.</param>
/// <param name="s">The string.</param>
/// <returns></returns>
/// <remarks>
/// Complexity: linear in the length of the string.
/// For full performance, use the RunAutomaton class.
/// </remarks>
public static bool Run(Automaton a,string s){if(a.IsSingleton){return s.Equals(a.Singleton);}if(a.IsDeterministic){State p=a.Initial;foreach(char t in
 s){State q=p.Step(t);if(q==null){return false;}p=q;}return p.Accept;}HashSet<State>states=a.GetStates();Automaton.SetStateNumbers(states);var pp=new LinkedList<State>();
var ppOther=new LinkedList<State>();var bb=new BitArray(states.Count);var bbOther=new BitArray(states.Count);pp.AddLast(a.Initial);var dest=new List<State>();
bool accept=a.Initial.Accept;foreach(char c in s){accept=false;ppOther.Clear();bbOther.SetAll(false);foreach(State p in pp){dest.Clear();p.Step(c,dest);
foreach(State q in dest){if(q.Accept){accept=true;}if(!bbOther.Get(q.Number)){bbOther.Set(q.Number,true);ppOther.AddLast(q);}}}LinkedList<State>tp=pp;
pp=ppOther;ppOther=tp;BitArray tb=bb;bb=bbOther;bbOther=tb;}return accept;}}}namespace Fare{public static class Datatypes{private static readonly Automaton
 ws=Automaton.Minimize(Automaton.MakeCharSet(" \t\n\r").Repeat());public static Automaton WhitespaceAutomaton{get{return ws;}}}}namespace Fare{public interface
 IAutomatonProvider{Automaton GetAutomaton(string name);}}namespace Fare{internal static class LinkedListExtensions{public static T RemoveAndReturnFirst<T>(this
 LinkedList<T>linkedList){T first=linkedList.First.Value;linkedList.RemoveFirst();return first;}}}namespace Fare{internal sealed class ListEqualityComparer<T>
:IEqualityComparer<List<T>>,IEquatable<ListEqualityComparer<T>>{/// <summary>
/// Implements the operator ==.
/// </summary>
/// <param name="left">The left.</param>
/// <param name="right">The right.</param>
/// <returns>
/// The result of the operator.
/// </returns>
public static bool operator==(ListEqualityComparer<T>left,ListEqualityComparer<T>right){return object.Equals(left,right);}/// <summary>
/// Implements the operator !=.
/// </summary>
/// <param name="left">The left.</param>
/// <param name="right">The right.</param>
/// <returns>
/// The result of the operator.
/// </returns>
public static bool operator!=(ListEqualityComparer<T>left,ListEqualityComparer<T>right){return!object.Equals(left,right);}/// <inheritdoc />
public bool Equals(List<T>x,List<T>y){if(x.Count!=y.Count){return false;}return x.SequenceEqual(y);}/// <inheritdoc />
public int GetHashCode(List<T>obj){ return obj.Aggregate(17,(current,item)=>(current*31)+item.GetHashCode());}/// <inheritdoc />
public bool Equals(ListEqualityComparer<T>other){return!object.ReferenceEquals(null,other);}/// <inheritdoc />
public override bool Equals(object obj){if(object.ReferenceEquals(null,obj)){return false;}if(object.ReferenceEquals(this,obj)){return true;}if(obj.GetType()
!=typeof(ListEqualityComparer<T>)){return false;}return this.Equals((ListEqualityComparer<T>)obj);}/// <inheritdoc />
public override int GetHashCode(){return base.GetHashCode();}}}namespace Fare{public static class MinimizationOperations{/// <summary>
/// Minimizes (and determinizes if not already deterministic) the given automaton.
/// </summary>
/// <param name="a">The automaton.</param>
public static void Minimize(Automaton a){if(!a.IsSingleton){switch(Automaton.Minimization){case Automaton.MinimizeHuffman:MinimizationOperations.MinimizeHuffman(a);
break;case Automaton.MinimizeBrzozowski:MinimizationOperations.MinimizeBrzozowski(a);break;default:MinimizationOperations.MinimizeHopcroft(a);break;}}
a.RecomputeHashCode();}/// <summary>
/// Minimizes the given automaton using Brzozowski's algorithm.
/// </summary>
/// <param name="a">The automaton.</param>
public static void MinimizeBrzozowski(Automaton a){if(a.IsSingleton){return;}BasicOperations.Determinize(a,SpecialOperations.Reverse(a).ToList());BasicOperations.Determinize(a,
SpecialOperations.Reverse(a).ToList());}public static void MinimizeHopcroft(Automaton a){a.Determinize();IList<Transition>tr=a.Initial.Transitions;if(tr.Count
==1){Transition t=tr[0];if(t.To==a.Initial&&t.Min==char.MinValue&&t.Max==char.MaxValue){return;}}a.Totalize(); HashSet<State>ss=a.GetStates();var states
=new State[ss.Count];int number=0;foreach(State q in ss){states[number]=q;q.Number=number++;}char[]sigma=a.GetStartPoints(); var reverse=new List<List<LinkedList<State>>>();
foreach(State s in states){var v=new List<LinkedList<State>>();Initialize(ref v,sigma.Length);reverse.Add(v);}var reverseNonempty=new bool[states.Length,
sigma.Length];var partition=new List<LinkedList<State>>();Initialize(ref partition,states.Length);var block=new int[states.Length];var active=new StateList[states.Length,
sigma.Length];var active2=new StateListNode[states.Length,sigma.Length];var pending=new LinkedList<IntPair>();var pending2=new bool[sigma.Length,states.Length];
var split=new List<State>();var split2=new bool[states.Length];var refine=new List<int>();var refine2=new bool[states.Length];var splitblock=new List<List<State>>();
Initialize(ref splitblock,states.Length);for(int q=0;q<states.Length;q++){splitblock[q]=new List<State>();partition[q]=new LinkedList<State>();for(int
 x=0;x<sigma.Length;x++){reverse[q][x]=new LinkedList<State>();active[q,x]=new StateList();}} foreach(State qq in states){int j=qq.Accept?0:1;partition[j].AddLast(qq);
block[qq.Number]=j;for(int x=0;x<sigma.Length;x++){char y=sigma[x];State p=qq.Step(y);reverse[p.Number][x].AddLast(qq);reverseNonempty[p.Number,x]=true;
}} for(int j=0;j<=1;j++){for(int x=0;x<sigma.Length;x++){foreach(State qq in partition[j]){if(reverseNonempty[qq.Number,x]){active2[qq.Number,x]=active[j,
x].Add(qq);}}}} for(int x=0;x<sigma.Length;x++){int a0=active[0,x].Size;int a1=active[1,x].Size;int j=a0<=a1?0:1;pending.AddLast(new IntPair(j,x));pending2[x,
j]=true;} int k=2;while(pending.Count>0){IntPair ip=pending.RemoveAndReturnFirst();int p=ip.N1;int x=ip.N2;pending2[x,p]=false; for(StateListNode m=active[p,
x].First;m!=null;m=m.Next){foreach(State s in reverse[m.State.Number][x]){if(!split2[s.Number]){split2[s.Number]=true;split.Add(s);int j=block[s.Number];
splitblock[j].Add(s);if(!refine2[j]){refine2[j]=true;refine.Add(j);}}}} foreach(int j in refine){if(splitblock[j].Count<partition[j].Count){LinkedList<State>
b1=partition[j];LinkedList<State>b2=partition[k];foreach(State s in splitblock[j]){b1.Remove(s);b2.AddLast(s);block[s.Number]=k;for(int c=0;c<sigma.Length;
c++){StateListNode sn=active2[s.Number,c];if(sn!=null&&sn.StateList==active[j,c]){sn.Remove();active2[s.Number,c]=active[k,c].Add(s);}}} for(int c=0;c
<sigma.Length;c++){int aj=active[j,c].Size;int ak=active[k,c].Size;if(!pending2[c,j]&&0<aj&&aj<=ak){pending2[c,j]=true;pending.AddLast(new IntPair(j,c));
}else{pending2[c,k]=true;pending.AddLast(new IntPair(k,c));}}k++;}foreach(State s in splitblock[j]){split2[s.Number]=false;}refine2[j]=false;splitblock[j].Clear();
}split.Clear();refine.Clear();} var newstates=new State[k];for(int n=0;n<newstates.Length;n++){var s=new State();newstates[n]=s;foreach(State q in partition[n])
{if(q==a.Initial){a.Initial=s;}s.Accept=q.Accept;s.Number=q.Number; q.Number=n;}} foreach(State s in newstates){s.Accept=states[s.Number].Accept;foreach
(Transition t in states[s.Number].Transitions){s.Transitions.Add(new Transition(t.Min,t.Max,newstates[t.To.Number]));}}a.RemoveDeadTransitions();}/// <summary>
/// Minimizes the given automaton using Huffman's algorithm.
/// </summary>
/// <param name="a">The automaton.</param>
public static void MinimizeHuffman(Automaton a){a.Determinize();a.Totalize();HashSet<State>ss=a.GetStates();var transitions=new Transition[ss.Count][];
State[]states=ss.ToArray();var mark=new List<List<bool>>();var triggers=new List<List<HashSet<IntPair>>>();foreach(State t in states){var v=new List<HashSet<IntPair>>();
Initialize(ref v,states.Length);triggers.Add(v);} for(int n1=0;n1<states.Length;n1++){states[n1].Number=n1;transitions[n1]=states[n1].GetSortedTransitions(false).ToArray();
for(int n2=n1+1;n2<states.Length;n2++){if(states[n1].Accept!=states[n2].Accept){mark[n1][n2]=true;}}} for(int n1=0;n1<states.Length;n1++){for(int n2=n1
+1;n2<states.Length;n2++){if(!mark[n1][n2]){if(MinimizationOperations.StatesAgree(transitions,mark,n1,n2)){MinimizationOperations.AddTriggers(transitions,
triggers,n1,n2);}else{MinimizationOperations.MarkPair(mark,triggers,n1,n2);}}}} int numclasses=0;foreach(State t in states){t.Number=-1;}for(int n1=0;
n1<states.Length;n1++){if(states[n1].Number==-1){states[n1].Number=numclasses;for(int n2=n1+1;n2<states.Length;n2++){if(!mark[n1][n2]){states[n2].Number
=numclasses;}}numclasses++;}} var newstates=new State[numclasses];for(int n=0;n<numclasses;n++){newstates[n]=new State();} for(int n=0;n<states.Length;
n++){newstates[states[n].Number].Number=n;if(states[n]==a.Initial){a.Initial=newstates[states[n].Number];}} for(int n=0;n<numclasses;n++){State s=newstates[n];
s.Accept=states[s.Number].Accept;foreach(Transition t in states[s.Number].Transitions){s.Transitions.Add(new Transition(t.Min,t.Max,newstates[t.To.Number]));
}}a.RemoveDeadTransitions();}private static void Initialize<T>(ref List<T>list,int size){for(int i=0;i<size;i++){list.Add(default(T));}}private static
 void AddTriggers(Transition[][]transitions,IList<List<HashSet<IntPair>>>triggers,int n1,int n2){Transition[]t1=transitions[n1];Transition[]t2=transitions[n2];
for(int k1=0,k2=0;k1<t1.Length&&k2<t2.Length;){if(t1[k1].Max<t2[k2].Min){k1++;}else if(t2[k2].Max<t1[k1].Min){k2++;}else{if(t1[k1].To!=t2[k2].To){int m1
=t1[k1].To.Number;int m2=t2[k2].To.Number;if(m1>m2){int t=m1;m1=m2;m2=t;}if(triggers[m1][m2]==null){triggers[m1].Insert(m2,new HashSet<IntPair>());}triggers[m1][m2].Add(new
 IntPair(n1,n2));}if(t1[k1].Max<t2[k2].Max){k1++;}else{k2++;}}}}private static void MarkPair(List<List<bool>>mark,IList<List<HashSet<IntPair>>>triggers,
int n1,int n2){mark[n1][n2]=true;if(triggers[n1][n2]!=null){foreach(IntPair p in triggers[n1][n2]){int m1=p.N1;int m2=p.N2;if(m1>m2){int t=m1;m1=m2;m2
=t;}if(!mark[m1][m2]){MarkPair(mark,triggers,m1,m2);}}}}private static bool StatesAgree(Transition[][]transitions,List<List<bool>>mark,int n1,int n2){
Transition[]t1=transitions[n1];Transition[]t2=transitions[n2];for(int k1=0,k2=0;k1<t1.Length&&k2<t2.Length;){if(t1[k1].Max<t2[k2].Min){k1++;}else if(t2[k2].Max
<t1[k1].Min){k2++;}else{int m1=t1[k1].To.Number;int m2=t2[k2].To.Number;if(m1>m2){int t=m1;m1=m2;m2=t;}if(mark[m1][m2]){return false;}if(t1[k1].Max<t2[k2].Max)
{k1++;}else{k2++;}}}return true;}
#region Nested type: IntPair
private sealed class IntPair{private readonly int n1;private readonly int n2;public IntPair(int n1,int n2){this.n1=n1;this.n2=n2;}public int N1{get{return
 n1;}}public int N2{get{return n2;}}}
#endregion
#region Nested type: StateList
private sealed class StateList{public int Size{get;set;}public StateListNode First{get;set;}public StateListNode Last{get;set;}public StateListNode Add(State
 q){return new StateListNode(q,this);}}
#endregion
#region Nested type: StateListNode
private sealed class StateListNode{public StateListNode(State q,StateList sl){State=q;StateList=sl;if(sl.Size++==0){sl.First=sl.Last=this;}else{sl.Last.Next
=this;Prev=sl.Last;sl.Last=this;}}public StateListNode Next{get;private set;}private StateListNode Prev{get;set;}public StateList StateList{get;private
 set;}public State State{get;private set;}public void Remove(){StateList.Size--;if(StateList.First==this){StateList.First=Next;}else{Prev.Next=Next;}if
(StateList.Last==this){StateList.Last=Prev;}else{Next.Prev=Prev;}}}
#endregion
}}namespace Fare{/// <summary>
/// Regular Expression extension to Automaton.
/// </summary>
public class RegExp{private readonly string b;private readonly RegExpSyntaxOptions flags;private static bool allowMutation;private char c;private int digits;
private RegExp exp1;private RegExp exp2;private char from;private Kind kind;private int max;private int min;private int pos;private string s;private char
 to;/// <summary>
///   Prevents a default instance of the <see cref = "RegExp" /> class from being created.
/// </summary>
private RegExp(){}/// <summary>
///   Initializes a new instance of the <see cref = "RegExp" /> class from a string.
/// </summary>
/// <param name = "s">A string with the regular expression.</param>
public RegExp(string s):this(s,RegExpSyntaxOptions.All){}/// <summary>
///   Initializes a new instance of the <see cref = "RegExp" /> class from a string.
/// </summary>
/// <param name = "s">A string with the regular expression.</param>
/// <param name = "syntaxFlags">Boolean 'or' of optional syntax constructs to be enabled.</param>
public RegExp(string s,RegExpSyntaxOptions syntaxFlags){this.b=s;this.flags=syntaxFlags;RegExp e;if(s.Length==0){e=RegExp.MakeString(string.Empty);}else
{e=this.ParseUnionExp();if(this.pos<b.Length){throw new ArgumentException("end-of-string expected at position "+this.pos);}}this.kind=e.kind;this.exp1
=e.exp1;this.exp2=e.exp2;this.s=e.s;this.c=e.c;this.min=e.min;this.max=e.max;this.digits=e.digits;this.from=e.from;this.to=e.to;this.b=null;}/// <summary>
///   Constructs new <code>Automaton</code> from this <code>RegExp</code>. 
///   Same as <code>toAutomaton(null)</code> (empty automaton map).
/// </summary>
/// <returns></returns>
public Automaton ToAutomaton(){return this.ToAutomatonAllowMutate(null,null,true);}/// <summary>
/// Constructs new <code>Automaton</code> from this <code>RegExp</code>.
/// Same as <code>toAutomaton(null,minimize)</code> (empty automaton map).
/// </summary>
/// <param name="minimize">if set to <c>true</c> [minimize].</param>
/// <returns></returns>
public Automaton ToAutomaton(bool minimize){return this.ToAutomatonAllowMutate(null,null,minimize);}/// <summary>
///   Constructs new <code>Automaton</code> from this <code>RegExp</code>. 
///   The constructed automaton is minimal and deterministic and has no 
///   transitions to dead states.
/// </summary>
/// <param name = "automatonProvider">The provider of automata for named identifiers.</param>
/// <returns></returns>
public Automaton ToAutomaton(IAutomatonProvider automatonProvider){return this.ToAutomatonAllowMutate(null,automatonProvider,true);}/// <summary>
///   Constructs new <code>Automaton</code> from this <code>RegExp</code>. 
///   The constructed automaton has no transitions to dead states.
/// </summary>
/// <param name = "automatonProvider">The provider of automata for named identifiers.</param>
/// <param name = "minimize">if set to <c>true</c> the automaton is minimized and determinized.</param>
/// <returns></returns>
public Automaton ToAutomaton(IAutomatonProvider automatonProvider,bool minimize){return this.ToAutomatonAllowMutate(null,automatonProvider,minimize);}
/// <summary>
///   Constructs new <code>Automaton</code> from this <code>RegExp</code>. 
///   The constructed automaton is minimal and deterministic and has no 
///   transitions to dead states.
/// </summary>
/// <param name = "automata">The a map from automaton identifiers to automata.</param>
/// <returns></returns>
public Automaton ToAutomaton(IDictionary<string,Automaton>automata){return this.ToAutomatonAllowMutate(automata,null,true);}/// <summary>
///   Constructs new <code>Automaton</code> from this <code>RegExp</code>. 
///   The constructed automaton has no transitions to dead states.
/// </summary>
/// <param name = "automata">The map from automaton identifiers to automata.</param>
/// <param name = "minimize">if set to <c>true</c> the automaton is minimized and determinized.</param>
/// <returns></returns>
public Automaton ToAutomaton(IDictionary<string,Automaton>automata,bool minimize){return this.ToAutomatonAllowMutate(automata,null,minimize);}/// <summary>
///   Sets or resets allow mutate flag.
///   If this flag is set, then automata construction uses mutable automata,
///   which is slightly faster but not thread safe.
/// </summary>
/// <param name = "flag">if set to <c>true</c> the flag is set.</param>
/// <returns>The previous value of the flag.</returns>
public bool SetAllowMutate(bool flag){bool@bool=allowMutation;allowMutation=flag;return@bool;}/// <inheritdoc />
public override string ToString(){return this.ToStringBuilder(new StringBuilder()).ToString();}/// <summary>
/// Returns the set of automaton identifiers that occur in this regular expression.
/// </summary>
/// <returns>The set of automaton identifiers that occur in this regular expression.</returns>
public HashSet<string>GetIdentifiers(){var set=new HashSet<string>();this.GetIdentifiers(set);return set;}private static RegExp MakeUnion(RegExp exp1,
RegExp exp2){var r=new RegExp();r.kind=Kind.RegexpUnion;r.exp1=exp1;r.exp2=exp2;return r;}private static RegExp MakeIntersection(RegExp exp1,RegExp exp2)
{var r=new RegExp();r.kind=Kind.RegexpIntersection;r.exp1=exp1;r.exp2=exp2;return r;}private static RegExp MakeConcatenation(RegExp exp1,RegExp exp2){
if((exp1.kind==Kind.RegexpChar||exp1.kind==Kind.RegexpString)&&(exp2.kind==Kind.RegexpChar||exp2.kind==Kind.RegexpString)){return RegExp.MakeString(exp1,
exp2);}var r=new RegExp();r.kind=Kind.RegexpConcatenation;if(exp1.kind==Kind.RegexpConcatenation&&(exp1.exp2.kind==Kind.RegexpChar||exp1.exp2.kind==Kind.RegexpString)
&&(exp2.kind==Kind.RegexpChar||exp2.kind==Kind.RegexpString)){r.exp1=exp1.exp1;r.exp2=RegExp.MakeString(exp1.exp2,exp2);}else if((exp1.kind==Kind.RegexpChar
||exp1.kind==Kind.RegexpString)&&exp2.kind==Kind.RegexpConcatenation&&(exp2.exp1.kind==Kind.RegexpChar||exp2.exp1.kind==Kind.RegexpString)){r.exp1=RegExp.MakeString(exp1,
exp2.exp1);r.exp2=exp2.exp2;}else{r.exp1=exp1;r.exp2=exp2;}return r;}private static RegExp MakeRepeat(RegExp exp){var r=new RegExp();r.kind=Kind.RegexpRepeat;
r.exp1=exp;return r;}private static RegExp MakeRepeat(RegExp exp,int min){var r=new RegExp();r.kind=Kind.RegexpRepeatMin;r.exp1=exp;r.min=min;return r;
}private static RegExp MakeRepeat(RegExp exp,int min,int max){var r=new RegExp();r.kind=Kind.RegexpRepeatMinMax;r.exp1=exp;r.min=min;r.max=max;return r;
}private static RegExp MakeOptional(RegExp exp){var r=new RegExp();r.kind=Kind.RegexpOptional;r.exp1=exp;return r;}private static RegExp MakeChar(char
@char){var r=new RegExp();r.kind=Kind.RegexpChar;r.c=@char;return r;}private static RegExp MakeInterval(int min,int max,int digits){var r=new RegExp();
r.kind=Kind.RegexpInterval;r.min=min;r.max=max;r.digits=digits;return r;}private static RegExp MakeAutomaton(string s){var r=new RegExp();r.kind=Kind.RegexpAutomaton;
r.s=s;return r;}private static RegExp MakeAnyString(){var r=new RegExp();r.kind=Kind.RegexpAnyString;return r;}private static RegExp MakeEmpty(){var r
=new RegExp();r.kind=Kind.RegexpEmpty;return r;}private static RegExp MakeAnyChar(){var r=new RegExp();r.kind=Kind.RegexpAnyChar;return r;}private static
 RegExp MakeAnyPrintableASCIIChar(){return MakeCharRange(' ','~');}private static RegExp MakeCharRange(char from,char to){var r=new RegExp();r.kind=Kind.RegexpCharRange;
r.from=from;r.to=to;return r;}private static RegExp MakeComplement(RegExp exp){var r=new RegExp();r.kind=Kind.RegexpComplement;r.exp1=exp;return r;}private
 static RegExp MakeString(string@string){var r=new RegExp();r.kind=Kind.RegexpString;r.s=@string;return r;}private static RegExp MakeString(RegExp exp1,
RegExp exp2){var sb=new StringBuilder();if(exp1.kind==Kind.RegexpString){sb.Append(exp1.s);}else{sb.Append(exp1.c);}if(exp2.kind==Kind.RegexpString){sb.Append(exp2.s);
}else{sb.Append(exp2.c);}return RegExp.MakeString(sb.ToString());}private Automaton ToAutomatonAllowMutate(IDictionary<string,Automaton>automata,IAutomatonProvider
 automatonProvider,bool minimize){bool@bool=false;if(allowMutation){@bool=this.SetAllowMutate(true);}Automaton a=this.ToAutomaton(automata,automatonProvider,
minimize);if(allowMutation){this.SetAllowMutate(@bool);}return a;}private Automaton ToAutomaton(IDictionary<string,Automaton>automata,IAutomatonProvider
 automatonProvider,bool minimize){IList<Automaton>list;Automaton a=null;switch(kind){case Kind.RegexpUnion:list=new List<Automaton>();this.FindLeaves(exp1,
Kind.RegexpUnion,list,automata,automatonProvider,minimize);this.FindLeaves(exp2,Kind.RegexpUnion,list,automata,automatonProvider,minimize);a=BasicOperations.Union(list);
a.Minimize();break;case Kind.RegexpConcatenation:list=new List<Automaton>();this.FindLeaves(exp1,Kind.RegexpConcatenation,list,automata,automatonProvider,
minimize);this.FindLeaves(exp2,Kind.RegexpConcatenation,list,automata,automatonProvider,minimize);a=BasicOperations.Concatenate(list);a.Minimize();break;
case Kind.RegexpIntersection:a=exp1.ToAutomaton(automata,automatonProvider,minimize).Intersection(exp2.ToAutomaton(automata,automatonProvider,minimize));
a.Minimize();break;case Kind.RegexpOptional:a=exp1.ToAutomaton(automata,automatonProvider,minimize).Optional();a.Minimize();break;case Kind.RegexpRepeat:
a=exp1.ToAutomaton(automata,automatonProvider,minimize).Repeat();a.Minimize();break;case Kind.RegexpRepeatMin:a=exp1.ToAutomaton(automata,automatonProvider,
minimize).Repeat(min);a.Minimize();break;case Kind.RegexpRepeatMinMax:a=exp1.ToAutomaton(automata,automatonProvider,minimize).Repeat(min,max);a.Minimize();
break;case Kind.RegexpComplement:a=exp1.ToAutomaton(automata,automatonProvider,minimize).Complement();a.Minimize();break;case Kind.RegexpChar:a=BasicAutomata.MakeChar(c);
break;case Kind.RegexpCharRange:a=BasicAutomata.MakeCharRange(from,to);break;case Kind.RegexpAnyChar:a=BasicAutomata.MakeAnyChar();break;case Kind.RegexpEmpty:
a=BasicAutomata.MakeEmpty();break;case Kind.RegexpString:a=BasicAutomata.MakeString(s);break;case Kind.RegexpAnyString:a=BasicAutomata.MakeAnyString();
break;case Kind.RegexpAutomaton:Automaton aa=null;if(automata!=null){automata.TryGetValue(s,out aa);}if(aa==null&&automatonProvider!=null){try{aa=automatonProvider.GetAutomaton(s);
}catch(IOException e){throw new ArgumentException(string.Empty,e);}}if(aa==null){throw new ArgumentException("'"+s+"' not found");}a=aa.Clone(); break;
case Kind.RegexpInterval:a=BasicAutomata.MakeInterval(min,max,digits);break;}return a;}private void FindLeaves(RegExp exp,Kind regExpKind,IList<Automaton>
list,IDictionary<String,Automaton>automata,IAutomatonProvider automatonProvider,bool minimize){if(exp.kind==regExpKind){this.FindLeaves(exp.exp1,regExpKind,
list,automata,automatonProvider,minimize);this.FindLeaves(exp.exp2,regExpKind,list,automata,automatonProvider,minimize);}else{list.Add(exp.ToAutomaton(automata,
automatonProvider,minimize));}}private StringBuilder ToStringBuilder(StringBuilder sb){switch(kind){case Kind.RegexpUnion:sb.Append("(");exp1.ToStringBuilder(sb);
sb.Append("|");exp2.ToStringBuilder(sb);sb.Append(")");break;case Kind.RegexpConcatenation:exp1.ToStringBuilder(sb);exp2.ToStringBuilder(sb);break;case
 Kind.RegexpIntersection:sb.Append("(");exp1.ToStringBuilder(sb);sb.Append("&");exp2.ToStringBuilder(sb);sb.Append(")");break;case Kind.RegexpOptional:
sb.Append("(");exp1.ToStringBuilder(sb);sb.Append(")?");break;case Kind.RegexpRepeat:sb.Append("(");exp1.ToStringBuilder(sb);sb.Append(")*");break;case
 Kind.RegexpRepeatMin:sb.Append("(");exp1.ToStringBuilder(sb);sb.Append("){").Append(min).Append(",}");break;case Kind.RegexpRepeatMinMax:sb.Append("(");
exp1.ToStringBuilder(sb);sb.Append("){").Append(min).Append(",").Append(max).Append("}");break;case Kind.RegexpComplement:sb.Append("~(");exp1.ToStringBuilder(sb);
sb.Append(")");break;case Kind.RegexpChar:sb.Append("\\").Append(c);break;case Kind.RegexpCharRange:sb.Append("[\\").Append(from).Append("-\\").Append(to).Append("]");
break;case Kind.RegexpAnyChar:sb.Append(".");break;case Kind.RegexpEmpty:sb.Append("#");break;case Kind.RegexpString:sb.Append("\"").Append(s).Append("\"");
break;case Kind.RegexpAnyString:sb.Append("@");break;case Kind.RegexpAutomaton:sb.Append("<").Append(s).Append(">");break;case Kind.RegexpInterval:string
 s1=Convert.ToDecimal(min).ToString();string s2=Convert.ToDecimal(max).ToString();sb.Append("<");if(digits>0){for(int i=s1.Length;i<digits;i++){sb.Append('0');
}}sb.Append(s1).Append("-");if(digits>0){for(int i=s2.Length;i<digits;i++){sb.Append('0');}}sb.Append(s2).Append(">");break;}return sb;}private void GetIdentifiers(HashSet<string>
set){switch(kind){case Kind.RegexpUnion:case Kind.RegexpConcatenation:case Kind.RegexpIntersection:exp1.GetIdentifiers(set);exp2.GetIdentifiers(set);break;
case Kind.RegexpOptional:case Kind.RegexpRepeat:case Kind.RegexpRepeatMin:case Kind.RegexpRepeatMinMax:case Kind.RegexpComplement:exp1.GetIdentifiers(set);
break;case Kind.RegexpAutomaton:set.Add(s);break;}}private RegExp ParseUnionExp(){RegExp e=this.ParseInterExp();if(this.Match('|')){e=RegExp.MakeUnion(e,
this.ParseUnionExp());}return e;}private bool Match(char@char){if(pos>=b.Length){return false;}if(b[pos]==@char){pos++;return true;}return false;}private
 RegExp ParseInterExp(){RegExp e=this.ParseConcatExp();if(this.Check(RegExpSyntaxOptions.Intersection)&&this.Match('&')){e=RegExp.MakeIntersection(e,this.ParseInterExp());
}return e;}private bool Check(RegExpSyntaxOptions flag){return(flags&flag)!=0;}private RegExp ParseConcatExp(){RegExp e=this.ParseRepeatExp();if(this.More()
&&!this.Peek(")|")&&(!this.Check(RegExpSyntaxOptions.Intersection)||!this.Peek("&"))){e=RegExp.MakeConcatenation(e,this.ParseConcatExp());}return e;}private
 bool More(){return pos<b.Length;}private bool Peek(string@string){return this.More()&&@string.IndexOf(b[pos])!=-1;}private RegExp ParseRepeatExp(){RegExp
 e=this.ParseComplExp();while(this.Peek("?*+{")){if(this.Match('?')){e=RegExp.MakeOptional(e);}else if(this.Match('*')){e=RegExp.MakeRepeat(e);}else if
(this.Match('+')){e=RegExp.MakeRepeat(e,1);}else if(this.Match('{')){int start=pos;while(this.Peek("0123456789")){this.Next();}if(start==pos){throw new
 ArgumentException("integer expected at position "+pos);}int n=int.Parse(b.Substring(start,pos-start));int m=-1;if(this.Match(',')){start=pos;while(this.Peek("0123456789"))
{this.Next();}if(start!=pos){m=int.Parse(b.Substring(start,pos-start));}}else{m=n;}if(!this.Match('}')){throw new ArgumentException("expected '}' at position "
+pos);}e=m==-1?RegExp.MakeRepeat(e,n):RegExp.MakeRepeat(e,n,m);}}return e;}private char Next(){if(!this.More()){throw new InvalidOperationException("unexpected end-of-string");
}return b[pos++];}private RegExp ParseComplExp(){if(this.Check(RegExpSyntaxOptions.Complement)&&this.Match('~')){return RegExp.MakeComplement(this.ParseComplExp());
}return this.ParseCharClassExp();}private RegExp ParseCharClassExp(){if(this.Match('[')){bool negate=false;if(this.Match('^')){negate=true;}RegExp e=this.ParseCharClasses();
if(negate){e=ExcludeChars(e,MakeAnyPrintableASCIIChar());}if(!this.Match(']')){throw new ArgumentException("expected ']' at position "+pos);}return e;
}return this.ParseSimpleExp();}private RegExp ParseSimpleExp(){if(this.Match('.')){return MakeAnyPrintableASCIIChar();}if(this.Check(RegExpSyntaxOptions.Empty)
&&this.Match('#')){return RegExp.MakeEmpty();}if(this.Check(RegExpSyntaxOptions.Anystring)&&this.Match('@')){return RegExp.MakeAnyString();}if(this.Match('"'))
{int start=pos;while(this.More()&&!this.Peek("\"")){this.Next();}if(!this.Match('"')){throw new ArgumentException("expected '\"' at position "+pos);}return
 RegExp.MakeString(b.Substring(start,((pos-1)-start)));}if(this.Match('(')){if(this.Match('?')){this.SkipNonCapturingSubpatternExp();}if(this.Match(')'))
{return RegExp.MakeString(string.Empty);}RegExp e=this.ParseUnionExp();if(!this.Match(')')){throw new ArgumentException("expected ')' at position "+pos);
}return e;}if((this.Check(RegExpSyntaxOptions.Automaton)||this.Check(RegExpSyntaxOptions.Interval))&&this.Match('<')){int start=pos;while(this.More()&&
!this.Peek(">")){this.Next();}if(!this.Match('>')){throw new ArgumentException("expected '>' at position "+pos);}string str=b.Substring(start,((pos-1)
-start));int i=str.IndexOf('-');if(i==-1){if(!this.Check(RegExpSyntaxOptions.Automaton)){throw new ArgumentException("interval syntax error at position "
+(pos-1));}return RegExp.MakeAutomaton(str);}if(!this.Check(RegExpSyntaxOptions.Interval)){throw new ArgumentException("illegal identifier at position "
+(pos-1));}try{if(i==0||i==str.Length-1||i!=str.LastIndexOf('-')){throw new FormatException();}string smin=str.Substring(0,i-0);string smax=str.Substring(i
+1,(str.Length-(i+1)));int imin=int.Parse(smin);int imax=int.Parse(smax);int numdigits=smin.Length==smax.Length?smin.Length:0;if(imin>imax){int t=imin;
imin=imax;imax=t;}return RegExp.MakeInterval(imin,imax,numdigits);}catch(FormatException){throw new ArgumentException("interval syntax error at position "
+(pos-1));}}if(this.Match('\\')){ if(this.Match('\\')){return MakeChar('\\');}bool inclusion; if((inclusion=this.Match('d'))||this.Match('D')){RegExp digitChars
=MakeCharRange('0','9');return inclusion?digitChars:ExcludeChars(digitChars,MakeAnyPrintableASCIIChar());} if((inclusion=this.Match('s'))||this.Match('S'))
{ RegExp whitespaceChars=MakeUnion(MakeChar(' '),MakeChar('\t'));return inclusion?whitespaceChars:ExcludeChars(whitespaceChars,MakeAnyPrintableASCIIChar());
} if((inclusion=this.Match('w'))||this.Match('W')){var ranges=new[]{MakeCharRange('A','Z'),MakeCharRange('a','z'),MakeCharRange('0','9')};RegExp wordChars
=ranges.Aggregate(MakeChar('_'),MakeUnion);return inclusion?wordChars:ExcludeChars(wordChars,MakeAnyPrintableASCIIChar());}}return RegExp.MakeChar(this.ParseCharExp());
}private void SkipNonCapturingSubpatternExp(){RegExpMatchingOptions.All().Any(this.Match);this.Match(':');}private char ParseCharExp(){this.Match('\\');
return this.Next();}private RegExp ParseCharClasses(){RegExp e=this.ParseCharClass();while(this.More()&&!this.Peek("]")){e=RegExp.MakeUnion(e,this.ParseCharClass());
}return e;}private RegExp ParseCharClass(){char@char=this.ParseCharExp();if(this.Match('-')){if(this.Peek("]")){return RegExp.MakeUnion(RegExp.MakeChar(@char),
RegExp.MakeChar('-'));}return RegExp.MakeCharRange(@char,this.ParseCharExp());}return RegExp.MakeChar(@char);}private static RegExp ExcludeChars(RegExp
 exclusion,RegExp allChars){return MakeIntersection(allChars,MakeComplement(exclusion));}private enum Kind{RegexpUnion,RegexpConcatenation,RegexpIntersection,
RegexpOptional,RegexpRepeat,RegexpRepeatMin,RegexpRepeatMinMax,RegexpComplement,RegexpChar,RegexpCharRange,RegexpAnyChar,RegexpEmpty,RegexpString,RegexpAnyString,
RegexpAutomaton,RegexpInterval}}}namespace Fare{public static class RegExpMatchingOptions{/// <summary>
/// Uses case-insensitive matching.
/// </summary>
public const char IgnoreCase='i';/// <summary>
/// Use single-line mode, where the period matches every character,
/// instead of every character except <code>\n</code>.
/// </summary>
public const char Singleline='s';/// <summary>
/// Use multiline mode, where <code>^</code> and <code>$</code> match
/// the beginning and end of each line, instead of the beginning and end of the input string.
/// </summary>
public const char Multiline='m';/// <summary>
/// Do not capture unnamed groups.
/// </summary>
public const char ExplicitCapture='n';/// <summary>
/// Exclude unescaped white space from the pattern
/// and enable comments after a hash sign <code>#</code>.
/// </summary>
public const char IgnorePatternWhitespace='x';public static IEnumerable<char>All(){yield return IgnoreCase;yield return Singleline;yield return Multiline;
yield return ExplicitCapture;yield return IgnorePatternWhitespace;}}}namespace Fare{[Flags]public enum RegExpSyntaxOptions{/// <summary>
/// Enables intersection.
/// </summary>
Intersection=0x0001,/// <summary>
/// Enables complement.
/// </summary>
Complement=0x0002,/// <summary>
/// Enables empty language.
/// </summary>
Empty=0x0004,/// <summary>
/// Enables anystring.
/// </summary>
Anystring=0x0008,/// <summary>
/// Enables named automata.
/// </summary>
Automaton=0x0010,/// <summary>
/// Enables numerical intervals.
/// </summary>
Interval=0x0020,/// <summary>
/// Enables all optional regexp syntax.
/// </summary>
All=0xffff}}namespace Fare{/// <summary>
/// Special automata operations.
/// </summary>
public static class SpecialOperations{/// <summary>
/// Reverses the language of the given (non-singleton) automaton while returning the set of 
/// new initial states.
/// </summary>
/// <param name="a">The automaton.</param>
/// <returns></returns>
public static HashSet<State>Reverse(Automaton a){ var m=new Dictionary<State,HashSet<Transition>>();HashSet<State>states=a.GetStates();HashSet<State>accept
=a.GetAcceptStates();foreach(State r in states){m.Add(r,new HashSet<Transition>());r.Accept=false;}foreach(State r in states){foreach(Transition t in r.Transitions)
{m[t.To].Add(new Transition(t.Min,t.Max,r));}}foreach(State r in states){r.Transitions=m[r].ToList();} a.Initial.Accept=true;a.Initial=new State();foreach
(State r in accept){a.Initial.AddEpsilon(r);}a.IsDeterministic=false;return accept;}/// <summary>
/// Returns an automaton that accepts the overlap of strings that in more than one way can be 
/// split into a left part being accepted by <code>a1</code> and a right part being accepted 
/// by <code>a2</code>.
/// </summary>
/// <param name="a1">The a1.</param>
/// <param name="a2">The a2.</param>
/// <returns></returns>
public static Automaton Overlap(Automaton a1,Automaton a2){throw new NotImplementedException();}private static void AcceptToAccept(Automaton a){throw new
 NotImplementedException();}/// <summary>
/// Returns an automaton that accepts the single chars that occur in strings that are accepted
/// by the given automaton. Never modifies the input automaton.
/// </summary>
/// <param name="a">The automaton.</param>
/// <returns></returns>
public static Automaton SingleChars(Automaton a){throw new NotImplementedException();}/// <summary>
/// Returns an automaton that accepts the trimmed language of the given automaton. The 
/// resulting automaton is constructed as follows: 1) Whenever a <code>c</code> character is
/// allowed in the original automaton, one or more <code>set</code> characters are allowed in
/// the new automaton. 2) The automaton is prefixed and postfixed with any number of <code>
/// set</code> characters.
/// </summary>
/// <param name="a">The automaton.</param>
/// <param name="set">The set of characters to be trimmed.</param>
/// <param name="c">The canonical trim character (assumed to be in <code>set</code>).</param>
/// <returns></returns>
public static Automaton Trim(Automaton a,string set,char c){throw new NotImplementedException();}private static void AddSetTransitions(State s,string set,
State p){throw new NotImplementedException();}/// <summary>
/// Returns an automaton that accepts the compressed language of the given automaton. 
/// Whenever a <code>c</code> character is allowed in the original automaton, one or more 
/// <code>set</code> characters are allowed in the new automaton.
/// </summary>
/// <param name="a">The automaton.</param>
/// <param name="set">The set of characters to be compressed.</param>
/// <param name="c">The canonical compress character (assumed to be in <code>set</code>).
/// </param>
/// <returns></returns>
public static Automaton Compress(Automaton a,string set,char c){throw new NotImplementedException();}/// <summary>
/// Returns an automaton where all transition labels have been substituted. 
/// <p> Each transition labeled <code>c</code> is changed to a set of transitions, one for 
/// each character in <code>map(c)</code>. If <code>map(c)</code> is null, then the 
/// transition is unchanged.
/// </p>
/// </summary>
/// <param name="a">The automaton.</param>
/// <param name="dictionary">The dictionary from characters to sets of characters (where 
/// characters are <code>char</code> objects).</param>
/// <returns></returns>
public static Automaton Subst(Automaton a,IDictionary<char,HashSet<char>>dictionary){throw new NotImplementedException();}/// <summary>
/// Rinds the largest entry whose value is less than or equal to c, or 0 if there is no
/// such entry.
/// </summary>
/// <param name="c">The c.</param>
/// <param name="points">The points.</param>
/// <returns></returns>
private static int FindIndex(char c,char[]points){throw new NotImplementedException();}/// <summary>
/// Returns an automaton where all transitions of the given char are replaced by a string.
/// </summary>
/// <param name="a">The automaton.</param>
/// <param name="c">The c.</param>
/// <param name="s">The s.</param>
/// <returns>
/// A new automaton.
/// </returns>
public static Automaton Subst(Automaton a,char c,string s){throw new NotImplementedException();}/// <summary>
/// Returns an automaton accepting the homomorphic image of the given automaton using the
/// given function.
/// <p>
/// This method maps each transition label to a new value.
/// <code>source</code> and <code>dest</code> are assumed to be arrays of same length,
/// and <code>source</code> must be sorted in increasing order and contain no duplicates.
/// <code>source</code> defines the starting points of char intervals, and the corresponding
/// entries in <code>dest</code> define the starting points of corresponding new intervals.
/// </p>
/// </summary>
/// <param name="a">The automaton.</param>
/// <param name="source">The source.</param>
/// <param name="dest">The dest.</param>
/// <returns></returns>
public static Automaton Homomorph(Automaton a,char[]source,char[]dest){throw new NotImplementedException();}/// <summary>
/// Returns an automaton with projected alphabet. The new automaton accepts all strings that
/// are projections of strings accepted by the given automaton onto the given characters
/// (represented by <code>Character</code>). If <code>null</code> is in the set, it abbreviates
/// the intervals u0000-uDFFF and uF900-uFFFF (i.e., the non-private code points). It is assumed
/// that all other characters from <code>chars</code> are in the interval uE000-uF8FF.
/// </summary>
/// <param name="a">The automaton.</param>
/// <param name="chars">The chars.</param>
/// <returns></returns>
public static Automaton ProjectChars(Automaton a,HashSet<char>chars){throw new NotImplementedException();}/// <summary>
/// Returns true if the language of this automaton is finite.
/// </summary>
/// <param name="a">The automaton.</param>
/// <returns>
///   <c>true</c> if the specified a is finite; otherwise, <c>false</c>.
/// </returns>
public static bool IsFinite(Automaton a){throw new NotImplementedException();}/// <summary>
/// Checks whether there is a loop containing s. (This is sufficient since there are never
/// transitions to dead states).
/// </summary>
/// <param name="s">The s.</param>
/// <param name="path">The path.</param>
/// <param name="visited">The visited.</param>
/// <returns>
///   <c>true</c> if the specified s is finite; otherwise, <c>false</c>.
/// </returns>
private static bool IsFinite(State s,HashSet<State>path,HashSet<State>visited){throw new NotImplementedException();}/// <summary>
/// Returns the set of accepted strings of the given length.
/// </summary>
/// <param name="a">The automaton.</param>
/// <param name="length">The length.</param>
/// <returns></returns>
public static HashSet<string>GetStrings(Automaton a,int length){throw new NotImplementedException();}private static void GetStrings(State s,HashSet<string>
strings,StringBuilder path,int length){throw new NotImplementedException();}/// <summary>
/// Returns the set of accepted strings, assuming this automaton has a finite language. If the
/// language is not finite, null is returned.
/// </summary>
/// <param name="a">The automaton.</param>
/// <returns></returns>
public static HashSet<string>GetFiniteStrings(Automaton a){throw new NotImplementedException();}/// <summary>
/// Returns the set of accepted strings, assuming that at most <code>limit</code> strings are
/// accepted. If more than <code>limit</code> strings are accepted, null is returned. If
/// <code>limit</code>&lt;0, then this methods works like {@link #getFiniteStrings(Automaton)}.
/// </summary>
/// <param name="a">The automaton.</param>
/// <param name="limit">The limit.</param>
/// <returns></returns>
public static HashSet<string>GetFiniteStrings(Automaton a,int limit){throw new NotImplementedException();}/// <summary>
/// Returns the strings that can be produced from the given state, or false if more than
/// <code>limit</code> strings are found. <code>limit</code>&lt;0 means "infinite".
/// </summary>
/// <param name="s">The s.</param>
/// <param name="pathStates">The path states.</param>
/// <param name="strings">The strings.</param>
/// <param name="path">The path.</param>
/// <param name="limit">The limit.</param>
/// <returns></returns>
private static bool GetFiniteStrings(State s,HashSet<State>pathStates,HashSet<string>strings,StringBuilder path,int limit){throw new NotImplementedException();
}/// <summary>
/// Returns the longest string that is a prefix of all accepted strings and visits each state
/// at most once.
/// </summary>
/// <param name="a">The automaton.</param>
/// <returns>
/// A common prefix.
/// </returns>
public static string GetCommonPrefix(Automaton a){throw new NotImplementedException();}/// <summary>
/// Prefix closes the given automaton.
/// </summary>
/// <param name="a">The automaton.</param>
public static void PrefixClose(Automaton a){throw new NotImplementedException();}/// <summary>
/// Constructs automaton that accepts the same strings as the given automaton but ignores upper/lower 
/// case of A-F.
/// </summary>
/// <param name="a">The automaton.</param>
/// <returns>An automaton.</returns>
public static Automaton HexCases(Automaton a){throw new NotImplementedException();}/// <summary>
/// Constructs automaton that accepts 0x20, 0x9, 0xa, and 0xd in place of each 0x20 transition
/// in the given automaton.
/// </summary>
/// <param name="a">The automaton.</param>
/// <returns>An automaton.</returns> 
public static Automaton ReplaceWhitespace(Automaton a){throw new NotImplementedException();}}}namespace Fare{/// <summary>
/// <tt>Automaton</tt> state.
/// </summary>
public class State:IEquatable<State>,IComparable<State>,IComparable{private readonly int id;private static int nextId;/// <summary>
/// Initializes a new instance of the <see cref="State"/> class. Initially, the new state is a 
///   reject state.
/// </summary>
public State(){this.ResetTransitions();id=Interlocked.Increment(ref nextId);}/// <summary>
/// Gets the id.
/// </summary>
public int Id{get{return this.id;}}/// <summary>
/// Gets or sets a value indicating whether this State is Accept.
/// </summary>
public bool Accept{get;set;}/// <summary>
/// Gets or sets this State Number.
/// </summary>
public int Number{get;set;}/// <summary>
/// Gets or sets this State Transitions.
/// </summary>
public IList<Transition>Transitions{get;set;}/// <summary>
/// Implements the operator ==.
/// </summary>
/// <param name="left">The left.</param>
/// <param name="right">The right.</param>
/// <returns>
/// The result of the operator.
/// </returns>
public static bool operator==(State left,State right){return Equals(left,right);}/// <summary>
/// Implements the operator !=.
/// </summary>
/// <param name="left">The left.</param>
/// <param name="right">The right.</param>
/// <returns>
/// The result of the operator.
/// </returns>
public static bool operator!=(State left,State right){return!Equals(left,right);}/// <inheritdoc />
public override bool Equals(object obj){if(object.ReferenceEquals(null,obj)){return false;}if(object.ReferenceEquals(this,obj)){return true;}if(obj.GetType()
!=typeof(State)){return false;}return this.Equals((State)obj);}/// <inheritdoc />
public override int GetHashCode(){unchecked{int result=id;result=(result*397)^Accept.GetHashCode();result=(result*397)^Number;return result;}}/// <inheritdoc />
public int CompareTo(object other){if(other==null){return 1;}if(other.GetType()!=typeof(State)){throw new ArgumentException("Object is not a State");}
return this.CompareTo((State)other);}/// <inheritdoc />
public bool Equals(State other){if(object.ReferenceEquals(null,other)){return false;}if(object.ReferenceEquals(this,other)){return true;}return other.id
==id&&other.Accept.Equals(Accept)&&other.Number==Number;}/// <inheritdoc />
public int CompareTo(State other){return other.Id-this.Id;}/// <inheritdoc />
public override string ToString(){var sb=new StringBuilder();sb.Append("state ").Append(this.Number);sb.Append(this.Accept?" [accept]":" [reject]");sb.Append(":\n");
foreach(Transition t in this.Transitions){sb.Append("  ").Append(t.ToString()).Append("\n");}return sb.ToString();}/// <summary>
/// Adds an outgoing transition.
/// </summary>
/// <param name="t">
/// The transition.
/// </param>
public void AddTransition(Transition t){this.Transitions.Add(t);}/// <summary>
/// Performs lookup in transitions, assuming determinism.
/// </summary>
/// <param name="c">
/// The character to look up.
/// </param>
/// <returns>
/// The destination state, null if no matching outgoing transition.
/// </returns>
public State Step(char c){return(from t in this.Transitions where t.Min<=c&&c<=t.Max select t.To).FirstOrDefault();}/// <summary>
/// Performs lookup in transitions, allowing nondeterminism.
/// </summary>
/// <param name="c">
/// The character to look up.
/// </param>
/// <param name="dest">
/// The collection where destination states are stored.
/// </param>
public void Step(char c,List<State>dest){dest.AddRange(from t in this.Transitions where t.Min<=c&&c<=t.Max select t.To);}/// <summary>
/// Gets the transitions sorted by (min, reverse max, to) or (to, min, reverse max).
/// </summary>
/// <param name="toFirst">
/// if set to <c>true</c> [to first].
/// </param>
/// <returns>
/// The transitions sorted by (min, reverse max, to) or (to, min, reverse max).
/// </returns>
public IList<Transition>GetSortedTransitions(bool toFirst){Transition[]e=this.Transitions.ToArray();Array.Sort(e,new TransitionComparer(toFirst));return
 e.ToList();}internal void AddEpsilon(State to){if(to.Accept){this.Accept=true;}foreach(Transition t in to.Transitions){this.Transitions.Add(t);}}internal
 void ResetTransitions(){this.Transitions=new List<Transition>();}}}namespace Fare{internal sealed class StateEqualityComparer:IEqualityComparer<State>
{/// <summary>
/// Determines whether the specified objects are equal.
/// </summary>
/// <param name="x">The first object of type <paramref name="x"/> to compare.</param>
/// <param name="y">The second object of type <paramref name="y"/> to compare.</param>
/// <returns>
/// true if the specified objects are equal; otherwise, false.
/// </returns>
public bool Equals(State x,State y){return x.Equals(y);}/// <summary>
/// Returns a hash code for this instance.
/// </summary>
/// <param name="obj">The obj.</param>
/// <returns>
/// A hash code for this instance, suitable for use in hashing algorithms and data structures
/// like a hash table. 
/// </returns>
/// <exception cref="T:System.ArgumentNullException">
/// The type of <paramref name="obj"/> is a reference type and <paramref name="obj"/> is null.
///   </exception>
public int GetHashCode(State obj){return obj.GetHashCode();}}}namespace Fare{/// <summary>
/// Pair of states.
/// </summary>
public class StatePair:IEquatable<StatePair>{/// <summary>
/// Initializes a new instance of the <see cref="StatePair"/> class.
/// </summary>
/// <param name="s">The s.</param>
/// <param name="s1">The s1.</param>
/// <param name="s2">The s2.</param>
public StatePair(State s,State s1,State s2){this.S=s;this.FirstState=s1;this.SecondState=s2;}/// <summary>
/// Initializes a new instance of the <see cref="StatePair"/> class.
/// </summary>
/// <param name="s1">The first state.</param>
/// <param name="s2">The second state.</param>
public StatePair(State s1,State s2):this(null,s1,s2){}public State S{get;set;}/// <summary>
/// Gets or sets the first component of this pair.
/// </summary>
/// <value>
/// The first state.
/// </value>
public State FirstState{get;set;}/// <summary>
/// Gets or sets the second component of this pair.
/// </summary>
/// <value>
/// The second state.
/// </value>
public State SecondState{get;set;}/// <summary>
/// Implements the operator ==.
/// </summary>
/// <param name="left">The left.</param>
/// <param name="right">The right.</param>
/// <returns>
/// The result of the operator.
/// </returns>
public static bool operator==(StatePair left,StatePair right){return Equals(left,right);}/// <summary>
/// Implements the operator !=.
/// </summary>
/// <param name="left">The left.</param>
/// <param name="right">The right.</param>
/// <returns>
/// The result of the operator.
/// </returns>
public static bool operator!=(StatePair left,StatePair right){return!Equals(left,right);}/// <inheritdoc />
public bool Equals(StatePair other){if(object.ReferenceEquals(null,other)){return false;}if(object.ReferenceEquals(this,other)){return true;}return object.Equals(other.FirstState,
this.FirstState)&&object.Equals(other.SecondState,this.SecondState);}/// <inheritdoc />
public override bool Equals(object obj){if(object.ReferenceEquals(null,obj)){return false;}if(object.ReferenceEquals(this,obj)){return true;}if(obj.GetType()
!=typeof(StatePair)){return false;}return this.Equals((StatePair)obj);}/// <inheritdoc />
public override int GetHashCode(){unchecked{var result=0;result=(result*397)^(this.FirstState!=null?this.FirstState.GetHashCode():0);result=(result*397)
^(this.SecondState!=null?this.SecondState.GetHashCode():0);return result;}}}}namespace Fare{public sealed class StringUnionOperations{private static readonly
 IComparer<char[]>lexicographicOrder=new LexicographicOrder();private readonly State root=new State();private StringBuilder previous;private IDictionary<State,
State>register=new Dictionary<State,State>();public static IComparer<char[]>LexicographicOrderComparer{get{return lexicographicOrder;}}public static Fare.State
 Build(IEnumerable<char[]>input){var builder=new StringUnionOperations();foreach(var chs in input){builder.Add(chs);}return StringUnionOperations.Convert(builder.Complete(),
new Dictionary<State,Fare.State>());}public void Add(char[]current){Debug.Assert(this.register!=null,"Automaton already built.");Debug.Assert(current.Length
>0,"Input sequences must not be empty.");Debug.Assert(this.previous==null||LexicographicOrderComparer.Compare(this.previous.ToString().ToCharArray(),current)
<=0,"Input must be sorted: "+this.previous+" >= "+current);Debug.Assert(this.SetPrevious(current)); int pos=0;int max=current.Length;State next;State state
=root;while(pos<max&&(next=state.GetLastChild(current[pos]))!=null){state=next;pos++;}if(state.HasChildren){this.ReplaceOrRegister(state);}StringUnionOperations.AddSuffix(state,
current,pos);}private static void AddSuffix(State state,char[]current,int fromIndex){for(int i=fromIndex;i<current.Length;i++){state=state.NewState(current[i]);
}state.IsFinal=true;}private static Fare.State Convert(State s,IDictionary<State,Fare.State>visited){Fare.State converted=visited[s];if(converted!=null)
{return converted;}converted=new Fare.State();converted.Accept=s.IsFinal;visited.Add(s,converted);int i=0;char[]labels=s.TransitionLabels;foreach(State
 target in s.States){converted.AddTransition(new Transition(labels[i++],StringUnionOperations.Convert(target,visited)));}return converted;}private State
 Complete(){if(this.register==null){throw new InvalidOperationException("register is null");}if(this.root.HasChildren){this.ReplaceOrRegister(this.root);
}this.register=null;return this.root;}private void ReplaceOrRegister(State state){State child=state.LastChild;if(child.HasChildren){this.ReplaceOrRegister(child);
}State registered=this.register[child];if(registered!=null){state.ReplaceLastChild(registered);}else{this.register.Add(child,child);}}private bool SetPrevious(char[]
current){if(this.previous==null){this.previous=new StringBuilder();}this.previous.Length=0;this.previous.Append(current);return true;}private sealed class
 LexicographicOrder:IComparer<char[]>{public int Compare(char[]s1,char[]s2){int lens1=s1.Length;int lens2=s2.Length;int max=Math.Min(lens1,lens2);for(int
 i=0;i<max;i++){char c1=s1[i];char c2=s2[i];if(c1!=c2){return c1-c2;}}return lens1-lens2;}}private sealed class State{private static readonly char[]noLabels
=new char[0];private static readonly State[]noStates=new State[0];private bool isFinal;private char[]labels=noLabels;private State[]states=noStates;public
 char[]TransitionLabels{get{return this.labels;}}public IEnumerable<State>States{get{return this.states;}}public bool HasChildren{get{return this.labels.Length
>0;}}public bool IsFinal{get{return this.isFinal;}set{this.isFinal=value;}}public State LastChild{get{Debug.Assert(this.HasChildren,"No outgoing transitions.");
return this.states[this.states.Length-1];}}public override bool Equals(object obj){var other=obj as State;if(other==null){return false;}return this.isFinal
==other.isFinal&&State.ReferenceEquals(states,other.states)&&object.Equals(labels,other.labels);}public override int GetHashCode(){int hash=this.isFinal
?1:0;hash^=(hash*31)+this.labels.Length;hash=this.labels.Aggregate(hash,(current,c)=>current^(current*31)+c); return this.states.Aggregate(hash,(current,
s)=>current^RuntimeHelpers.GetHashCode(s));}public State NewState(char label){Debug.Assert(Array.BinarySearch(this.labels,label)<0,"State already has transition labeled: "
+label);this.labels=CopyOf(this.labels,this.labels.Length+1);this.states=CopyOf(this.states,this.states.Length+1);this.labels[this.labels.Length-1]=label;
return states[this.states.Length-1]=new State();}public State GetLastChild(char label){int index=this.labels.Length-1;State s=null;if(index>=0&&this.labels[index]
==label){s=this.states[index];}Debug.Assert(s==this.GetState(label));return s;}public void ReplaceLastChild(State state){Debug.Assert(this.HasChildren,
"No outgoing transitions.");this.states[this.states.Length-1]=state;}private static char[]CopyOf(char[]original,int newLength){var copy=new char[newLength];
Array.Copy(original,0,copy,0,Math.Min(original.Length,newLength));return copy;}private static State[]CopyOf(State[]original,int newLength){var copy=new
 State[newLength];Array.Copy(original,0,copy,0,Math.Min(original.Length,newLength));return copy;}private static bool ReferenceEquals(Object[]a1,Object[]
a2){if(a1.Length!=a2.Length){return false;}return!a1.Where((t,i)=>t!=a2[i]).Any();}private State GetState(char label){int index=Array.BinarySearch(this.labels,
label);return index>=0?states[index]:null;}}}}namespace Fare{///<summary>
///  <tt>Automaton</tt> transition. 
///  <p>
///    A transition, which belongs to a source state, consists of a Unicode character interval
///    and a destination state.
///  </p>
///</summary>
public class Transition:IEquatable<Transition>{private readonly char max;private readonly char min;private readonly State to;/// <summary>
/// Initializes a new instance of the <see cref="Transition"/> class.
/// (Constructs a new singleton interval transition).
/// </summary>
/// <param name="c">The transition character.</param>
/// <param name="to">The destination state.</param>
public Transition(char c,State to){this.min=this.max=c;this.to=to;}/// <summary>
/// Initializes a new instance of the <see cref="Transition"/> class.
/// (Both end points are included in the interval).
/// </summary>
/// <param name="min">The transition interval minimum.</param>
/// <param name="max">The transition interval maximum.</param>
/// <param name="to">The destination state.</param>
public Transition(char min,char max,State to){if(max<min){char t=max;max=min;min=t;}this.min=min;this.max=max;this.to=to;}/// <summary>
/// Gets the minimum of this transition interval.
/// </summary>
public char Min{get{return this.min;}}/// <summary>
/// Gets the maximum of this transition interval.
/// </summary>
public char Max{get{return this.max;}}/// <summary>
/// Gets the destination of this transition.
/// </summary>
public State To{get{return this.to;}}/// <summary>
/// Implements the operator ==.
/// </summary>
/// <param name="left">The left.</param>
/// <param name="right">The right.</param>
/// <returns>
/// The result of the operator.
/// </returns>
public static bool operator==(Transition left,Transition right){return Equals(left,right);}/// <summary>
/// Implements the operator !=.
/// </summary>
/// <param name="left">The left.</param>
/// <param name="right">The right.</param>
/// <returns>
/// The result of the operator.
/// </returns>
public static bool operator!=(Transition left,Transition right){return!Equals(left,right);}/// <inheritdoc />
public override string ToString(){var sb=new StringBuilder();Transition.AppendCharString(min,sb);if(min!=max){sb.Append("-");Transition.AppendCharString(max,
sb);}sb.Append(" -> ").Append(to.Number);return sb.ToString();}/// <inheritdoc />
public override bool Equals(object obj){if(object.ReferenceEquals(null,obj)){return false;}if(object.ReferenceEquals(this,obj)){return true;}if(obj.GetType()
!=typeof(Transition)){return false;}return this.Equals((Transition)obj);}/// <inheritdoc />
public override int GetHashCode(){unchecked{int result=min.GetHashCode();result=(result*397)^max.GetHashCode();result=(result*397)^(to!=null?to.GetHashCode()
:0);return result;}}/// <inheritdoc />
public bool Equals(Transition other){if(object.ReferenceEquals(null,other)){return false;}if(object.ReferenceEquals(this,other)){return true;}return other.min
==min&&other.max==max&&object.Equals(other.to,to);}private static void AppendCharString(char c,StringBuilder sb){if(c>=0x21&&c<=0x7e&&c!='\\'&&c!='"')
{sb.Append(c);}else{sb.Append("\\u");string s=((int)c).ToString("x");if(c<0x10){sb.Append("000").Append(s);}else if(c<0x100){sb.Append("00").Append(s);
}else if(c<0x1000){sb.Append("0").Append(s);}else{sb.Append(s);}}}private void AppendDot(StringBuilder sb){sb.Append(" -> ").Append(this.to.Number).Append(" [label=\"");
Transition.AppendCharString(this.min,sb);if(this.min!=this.max){sb.Append("-");Transition.AppendCharString(this.max,sb);}sb.Append("\"]\n");}}}namespace
 Fare{internal sealed class TransitionComparer:IComparer<Transition>{private readonly bool toFirst;/// <summary>
/// Initializes a new instance of the <see cref="TransitionComparer"/> class.
/// </summary>
/// <param name="toFirst">if set to <c>true</c> [to first].</param>
public TransitionComparer(bool toFirst){this.toFirst=toFirst;}/// <summary>
/// Compares by (min, reverse max, to) or (to, min, reverse max).
/// </summary>
/// <param name="t1">The first Transition.</param>
/// <param name="t2">The second Transition.</param>
/// <returns></returns>
public int Compare(Transition t1,Transition t2){if(this.toFirst){if(t1.To!=t2.To){if(t1.To==null){return-1;}if(t2.To==null){return 1;}if(t1.To.Number<
t2.To.Number){return-1;}if(t1.To.Number>t2.To.Number){return 1;}}}if(t1.Min<t2.Min){return-1;}if(t1.Min>t2.Min){return 1;}if(t1.Max>t2.Max){return-1;}
if(t1.Max<t2.Max){return 1;}if(!this.toFirst){if(t1.To!=t2.To){if(t1.To==null){return-1;}if(t2.To==null){return 1;}if(t1.To.Number<t2.To.Number){return
-1;}if(t1.To.Number>t2.To.Number){return 1;}}}return 0;}}}namespace Fare{/// <summary>
/// An object that will generate text from a regular expression. In a way, 
/// it's the opposite of a regular expression matcher: an instance of this class
/// will produce text that is guaranteed to match the regular expression passed in.
/// </summary>
public class Xeger{private const RegExpSyntaxOptions AllExceptAnyString=RegExpSyntaxOptions.All&~RegExpSyntaxOptions.Anystring;private readonly Automaton
 automaton;private readonly Random random;/// <summary>
/// Initializes a new instance of the <see cref="Xeger"/> class.
/// </summary>
/// <param name="regex">The regex.</param>
/// <param name="random">The random.</param>
public Xeger(string regex,Random random){if(string.IsNullOrEmpty(regex)){throw new ArgumentNullException("regex");}if(random==null){throw new ArgumentNullException("random");
}regex=RemoveStartEndMarkers(regex);this.automaton=new RegExp(regex,AllExceptAnyString).ToAutomaton();this.random=random;}/// <summary>
/// Initializes a new instance of the <see cref="Xeger"/> class.
/// </summary>
/// <param name="regex">The regex.</param>
public Xeger(string regex):this(regex,new Random()){}/// <summary>
/// Generates a random String that is guaranteed to match the regular expression passed to the constructor.
/// </summary>
/// <returns></returns>
public string Generate(){var builder=new StringBuilder();this.Generate(builder,automaton.Initial);return builder.ToString();}/// <summary>
/// Generates a random number within the given bounds.
/// </summary>
/// <param name="min">The minimum number (inclusive).</param>
/// <param name="max">The maximum number (inclusive).</param>
/// <param name="random">The object used as the randomizer.</param>
/// <returns>A random number in the given range.</returns>
private static int GetRandomInt(int min,int max,Random random){int maxForRandom=max-min+1;return random.Next(maxForRandom)+min;}private void Generate(StringBuilder
 builder,State state){var transitions=state.GetSortedTransitions(true);if(transitions.Count==0){if(!state.Accept){throw new InvalidOperationException("state");
}return;}int nroptions=state.Accept?transitions.Count:transitions.Count-1;int option=Xeger.GetRandomInt(0,nroptions,random);if(state.Accept&&option==0)
{ return;} Transition transition=transitions[option-(state.Accept?1:0)];this.AppendChoice(builder,transition);Generate(builder,transition.To);}private
 void AppendChoice(StringBuilder builder,Transition transition){var c=(char)Xeger.GetRandomInt(transition.Min,transition.Max,random);builder.Append(c);
}private string RemoveStartEndMarkers(string regExp){if(regExp.StartsWith("^")){regExp=regExp.Substring(1);}if(regExp.EndsWith("$")){regExp=regExp.Substring(0,
regExp.Length-1);}return regExp;}}}