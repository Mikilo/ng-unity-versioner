using Mono.Cecil;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using UnityEngine;

namespace NGUnityVersioner
{
	[Serializable]
	public class AssemblyMeta : ISerializationCallbackReceiver
	{
		[SerializeField]
		private string			assemblyPath;
		public string			AssemblyPath { get { return this.assemblyPath; } }
		public NamespaceMeta	GlobalNamespace { get; private set; }
		[SerializeField]
		private TypeMeta[]		types;
		public TypeMeta[]		Types { get { return this.types; } }
		[SerializeField]
		private string[]		friendAssemblies;
		public string[]			FriendAssemblies { get { return this.friendAssemblies; } }

		private StringTable		stringTable = new StringTable();
		private string			lastFullNamespace = null;
		private NamespaceMeta	lastNamespace = null;
		private TypeReference	lastTypeRef;
		private TypeMeta		lastTypeRefAsMeta;

		public static void				Save(string filepath, params AssemblyMeta[] assemblies)
		{
			using (MemoryStream assembliesStream = new MemoryStream(1 << 20)) // 1MB
			using (BinaryWriter assembliesWriter = new BinaryWriter(assembliesStream))
			{
				StringTable	sharedStringTable = new StringTable();

				// Write assemblies into the buffer to populate the string table.
				for (int i = 0, max = assemblies.Length; i < max; ++i)
					assemblies[i].Save(assembliesWriter, sharedStringTable);

				using (BinaryWriter finalWriter = new BinaryWriter(File.Open(filepath, FileMode.Create, FileAccess.Write)))
				{
					using (MemoryStream stringTableStream = new MemoryStream(1 << 20))
					using (BinaryWriter stringTableWriter = new BinaryWriter(stringTableStream))
					{
						sharedStringTable.Save(stringTableWriter);

						using (MemoryStream GZipStringTableStream = new MemoryStream(1 << 20))
						{
							using (GZipStream compressionStream = new GZipStream(finalWriter.BaseStream, CompressionMode.Compress, true))
							{
								stringTableStream.WriteTo(compressionStream);
								assembliesStream.WriteTo(compressionStream);
							}
						}
					}
				}
			}
		}

		public static AssemblyMeta[]	Load(string filepath)
		{
			using (FileStream	fileStream = File.Open(filepath, FileMode.Open, FileAccess.Read))
			using (GZipStream	decompressionStream = new GZipStream(fileStream, CompressionMode.Decompress))
			{
				using (MemoryStream decompressedStringTableStream = new MemoryStream(1 << 20))
				{
					decompressionStream.CopyTo(decompressedStringTableStream);
					decompressedStringTableStream.Seek(0, SeekOrigin.Begin);

					using (BinaryReader reader = new BinaryReader(decompressedStringTableStream))
					{
						StringTable	sharedStringTable;

						sharedStringTable = new StringTable(reader);

						List<AssemblyMeta>	result = new List<AssemblyMeta>();

						while (reader.BaseStream.Position != reader.BaseStream.Length)
							result.Add(new AssemblyMeta(reader, sharedStringTable));

						return result.ToArray();
					}
				}
			}
		}

		public static string			GetObsoleteMessage(ICustomAttributeProvider attributeProvider)
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

		public	AssemblyMeta(BinaryReader reader, StringTable sharedStringTable = null)
		{
			if (sharedStringTable == null)
				this.stringTable = new StringTable(reader);
			else
				this.stringTable = sharedStringTable;

			this.assemblyPath = reader.ReadString();
			this.friendAssemblies = new string[reader.ReadUInt16()];
			for (int i = 0, max = this.FriendAssemblies.Length; i < max; ++i)
				this.FriendAssemblies[i] = reader.ReadString();

			this.GlobalNamespace = new NamespaceMeta(string.Empty);
			this.types = new TypeMeta[reader.ReadInt32()];

			for (int i = 0, max = this.Types.Length; i < max; ++i)
			{
				this.Types[i] = new TypeMeta(this.stringTable, reader);
				this.GenerateNamespace(this.Types[i].Namespace).Types.Add(this.Types[i]);
			}
		}

		public	AssemblyMeta(string assemblyPath)
		{
			this.assemblyPath = assemblyPath;

			using (AssemblyDefinition assemblyDef = AssemblyDefinition.ReadAssembly(assemblyPath))
			{
				if (assemblyDef.HasCustomAttributes == true)
				{
					List<string>	friendAssemblies = new List<string>() { assemblyDef.Name.Name };
					TypeReference	internalsVisible = assemblyDef.MainModule.ImportReference(typeof(System.Runtime.CompilerServices.InternalsVisibleToAttribute));

					for (int i = 0, max = assemblyDef.CustomAttributes.Count; i < max; ++i)
					{
						CustomAttribute	attribute = assemblyDef.CustomAttributes[i];

						if (attribute.AttributeType.FullName == internalsVisible.FullName)
						{
							for (int j = 0, max2 = attribute.ConstructorArguments.Count; j < max2; ++j)
								friendAssemblies.Add(attribute.ConstructorArguments[j].Value.ToString());
						}
					}

					this.friendAssemblies = friendAssemblies.ToArray();
				}
				else
					this.friendAssemblies = new string[] { assemblyDef.Name.Name };

				this.GlobalNamespace = new NamespaceMeta(string.Empty);

				List<TypeMeta>	types = new List<TypeMeta>(1024);

				for (int i = 0, max = assemblyDef.Modules.Count; i < max; ++i)
				{
					ModuleDefinition	moduleDef = assemblyDef.Modules[i];

					if (moduleDef.HasTypes == true)
					{
						for (int j = 0, max2 = moduleDef.Types.Count; j < max2; ++j)
						{
							NamespaceMeta	namespaceMeta = this.GenerateNamespace(moduleDef.Types[j].Namespace);

							TypeMeta	typeMeta = new TypeMeta(moduleDef.Types[j]);
							namespaceMeta.Types.Add(typeMeta);
							types.Add(typeMeta);
						}
					}
				}

				types.Sort((a, b) => a.Name.CompareTo(b.Name));
				this.types = types.ToArray();
			}
		}

