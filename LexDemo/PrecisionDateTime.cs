
using System;
using System.Runtime.InteropServices;

namespace LexDemo
{
	public static class PrecisionDateTime
	{
		public static bool IsAvailable { get; private set; }
		[DllImport("Kernel32.dll", CallingConvention = CallingConvention.Winapi)]
		private static extern void GetSystemTimePreciseAsFileTime(out long filetime); 
		public static DateTime UtcNow 
			{ 
			get { 
				if (!IsAvailable) 
				{ 
					throw new InvalidOperationException("High resolution clock isn't available."); 
				} 
				long filetime; 
				GetSystemTimePreciseAsFileTime(out filetime); 
				return DateTime.FromFileTimeUtc(filetime);
			} 
		}
		static PrecisionDateTime()
		{
			try 
			{ 
				long filetime; 
				GetSystemTimePreciseAsFileTime(out filetime); 
				IsAvailable = true; 
			}
			catch (EntryPointNotFoundException)
			{             // Not running Windows 8 or higher.             
				IsAvailable = false;
			}
		}

	}
}
