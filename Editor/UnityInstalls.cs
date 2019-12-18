using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace NGUnityVersioner
{
	public static class UnityInstalls
	{
		public const string	UnityInstallPathsPrefKey = "NGUnityDetector_UnityInstallPaths";
		public const char	Separator = ';';

		public static event Action	UnityInstallsChanged;

		public static int	InstallPathsCount { get { UnityInstalls.LazyInitialize(); return UnityInstalls.installPaths.Count; } }
		public static int	UnityInstallsCount { get { UnityInstalls.LazyInitialize(); return UnityInstalls.unityInstalls.Count; } }

		public static IEnumerable<KeyValuePair<int, string>>	EachPath
		{
			get
			{
				UnityInstalls.LazyInitialize();

				for (int i = 0, max = UnityInstalls.installPaths.Count; i < max; ++i)
					yield return new KeyValuePair<int, string>(i, UnityInstalls.installPaths[i]);
			}
		}

		public static IEnumerable<KeyValuePair<string, string>>	EachInstall
		{
			get
			{
				UnityInstalls.LazyInitialize();

				foreach (var item in UnityInstalls.unityInstalls)
					yield return item;
			}
		}

		private static List<string>					installPaths;
		private static Dictionary<string, string>	unityInstalls;

		public static void	AddPath(string path = null)
		{
			UnityInstalls.LazyInitialize();

			if (path == null)
			{
				if (UnityInstalls.installPaths.Count > 0)
					UnityInstalls.installPaths.Add(UnityInstalls.installPaths[UnityInstalls.installPaths.Count - 1]);
				else
					UnityInstalls.installPaths.Add(string.Empty);
				UnityInstalls.Save();
			}
			else if (UnityInstalls.installPaths.Contains(path) == false)
			{
				UnityInstalls.installPaths.Add(path);
				UnityInstalls.UpdateUnityInstalls();
				UnityInstalls.Save();
			}
		}

		public static void	RemovePath(int i)
		{
			UnityInstalls.LazyInitialize();
			UnityInstalls.installPaths.RemoveAt(i);
			UnityInstalls.UpdateUnityInstalls();
			UnityInstalls.Save();
		}

		public static void	SetPath(int i, string path)
		{
			UnityInstalls.LazyInitialize();

			if (i >= 0 && i < UnityInstalls.installPaths.Count)
				UnityInstalls.installPaths[i] = path;

			UnityInstalls.UpdateUnityInstalls();
			UnityInstalls.Save();
		}

		/// <summary>Returns the path of the Unity installation corresponding to the version. Returns null if none is available.</summary>
		/// <param name="unityVersion"></param>
		/// <returns></returns>
		public static string	GetUnityExecutable(string unityVersion)
		{
			UnityInstalls.LazyInitialize();

			string	path;

			if (UnityInstalls.unityInstalls.TryGetValue(unityVersion, out path) == true)
				return Path.Combine(path, @"Editor\Unity.exe");
			return null;
		}

		private static void	UpdateUnityInstalls()
		{
			UnityInstalls.unityInstalls.Clear();

			for (int i = 0, max = UnityInstalls.installPaths.Count; i < max; i++)
				UnityInstalls.ExtractUnityInstalls(UnityInstalls.installPaths[i]);

			if (UnityInstalls.UnityInstallsChanged != null)
				UnityInstalls.UnityInstallsChanged();
		}

		private static void	ExtractUnityInstalls(string path)
		{
			if (Directory.Exists(path) == false)
				return;

			string[]	dirs = Directory.GetDirectories(path);

			for (int j = 0, max = dirs.Length; j < max; j++)
			{
				path = Path.Combine(dirs[j], @"Editor\Uninstall.exe");

				if (File.Exists(path) == true)
				{
					string	version = Utility.GetUnityVersion(dirs[j]);

					if (UnityInstalls.unityInstalls.ContainsKey(version) == false)
						UnityInstalls.unityInstalls.Add(version, dirs[j]);
					else
						UnityInstalls.unityInstalls[version] = dirs[j];
				}
			}
		}

		/// <summary>Saves the paths in editor preferences in 100 ticks.</summary>
		private static void	Save()
		{
			if (UnityInstalls.installPaths != null)
				Utility.RegisterIntervalCallback(UnityInstalls.DelayedSave, 100);
		}

		private static void	DelayedSave()
		{
			EditorPrefs.SetString(UnityInstalls.UnityInstallPathsPrefKey, string.Join(UnityInstalls.Separator.ToString(), UnityInstalls.installPaths.ToArray()));
		}

		private static void	LazyInitialize()
		{
			if (UnityInstalls.installPaths == null)
			{
				try
				{
					string	rawPaths = EditorPrefs.GetString(UnityInstalls.UnityInstallPathsPrefKey);

					if (string.IsNullOrEmpty(rawPaths) == false)
					{
						string[]	paths = rawPaths.Split(UnityInstalls.Separator);

						if (paths.Length > 0)
						{
							UnityInstalls.installPaths = new List<string>(paths);
							UnityInstalls.unityInstalls = new Dictionary<string, string>();
							UnityInstalls.UpdateUnityInstalls();
						}
					}
				}
				catch (Exception ex)
				{
					Debug.LogException(ex);
				}
				finally
				{
					if (UnityInstalls.installPaths == null)
						UnityInstalls.installPaths = new List<string>();
					if (UnityInstalls.unityInstalls == null)
						UnityInstalls.unityInstalls = new Dictionary<string, string>();
				}
			}
		}
	}
}