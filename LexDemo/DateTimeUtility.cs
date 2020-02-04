
using System;
using System.Runtime.InteropServices;

namespace LexDemo
{
	public static class DateTimeUtility
	{
		private struct _Win32
		{
			[DllImport("Kernel32.dll", CallingConvention = CallingConvention.Winapi)]
			public static extern void GetSystemTimePreciseAsFileTime(out long fileTime);

		}

		public static readonly bool HasPrecisionTime = _HasPrecisionTime();

		static bool _HasPrecisionTime()
		{
			try
			{
				long filetime;
				_Win32.GetSystemTimePreciseAsFileTime(out filetime);
				return true;
			}
			catch (EntryPointNotFoundException)
			{
				// Not running Windows 8 or higher.
				return false;
			}
		}
		
		/// <summary>
		/// Retrieves the precise UTC time
		/// </summary>
		public static DateTime UtcNow 
			{ 
			get { 
				if (!HasPrecisionTime) 
					throw new NotSupportedException("This system does not support a high resolution clock."); 
				long result; 
				_Win32.GetSystemTimePreciseAsFileTime(out result); 
				return DateTime.FromFileTimeUtc(result);
			} 
		}
	}
}
