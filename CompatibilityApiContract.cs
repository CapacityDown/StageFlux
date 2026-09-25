using System;
using System.Reflection;

namespace REPOJP.StagePhysicsEvents;

internal static class CompatibilityApiContract
{
    private const BindingFlags PublicStatic =
        BindingFlags.Public | BindingFlags.Static;

    internal static bool TryReadApiVersion(Type? apiType, out int apiVersion)
    {
        apiVersion = 0;
        if (apiType == null || !apiType.IsPublic)
        {
            return false;
        }

        try
        {
            FieldInfo? field = apiType.GetField("ApiVersion", PublicStatic);
            if (field != null && field.IsLiteral && field.FieldType == typeof(int))
            {
                object? value = field.GetRawConstantValue();
                if (value is int fieldVersion)
                {
                    apiVersion = fieldVersion;
                    return true;
                }
            }

            PropertyInfo? property = apiType.GetProperty("ApiVersion", PublicStatic);
            MethodInfo? getter = property?.GetGetMethod(nonPublic: false);
            if (property?.PropertyType == typeof(int) &&
                property.GetIndexParameters().Length == 0 &&
                getter?.IsStatic == true &&
                property.GetValue(null) is int propertyVersion)
            {
                apiVersion = propertyVersion;
                return true;
            }
        }
        catch
        {
            // The caller owns logging and fallback policy.
        }
        return false;
    }

    internal static MethodInfo? FindMethod(
        Type apiType,
        string name,
        Type returnType,
        params Type[] parameterTypes)
    {
        try
        {
            MethodInfo? method = apiType.GetMethod(
                name,
                PublicStatic,
                null,
                parameterTypes,
                null);
            return method?.ReturnType == returnType &&
                method.ContainsGenericParameters == false
                ? method
                : null;
        }
        catch
        {
            return null;
        }
    }
}
