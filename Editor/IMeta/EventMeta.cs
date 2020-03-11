using Mono.Cecil;
using System;
using System.IO;
using System.Text;
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

		public	EventMeta(ISharedTable stringTable, TypeMeta declaringType, BinaryReader reader)
		{
			byte[]	rawData = reader.ReadBytes(10);

			this.name = stringTable.FetchString(rawData[0] | (rawData[1] << 8) | (rawData[2] << 16));
			this.declaringType = stringTable.FetchString(rawData[3] | (rawData[4] << 8) | (rawData[5] << 16));
			this.type = stringTable.FetchString(rawData[6] | (rawData[7] << 8) | (rawData[8] << 16));

			byte	flags = rawData[9];

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

		public void	Save(ISharedTable stringTable, BinaryWriter writer)
		{
			writer.WriteInt24(stringTable.RegisterString(this.name));
			writer.WriteInt24(stringTable.RegisterString(this.declaringType));
			writer.WriteInt24(stringTable.RegisterString(this.type));
			writer.Write((Byte)((this.hasAdd ? 1 : 0) | (this.hasRemove ? 2 : 0) | (this.errorMessage != null ? 4 : 0)));
			if (this.errorMessage != null)
				writer.WriteInt24(stringTable.RegisterString(this.errorMessage));
		}

		public int	GetSignatureHash()
		{
			StringBuilder	buffer = Utility.GetBuffer();

			buffer.Append(this.name);
			buffer.Append(this.errorMessage);
			buffer.Append(this.declaringType);
			buffer.Append(this.type);
			buffer.Append(this.hasAdd);
			buffer.Append(this.hasRemove);

			return Utility.ReturnBuffer(buffer).GetHashCode();
		}

		public override string	ToString()
		{
			return this.type + " " + this.declaringType + "::" + this.name + " (" + (this.hasAdd ? "add;" : string.Empty) + (this.hasRemove ? "remove;" : string.Empty) + ")";
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