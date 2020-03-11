using Mono.Cecil;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace NGUnityVersioner
{
	[Serializable]
	public class UnityMeta
	{
		[SerializeField]
		private string	version;
		public string	Version { get { return this.version; } }

		[SerializeField]
		private AssemblyMeta[]	assembliesMeta;
		public AssemblyMeta[]	AssembliesMeta { get { return this.assembliesMeta; } }

		/// <summary>Creates a UnityMeta from a Unity installation path and automatically adds it to DatabaseMeta.</summary>
		/// <param name="unityInstallPath"></param>
		/// <returns></returns>
		public static UnityMeta	Create(string unityInstallPath)
		{
			UnityMeta	unityMeta;

			try
			{
				// Gather all assemblies referencing UnityEditor.
				string	unityEditor = Path.Combine(unityInstallPath, @"Editor\Data\Managed\UnityEditor.dll");

				if (File.Exists(unityEditor) == false)
				{
					Debug.LogError("Assembly at \"" + unityEditor + "\" was not found.");
					return null;
				}

				string[]	editorAssemblies = Directory.GetFiles(Path.Combine(unityInstallPath, @"Editor\Data\Managed"), "*.dll");

				for (int i = 0, max = editorAssemblies.Length; i < max; ++i)
				{
					using (AssemblyDefinition assemblyDef = AssemblyDefinition.ReadAssembly(editorAssemblies[i]))
					{
						int	j = 0;
						int	max2 = assemblyDef.Modules.Count;

						for (; j < max2; ++j)
						{
							ModuleDefinition	moduleDef = assemblyDef.Modules[j];

							if (moduleDef.HasAssemblyReferences == true)
							{
								int	k = 0;
								int	max3 = moduleDef.AssemblyReferences.Count;

								for (; k < max3; ++k)
								{
									if (moduleDef.AssemblyReferences[k].Name == "UnityEditor")
										break;
								}

								if (k < max3)
									break;
							}
						}

						if (j == max2)
							editorAssemblies[i] = null;
					}
				}

				string		runtimeAssembliesPath = Path.Combine(unityInstallPath, @"Editor\Data\Managed\UnityEngine");
				string[]	runtimeAssemblies;

				if (Directory.Exists(runtimeAssembliesPath) == true)
					runtimeAssemblies = Directory.GetFiles(runtimeAssembliesPath, "*.dll");
				else
				{
					// Switch to Unity <=2017.1.
					runtimeAssembliesPath = Path.Combine(unityInstallPath, @"Editor\Data\Managed\UnityEngine.dll");

					if (File.Exists(runtimeAssembliesPath) == false)
					{
						Debug.LogError("Runtime assembly at \"" + runtimeAssembliesPath + "\" was not found.");
						return null;
					}

					runtimeAssemblies = new string[] { runtimeAssembliesPath };
				}

				string		extensionAssembliesPath = Path.Combine(unityInstallPath, @"Editor\Data\UnityExtensions\Unity");
				string[]	extensionAssemblies;

				if (Directory.Exists(extensionAssembliesPath) == false) // Does not exist in Unity 4.5.
					extensionAssemblies = new string[0];
				else
					extensionAssemblies = Directory.GetFiles(extensionAssembliesPath, "*.dll", SearchOption.AllDirectories);

				List<AssemblyMeta>	meta = new List<AssemblyMeta>();

				try
				{
					meta.Add(new AssemblyMeta(unityEditor.Substring(unityInstallPath.Length + 1), unityEditor));
				}
				catch (Exception)
				{
					Debug.LogError("Main editor assembly \"" + unityEditor + "\" failed extraction.");
					throw;
				}

				for (int i = 0, max = runtimeAssemblies.Length; i < max; ++i)
				{
					try
					{
						meta.Add(new AssemblyMeta(runtimeAssemblies[i].Substring(unityInstallPath.Length + 1), runtimeAssemblies[i]));
					}
					catch (Exception)
					{
						Debug.LogError("Runtime assembly \"" + runtimeAssemblies[i] + "\" failed extraction.");
						throw;
					}
				}

				for (int i = 0, max = editorAssemblies.Length; i < max; ++i)
				{
					try
					{
						if (editorAssemblies[i] != null)
							meta.Add(new AssemblyMeta(editorAssemblies[i].Substring(unityInstallPath.Length + 1), editorAssemblies[i]));
					}
					catch (Exception)
					{
						Debug.LogError("Editor assembly \"" + editorAssemblies[i] + "\" failed extraction.");
						throw;
					}
				}

				for (int i = 0, max = extensionAssemblies.Length; i < max; ++i)
				{
					try
					{
						meta.Add(new AssemblyMeta(extensionAssemblies[i].Substring(unityInstallPath.Length + 1), extensionAssemblies[i]));
					}
					catch (Exception)
					{
						Debug.LogWarning("Extension assembly \"" + extensionAssemblies[i] + "\" failed extraction and has been discarded.");
					}
				}

				unityMeta = new UnityMeta(Utility.GetUnityVersion(unityInstallPath), meta.ToArray());
			}
			catch (Exception ex)
			{
				Debug.LogException(ex);
				throw;
			}

			DatabaseMeta.GetDatabase().Add(unityMeta);

			return unityMeta;
		}

		public	UnityMeta(BinaryReader reader, ISharedTable sharedStringTable)
		{
			this.version = reader.ReadString();
			this.assembliesMeta = new AssemblyMeta[reader.ReadUInt16()];

			for (int i = 0, max = this.assembliesMeta.Length; i < max; ++i)
			{
				try
				{
					this.assembliesMeta[i] = sharedStringTable.FetchAssembly(reader.ReadInt24());
				}
				catch (Exception)
				{
					Debug.LogError("Assembly #" + i + " failed in Unity " + this.version + ".");
					throw;
				}
			}
		}

		public	UnityMeta(string version, AssemblyMeta[] assembliesMeta)
		{
			this.version = version;
			this.assembliesMeta = assembliesMeta;
		}

		public void	Save(BinaryWriter writer, SharedTable sharedStringTable)
		{
			writer.Write(this.version);

			// Write Types into the buffer to populate the string table.
			writer.Write((UInt16)this.assembliesMeta.Length);
			for (int i = 0, max = this.assembliesMeta.Length; i < max; ++i)
				writer.WriteInt24(sharedStringTable.RegisterAssembly(this.assembliesMeta[i]));
		}
	}
}