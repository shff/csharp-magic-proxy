using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using System.Reflection;
using System.Reflection.Emit;
using System.Collections;
using System.Threading;

using System.ComponentModel;

namespace MagicProxy
{
    public static class Core
    {
        public static object Proxy<T>()
        {
            var targetType = typeof(T);

            var proxyTypeName = targetType.FullName + "Proxy";

            var domain = Thread.GetDomain();
            var assemblyName = new AssemblyName("ProxyAssembly");
            var assemblyBuilder = domain.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.Run);
            var moduleBuilder = assemblyBuilder.DefineDynamicModule("ProxyModule");
            var typeBuilder = moduleBuilder.DefineType(proxyTypeName,
                TypeAttributes.Class | TypeAttributes.Public | TypeAttributes.Sealed,
                targetType, new Type[] { typeof(INotifyPropertyChanged), typeof(IDataErrorInfo) });

            var propertyBuilder = typeBuilder.DefineProperty("Error",
                PropertyAttributes.None, typeof(string), null);
            var indexerBuilder = typeBuilder.DefineProperty("Item",
                PropertyAttributes.None, typeof(string), new Type[] { typeof(string) });

            var propertyGetter = typeBuilder.DefineMethod("get_Error",
                MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.HideBySig | MethodAttributes.SpecialName,
                CallingConventions.Standard, typeof(string), new Type[] { });
            var PIL = propertyGetter.GetILGenerator();
            PIL.Emit(OpCodes.Ret);

            var indexerGetter = typeBuilder.DefineMethod("get_Item",
                MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.HideBySig | MethodAttributes.SpecialName,
                CallingConventions.Standard, typeof(string), new Type[] { typeof(string) });
            var IIL = indexerGetter.GetILGenerator();
            IIL.Emit(OpCodes.Ret);

            var eventBuilder = typeBuilder.DefineEvent("PropertyChanged",
                EventAttributes.None, typeof(PropertyChangedEventHandler));
            var fieldBuilder = typeBuilder.DefineField("PropertyChanged",
                typeof(PropertyChangedEventHandler), FieldAttributes.Private);
            DefineAddMethodForEvent(typeBuilder, typeof(PropertyChangedEventHandler), fieldBuilder, eventBuilder);
            DefineRemoveMethodForEvent(typeBuilder, typeof(PropertyChangedEventHandler), fieldBuilder, eventBuilder);

            foreach (var method in targetType.GetMethods().Where(a => a.Name.StartsWith("set_")))
            {
                var parameters = method.GetParameters().Select(a => a.ParameterType).ToArray();

                var methodBuilder = typeBuilder.DefineMethod(
                    method.Name,
                    MethodAttributes.Public | MethodAttributes.Virtual,
                    CallingConventions.Standard,
                    method.ReturnType,
                    parameters);

                var IL = methodBuilder.GetILGenerator();

                IL.Emit(OpCodes.Ldarg_0);
                for (int i = 0; i < parameters.Length; i++)
                    IL.Emit(OpCodes.Ldarg, i + 1);
                IL.EmitCall(OpCodes.Call, method, null);

                IL.Emit(OpCodes.Ldarg_0);
                IL.Emit(OpCodes.Ldfld, fieldBuilder);
                IL.Emit(OpCodes.Ldarg_0);
                IL.Emit(OpCodes.Ldstr, method.Name.Substring(4));
                IL.Emit(OpCodes.Newobj, typeof(PropertyChangedEventArgs).GetConstructor(new[] { typeof(string) }));
                IL.Emit(OpCodes.Call, typeof(PropertyChangedEventHandler).GetMethod("Invoke", new Type[] { typeof(object), typeof(PropertyChangedEventArgs) }));

                IL.Emit(OpCodes.Ret);
            }

            var proxyType = typeBuilder.CreateType();
            var instance = Activator.CreateInstance(proxyType);

            return instance;
        }

        private static void DefineRemoveMethodForEvent(TypeBuilder typeBuilder, Type eventHandlerType, FieldBuilder fieldBuilder, EventBuilder eventBuilder)
        {
            var removeMethodInfo = typeof(Delegate).GetMethod("Remove", BindingFlags.Public | BindingFlags.Static, null,
                                    new[] { typeof(Delegate), typeof(Delegate) }, null);

            var removeMethodBuilder = typeBuilder.DefineMethod("remove_PropertyChanged", MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.Virtual | MethodAttributes.SpecialName, typeof(void), new[] { eventHandlerType });
            var removeMethodGenerator = removeMethodBuilder.GetILGenerator();
            removeMethodGenerator.Emit(OpCodes.Ldarg_0);
            removeMethodGenerator.Emit(OpCodes.Ldarg_0);
            removeMethodGenerator.Emit(OpCodes.Ldfld, fieldBuilder);
            removeMethodGenerator.Emit(OpCodes.Ldarg_1);
            removeMethodGenerator.EmitCall(OpCodes.Call, removeMethodInfo, null);
            removeMethodGenerator.Emit(OpCodes.Castclass, eventHandlerType);
            removeMethodGenerator.Emit(OpCodes.Stfld, fieldBuilder);
            removeMethodGenerator.Emit(OpCodes.Ret);
            eventBuilder.SetAddOnMethod(removeMethodBuilder);
        }

        private static void DefineAddMethodForEvent(TypeBuilder typeBuilder, Type eventHandlerType, FieldBuilder fieldBuilder, EventBuilder eventBuilder)
        {
            var combineMethodInfo = typeof(Delegate).GetMethod("Combine", BindingFlags.Public | BindingFlags.Static, null,
                                     new[] { typeof(Delegate), typeof(Delegate) }, null);

            var addMethodBuilder = typeBuilder.DefineMethod("add_PropertyChanged", MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.Virtual | MethodAttributes.SpecialName, typeof(void), new[] { eventHandlerType });
            var addMethodGenerator = addMethodBuilder.GetILGenerator();
            addMethodGenerator.Emit(OpCodes.Ldarg_0);
            addMethodGenerator.Emit(OpCodes.Ldarg_0);
            addMethodGenerator.Emit(OpCodes.Ldfld, fieldBuilder);
            addMethodGenerator.Emit(OpCodes.Ldarg_1);
            addMethodGenerator.EmitCall(OpCodes.Call, combineMethodInfo, null);
            addMethodGenerator.Emit(OpCodes.Castclass, eventHandlerType);
            addMethodGenerator.Emit(OpCodes.Stfld, fieldBuilder);
            addMethodGenerator.Emit(OpCodes.Ret);
            eventBuilder.SetAddOnMethod(addMethodBuilder);
        }
    }
}
