using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;

namespace Zintom.OpenAIWrapper.Models;

/// <summary>
/// Defines a function which can be executed by a language model.
/// <para/>
/// Use the <see cref="Builder"/> to instantiate this class.
/// </summary>
public sealed partial class FunctionDefinition
{
    private string _name = "";
    private string _description = "";
    private List<FunctionParameter> _parameters = null!; // The only way to get an instance of FunctionDefinition is going through the builder, which sets this field.
    private Delegate? _method;

    /// <summary>
    /// Cache to store the JSON schema after using ToJsonSchema so that it only calculates it once.
    /// </summary>
    private string? _jsonSchema = null;

    /// <summary>
    /// The name of this function.
    /// </summary>
    public string Name { get => _name; }

    /// <summary>
    /// The description of this function.
    /// </summary>
    public string Description { get => _description; }

    /// <summary>
    /// Should only be instantiated by the <see cref="Builder"/>.
    /// </summary>
    private FunctionDefinition() { }

    public static FunctionDefinition FromMethod(Delegate method)
    {
        var methodInfo = method.GetMethodInfo();
        var methodParameters = methodInfo.GetParameters();

        var functionDefinition = new FunctionDefinition() { _name = methodInfo.Name, _method = method };

        // Assign function description from MethodInfo.
        functionDefinition._description = GetAttribute<FunctionDescriptionAttribute>(methodInfo)?._description ?? throw new ModelRequiredAttributeMissingException(typeof(FunctionDescriptionAttribute), methodInfo.DeclaringType?.FullName + "." + methodInfo.Name);

        // Process all the method parameters.
        functionDefinition._parameters = new();
        foreach (var methodParameter in methodParameters)
        {
            string type = methodParameter.ParameterType.Name;
            if (IsNumericType(methodParameter.ParameterType))
            {
                type = "Number";
            }

            var p = new FunctionParameter()
            {
                _name = methodParameter.Name ?? "",
                _type = type
            };

            if (methodParameter.ParameterType == typeof(bool))
                p.isBoolean = true;

            var descriptionAttr = GetAttribute<ParamDescriptionAttribute>(methodParameter);
            p._description = descriptionAttr?._description ?? throw new ModelRequiredAttributeMissingException(typeof(ParamDescriptionAttribute), methodInfo.DeclaringType?.FullName + "." + methodInfo.Name, $"({methodParameter.ParameterType.Name}) {methodParameter.Name}");
            p._required = descriptionAttr?._required ?? false;

            var enumAttr = GetAttribute<EnumValuesAttribute>(methodParameter);
            p._enumValues = enumAttr?._possibleEnumValues;

            functionDefinition._parameters.Add(p);
        }

        return functionDefinition;
    }

    public class ModelRequiredAttributeMissingException : Exception
    {
        public ModelRequiredAttributeMissingException(Type attributeType, string? methodName = null, string? parameterName = null, string? parameterType = null) : base(GetMessage(attributeType, methodName, parameterName, parameterType))
        {

        }

        private static string GetMessage(Type attributeType, string? method = null, string? parameter = null, string? parameterType = null)
        {
            if (attributeType == typeof(FunctionDescriptionAttribute))
            {
                return $"Error on method '{method}'. A description of the function is required. Endorse the method with the {nameof(FunctionDescriptionAttribute)}.";
            }
            else if (attributeType == typeof(ParamDescriptionAttribute))
            {
                return $"Error on parameter '{parameter}' on method '{method}'. A description of all parameters (excluding string-enums) is required by the model. Endorse all parameters (excluding string-enums) with the {nameof(ParamDescriptionAttribute)}.";
            }
            else if (attributeType == typeof(EnumValuesAttribute))
            {
                return $"A set of enum options must be made available to the model. Endorse all intended enum parameters with the {nameof(EnumValuesAttribute)}.";
            }

            return $"{nameof(ModelRequiredAttributeMissingException)}";
        }
    }

