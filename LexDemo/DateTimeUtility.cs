
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
		/// <summary>
		/// Indicates whether or not the precision clock is available
		/// </summary>
		public static bool HasPreciseTime { get; }  = _HasPreciseTime();

		static bool _HasPreciseTime()
		{
			// check for windows 8 or higher (winNT 6.2)
			if (PlatformID.Win32NT!=Environment.OSVersion.Platform)
				return false;
			if (6>Environment.OSVersion.Version.Major) return false;
			if (6==Environment.OSVersion.Version.Major && 2>Environment.OSVersion.Version.Minor)
				return false;
			try
			{
				long filetime;
				_Win32.GetSystemTimePreciseAsFileTime(out filetime);
				return true;
			}
			catch (EntryPointNotFoundException)
			{
				return false;
			}
		}
		
		/// <summary>
		/// Retrieves the UTC time, precise if available
		/// </summary>
		public static DateTime UtcNow 
			{ 
			get {
				if (!HasPreciseTime)
					return DateTime.UtcNow;
				long result; 
				_Win32.GetSystemTimePreciseAsFileTime(out result); 
				return DateTime.FromFileTimeUtc(result);
			} 
		}
	}
}
