using Mono.Cecil;
using System;
using System.IO;
using UnityEngine;

namespace NGUnityVersioner
{
	[Serializable]
	public class PropertyMeta : IMeta, ISerializationCallbackReceiver
	{
		[SerializeField]
		private string	name;
		public string	Name { get { return this.name; } }
		[SerializeField]
		private string	errorMessage;
		public string	ErrorMessage { get { return this.errorMessage; } }

		[SerializeField]
		private string	declaringType;
		public string	DeclaringType { get { return this.declaringType; } }
		[SerializeField]
		private string	type;
		public string	Type { get { return this.type; } }

		[SerializeField]
		private bool	hasGetter;
		public bool		HasGetter { get { return this.hasGetter; } }
		[SerializeField]
		private bool	hasSetter;
		public bool		HasSetter { get { return this.hasSetter; } }

		public readonly AssemblyMeta	root;

		public	PropertyMeta(AssemblyMeta root, TypeMeta declaringType, BinaryReader reader)
		{
			this.root = root;
			this.name = root.FetchString(reader.ReadInt24());

			this.declaringType = declaringType.FullName;
			this.type = root.FetchString(reader.ReadInt24());

			byte	flags = reader.ReadByte();

			this.hasGetter = (flags & 1) != 0;
			this.hasSetter = (flags & 2) != 0;
			if ((flags & 4) != 0)
				this.errorMessage = root.FetchString(reader.ReadInt24());
		}

		public	PropertyMeta(AssemblyMeta root, PropertyDefinition property)
		{
			this.root = root;
			this.name = property.Name;
			this.errorMessage = AssemblyMeta.GetObsoleteMessage(property);

			this.declaringType = property.DeclaringType.FullName;
			this.type = property.PropertyType.FullName;

			this.hasGetter = property.GetMethod != null;
			this.hasSetter = property.SetMethod != null;
		}

		public void	Save(BinaryWriter writer)
		{
			writer.WriteInt24(this.root.RegisterString(this.Name));
			writer.WriteInt24(this.root.RegisterString(this.Type));
			writer.Write((Byte)((this.HasGetter ? 1 : 0) | (this.HasSetter ? 2 : 0) | (this.ErrorMessage != null ? 4 : 0)));
			if (this.ErrorMessage != null)
				writer.WriteInt24(this.root.RegisterString(this.ErrorMessage));
		}

		public override string	ToString()
		{
			return this.Type + " " + this.DeclaringType + "::" + this.Name + " (" + (this.HasGetter ? "get;" : string.Empty) + (this.HasSetter ? "set;" : string.Empty) + ")";
		}

		// Hack to bypass null-to-empty string serialization.
		[SerializeField]
		private bool	hasError;

		void	ISerializationCallbackReceiver.OnBeforeSerialize()
		{
			this.hasError = this.ErrorMessage != null;
		}

		void	ISerializationCallbackReceiver.OnAfterDeserialize()
		{
			if (this.hasError == false)
				this.errorMessage = null;
		}
	}
}