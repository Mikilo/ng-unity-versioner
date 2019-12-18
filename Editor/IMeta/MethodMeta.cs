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

		public readonly AssemblyMeta	root;

		public	MethodMeta(AssemblyMeta root, TypeMeta declaringType, BinaryReader reader)
		{
			this.root = root;
			this.name = root.FetchString(reader.ReadInt24());

			this.declaringType = declaringType.FullName;
			this.returnType = root.FetchString(reader.ReadInt24());

			byte	flags = reader.ReadByte();

			this.isPublic = (flags & 1) != 0;

			int	length = reader.ReadUInt16();

			this.parametersType = new string[length];
			this.parametersName = new string[length];

			for (int i = 0, max = length; i < max; ++i)
			{
				this.parametersType[i] = root.FetchString(reader.ReadInt24());
				this.parametersName[i] = root.FetchString(reader.ReadInt24());
			}

			if ((flags & 4) != 0)
				this.errorMessage = root.FetchString(reader.ReadInt24());
		}

		public	MethodMeta(AssemblyMeta root, MethodDefinition methodDef)
		{
			this.root = root;
			this.name = methodDef.Name;
			this.errorMessage = AssemblyMeta.GetObsoleteMessage(methodDef);
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

		public void	Save(BinaryWriter writer)
		{
			writer.WriteInt24(this.root.RegisterString(this.Name));
			writer.WriteInt24(this.root.RegisterString(this.ReturnType));
			writer.Write((Byte)((this.IsPublic ? 1 : 0) | (this.ErrorMessage != null ? 4 : 0)));
			writer.Write((UInt16)this.ParametersType.Length);

			for (int i = 0, max = this.ParametersType.Length; i < max; ++i)
			{
				writer.WriteInt24(this.root.RegisterString(this.ParametersType[i]));
				writer.WriteInt24(this.root.RegisterString(this.ParametersName[i]));
			}

			if (this.ErrorMessage != null)
				writer.WriteInt24(this.root.RegisterString(this.ErrorMessage));
		}

		public override string	ToString()
		{
			StringBuilder	buffer = new StringBuilder(this.ReturnType);

			buffer.Append(' ');
			buffer.Append(this.DeclaringType);
			buffer.Append("::");
			buffer.Append(this.Name);
			buffer.Append('(');
			for (int i = 0, max = this.ParametersType.Length; i < max; ++i)
			{
				if (i > 0)
					buffer.Append(',');
				buffer.Append(this.ParametersType[i]);
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