using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace F
{
#if FALIB
	public
#endif
	static class RangeUtility
	{

		public static bool Intersects(int[] x,int[] y)
		{
			if (null == x || null == y) return false;
			if (x == y) return true;
			for(var i = 0;i<x.Length;i+=2)
			{
				for (var j = 0; j < y.Length; j += 2)
				{
					if (Intersects(x[i], x[i + 1], y[j], y[j + 1]))
						return true;
					if (x[i] > y[j + 1])
						return false;
				}
			}
			return false;
		}
		public static bool Intersects(int xf,int xl,int yf,int yl)
		{
			return (xf >= yf && xf <= yl) ||
				(xl >= yf && xl <= yl);
		}
		public static bool Intersects(KeyValuePair<int, int> x, KeyValuePair<int, int> y)
		{
			return (x.Key >= y.Key && x.Key <= y.Value) ||
				(x.Value >= y.Key && x.Value <= y.Value) ||
				(y.Key >= x.Key && y.Key <= x.Value) ||
				(y.Value >= x.Key && y.Value <= x.Value);
		}
		public static KeyValuePair<int,int>[] ToPairs(int[] packedRanges)
		{
			var result = new KeyValuePair<int, int>[packedRanges.Length / 2];
			for(var i = 0;i<result.Length;++i)
			{
				var j = i * 2;
				result[i] = new KeyValuePair<int, int>(packedRanges[j], packedRanges[j + 1]);	
			}
			return result;
		}
		public static int[] FromPairs(IList<KeyValuePair<int,int>> pairs)
		{
			var result = new int[pairs.Count * 2];
			for(int ic=pairs.Count,i = 0;i<ic;++i)
			{
				var pair = pairs[i];
				var j = i * 2;
				result[j] = pair.Key;
				result[j + 1] = pair.Value;
			}
			return result;
		}
		public static void NormalizeRangeArray(int[] packedRanges)
		{
			var pairs = ToPairs(packedRanges);
			Array.Sort(pairs, (x, y) => { return x.Key.CompareTo(y.Key); });
			NormalizeSortedRangeList(pairs);
			for(var i = 0;i<pairs.Length;++i)
			{
				var j = i * 2;
				packedRanges[j] = pairs[i].Key;
				packedRanges[j + 1] = pairs[i].Value;
			}
		}
		
		public static void NormalizeSortedRangeList(IList<KeyValuePair<int, int>> pairs)
		{

			var or = default(KeyValuePair<int, int>);
			for (int i = 1; i < pairs.Count; ++i)
			{
				if (pairs[i - 1].Value+1 >= pairs[i].Key)
				{
					var nr = new KeyValuePair<int, int>(pairs[i - 1].Key, pairs[i].Value);
					pairs[i - 1] = or = nr;
					pairs.RemoveAt(i);
					--i; // compensated for by ++i in for loop
				}
			}
		}
		public static KeyValuePair<int,int>[] Subtract(KeyValuePair<int,int> x,KeyValuePair<int,int> y)
		{
			if (!Intersects(x, y))
				return new KeyValuePair<int, int>[] { x };
			if (y.Key <= x.Key && y.Value >= x.Value)
				return new KeyValuePair<int, int>[0];
			if(y.Key<=x.Key)
			{
				return new KeyValuePair<int, int>[] { new KeyValuePair<int, int>(y.Value+1, x.Value) };
			}
			if(y.Value>=x.Value)
				return new KeyValuePair<int, int>[] { new KeyValuePair<int, int>(x.Key, y.Key-1) };
			return new KeyValuePair<int, int>[] { 
				new KeyValuePair<int, int>(x.Key,y.Key-1),
				new KeyValuePair<int, int>(y.Value+1,x.Value)
			};
			
		}
		public static KeyValuePair<int, int>[] Subtract(IEnumerable<KeyValuePair<int, int>> ranges, KeyValuePair<int, int> range)
		{
			var result = new List<KeyValuePair<int, int>>();
			foreach(var x in ranges)
			{
				result.AddRange(Subtract(x, range));
			}
			return result.ToArray();
		}
		public static int[] NotRanges(int[] ranges)
		{
			return FromPairs(new List<KeyValuePair<int, int>>(NotRanges(ToPairs(ranges))));
		}
		public static IEnumerable<KeyValuePair<int,int>> NotRanges(IEnumerable<KeyValuePair<int,int>> ranges)
		{
			// expects ranges to be normalized
			var last = 0x10ffff;
			using (var e = ranges.GetEnumerator())
			{
				if (!e.MoveNext())
				{
					yield return new KeyValuePair<int, int>(0x0, 0x10ffff);
					yield break;
				}
				if (e.Current.Key > 0)
				{
					yield return new KeyValuePair<int, int>(0, unchecked(e.Current.Key- 1));
					last = e.Current.Value;
					if (0x10ffff <= last)
						yield break;
				}
				while (e.MoveNext())
				{
					if (0x10ffff <= last)
						yield break;
					if (unchecked(last + 1) < e.Current.Key)
						yield return new KeyValuePair<int, int>(unchecked(last + 1), unchecked((e.Current.Key - 1)));
					last = e.Current.Value;
				}
				if (0x10ffff> last)
					yield return new KeyValuePair<int, int>(unchecked((last + 1)), 0x10ffff);

			}

		}
		public static int[] GetRanges(IEnumerable<int> sortedChars)
		{
			var result = new List<int>();
			int first;
			int last;
			using (var e = sortedChars.GetEnumerator())
			{
				bool moved = e.MoveNext();
				while (moved)
				{
					first = last = e.Current;
					while ((moved = e.MoveNext()) && (e.Current == last || e.Current == last + 1))
					{
						last = e.Current;
					}
					result.Add(first);
					result.Add(last);
				}
			}
			return result.ToArray();
		}
		public static string ToString(KeyValuePair<int,int> range)
		{
			var sb = new StringBuilder();
			_AppendRangeTo(sb, new int[] { range.Key, range.Value }, 0);
			return sb.ToString();
		}
		public static string ToString(IEnumerable<KeyValuePair<int, int>> ranges)
		{
			var sb = new StringBuilder();
			foreach(var range in ranges)
				_AppendRangeTo(sb, new int[] { range.Key, range.Value }, 0);
			return sb.ToString();
		}
		static void _AppendRangeTo(StringBuilder builder, int[] ranges, int index)
		{
			_AppendRangeCharTo(builder, ranges[index]);
			if (0 == ranges[index + 1].CompareTo(ranges[index])) return;
			if (ranges[index + 1] == ranges[index] + 1) // spit out 1 length ranges as two chars
			{
				_AppendRangeCharTo(builder, ranges[index + 1]);
				return;
			}
			builder.Append('-');
			_AppendRangeCharTo(builder, ranges[index + 1]);
		}
		static void _AppendRangeCharTo(StringBuilder builder, int rangeChar)
		{
			switch (rangeChar)
			{
				case '-':
				case '\\':
					builder.Append('\\');
					builder.Append(char.ConvertFromUtf32(rangeChar));
					return;
				case '\t':
					builder.Append("\\t");
					return;
				case '\n':
					builder.Append("\\n");
					return;
				case '\r':
					builder.Append("\\r");
					return;
				case '\0':
					builder.Append("\\0");
					return;
				case '\f':
					builder.Append("\\f");
					return;
				case '\v':
					builder.Append("\\v");
					return;
				case '\b':
					builder.Append("\\b");
					return;
				default:
					var s = char.ConvertFromUtf32(rangeChar);
					if (!char.IsLetterOrDigit(s, 0) && !char.IsSeparator(s, 0) && !char.IsPunctuation(s, 0) && !char.IsSymbol(s, 0))
					{
						if (s.Length == 1)
						{
							builder.Append("\\u");
							builder.Append(unchecked((ushort)rangeChar).ToString("x4"));
						}
						else
						{
							builder.Append("\\U");
							rangeChar.ToString("x8");
						}

					}
					else
						builder.Append(s);
					break;
			}
		}
	}
}
