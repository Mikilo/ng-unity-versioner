using Mono.Cecil;
using System;
using System.Collections.Generic;

namespace NGUnityVersioner
{
	public class NamespaceMeta
	{
		public string	Name { get; }

		public readonly List<NamespaceMeta>	Namespaces = new List<NamespaceMeta>();
		public readonly List<TypeMeta>		Types = new List<TypeMeta>();

		public	NamespaceMeta(string name)
		{
			this.Name = name;
		}

		public TypeMeta	Resolve(AssemblyMeta assemblyMeta, TypeReference typeRef)
		{
			if (typeRef.Namespace.EndsWith(this.Name) == false)
				throw new Exception("Mismatch namespace \"" + typeRef.Namespace + "\".");

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

		public override string	ToString()
		{
			return this.Name;
		}
	}
}