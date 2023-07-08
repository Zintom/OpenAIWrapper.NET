using System;
using System.Reflection;

namespace Zintom.OpenAIWrapper.Misc;

#pragma warning disable IDE0066 // Convert switch statement to expression
internal static class ReflectionHelpers
{

    /// <summary>
    /// Determines if the given <see cref="Type"/> <paramref name="t"/> is a 'whole' number.
    /// </summary>
    /// <param name="t">The type</param>
    internal static bool IsIntegralNumber(Type t)
    {
        // https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/integral-numeric-types
        switch (Type.GetTypeCode(t))
        {
            case TypeCode.Byte:
            case TypeCode.SByte:
            case TypeCode.UInt16:
            case TypeCode.UInt32:
            case TypeCode.UInt64:
            case TypeCode.Int16:
            case TypeCode.Int32:
            case TypeCode.Int64:
                return true;
            default:
                return false;
        }
    }

    /// <summary>
    /// Determines if the given <see cref="Type"/> <paramref name="t"/> is a 'floating-point' number.
    /// </summary>
    /// <param name="t">The type</param>
    internal static bool IsFloatingPointNumber(Type t)
    {
        // https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/floating-point-numeric-types
        switch (Type.GetTypeCode(t))
        {
            case TypeCode.Decimal:
            case TypeCode.Double:
            case TypeCode.Single:
                return true;
            default:
                return false;
        }
    }

    /// <summary>
    /// Gets the first custom attribute <typeparamref name="T"/> from the given <paramref name="provider"/>.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="provider">Can be of type <see cref="MethodInfo"/>, <see cref="ParameterInfo"/> etc.</param>
    /// <returns></returns>
    internal static T? GetCustomAttributeFirstOrNull<T>(ICustomAttributeProvider provider) where T : Attribute
    {
        foreach (var attr in provider.GetCustomAttributes(typeof(T), false))
        {
            return (T?)attr;
        }

        return null;
    }

}
#pragma warning restore IDE0066 // Convert switch statement to expression
