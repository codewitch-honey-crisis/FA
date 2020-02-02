using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace F
{
	partial class CharCls
	{
		static Lazy<IDictionary<string, int[]>> _CharacterClasses = new Lazy<IDictionary<string, int[]>>(_GetCharacterClasses);
		static IDictionary<string,int[]> _GetCharacterClasses()
		{
			var result = new Dictionary<string, int[]>();
			var fa = typeof(CharCls).GetFields();
			for (var i = 0; i < fa.Length; i++)
			{
				var f = fa[i];
				if (f.FieldType == typeof(int[]))
				{
					result.Add(f.Name, (int[])f.GetValue(null));
				}
				
			}
			return result;
		}
		public static IDictionary<string,int[]> CharacterClasses {  get { return _CharacterClasses.Value; } }
	}
}