		public NamespaceMeta	GenerateNamespace(string @namespace)
		{
			if (string.IsNullOrEmpty(@namespace) == true)
				return this.GlobalNamespace;

			string[]		targetNamespaces = @namespace.Split('.');
			NamespaceMeta	currentNamespace = this.GlobalNamespace;

			for (int n = 0, max = targetNamespaces.Length; n < max; ++n)
			{
				int	i = 0;
				int	namespacesCount = currentNamespace.Namespaces.Count;

				for (; i < namespacesCount; ++i)
				{
					NamespaceMeta	namespaceMeta = currentNamespace.Namespaces[i];

					if (namespaceMeta.Name == targetNamespaces[n])
					{
						currentNamespace = namespaceMeta;
						break;
					}
				}

				if (i == namespacesCount)
				{
					NamespaceMeta	newNamespace = new NamespaceMeta(targetNamespaces[n]);
					currentNamespace.Namespaces.Add(newNamespace);
					currentNamespace = newNamespace;
				}
			}

			return currentNamespace;
		}

		public NamespaceMeta	Resolve(string @namespace)
		{
			if (@namespace == string.Empty)
				return this.GlobalNamespace;

			if (this.lastFullNamespace == @namespace)
				return this.lastNamespace;

			string[]		targetNamespaces = @namespace.Split('.');
			NamespaceMeta	currentNamespace = this.GlobalNamespace;

			for (int n = 0, max = targetNamespaces.Length; n < max; ++n)
			{
				int	i = 0;
				int	namespacesCount = currentNamespace.Namespaces.Count;

				for (; i < namespacesCount; ++i)
				{
					NamespaceMeta	namespaceMeta = currentNamespace.Namespaces[i];

					if (namespaceMeta.Name == targetNamespaces[n])
					{
						currentNamespace = namespaceMeta;
						break;
					}
				}

				if (i == namespacesCount)
				{
					currentNamespace = null;
					break;
				}
			}

			this.lastFullNamespace = @namespace;
			this.lastNamespace = currentNamespace;

			return currentNamespace;
		}

		public TypeMeta		Resolve(TypeReference typeRef)
		{
			if (this.lastTypeRef == typeRef)
				return this.lastTypeRefAsMeta;

			NamespaceMeta	namespaceMeta = this.Resolve(typeRef.Namespace);

			this.lastTypeRef = typeRef;

			if (namespaceMeta != null)
			{
				this.lastTypeRefAsMeta = namespaceMeta.Resolve(this, typeRef);
				return this.lastTypeRefAsMeta;
			}

			this.lastTypeRefAsMeta = null;
			return null;
		}

		public EventMeta	Resolve(EventReference eventRef)
		{
			TypeMeta	type = this.Resolve(eventRef.DeclaringType);

			if (type != null)
				return type.Resolve(eventRef);
			return null;
		}

		public FieldMeta	Resolve(FieldReference fieldRef)
		{
			TypeMeta	type = this.Resolve(fieldRef.DeclaringType);

			if (type != null)
				return type.Resolve(fieldRef);
			return null;
		}

		public PropertyMeta	Resolve(PropertyReference propertyRef)
		{
			TypeMeta	type = this.Resolve(propertyRef.DeclaringType);

			if (type != null)
				return type.Resolve(propertyRef);
			return null;
		}

		public MethodMeta	Resolve(MethodReference methodRef)
		{
			TypeMeta	type = this.Resolve(methodRef.DeclaringType);

			if (type != null)
				return type.Resolve(methodRef);
			return null;
		}

		public bool	IsFriend(string assemblyName)
		{
			for (int i = 0, max = this.FriendAssemblies.Length; i < max; ++i)
			{
				if (this.FriendAssemblies[i] == assemblyName)
					return true;
			}

			return false;
		}

		public void	Save(BinaryWriter writer, StringTable sharedStringTable = null)
		{
			StringTable	temp = this.stringTable;

			this.stringTable = sharedStringTable ?? temp;

			using (MemoryStream memory = new MemoryStream(1 << 18)) // 256kB
			using (BinaryWriter subWriter = new BinaryWriter(memory))
			{
				// Write Types into the buffer to populate the string table.
				subWriter.Write(this.Types.Length);
				for (int i = 0, max = this.Types.Length; i < max; ++i)
					this.Types[i].Save(sharedStringTable, subWriter);

				if (sharedStringTable == null)
					this.stringTable.Save(writer);

				writer.Write(this.assemblyPath);
				writer.Write((UInt16)this.FriendAssemblies.Length);
				for (int i = 0, max = this.FriendAssemblies.Length; i < max; ++i)
					writer.Write(this.FriendAssemblies[i]);

				memory.WriteTo(writer.BaseStream);
			}

			this.stringTable = temp;
		}

		void	ISerializationCallbackReceiver.OnBeforeSerialize()
		{
		}

		void	ISerializationCallbackReceiver.OnAfterDeserialize()
		{
			if (this.Types != null)
			{
				this.GlobalNamespace = new NamespaceMeta(string.Empty);

				for (int i = 0, max = this.Types.Length; i < max; ++i)
					this.GenerateNamespace(this.Types[i].Namespace).Types.Add(this.Types[i]);
			}
		}
	}
}