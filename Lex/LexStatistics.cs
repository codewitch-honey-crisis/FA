using System;
using System.Collections.Generic;
using System.Text;

namespace L
{
	public struct LexStatistics
	{
		public readonly int MaxFiberCount;
		public readonly float AverageCharacterPasses;
		public LexStatistics(int maxFiberCount,float averageCharacterPasses)
		{
			MaxFiberCount = maxFiberCount;
			AverageCharacterPasses = averageCharacterPasses;
		}
	}
}
