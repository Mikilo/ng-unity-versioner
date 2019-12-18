﻿using Mono.Cecil;
using System;
using System.IO;
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

		public readonly AssemblyMeta	root;

		public FieldMeta(AssemblyMeta root, TypeMeta declaringType, BinaryReader reader)
		{
			this.root = root;
			this.name = root.FetchString(reader.ReadInt24());
			this.errorMessage = root.FetchString(reader.ReadInt24());

			this.declaringType = declaringType.FullName;
			this.type = root.FetchString(reader.ReadInt24());
		}

		public	FieldMeta(AssemblyMeta root, FieldDefinition fieldDef)
		{
			this.root = root;
			this.name = fieldDef.Name;
			this.errorMessage = AssemblyMeta.GetObsoleteMessage(fieldDef);

			this.declaringType = fieldDef.DeclaringType.FullName;
			this.type = fieldDef.FieldType.FullName;
		}

		public	FieldMeta(FieldReference fieldRef)
		{
			this.name = fieldRef.Name;
			this.declaringType = fieldRef.DeclaringType.FullName;
			this.type = fieldRef.FieldType.FullName;
		}

		public void	Save(BinaryWriter writer)
		{
			writer.WriteInt24(this.root.RegisterString(this.Name));
			writer.WriteInt24(this.root.RegisterString(this.ErrorMessage));
			writer.WriteInt24(this.root.RegisterString(this.Type));
		}

		public override string	ToString()
		{
			return this.Type + " " + this.DeclaringType + "::" + this.Name;
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