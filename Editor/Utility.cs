using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;

namespace NGUnityVersioner
{
	/// <summary>
	/// <para>Gives this public non-static field a default value when calling Utility.LoadEditorPref.</para>
	/// <para>Only works on integer, float, bool, string and enum.</para>
	/// </summary>
	[AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
	public sealed class DefaultValueEditorPrefAttribute : Attribute
	{
		public readonly object	defaultValue;

		public	DefaultValueEditorPrefAttribute(object defaultValue)
		{
			this.defaultValue = defaultValue;
		}
	}

	public class Utility : NGToolsEditor.Utility
	{
		public static string	GetPerProjectPrefix()
		{
			return PlayerSettings.productName + '.';
		}

		public static IEnumerable<FieldInfo>	EachFieldHierarchyOrdered(Type t, Type stopType, BindingFlags flags)
		{
			var	inheritances = new Stack<Type>();

			inheritances.Push(t);

			if (t.BaseType != null)
			{
				while (t.BaseType != stopType)
				{
					inheritances.Push(t.BaseType);
					t = t.BaseType;
				}
			}

			foreach (var type in inheritances)
			{
				FieldInfo[]	fields = type.GetFields(flags | BindingFlags.DeclaredOnly);

				for (int i = 0, max = fields.Length; i < max; i++)
					yield return fields[i];
			}
		}

		public static void	ShowExplorer(string itemPath)
		{
			itemPath = itemPath.Replace(@"/", @"\"); // explorer doesn't like front slashes
			Process.Start("explorer.exe", "/select," + itemPath);
		}

		/// <summary>
		/// Checks if "ProjectSettings/ProjectVersion.txt" exists.
		/// </summary>
		/// <param name="path"></param>
		/// <returns></returns>
		public static bool	IsUnityProject(string path)
		{
			return string.IsNullOrEmpty(path) == false && (File.Exists(Path.Combine(path, "ProjectSettings/ProjectVersion.txt")) || Directory.Exists(Path.Combine(path, "ProjectSettings")));
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
					return version;
				}

				filePath = path.Substring(0, n);
			}

			// Minor version.
			int	dot = filePath.LastIndexOf('.', n - 1);
			if (dot == -1)
			{
				version = string.Empty;
				Utility.pathsVersions.Add(filePath, version);
				return version;
			}

			// Major version.
			dot = filePath.LastIndexOf('.', dot - 1);
			if (dot == -1)
			{
				version = string.Empty;
				Utility.pathsVersions.Add(filePath, version);
				return version;
			}

			// Find the earliest non-numeric char.
			int	offset = 1;
			while (filePath[dot - offset - 1] >= '0' && filePath[dot - offset - 1] <= '9')
				++offset;

			string	unityVersion = filePath.Substring(dot - offset, filePath.Length - (dot - offset));

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

	public static class CSharpExtension
	{
		public static void	WriteInt24(this BinaryWriter writer, int value)
		{
			writer.Write((Byte)(value & 0xFF));
			writer.Write((Byte)((value >> 8) & 0xFF));
			writer.Write((Byte)((value >> 16) & 0xFF));
		}

		public static int	ReadInt24(this BinaryReader reader)
		{
			return reader.ReadByte() | (reader.ReadByte() << 8) | (reader.ReadByte() << 16);
		}

		public static int		IndexOf(this StringBuilder buffer, string needle)
		{
			for (int i = 0; i<buffer.Length; i++)
			{
				if (buffer[i] == needle[0])
				{
					int	j = 1;

					++i;

					for (; j<needle.Length && i<buffer.Length; j++, ++i)
					{
						if (needle[j] != buffer[i])
							break;
					}

					if (j == needle.Length)
						return i - j;
				}
			}

			return -1;
		}

		private static Dictionary<Type, string>	cachedShortAssemblyTypes;

		public static string	GetShortAssemblyType(this Type t)
		{
			if (CSharpExtension.cachedShortAssemblyTypes == null)
				CSharpExtension.cachedShortAssemblyTypes = new Dictionary<Type, string>(32);

			string	shortAssemblyType;

			if (CSharpExtension.cachedShortAssemblyTypes.TryGetValue(t, out shortAssemblyType) == true)
				return shortAssemblyType;

			if (t.IsGenericType == true)
			{
				StringBuilder	buffer = new StringBuilder();
				Type			declaringType = t.DeclaringType;
				Type[]			types = t.GetGenericArguments();

				buffer.Append(t.Namespace);
				buffer.Append('.');

				while (declaringType != null)
				{
					buffer.Append(declaringType.Name);

					declaringType = declaringType.DeclaringType;
					buffer.Append('+');
				}

				buffer.Append(t.Name);
				buffer.Append("[");

				for (int i = 0, max = types.Length; i < max; i++)
				{
					if (i > 0)
						buffer.Append(',');

					buffer.Append("[");
					buffer.Append(types[i].GetShortAssemblyType());
					buffer.Append("]");
				}

				buffer.Append("]");

				if (t.Module.Name.StartsWith("mscorlib.dll") == false)
				{
					buffer.Append(',');
					buffer.Append(t.Module.Name.Substring(0, t.Module.Name.Length - ".dll".Length));
				}

				return buffer.ToString();
			}

			if (t.Module.Name.StartsWith("mscorlib.dll") == true)
				return t.ToString();
			return t.FullName + "," + t.Module.Name.Substring(0, t.Module.Name.Length - ".dll".Length);
		}

		public static readonly string[]	cachedHexaStrings = new string[256];
		
		public static string	ToHex(this sbyte input)
		{
			return CSharpExtension.ToHex((byte)input);
		}

		public static string	ToHex(this byte input)
		{
			if (CSharpExtension.cachedHexaStrings[input] == null)
			{
				if (input <= 16)
					CSharpExtension.cachedHexaStrings[input] = ((char)(input > 9 ? input + 0x37 + 0x20 : input + 0x30)).ToString();
				else
				{
					char[]	c = new char[2];
					byte	b;
			
					b = ((byte)(input >> 4));
					c[0] = (char)(b > 9 ? b + 0x37 + 0x20 : b + 0x30);

					b = ((byte)(input & 0x0F));
					c[1] = (char)(b > 9 ? b + 0x37 + 0x20 : b + 0x30);
					CSharpExtension.cachedHexaStrings[input] = new string(c);
				}
			}

			return CSharpExtension.cachedHexaStrings[input];
		}

		public static sbyte		HexToSByte(this string str)
		{
			return (sbyte)CSharpExtension.HexToByte(str);
		}

		public static byte		HexToByte(this string str)
		{
			if (str.Length == 0 || str.Length > 2)
				return 0;

			byte	buffer = 0;
			char	c;

			// Convert first half of byte
			c = str[0];
			buffer = (byte)(c > '9' ? (c > 'Z' ? (c - 'a' + 10) : (c - 'A' + 10)) : (c - '0'));

			if (str.Length > 1)
			{
				buffer <<= 4;

				// Convert second half of byte
				c = str[1];
				buffer |= (byte)(c > '9' ? (c > 'Z' ? (c - 'a' + 10) : (c - 'A' + 10)) : (c - '0'));
			}

			return buffer;
		}

		public static bool	IsStruct(this Type t)
		{
			return t.IsValueType == true && t.IsPrimitive == false && t.IsEnum == false && t != typeof(Decimal);
		}

		public static string[]	cachedNumbers;

		public static string	ToCachedString(this int i)
		{
			if (CSharpExtension.cachedNumbers == null)
				CSharpExtension.cachedNumbers = CSharpExtension.GenerateCachedNumbers(4096);

			if (0 <= i && i < CSharpExtension.cachedNumbers.Length)
				return CSharpExtension.cachedNumbers[i];
			return i.ToString();
		}

		private static string[]	GenerateCachedNumbers(int max)
		{
			string[]	array = new string[max];

			for (int i = 0; i < max; i++)
				array[i] = i.ToString();

			return array;
		}
	}
}