using System;
using System.Reflection;

namespace Rsdn.Janus
{
	public static class ApplicationInfo
	{
		public const string ApplicationName = "RSDN@Home";

		public const string Company = "RSDN Team";
		public const string Copyright = "Copyright © by RSDN Team (http://rsdn.ru), 2002-2012";

		public const string ResourcesNamespace = "Rsdn.Janus.Resources.";

		public const string MachineConfigFolder = "Janus";
		public const string AppDataFolder = "Janus";

		public static Version Version { get; }
		public static string FullVersion { get; }
		public static string NameWithVersion { get; }
		public static string NameWithVersionAndCopyright { get; }

		static ApplicationInfo()
		{
			var fileVerStr = Assembly
				.GetEntryAssembly()
				.GetCustomAttribute<AssemblyFileVersionAttribute>()
				.Version;

			Version = new Version(fileVerStr);
			FullVersion = Version.ToString();
			NameWithVersion = $"{ApplicationName} {Resources.Version.ToLower()} {FullVersion}";
			NameWithVersionAndCopyright = $"{NameWithVersion}, {Copyright}";
		}
	}
}