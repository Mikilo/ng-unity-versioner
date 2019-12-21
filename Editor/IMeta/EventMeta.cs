using Mono.Cecil;
using System;
using System.IO;
using UnityEngine;

namespace NGUnityVersioner
{
	[Serializable]
	public class EventMeta : IMeta, ISerializationCallbackReceiver
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
		private bool	hasAdd;
		public bool		HasAdd { get { return this.hasAdd; } }
		[SerializeField]
		private bool	hasRemove;
		public bool		HasRemove { get { return this.hasRemove; } }

		public	EventMeta(IStringTable stringTable, TypeMeta declaringType, BinaryReader reader)
		{
			this.name = stringTable.FetchString(reader.ReadInt24());

			this.declaringType = declaringType.FullName;
			this.type = stringTable.FetchString(reader.ReadInt24());

			byte	flags = reader.ReadByte();

			this.hasAdd = (flags & 1) != 0;
			this.hasRemove = (flags & 2) != 0;
			if ((flags & 4) != 0)
				this.errorMessage = stringTable.FetchString(reader.ReadInt24());
		}

		public	EventMeta(EventDefinition @event)
		{
			this.name = @event.Name;
			this.errorMessage = Utility.GetObsoleteMessage(@event);

			this.declaringType = @event.DeclaringType.FullName;
			this.type = @event.EventType.FullName;

			this.hasAdd = @event.AddMethod != null;
			this.hasRemove = @event.RemoveMethod != null;
		}

		public void	Save(IStringTable stringTable, BinaryWriter writer)
		{
			writer.WriteInt24(stringTable.RegisterString(this.Name));
			writer.WriteInt24(stringTable.RegisterString(this.Type));
			writer.Write((Byte)((this.HasAdd ? 1 : 0) | (this.HasRemove ? 2 : 0) | (this.ErrorMessage != null ? 4 : 0)));
			if (this.ErrorMessage != null)
				writer.WriteInt24(stringTable.RegisterString(this.ErrorMessage));
		}

		public override string	ToString()
		{
			return this.Type + " " + this.DeclaringType + "::" + this.Name + " (" + (this.HasAdd ? "add;" : string.Empty) + (this.HasRemove ? "remove;" : string.Empty) + ")";
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