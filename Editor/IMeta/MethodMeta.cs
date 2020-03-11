using Mono.Cecil;
using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace NGUnityVersioner
{
	[Serializable]
	public class MethodMeta : IMeta, ISerializationCallbackReceiver
	{
		[SerializeField]
		private string	name;
		public string	Name { get { return this.name; } }
		[SerializeField]
		private string	errorMessage;
		public string	ErrorMessage { get { return this.errorMessage; } }

		[SerializeField]
		private bool		isPublic;
		public bool			IsPublic { get { return this.isPublic; } }
		[SerializeField]
		private string		declaringType;
		public string		DeclaringType { get { return this.declaringType; } }
		[SerializeField]
		private string		returnType;
		public string		ReturnType { get { return this.returnType; } }
		[SerializeField]
		private string[]	parametersType;
		public string[]		ParametersType { get { return this.parametersType; } }
		[SerializeField]
		private string[]	parametersName;
		public string[]		ParametersName { get { return this.parametersName; } }

		public	MethodMeta(ISharedTable stringTable, TypeMeta declaringType, BinaryReader reader)
		{
			byte[]	rawData = reader.ReadBytes(12);

			this.name = stringTable.FetchString(rawData[0] | (rawData[1] << 8) | (rawData[2] << 16));
			this.declaringType = stringTable.FetchString(rawData[3] | (rawData[4] << 8) | (rawData[5] << 16));
			this.returnType = stringTable.FetchString(rawData[6] | (rawData[7] << 8) | (rawData[8] << 16));

			byte	flags = rawData[9];

			this.isPublic = (flags & 1) != 0;

			int	length = rawData[10] | (rawData[11] << 8);

			this.parametersType = new string[length];
			this.parametersName = new string[length];

			rawData = reader.ReadBytes(length * 6);

			for (int i = 0, j = 0; j < length; ++j, i += 6)
			{
				this.parametersType[j] = stringTable.FetchString(rawData[i] | (rawData[i + 1] << 8) | (rawData[i + 2] << 16));
				this.parametersName[j] = stringTable.FetchString(rawData[i + 3] | (rawData[i + 4] << 8) | (rawData[i + 5] << 16));
			}

			if ((flags & 4) != 0)
				this.errorMessage = stringTable.FetchString(reader.ReadInt24());
		}

		public	MethodMeta(MethodDefinition methodDef)
		{
			this.name = methodDef.Name;
			this.errorMessage = Utility.GetObsoleteMessage(methodDef);
			this.isPublic = methodDef.IsPublic;

			this.declaringType = methodDef.DeclaringType.FullName;
			this.returnType = methodDef.ReturnType.FullName;

			if (methodDef.HasParameters == true)
			{
				this.parametersType = new string[methodDef.Parameters.Count];
				this.parametersName = new string[methodDef.Parameters.Count];

				for (int i = 0, max = methodDef.Parameters.Count; i < max; ++i)
				{
					ParameterDefinition	paramDef = methodDef.Parameters[i];

					this.ParametersType[i] = paramDef.ParameterType.FullName;
					this.ParametersName[i] = paramDef.Name;
				}
			}
			else
			{
				this.parametersType = new string[0];
				this.parametersName = new string[0];
			}
		}

		public	MethodMeta(MethodReference methodRef, string error = null)
		{
			this.name = methodRef.Name;
			this.errorMessage = error;

			this.declaringType = methodRef.DeclaringType.FullName;
			this.returnType = methodRef.ReturnType.FullName;

			if (methodRef.HasParameters == true)
			{
				this.parametersType = new string[methodRef.Parameters.Count];
				this.parametersName = new string[methodRef.Parameters.Count];

				for (int i = 0, max = methodRef.Parameters.Count; i < max; ++i)
				{
					ParameterDefinition	paramDef = methodRef.Parameters[i];

					this.ParametersType[i] = paramDef.ParameterType.FullName;
					this.ParametersName[i] = paramDef.Name;
				}
			}
			else
			{
				this.parametersType = new string[0];
				this.parametersName = new string[0];
			}
		}

		public void	Save(ISharedTable stringTable, BinaryWriter writer)
		{
			writer.WriteInt24(stringTable.RegisterString(this.name));
			writer.WriteInt24(stringTable.RegisterString(this.declaringType));
			writer.WriteInt24(stringTable.RegisterString(this.returnType));
			writer.Write((Byte)((this.isPublic ? 1 : 0) | (this.errorMessage != null ? 4 : 0)));
			writer.Write((UInt16)this.parametersType.Length);

			for (int i = 0, max = this.parametersType.Length; i < max; ++i)
			{
				writer.WriteInt24(stringTable.RegisterString(this.parametersType[i]));
				writer.WriteInt24(stringTable.RegisterString(this.parametersName[i]));
			}

			if (this.errorMessage != null)
				writer.WriteInt24(stringTable.RegisterString(this.errorMessage));
		}

		public int	GetSignatureHash()
		{
			StringBuilder	buffer = Utility.GetBuffer();

			buffer.Append(this.name);
			buffer.Append(this.errorMessage);
			buffer.Append(this.isPublic);
			buffer.Append(this.declaringType);
			buffer.Append(this.returnType);

			for (int i = 0, max = this.parametersType.Length; i < max; ++i)
			{
				buffer.Append(this.parametersType[i]);
				buffer.Append(this.parametersName[i]);
			}

			return Utility.ReturnBuffer(buffer).GetHashCode();
		}

		public override string	ToString()
		{
			StringBuilder	buffer = new StringBuilder(this.returnType);

			buffer.Append(' ');
			buffer.Append(this.declaringType);
			buffer.Append("::");
			buffer.Append(this.name);
			buffer.Append('(');

			for (int i = 0, max = this.parametersType.Length; i < max; ++i)
			{
				if (i > 0)
					buffer.Append(',');
				buffer.Append(this.parametersType[i]);
			}
			buffer.Append(')');

			return buffer.ToString();
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