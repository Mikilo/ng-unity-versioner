using Mono.Cecil;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace NGUnityVersioner
{
	public class Utility : NGToolsStandaloneEditor.Utility
	{
		public static int	CompareVersionFromPath(string a, string b)
		{
			return Utility.CompareVersion(Path.GetFileNameWithoutExtension(a), Path.GetFileNameWithoutExtension(b));
		}

		public static int	CompareVersion(AssemblyUsagesResult a, AssemblyUsagesResult b)
		{
			return Utility.CompareVersion(a.unityMeta.Version, b.unityMeta.Version);
		}

		public static int	CompareVersion(string a, string b)
		{
			string[]	aParts = a.Split('.', 'a', 'b', 'f', 'r', 'p', 'x');
			string[]	bParts = b.Split('.', 'a', 'b', 'f', 'r', 'p', 'x');

			if (aParts.Length != bParts.Length)
				return bParts.Length - aParts.Length;

			try
			{
				for (int i = 0, max = aParts.Length; i < max; ++i)
				{
					int	aNum = Utility.ParseInt(aParts[i]);
					int	bNum = Utility.ParseInt(bParts[i]);

					if (aNum != bNum)
						return bNum - aNum;
				}
			}
			catch (Exception)
			{
				return 0;
			}

			return b.CompareTo(a);
		}

		public static int	ParseInt(string input)
		{
			int	n = 0;

			for (int i = 0, max = input.Length; i < max; ++i)
			{
				char	c = input[i];

				if (c >= '0' && c <= '9')
					n = n * 10 + c - '0';
				else
					break;
			}

			return n;
		}

		public static string	GetObsoleteMessage(ICustomAttributeProvider attributeProvider)
		{
			if (attributeProvider.HasCustomAttributes == true)
			{
				for (int i = 0, max = attributeProvider.CustomAttributes.Count; i < max; ++i)
				{
					CustomAttribute	attribute = attributeProvider.CustomAttributes[i];

					if (attribute.AttributeType.FullName == typeof(ObsoleteAttribute).FullName)
					{
						if (attribute.HasConstructorArguments == true)
							return attribute.ConstructorArguments[0].Value.ToString();
						return string.Empty;
					}
				}
			}

			return null;
		}

		private static Dictionary<string, string>	pathsVersions = new Dictionary<string, string>();

		/// <summary>Looks into Unity installs, then into ProjectSettings, then path.</summary>
		/// <param name="path"></param>
		/// <returns></returns>
		public static string	GetUnityVersion(string path)
		{
			string	version;

			if (Utility.pathsVersions.TryGetValue(path, out version) == true)
				return version;

			// Search into install directory.
			string	uninstallPath = Path.Combine(path, @"Editor\Uninstall.exe");

			if (File.Exists(uninstallPath) == true)
			{
				FileVersionInfo	fileVersion = FileVersionInfo.GetVersionInfo(uninstallPath);
				version = fileVersion.ProductName.Replace("Unity", string.Empty).Replace("(64-bit)", string.Empty).Replace(" ", string.Empty);

				Utility.pathsVersions.Add(path, version);
				return version;
			}

			// Search into Unity project.
			uninstallPath = Path.Combine(path, @"ProjectSettings\ProjectVersion.txt");

			if (File.Exists(uninstallPath) == true)
			{
				using (FileStream fs = File.Open(uninstallPath, FileMode.Open, FileAccess.Read, FileShare.Read))
				using (BufferedStream bs = new BufferedStream(fs))
				using (StreamReader sr = new StreamReader(bs))
				{
					string	line;

					while ((line = sr.ReadLine()) != null)
					{
						if (line.StartsWith("m_EditorVersion: ") == true)
						{
							version = line.Substring("m_EditorVersion: ".Length);
							Utility.pathsVersions.Add(path, version);
							return version;
						}
					}
				}
			}

			// Search through directory name.
			int	n = path.Length;

			if (n < 7)
			{
				version = string.Empty;
				Utility.pathsVersions.Add(path, version);
				return version;
			}

			string	filePath = path;

			// If path, assume and remove the extension.
			if (File.Exists(path) == true)
			{
				n = path.LastIndexOf('.');
				if (n == -1)
				{
					version = string.Empty;
					Utility.pathsVersions.Add(path, version);
					return version;
				}

				filePath = path.Substring(0, n);
			}

			// Minor version.
			int	dot = filePath.LastIndexOf('.', n - 1);
			if (dot == -1)
			{
				version = string.Empty;
				Utility.pathsVersions.Add(path, version);
				return version;
			}

			// Major version.
			dot = filePath.LastIndexOf('.', dot - 1);
			if (dot == -1)
			{
				version = string.Empty;
				Utility.pathsVersions.Add(path, version);
				return version;
			}

			// Find the earliest non-numeric char.
			int	offset = 1;

			if (offset < dot)
			{
				n = dot - offset - 1;

				while (n >= 0 && filePath[n] >= '0' && filePath[n] <= '9')
				{
					++offset;
					--n;
				}
			}

			string unityVersion = filePath.Substring(dot - offset, filePath.Length - (dot - offset));

			for (int i = unityVersion.LastIndexOf('.') + 1; i < unityVersion.Length; i++)
			{
				if ((unityVersion[i] < '0' || unityVersion[i] > '9') &&
					unityVersion[i] != 'a' && unityVersion[i] != 'b' && unityVersion[i] != 'f' && unityVersion[i] != 'p' && unityVersion[i] != 'x')
				{
					version = unityVersion.Substring(0, i);
					Utility.pathsVersions.Add(path, version);
					return version;
				}
			}

			Utility.pathsVersions.Add(path, unityVersion);
			return unityVersion;
		}
	}
}