    private static bool IsNumericType(Type t)
    {
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
            case TypeCode.Decimal:
            case TypeCode.Double:
            case TypeCode.Single:
                return true;
            default:
                return false;
        }
    }

    private static T? GetAttribute<T>(ICustomAttributeProvider info) where T : Attribute
    {
        foreach (var attr in info.GetCustomAttributes(typeof(T), false))
        {
            return (T?)attr;
        }

        return null;
    }

    /// <summary>
    /// Holds the details regarding a parameter for a function.
    /// </summary>
    private sealed class FunctionParameter
    {
        internal required string _name;
        internal required string _type;
        internal string? _description;
        internal string[]? _enumValues;
        internal bool _required;
        internal bool isBoolean = false;
    }

    /// <summary>
    /// Runs the function which was bound to this definition using the given <paramref name="modelGivenArguments"/>.
    /// </summary>
    /// <param name="modelGivenArguments"></param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    public string? RunFunction(List<FunctionCall.ArgumentDefinition> modelGivenArguments)
    {
        if (_method == null)
            throw new InvalidOperationException("This function-definition does not have a defined function (delegate) to invoke.");

        var methodParameters = _method.Method.GetParameters();

        // Create an array of objects to hold the arguments for the dynamic function call.
        // We use the number of parameters the function is EXPECTING, not the amount provided by the model.
        object?[] argumentObjects = new object[methodParameters.Length];

        // Deliberately leaves any non-set parameters to null (they are optional, but must be provided to dynamic invoke or the call will fail).
        for (int i = 0; i < modelGivenArguments.Count; i++)
        {
            // The model does not provide actual boolean values, it provides the 'string'
            // representation "true" or "false", so, if the parameter we are providing is a boolean,
            // we parse that 'string' provided by the model into an actual boolean value.
            if (methodParameters[i].ParameterType == typeof(bool) &&
                bool.TryParse(modelGivenArguments[i].Value?.ToString(), out bool parsedBool))
            {
                argumentObjects[i] = parsedBool;
                continue;
            }

            argumentObjects[i] = modelGivenArguments[i].Value;
        }

        try
        {
            return (string?)(_method?.DynamicInvoke(argumentObjects));
        }
        catch (TargetInvocationException e)
        {
            NLog.LogManager.GetCurrentClassLogger().Debug(
                $"Function call failed '{nameof(TargetInvocationException)}'\nMessage: '{e.Message}'\nInnerException Message: '{e.InnerException?.Message}'\n" +
                $"Model Provided Arguments: {string.Join(',', modelGivenArguments.Select((definition) => $"{{ Name: '{definition.Name}', Value: '{definition.Value}', Type: '{definition.Type}' }}"))}");

            return $"Function call failed due to invalid parameters provided by the language model, the error message is: {e.InnerException?.Message}. Please try the function call again.";
        }
    }

    /// <summary>
    /// Creates a JSON Schema of the given function and its parameters.
    /// </summary>
    /// <returns></returns>
    public string ToJsonSchema()
    {
        // https://json-schema.org/understanding-json-schema/

        if (_jsonSchema != null)
            return _jsonSchema;

        using MemoryStream stream = new();
        using Utf8JsonWriter writer = new(stream, new JsonWriterOptions() { Indented = false, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });

        writer.WriteStartObject();
        writer.WriteString("name", _name);
        writer.WriteString("description", _description);

        // "parameters" is the JSON object which holds the nested object of parameters, and the specification of any 'required' parameters.
        writer.WriteStartObject("parameters");

        writer.WriteString("type", "object");

        // "properties" is the object which holds the actual definitions of the parameters, consisting of a 'type', and a 'description' or 'enum'.
        writer.WriteStartObject("properties");

        List<string> requiredParameterNames = new();

        for (int i = 0; i < _parameters.Count; i++)
        {
            FunctionParameter parameter = _parameters[i];

            writer.WriteStartObject(parameter._name);

            writer.WriteString("type", parameter._type);
            if (parameter.isBoolean)
            {
                // A boolean is a fancy enum with values "true" or "false"
                writer.WriteStartArray("enum");

                writer.WriteStringValue("true");
                writer.WriteStringValue("false");

                writer.WriteEndArray();
            }
            else if (parameter._enumValues == null)
            {
                // We are dealing with a non-enum type.
                writer.WriteString("description", parameter._description);
            }
            else
            {
                // We are dealing with an enum type.

                writer.WriteStartArray("enum");
                for (int e = 0; e < parameter._enumValues?.Length; e++)
                {
                    writer.WriteStringValue(parameter._enumValues?[e]);
                }
                writer.WriteEndArray();
            }

            writer.WriteEndObject();

            if (parameter._required)
                requiredParameterNames.Add(parameter._name);
        }

        writer.WriteEndObject();

        if (requiredParameterNames.Count > 0)
        {
            writer.WriteStartArray("required");

            for (int pn = 0; pn < requiredParameterNames.Count; pn++)
            {
                writer.WriteStringValue(requiredParameterNames[pn]);
            }

            writer.WriteEndArray();
        }

        writer.WriteEndObject();
        writer.WriteEndObject();

        writer.Flush();

        using (var sr = new StreamReader(stream))
        {
            stream.Position = 0;
            _jsonSchema = sr.ReadToEnd();
            return _jsonSchema;
        }
    }
}
