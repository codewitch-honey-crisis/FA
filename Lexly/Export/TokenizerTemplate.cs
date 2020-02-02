using System.Collections.Generic;

namespace Lexly
{
	partial class TokenizerTemplate : Tokenizer
	{
		internal static int[][] Program;
		internal static string[] BlockEnds;
		internal static int[] NodeFlags;
		public TokenizerTemplate(IEnumerable<char> input) :
			   base(Program, BlockEnds, NodeFlags, input)
		{
		}
	}
}
