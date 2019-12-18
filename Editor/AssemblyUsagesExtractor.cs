using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Collections.Generic;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace NGUnityVersioner
{
	public static class AssemblyUsagesExtractor
	{
		private class AutoIndent : IDisposable
		{
			private static AutoIndent	instance;
			public static AutoIndent	Instance
			{
				get
				{
					if (instance == null)
						instance = new AutoIndent();
					++depth;
					return instance;
				}
			}

			private	AutoIndent()
			{
			}

			public void	Dispose()
			{
				--depth;
			}
		}

		internal static int	debug = 0;
		private static int	depth = 0;

		public static AssemblyUsages	InspectAssembly(IEnumerable<string> assembliesPath, string[] filterNamespaces, string[] targetNamespaces)
		{
			AssemblyUsages	result = new AssemblyUsages()
			{
				assemblies = new List<string>(assembliesPath),
				filterNamespaces = filterNamespaces,
				targetNamespaces = targetNamespaces,
			};

			foreach (string assemblyPath in assembliesPath)
			{
				using (AssemblyDefinition	assemblyDef = AssemblyDefinition.ReadAssembly(assemblyPath))
				{
					if (debug > 1) AssemblyUsagesExtractor.Log("Assembly {0}", assemblyDef);

					using (AutoIndent.Instance)
					{
						AssemblyUsagesExtractor.InspectAttributes(result, assemblyDef);
						AssemblyUsagesExtractor.InspectSecurityDeclarations(result, assemblyDef);

						foreach (ModuleDefinition module in assemblyDef.Modules)
							AssemblyUsagesExtractor.InspectModule(result, module);
					}
				}
			}

			return result;
		}

		public static AssemblyUsages	InspectAssembly(AssemblyDefinition assemblyDef, string[] filterNamespaces)
		{
			AssemblyUsages	result = new AssemblyUsages()
			{
				assemblies = new List<string>() { assemblyDef.FullName },
				filterNamespaces = filterNamespaces,
			};

			if (debug > 1)AssemblyUsagesExtractor.Log("Assembly {0}", assemblyDef);

			using (AutoIndent.Instance)
			{
				AssemblyUsagesExtractor.InspectAttributes(result, assemblyDef);
				AssemblyUsagesExtractor.InspectSecurityDeclarations(result, assemblyDef);

				foreach (ModuleDefinition module in assemblyDef.Modules)
					AssemblyUsagesExtractor.InspectModule(result, module);
			}

			return result;
		}

		private static void	InspectModule(AssemblyUsages result, ModuleDefinition moduleDef)
		{
			if (debug > 1)AssemblyUsagesExtractor.Log("Module {0}", moduleDef);

			using (AutoIndent.Instance)
			{
				AssemblyUsagesExtractor.InspectAttributes(result, moduleDef);

				if (moduleDef.HasTypes == true)
				{
					int	filterNamespacesLength = result.filterNamespaces.Length;

					foreach (TypeDefinition typeDef in moduleDef.Types)
					{
						if ((filterNamespacesLength == 0 || result.filterNamespaces.FirstOrDefault(ns => typeDef.Namespace.StartsWith(ns)) != null) &&
							(filterNamespacesLength != 0 || result.targetNamespaces.FirstOrDefault(ns => typeDef.Namespace.StartsWith(ns)) == null))
						{
							AssemblyUsagesExtractor.InspectType(result, typeDef);
						}
					}
				}
			}
		}

		public static void	InspectType(AssemblyUsages result, TypeDefinition typeDef)
		{
			if (debug > 1)AssemblyUsagesExtractor.Log("Type {0}", typeDef);

			using (AutoIndent.Instance)
			{
				AssemblyUsagesExtractor.InspectAttributes(result, typeDef);
				AssemblyUsagesExtractor.InspectSecurityDeclarations(result, typeDef);
				AssemblyUsagesExtractor.InspectGenericParameters(result, typeDef);

				if (typeDef.HasInterfaces)
				{
					foreach (InterfaceImplementation interfaceImpl in typeDef.Interfaces)
						AssemblyUsagesExtractor.InspectInterface(result, interfaceImpl);
				}

				if (typeDef.HasNestedTypes)
				{
					foreach (TypeDefinition nestedType in typeDef.NestedTypes)
						AssemblyUsagesExtractor.InspectType(result, nestedType);
				}

				if (typeDef.HasEvents)
				{
					foreach (EventDefinition @event in typeDef.Events)
						AssemblyUsagesExtractor.InspectEvent(result, @event);
				}

				if (typeDef.HasFields)
				{
					foreach (FieldDefinition field in typeDef.Fields)
						AssemblyUsagesExtractor.InspectField(result, field);
				}

				if (typeDef.HasProperties)
				{
					foreach (PropertyDefinition property in typeDef.Properties)
						AssemblyUsagesExtractor.InspectProperty(result, property);
				}

				foreach (MethodDefinition method in typeDef.Methods)
					AssemblyUsagesExtractor.InspectMethod(result, method);
			}
		}

		public static void	InspectInterface(AssemblyUsages result, InterfaceImplementation interfaceImpl)
		{
			if (debug > 1)AssemblyUsagesExtractor.Log("Interface {0}", interfaceImpl.InterfaceType);

			using (AutoIndent.Instance)
			{
				result.RegisterTypeRef(interfaceImpl.InterfaceType);
				AssemblyUsagesExtractor.InspectAttributes(result, interfaceImpl);
			}
		}

		private static void	InspectEvent(AssemblyUsages result, EventDefinition eventDef)
		{
			if (debug > 1)AssemblyUsagesExtractor.Log("Event {0}", eventDef);

			using (AutoIndent.Instance)
			{
				result.RegisterTypeRef(eventDef.EventType);
				AssemblyUsagesExtractor.InspectAttributes(result, eventDef);

				if (eventDef.AddMethod != null)
					AssemblyUsagesExtractor.InspectMethod(result, eventDef.AddMethod);

				if (eventDef.RemoveMethod != null)
					AssemblyUsagesExtractor.InspectMethod(result, eventDef.RemoveMethod);

				if (eventDef.InvokeMethod != null)
					AssemblyUsagesExtractor.InspectMethod(result, eventDef.InvokeMethod);
			}
		}

		public static void	InspectField(AssemblyUsages result, FieldDefinition field)
		{
			if (debug > 1)AssemblyUsagesExtractor.Log("Field {0}", field);

			using (AutoIndent.Instance)
			{
				result.RegisterTypeRef(field.FieldType);
				AssemblyUsagesExtractor.InspectAttributes(result, field);
			}
		}

		private static void	InspectProperty(AssemblyUsages result, PropertyDefinition propertyDef)
		{
			if (debug > 1)AssemblyUsagesExtractor.Log("Property {0}", propertyDef);

			using (AutoIndent.Instance)
			{
				result.RegisterTypeRef(propertyDef.PropertyType);
				AssemblyUsagesExtractor.InspectAttributes(result, propertyDef);
				AssemblyUsagesExtractor.InspectIndexer(result, propertyDef);

				if (propertyDef.GetMethod != null)
					AssemblyUsagesExtractor.InspectMethod(result, propertyDef.GetMethod);

				if (propertyDef.SetMethod != null)
					AssemblyUsagesExtractor.InspectMethod(result, propertyDef.SetMethod);
			}
		}

		public static void	InspectMethod(AssemblyUsages result, MethodDefinition methodDef)
		{
			if (debug > 1)AssemblyUsagesExtractor.Log("Method {0}", methodDef);

			using (AutoIndent.Instance)
			{
				AssemblyUsagesExtractor.InspectAttributes(result, methodDef);
				AssemblyUsagesExtractor.InspectSecurityDeclarations(result, methodDef);
				AssemblyUsagesExtractor.InspectGenericParameters(result, methodDef);
				AssemblyUsagesExtractor.InspectParameters(result, methodDef);
				AssemblyUsagesExtractor.InspectMethodReturnType(result, methodDef.MethodReturnType);

				if (methodDef.HasBody)
				{
					if (methodDef.Body.HasVariables)
					{
						foreach (VariableDefinition variable in methodDef.Body.Variables)
						{
							if (debug > 1)AssemblyUsagesExtractor.Log("Var {0} {1}", variable.Index, variable.VariableType);
							result.RegisterTypeRef(variable.VariableType);
						}
					}

					foreach (Instruction il in methodDef.Body.Instructions)
					{
						if (il.OpCode == OpCodes.Ldsfld || il.OpCode == OpCodes.Ldsflda || il.OpCode == OpCodes.Stsfld ||
							il.OpCode == OpCodes.Ldfld || il.OpCode == OpCodes.Ldflda || il.OpCode == OpCodes.Stfld)
						{
							FieldReference	fieldRef = il.Operand as FieldReference;

							if (result.RegisterFieldRef(fieldRef) == true)
								if (debug > 1)AssemblyUsagesExtractor.Log("Use Field {0}", fieldRef.FullName);
							else
								if (debug > 1)AssemblyUsagesExtractor.Log("Cache Use Field {0}", fieldRef.FullName);
						}
						else if (il.OpCode == OpCodes.Call || il.OpCode == OpCodes.Callvirt || il.OpCode == OpCodes.Calli ||
								 il.OpCode == OpCodes.Newobj || il.OpCode == OpCodes.Ldftn || il.OpCode == OpCodes.Ldvirtftn ||
								 il.OpCode == OpCodes.Jmp)
						{
							MethodReference	methodRef = il.Operand as MethodReference;

							if (methodRef != null)
							{
								if (result.RegisterMethodRef(methodRef) == true)
								{
									if (debug > 1)AssemblyUsagesExtractor.Log("Call {0}", methodRef.FullName);

									using (AutoIndent.Instance)
									{
										AssemblyUsagesExtractor.InspectParameters(result, methodRef);
									}
								}
								else
									if (debug > 1)AssemblyUsagesExtractor.Log("Cache Call {0}", methodRef.FullName);
							}
						}
						else if (il.OpCode == OpCodes.Castclass || il.OpCode == OpCodes.Isinst || il.OpCode == OpCodes.Box || il.OpCode == OpCodes.Newarr || il.OpCode == OpCodes.Ldobj || il.OpCode == OpCodes.Stobj || il.OpCode == OpCodes.Cpobj || il.OpCode == OpCodes.Unbox || il.OpCode == OpCodes.Unbox_Any || il.OpCode == OpCodes.Initobj || il.OpCode == OpCodes.Sizeof || il.OpCode == OpCodes.Refanyval || il.OpCode == OpCodes.Mkrefany || il.OpCode == OpCodes.Ldelema || il.OpCode == OpCodes.Ldelem_Any || il.OpCode == OpCodes.Constrained)
						{
							result.RegisterTypeRef(il.Operand as TypeReference);
						}
						else if (il.OpCode == OpCodes.Ldtoken)
						{
							TypeReference	typeRef = il.Operand as TypeReference;

							if (typeRef != null)
							{
								result.RegisterTypeRef(typeRef);
								continue;
							}

							FieldReference	fieldRef = il.Operand as FieldReference;

							if (fieldRef != null)
							{
								result.RegisterFieldRef(fieldRef);
								continue;
							}

							Debug.LogError("UNHANDLED OpCodes.Ldtoken " + il.Operand.GetType());
						}
					}
				}
			}
		}

		public static void	InspectMethodReturnType(AssemblyUsages result, MethodReturnType methodReturnType)
		{
			if (debug > 1)AssemblyUsagesExtractor.Log("Return {0}", methodReturnType.ReturnType);

			using (AutoIndent.Instance)
			{
				result.RegisterTypeRef(methodReturnType.ReturnType);
				AssemblyUsagesExtractor.InspectAttributes(result, methodReturnType);
			}
		}

		public static void	InspectGenericParameters(AssemblyUsages result, IGenericParameterProvider parameterProvider)
		{
			if (parameterProvider.HasGenericParameters == true)
			{
				using (AutoIndent.Instance)
				{
					foreach (GenericParameter parameter in parameterProvider.GenericParameters)
						AssemblyUsagesExtractor.InspectGenericParameter(result, parameter);
				}
			}
		}

		public static void	InspectGenericParameter(AssemblyUsages result, GenericParameter parameter)
		{
			if (debug > 1)AssemblyUsagesExtractor.Log("GenParam {0}", parameter);
			AssemblyUsagesExtractor.InspectAttributes(result, parameter);

			if (parameter.HasConstraints)
			{
				foreach (GenericParameterConstraint constraint in parameter.Constraints)
				{
					if (debug > 1)AssemblyUsagesExtractor.Log("Constraint {0}", constraint);
					result.RegisterTypeRef(constraint.ConstraintType);
					AssemblyUsagesExtractor.InspectAttributes(result, constraint);
				}
			}
		}

		public static void	InspectIndexer(AssemblyUsages result, PropertyDefinition propertyDef)
		{
			if (propertyDef.HasParameters == true)
				AssemblyUsagesExtractor.InspectParameters(result, propertyDef.Parameters);
		}

		public static void	InspectParameters(AssemblyUsages result, MethodReference methodRef)
		{
			if (methodRef.HasParameters == true)
				AssemblyUsagesExtractor.InspectParameters(result, methodRef.Parameters);
		}

		public static void	InspectParameters(AssemblyUsages result, Collection<ParameterDefinition> parameters)
		{
			foreach (ParameterDefinition parameter in parameters)
			{
				if (debug > 1)AssemblyUsagesExtractor.Log("Param {0} {1}", parameter.ParameterType, parameter.Name);

				using (AutoIndent.Instance)
				{
					result.RegisterTypeRef(parameter.ParameterType);
					AssemblyUsagesExtractor.InspectAttributes(result,parameter);
				}
			}
		}

		public static void	InspectSecurityDeclarations(AssemblyUsages result, ISecurityDeclarationProvider securityDeclarationProvider)
		{
			if (securityDeclarationProvider.HasSecurityDeclarations == true)
			{
				foreach (SecurityDeclaration securityDeclaration in securityDeclarationProvider.SecurityDeclarations)
				{
					if (debug > 1)AssemblyUsagesExtractor.Log("SecDecl {0}", securityDeclaration);

					using (AutoIndent.Instance)
					{
						AssemblyUsagesExtractor.InspectSecurityDeclaration(result, securityDeclaration);
					}
				}
			}
		}

		public static void	InspectSecurityDeclaration(AssemblyUsages result, SecurityDeclaration securityDeclaration)
		{
			if (securityDeclaration.HasSecurityAttributes == true)
			{
				foreach (SecurityAttribute attribute in securityDeclaration.SecurityAttributes)
				{
					if (debug > 1)AssemblyUsagesExtractor.Log("SecAttr {0}", attribute);

					using (AutoIndent.Instance)
					{
						AssemblyUsagesExtractor.InspectAttribute(result, attribute);
					}
				}
			}
		}

		public static void	InspectAttributes(AssemblyUsages result, ICustomAttributeProvider attributeProvider)
		{
			if (attributeProvider.HasCustomAttributes)
			{
				foreach (CustomAttribute attribute in attributeProvider.CustomAttributes)
				{
					AssemblyUsagesExtractor.InspectAttribute(result, attribute);
				}
			}
		}

		public static void	InspectAttribute(AssemblyUsages result, ICustomAttribute attribute)
		{
			if (debug > 1)AssemblyUsagesExtractor.Log("Attribute {0}", attribute.AttributeType);

			using (AutoIndent.Instance)
			{
				result.RegisterTypeRef(attribute.AttributeType);

				//try
				//{
				//	// Do we need to inspect an attribute deeper?
				//	if (attribute.HasConstructorArguments)
				//	{
				//		foreach (var arg in attribute.ConstructorArguments)
				//		{
				//			if (debug > 1)AssemblyUsagesExtractor.Log("Arg {0} {1}", arg.Type, arg.Value);
				//			result.RegisterTypeRef(arg.Type);
				//		}
				//	}
				//	if (attribute.HasFields)
				//	{
				//		foreach (var field in attribute.Fields)
				//		{
				//			if (debug > 1)AssemblyUsagesExtractor.Log("Field {0} {1} {2}", field.Name, field.Argument.Type, field.Argument.Value);
				//			result.RegisterTypeRef(field.Argument.Type);
				//		}
				//	}
				//	if (attribute.HasProperties)
				//	{
				//		foreach (var property in attribute.Properties)
				//		{
				//			if (debug > 1)AssemblyUsagesExtractor.Log("Property {0} {1} {2}", property.Name, property.Argument.Type, property.Argument.Value);
				//			result.RegisterTypeRef(property.Argument.Type);
				//		}
				//	}
				//}
				//catch (Exception ex)
				//{
				//	if (debug > 0)
				//		Console.WriteLine(ex.ToString());
				//}
			}
		}

		private static void	Log(string message, params object[] args)
		{
			if (debug > 1)
			{
				Console.Write(new string('\t', depth));
				Console.WriteLine(message, args);
			}
		}
	}
}