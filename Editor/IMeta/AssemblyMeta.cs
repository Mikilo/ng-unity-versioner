using Mono.Cecil;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace NGUnityVersioner
{
	[Serializable]
	public class AssemblyMeta : IMetaSignature
	{
		[SerializeField]
		private string			assemblyPath;
		public string			AssemblyPath { get { return this.assemblyPath; } }
		public NamespaceMeta	globalNamespace;
		public NamespaceMeta	GlobalNamespace
		{
			get
			{
				if (this.globalNamespace == null)
				{
					this.globalNamespace = new NamespaceMeta(string.Empty);

					for (int i = 0, max = this.Types.Length; i < max; ++i)
						this.GenerateNamespace(this.Types[i].Namespace).Types.Add(this.Types[i]);
				}

				return this.globalNamespace;
			}
		}

		[SerializeField]
		private TypeMeta[]		types;
		public TypeMeta[]		Types { get { return this.types; } }
		[SerializeField]
		private string[]		friendAssemblies;
		public string[]			FriendAssemblies { get { return this.friendAssemblies; } }

		private Dictionary<string, NamespaceMeta>	namespaceCache = new Dictionary<string, NamespaceMeta>();
		private Dictionary<string, TypeMeta>		typeCache = new Dictionary<string, TypeMeta>();

		public	AssemblyMeta(ISharedTable sharedStringTable, BinaryReader reader)
		{
			this.assemblyPath = reader.ReadString();

			this.friendAssemblies = new string[reader.ReadUInt16()];

			byte[]	rawData = reader.ReadBytes(this.friendAssemblies.Length * 3);

			for (int i = 0, j = 0, max = this.friendAssemblies.Length; i < max; ++i, j += 3)
				this.friendAssemblies[i] = sharedStringTable.FetchString(rawData[j] | (rawData[j + 1] << 8) | (rawData[j + 2] << 16));

			this.types = new TypeMeta[reader.ReadInt32()];

			rawData = reader.ReadBytes(this.types.Length * 3);

			for (int i = 0, j = 0, max = this.types.Length; i < max; ++i, j += 3)
			{
				try
				{
					TypeMeta	typeMeta = sharedStringTable.FetchType(rawData[j] | (rawData[j + 1] << 8) | (rawData[j + 2] << 16));
					this.types[i] = typeMeta;
					this.typeCache.Add(typeMeta.FullName, typeMeta);
				}
				catch (Exception)
				{
					Debug.LogError("Type #" + i + " failed in assembly \"" + this.assemblyPath + "\".");
					throw;
				}
			}
		}

		public	AssemblyMeta(string relativePath, string assemblyPath)
		{
			this.assemblyPath = relativePath;

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

				List<TypeMeta>	types = new List<TypeMeta>(1024);

				for (int i = 0, max = assemblyDef.Modules.Count; i < max; ++i)
				{
					ModuleDefinition	moduleDef = assemblyDef.Modules[i];

					if (moduleDef.HasTypes == true)
					{
						for (int j = 0, max2 = moduleDef.Types.Count; j < max2; ++j)
							types.Add(new TypeMeta(moduleDef.Types[j]));
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

			NamespaceMeta	hitNamespace;

			if (this.namespaceCache.TryGetValue(@namespace, out hitNamespace) == true)
				return hitNamespace;

			string[]		targetNamespaces = @namespace.Split('.');
			NamespaceMeta	currentNamespace = this.GlobalNamespace;

			for (int n = 0, max = targetNamespaces.Length; n < max; ++n)
			{
				int		i = 0;
				int		namespacesCount = currentNamespace.Namespaces.Count;
				string	targetNamespace = targetNamespaces[n];

				for (; i < namespacesCount; ++i)
				{
					NamespaceMeta	namespaceMeta = currentNamespace.Namespaces[i];

					if (namespaceMeta.Name == targetNamespace)
					{
						currentNamespace = namespaceMeta;
						break;
					}
				}

				if (i == namespacesCount)
				{
					NamespaceMeta	newNamespace = new NamespaceMeta(targetNamespace);
					currentNamespace.Namespaces.Add(newNamespace);
					currentNamespace = newNamespace;
				}
			}

			this.namespaceCache.Add(@namespace, currentNamespace);

			return currentNamespace;
		}

		public NamespaceMeta	Resolve(string @namespace)
		{
			if (@namespace == string.Empty)
				return this.GlobalNamespace;

			NamespaceMeta	hitNamespace;

			if (this.namespaceCache.TryGetValue(@namespace, out hitNamespace) == true)
				return hitNamespace;

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

			this.namespaceCache.Add(@namespace, currentNamespace);

			return currentNamespace;
		}

		public TypeMeta		Resolve(TypeReference typeRef)
		{
			TypeMeta	typeMeta;

			this.typeCache.TryGetValue(this.GetFullname(typeRef), out typeMeta);

			return typeMeta;
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

		public int	GetSignatureHash()
		{
			StringBuilder	buffer = Utility.GetBuffer();

			buffer.Append(this.assemblyPath);
			for (int i = 0, max = this.friendAssemblies.Length; i < max; ++i)
				buffer.Append(this.friendAssemblies[i]);

			for (int i = 0, max = this.types.Length; i < max; ++i)
				buffer.Append(this.types[i].GetSignatureHash());

			return Utility.ReturnBuffer(buffer).GetHashCode();
		}

		public void	Save(SharedTable sharedStringTable, BinaryWriter writer)
		{
			writer.Write(this.assemblyPath);

			writer.Write((UInt16)this.friendAssemblies.Length);
			for (int i = 0, max = this.friendAssemblies.Length; i < max; ++i)
				writer.WriteInt24(sharedStringTable.RegisterString(this.friendAssemblies[i]));

			writer.Write(this.types.Length);
			for (int i = 0, max = this.types.Length; i < max; ++i)
			{
				try
				{
					writer.WriteInt24(sharedStringTable.RegisterType(this.types[i]));
				}
				catch (Exception ex)
				{
					Debug.LogError("Type #" + i + " failed.");
					Debug.LogException(ex);
					throw;
				}
			}
		}

		private string	GetFullname(TypeReference typeRef)
		{
			if (typeRef.IsGenericInstance == true)
			{
				string	ns = typeRef.Namespace;

				if (string.IsNullOrEmpty(ns) == false)
					return ns + '.' + typeRef.Name;
				return typeRef.Name;
			}

			return typeRef.FullName;
		}
	}
}