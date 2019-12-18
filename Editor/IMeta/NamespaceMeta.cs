using Mono.Cecil;
using System;
using System.Collections.Generic;
using System.IO;

namespace NGUnityVersioner
{
	public class NamespaceMeta : IMeta
	{
		public string	Name { get; }
		public string	ErrorMessage { get; }

		public readonly List<NamespaceMeta>	Namespaces = new List<NamespaceMeta>();
		public readonly List<TypeMeta>		Types = new List<TypeMeta>();

		public	NamespaceMeta(IStringTable stringTable, BinaryReader reader)
		{
			this.Name = stringTable.FetchString(reader.ReadInt24());

			this.Namespaces = new List<NamespaceMeta>(reader.ReadByte());
			for (int i = 0, max = this.Namespaces.Count; i < max; ++i)
				this.Namespaces[i] = new NamespaceMeta(stringTable, reader);

			this.Types = new List<TypeMeta>(reader.ReadInt24());
			for (int i = 0, max = this.Types.Count; i < max; ++i)
				this.Types[i] = new TypeMeta(stringTable, reader);
		}

		public	NamespaceMeta(string name)
		{
			this.Name = name;
		}

		public TypeMeta	Resolve(AssemblyMeta assemblyMeta, TypeReference typeRef)
		{
			if (typeRef.Namespace.EndsWith(this.Name) == false)
				throw new Exception($"Mismatch namespace \"{typeRef.Namespace}\".");

			for (int i = 0, max = this.Types.Count; i < max; ++i)
			{
				TypeMeta	type = this.Types[i];

				if (type.Name == typeRef.Name)
				{
					if (type.IsPublic == true || assemblyMeta.IsFriend(typeRef.Module.Assembly.Name.Name) == true)
						return type;
					break;
				}
			}

			return null;
		}

		public void	Save(IStringTable stringTable, BinaryWriter writer)
		{
			writer.WriteInt24(stringTable.RegisterString(this.Name));

			writer.Write((Byte)this.Namespaces.Count);
			for (int i = 0, max = this.Namespaces.Count; i < max; ++i)
				this.Namespaces[i].Save(stringTable, writer);

			writer.WriteInt24(this.Types.Count);
			for (int i = 0, max = this.Types.Count; i < max; ++i)
				this.Types[i].Save(stringTable, writer);
		}

		public override string	ToString()
		{
			return this.Name;
		}
	}
}