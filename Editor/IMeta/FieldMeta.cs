using Mono.Cecil;
using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace NGUnityVersioner
{
	[Serializable]
	public class FieldMeta : IMeta, ISerializationCallbackReceiver
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

		public	FieldMeta(ISharedTable stringTable, TypeMeta declaringType, BinaryReader reader)
		{
			byte[]	rawData = reader.ReadBytes(12);

			this.name = stringTable.FetchString(rawData[0] | (rawData[1] << 8) | (rawData[2] << 16));
			this.errorMessage = stringTable.FetchString(rawData[3] | (rawData[4] << 8) | (rawData[5] << 16));
			this.declaringType = stringTable.FetchString(rawData[6] | (rawData[7] << 8) | (rawData[8] << 16));
			this.type = stringTable.FetchString(rawData[9] | (rawData[10] << 8) | (rawData[11] << 16));
		}

		public	FieldMeta(FieldDefinition fieldDef)
		{
			this.name = fieldDef.Name;
			this.errorMessage = Utility.GetObsoleteMessage(fieldDef);

			this.declaringType = fieldDef.DeclaringType.FullName;
			this.type = fieldDef.FieldType.FullName;
		}

		public	FieldMeta(FieldReference fieldRef)
		{
			this.name = fieldRef.Name;
			this.declaringType = fieldRef.DeclaringType.FullName;
			this.type = fieldRef.FieldType.FullName;
		}

		public void	Save(ISharedTable stringTable, BinaryWriter writer)
		{
			writer.WriteInt24(stringTable.RegisterString(this.name));
			writer.WriteInt24(stringTable.RegisterString(this.errorMessage));
			writer.WriteInt24(stringTable.RegisterString(this.declaringType));
			writer.WriteInt24(stringTable.RegisterString(this.type));
		}

		public int	GetSignatureHash()
		{
			StringBuilder	buffer = Utility.GetBuffer();

			buffer.Append(this.name);
			buffer.Append(this.errorMessage);
			buffer.Append(this.declaringType);
			buffer.Append(this.type);

			return Utility.ReturnBuffer(buffer).GetHashCode();
		}

		public override string	ToString()
		{
			return this.type + " " + this.declaringType + "::" + this.name;
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