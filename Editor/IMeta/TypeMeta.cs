using Mono.Cecil;
using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace NGUnityVersioner
{
	[Serializable]
	public class TypeMeta : IMeta, ISerializationCallbackReceiver
	{
		public string	FullName
		{
			get
			{
				if (string.IsNullOrEmpty(this.@namespace) == true)
					return this.name;
				return this.@namespace + "." + this.name;
			}
		}

		// Keep this notation for later use of C# >7
		//[field:SerializeField]
		//public string	Name { get; private set; }

		[SerializeField]
		private string	name;
		public string	Name { get { return this.name; } }
		[SerializeField]
		private string	errorMessage;
		public string	ErrorMessage { get { return this.errorMessage; } }

		[SerializeField]
		private string			@namespace;
		public string			Namespace { get { return this.@namespace; } }
		[SerializeField]
		private bool			isPublic;
		public bool				IsPublic { get { return this.isPublic; } }
		[SerializeField]
		private EventMeta[]		events;
		public EventMeta[]		Events { get { return this.events; } }
		[SerializeField]
		private FieldMeta[]		fields;
		public FieldMeta[]		Fields { get { return this.fields; } }
		[SerializeField]
		private PropertyMeta[]	properties;
		public PropertyMeta[]	Properties { get { return this.properties; } }
		[SerializeField]
		private MethodMeta[]	methods;
		public MethodMeta[]		Methods { get { return this.methods; } }

		public	TypeMeta(ISharedTable stringTable, BinaryReader reader)
		{
			byte[]	rawData = reader.ReadBytes(7);

			this.@namespace = stringTable.FetchString(rawData[0] | (rawData[1] << 8) | (rawData[2] << 16));
			this.name = stringTable.FetchString(rawData[3] | (rawData[4] << 8) | (rawData[5] << 16));

			byte	flags = rawData[6];

			this.isPublic = (flags & 1) != 0;
			if ((flags & 4) != 0)
				this.errorMessage = stringTable.FetchString(reader.ReadInt24());

			if ((flags & 8) != 0)
				this.events = new EventMeta[reader.ReadUInt16()];
			else
				this.events = new EventMeta[reader.ReadByte()];

			for (int i = 0, max = this.events.Length; i < max; ++i)
				this.events[i] = stringTable.FetchEvent(reader.ReadInt24());

			if ((flags & 16) != 0)
				this.fields = new FieldMeta[reader.ReadUInt16()];
			else
				this.fields = new FieldMeta[reader.ReadByte()];

			for (int i = 0, max = this.fields.Length; i < max; ++i)
					this.fields[i] = stringTable.FetchField(reader.ReadInt24());

			if ((flags & 32) != 0)
				this.properties = new PropertyMeta[reader.ReadUInt16()];
			else
				this.properties = new PropertyMeta[reader.ReadByte()];

			for (int i = 0, max = this.properties.Length; i < max; ++i)
				this.properties[i] = stringTable.FetchProperty(reader.ReadInt24());

			if ((flags & 64) != 0)
				this.methods = new MethodMeta[reader.ReadUInt16()];
			else
				this.methods = new MethodMeta[reader.ReadByte()];

			for (int i = 0, max = this.methods.Length; i < max; ++i)
				this.methods[i] = stringTable.FetchMethod(reader.ReadInt24());
		}

		public	TypeMeta(TypeDefinition typeDef)
		{
			this.@namespace = typeDef.Namespace;
			this.name = typeDef.Name;
			this.isPublic = typeDef.IsPublic;
			this.errorMessage = Utility.GetObsoleteMessage(typeDef);

			if (typeDef.HasEvents == true)
			{
				this.events = new EventMeta[typeDef.Events.Count];
				for (int i = 0, max = typeDef.Events.Count; i < max; ++i)
					this.events[i] = new EventMeta(typeDef.Events[i]);
			}
			else
				this.events = new EventMeta[0];

			if (typeDef.HasFields == true)
			{
				this.fields = new FieldMeta[typeDef.Fields.Count];
				for (int i = 0, max = typeDef.Fields.Count; i < max; ++i)
					this.fields[i] = new FieldMeta(typeDef.Fields[i]);
			}
			else
				this.fields = new FieldMeta[0];

			if (typeDef.HasProperties == true)
			{
				this.properties = new PropertyMeta[typeDef.Properties.Count];
				for (int i = 0, max = typeDef.Properties.Count; i < max; ++i)
					this.properties[i] = new PropertyMeta(typeDef.Properties[i]);
			}
			else
				this.properties = new PropertyMeta[0];

			if (typeDef.HasMethods == true)
			{
				this.methods = new MethodMeta[typeDef.Methods.Count];
				for (int i = 0, max = typeDef.Methods.Count; i < max; ++i)
					this.methods[i] = new MethodMeta(typeDef.Methods[i]);
			}
			else
				this.methods = new MethodMeta[0];
		}

		public	TypeMeta(TypeReference typeRef, string error = null)
		{
			this.@namespace = typeRef.Namespace;
			this.errorMessage = error;

			if (typeRef.IsGenericInstance == true)
				this.name = typeRef.FullName.Substring(this.@namespace.Length + 1);
			else if (typeRef.HasGenericParameters == true)
			{
				StringBuilder	buffer = Utility.GetBuffer();

				buffer.Append(typeRef.Name);
				buffer.Append('<');

				for (int i = 0, max = typeRef.GenericParameters.Count; i < max; ++i)
				{
					GenericParameter	param = typeRef.GenericParameters[i];

					buffer.Append(param.FullName);
				}
				buffer.Append('>');

				this.name = buffer.ToString();
			}
			else
				this.name = typeRef.Name;
		}

		public EventMeta	Resolve(EventReference eventRef)
		{
			for (int i = 0, max = this.events.Length; i < max; ++i)
			{
				EventMeta	@event = this.events[i];

				if (@event.Name == eventRef.Name)
					return @event;
			}

			return null;
		}

		public FieldMeta	Resolve(FieldReference fieldRef)
		{
			string	fieldRefName = fieldRef.Name;

			for (int i = 0, max = this.fields.Length; i < max; ++i)
			{
				FieldMeta	field = this.fields[i];

				if (field.Name == fieldRefName)
					return field;
			}

			return null;
		}

		public PropertyMeta	Resolve(PropertyReference propertyRef)
		{
			string	propertyRefName = propertyRef.Name;

			for (int i = 0, max = this.properties.Length; i < max; ++i)
			{
				PropertyMeta	property = this.properties[i];

				if (property.Name == propertyRefName)
					return property;
			}

			return null;
		}

		public MethodMeta	Resolve(MethodReference methodRef)
		{
			string	methodName = methodRef.Name;
			int		targetParametersCount = methodRef.Parameters.Count;
			bool	hasAMatchingMethod = false;

			for (int i = 0, max = this.methods.Length; i < max; ++i)
			{
				MethodMeta	method = this.methods[i];

				if (method.Name == methodName)
				{
					hasAMatchingMethod = true;

					if (targetParametersCount == method.ParametersType.Length)
					{
						int	j = 0;

						for (; j < targetParametersCount; ++j)
						{
							TypeReference	paramType = methodRef.Parameters[j].ParameterType;
							string			targetParameterType = method.ParametersType[j];

							if (paramType.IsGenericParameter == true ||
								paramType.ContainsGenericParameter == true)
							{
								if (paramType.FullName.Replace("!!0", "T").Replace("!0", "T") != method.ParametersType[j])
									break;
							}
							else if (paramType.FullName != targetParameterType)
								break;
						}

						if (j == targetParametersCount)
						{
							return method;
							// Due to inheritage, checking accessor might be cumbersome to resolve. (I am being lazy)
							//if (method.IsPublic == true || this.root.IsFriend(methodRef.Module.Assembly.Name.Name) == true)
							//{
							//	Console.WriteLine("Found Method " + method + " " + method.IsPublic + " " + methodRef.Module.Assembly.Name.Name);
							//	return method;
							//}

							//break;
						}
					}
				}
			}

			// Return a method with an unmatch message.
			if (hasAMatchingMethod == true)
				return new MethodMeta(methodRef, "Method signature does not exist, but an overload is available.");

			return null;
		}

		public void	Save(ISharedTable stringTable, BinaryWriter writer)
		{
			bool	manyEvents = this.events.Length > 256;
			bool	manyFields = this.fields.Length > 256;
			bool	manyProperties = this.properties.Length > 256;
			bool	manyMethods = this.methods.Length > 256;

			writer.WriteInt24(stringTable.RegisterString(this.@namespace));
			writer.WriteInt24(stringTable.RegisterString(this.name));

			byte	b = (Byte)((this.IsPublic ? 1 : 0) |
							   (this.ErrorMessage != null ? 4 : 0) |
							   (manyEvents == true ? 8 : 0) |
							   (manyFields == true ? 16 : 0) |
							   (manyProperties == true ? 32 : 0) |
							   (manyMethods == true ? 64 : 0));

			writer.Write(b);

			if (this.ErrorMessage != null)
				writer.WriteInt24(stringTable.RegisterString(this.errorMessage));

			if (manyEvents == true)
				writer.Write((UInt16)this.events.Length);
			else
				writer.Write((Byte)this.events.Length);

			for (int i = 0, max = this.events.Length; i < max; ++i)
				writer.WriteInt24(stringTable.RegisterEvent(this.events[i]));

			if (manyFields == true)
				writer.Write((UInt16)this.fields.Length);
			else
				writer.Write((Byte)this.fields.Length);

			for (int i = 0, max = this.fields.Length; i < max; ++i)
				writer.WriteInt24(stringTable.RegisterField(this.fields[i]));

			if (manyProperties == true)
				writer.Write((UInt16)this.properties.Length);
			else
				writer.Write((Byte)this.properties.Length);

			for (int i = 0, max = this.properties.Length; i < max; ++i)
				writer.WriteInt24(stringTable.RegisterProperty(this.properties[i]));

			if (manyMethods == true)
				writer.Write((UInt16)this.methods.Length);
			else
				writer.Write((Byte)this.methods.Length);

			for (int i = 0, max = this.methods.Length; i < max; ++i)
				writer.WriteInt24(stringTable.RegisterMethod(this.methods[i]));
		}

		public int	GetSignatureHash()
		{
			StringBuilder	buffer = Utility.GetBuffer();

			buffer.Append(this.@namespace);
			buffer.Append(this.name);
			buffer.Append(this.errorMessage);
			buffer.Append(this.isPublic);

			for (int i = 0, max = this.events.Length; i < max; ++i)
			{
				buffer.Append(this.events[i].Name);
				buffer.Append(this.events[i].ErrorMessage);
				buffer.Append(this.events[i].DeclaringType);
				buffer.Append(this.events[i].Type);
				buffer.Append(this.events[i].HasAdd);
				buffer.Append(this.events[i].HasRemove);
			}

			for (int i = 0, max = this.fields.Length; i < max; ++i)
			{
				buffer.Append(this.fields[i].Name);
				buffer.Append(this.fields[i].ErrorMessage);
				buffer.Append(this.fields[i].DeclaringType);
				buffer.Append(this.fields[i].Type);
			}

			for (int i = 0, max = this.properties.Length; i < max; ++i)
			{
				buffer.Append(this.properties[i].Name);
				buffer.Append(this.properties[i].ErrorMessage);
				buffer.Append(this.properties[i].DeclaringType);
				buffer.Append(this.properties[i].Type);
				buffer.Append(this.properties[i].HasGetter);
				buffer.Append(this.properties[i].HasSetter);
			}

			for (int i = 0, max = this.methods.Length; i < max; ++i)
			{
				buffer.Append(this.methods[i].Name);
				buffer.Append(this.methods[i].ErrorMessage);
				buffer.Append(this.methods[i].IsPublic);
				buffer.Append(this.methods[i].DeclaringType);
				buffer.Append(this.methods[i].ReturnType);

				for (int j = 0, max2 = this.methods[i].ParametersType.Length; j < max2; ++j)
				{
					buffer.Append(this.methods[i].ParametersType[j]);
					buffer.Append(this.methods[i].ParametersName[j]);
				}
			}

			return Utility.ReturnBuffer(buffer).GetHashCode();
		}

		public override string	ToString()
		{
			return this.FullName;
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