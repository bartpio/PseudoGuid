using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace PseudoGuid
{
    internal static class ArgumentNullExceptionPolyfill
    {
        public static void ThrowIfNull(object? argument, [CallerArgumentExpression(nameof(argument))] string? paramName = null)
        {
            if (argument is null)
            {
                throw new ArgumentNullException(paramName);
            }
        }
    }

    internal static class EnumPolyfill
    {
#if NET
        public static bool IsDefined<TKind>(TKind value) where TKind : struct, Enum => Enum.IsDefined(value);

        public static IEnumerable<TKind> GetValues<TKind>() where TKind : struct, Enum => Enum.GetValues<TKind>();

        public static IEnumerable<string> GetNames<TKind>() where TKind : struct, Enum => Enum.GetNames<TKind>();

        public static TKind Parse<TKind>(string name) where TKind : struct, Enum => Enum.Parse<TKind>(name);
#else
        public static bool IsDefined<TKind>(TKind value) where TKind : struct, Enum => Enum.IsDefined(typeof(TKind), value);

        public static IEnumerable<TKind> GetValues<TKind>() where TKind : struct, Enum => (TKind[])Enum.GetValues(typeof(TKind));

        public static IEnumerable<string> GetNames<TKind>() where TKind : struct, Enum => Enum.GetNames(typeof(TKind));

        public static TKind Parse<TKind>(string name) where TKind : struct, Enum => (TKind)Enum.Parse(typeof(TKind), name);
#endif
    }
}

#if !NET
namespace System.Runtime.CompilerServices
{
    [AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false, Inherited = false)]
    internal sealed class CallerArgumentExpressionAttribute(string parameterName) : Attribute
    {
        public string ParameterName { get; } = parameterName;
    }
}
#endif
