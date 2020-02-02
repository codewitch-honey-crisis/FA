using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace L
{
	static class RangeUtility
	{
		public static int[] Merge(int[] x,int[] y)
		{
			var pairs = new List<KeyValuePair<int, int>>((x.Length + y.Length) / 2);
			pairs.AddRange(ToPairs(x));
			pairs.AddRange(ToPairs(y));
			NormalizeRangeList(pairs);
			return FromPairs(pairs);
		}
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
			NormalizeRangeList(pairs);
			for(var i = 0;i<pairs.Length;++i)
			{
				var j = i * 2;
				packedRanges[j] = pairs[i].Key;
				packedRanges[j + 1] = pairs[i].Value;
			}
		}
		public static void NormalizeRangeList(IList<KeyValuePair<int,int>> pairs)
		{

			_Sort(pairs, 0, pairs.Count - 1);
			var or = default(KeyValuePair<int,int>);
			for (int i = 1; i < pairs.Count; ++i)
			{
				if (pairs[i - 1].Value >= pairs[i].Key)
				{
					var nr = new KeyValuePair<int,int>(pairs[i - 1].Key, pairs[i].Value);
					pairs[i - 1] = or = nr;
					pairs.RemoveAt(i);
					--i; // compensated for by ++i in for loop
				}
			}
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

		static void _Sort(IList<KeyValuePair<int,int>> arr, int left, int right)
		{
			if (left < right)
			{
				int pivot = _Partition(arr, left, right);

				if (1 < pivot)
				{
					_Sort(arr, left, pivot - 1);
				}
				if (pivot + 1 < right)
				{
					_Sort(arr, pivot + 1, right);
				}
			}

		}
		static int _ComparePairTo(KeyValuePair<int,int> x, KeyValuePair<int, int> y)
		{
			var c = x.Key.CompareTo(y.Key);
			if (c != 0) return c;
			return x.Value.CompareTo(y.Value);
		}
		static int _Partition(IList<KeyValuePair<int,int>> arr, int left, int right)
		{
			KeyValuePair<int,int> pivot = arr[left];
			while (true)
			{

				while (0<_ComparePairTo(arr[left],pivot))
				{
					++left;
				}

				while (0>_ComparePairTo(arr[right] , pivot))
				{
					--right;
				}

				if (left < right)
				{
					if (0==_ComparePairTo(arr[left] , arr[right])) return right;

					var swap = arr[left];
					arr[left] = arr[right];
					arr[right] = swap;


				}
				else
				{
					return right;
				}
			}
		}
	}
}
