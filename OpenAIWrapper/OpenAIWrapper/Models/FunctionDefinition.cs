using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using static Zintom.OpenAIWrapper.Misc.ReflectionHelpers;

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
    private List<FunctionParameter> _parameters = null!;
    private Delegate? _targetMethod;

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

    /// <summary>
    /// Generates a <see cref="FunctionDefinition"/> from the given <paramref name="method"/>.
    /// <para/>
    /// <paramref name="method"/> must be annotated with the relevant <see cref="FunctionDescriptionAttribute"/>, <see cref="ParamDescriptionAttribute"/>, and <see cref="EnumValuesAttribute"/>.
    /// </summary>
    /// <param name="method"></param>
    /// <returns></returns>
    /// <exception cref="ModelRequiredAttributeMissingException"></exception>
    public static FunctionDefinition FromMethod(Delegate method)
    {
        var methodInfo = method.GetMethodInfo();
        var methodParameters = methodInfo.GetParameters();

        var functionDefinition = new FunctionDefinition() { _name = methodInfo.Name, _targetMethod = method };

        // Assign function description from MethodInfo.
        functionDefinition._description = GetCustomAttributeFirstOrNull<FunctionDescriptionAttribute>(methodInfo)?._description ?? throw new ModelRequiredAttributeMissingException(typeof(FunctionDescriptionAttribute), methodInfo.DeclaringType?.FullName + "." + methodInfo.Name);

        // Process all the method parameters.
        functionDefinition._parameters = new();
        foreach (var methodParameter in methodParameters)
        {
            string? type = null;

            /*
             * Integer vs Number
             * https://json-schema.org/understanding-json-schema/reference/numeric.html
             */

            type = GetFriendlyTypeName(methodParameter.ParameterType);

            var p = new FunctionParameter()
            {
                _name = methodParameter.Name ?? "",
                _type = type
            };

            var descriptionAttr = GetCustomAttributeFirstOrNull<ParamDescriptionAttribute>(methodParameter);
            p._description = descriptionAttr?._description;

            p._required = descriptionAttr?._required ?? false;

            var enumAttr = GetCustomAttributeFirstOrNull<EnumValuesAttribute>(methodParameter);
            p._enumValues = enumAttr?._possibleEnumValues;

            // Check that the element type in the enum array matches the type of the parameter.
            if(p._enumValues != null && p._enumValues.Length > 0 &&
               p._enumValues[0].GetType() != methodParameter.ParameterType)
            {
                throw new Exception($"The {nameof(EnumValuesAttribute)} was applied to a parameter with the type '{methodParameter.ParameterType.Name}', however " +
                                    $"the enum values given were of type '{p._enumValues[0].GetType()}'. Parameter name: '{methodParameter.Name}', Method name: '{methodInfo.Name}'.");
            }

            if (string.IsNullOrEmpty(p._description) && // No description
                (type != "string" || p._enumValues?.Length == 0) && // Not an enum
                type != "boolean") // Not a boolean
            {
                // The model requires a description on non-enum, non-boolean values.
                throw new ModelRequiredAttributeMissingException(typeof(ParamDescriptionAttribute), methodInfo.DeclaringType?.FullName + "." + methodInfo.Name, $"({methodParameter.ParameterType.Name}) {methodParameter.Name}");
            }

            functionDefinition._parameters.Add(p);
        }

        return functionDefinition;
    }

    /// <summary>
    /// Gets the JSON Schema "friendly" type name of the given <paramref name="type"/>, if the given <paramref name="type"/> is not supported or is <see langword="null"/>, then 'string' will be returned.
    /// </summary>
    /// <param name="type"></param>
    /// <returns></returns>
    private static string GetFriendlyTypeName(Type? type)
    {
        if (type == null)
            return "string";

        string? typeString;

        if (IsIntegralNumber(type))
        {
            typeString = "integer";
        }
        else if (IsFloatingPointNumber(type))
        {
            typeString = "number";
        }
        else if (type == typeof(bool))
        {
            typeString = "boolean";
        }
        else
        {
            typeString = "string";
        }

        return typeString;
    }

    /// <summary>
    /// An exception when an attribute was not applied on a method or parameter and a language model requires it.
    /// </summary>
    public class ModelRequiredAttributeMissingException : Exception
    {
        /// <inheritdoc cref="ModelRequiredAttributeMissingException"/>
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
                return $"Error on parameter '{parameter}' on method '{method}'. A description of all parameters (excluding string-enums and booleans) is required by the model. Endorse all parameters (excluding string-enums and booleans) with the {nameof(ParamDescriptionAttribute)}.";
            }
            else if (attributeType == typeof(EnumValuesAttribute))
            {
                return $"A set of enum options must be made available to the model. Endorse all intended enum parameters with the {nameof(EnumValuesAttribute)}.";
            }

            return $"{nameof(ModelRequiredAttributeMissingException)}";
        }
    }

    /// <summary>
    /// Holds the details regarding a parameter for a function.
    /// </summary>
    private sealed class FunctionParameter
    {
        internal required string _name;
        internal required string _type;
        internal string? _description;
        internal object[]? _enumValues;
        internal bool _required;
    }

    /// <summary>
    /// Runs the function which was bound to this definition using the given <paramref name="modelGivenArguments"/>.
    /// </summary>
    /// <param name="modelGivenArguments"></param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    public string? RunFunction(List<FunctionCall.ArgumentDefinition> modelGivenArguments)
    {
        if (_targetMethod == null)
            throw new InvalidOperationException("This function-definition does not have a defined function (delegate) to invoke.");

        var targetMethodParameters = _targetMethod.Method.GetParameters();

        // Create an array of objects to hold the arguments for the dynamic function call.
        // We use the number of parameters the function is EXPECTING, not the amount provided by the model.
        object?[] methodParameterObjects = new object[targetMethodParameters.Length];

        // If the model has given less arguments than we need (i.e optional parameters)
        // we need to ensure we leave those object un-initialised (null).
        int initAmount = targetMethodParameters.Length;
        if (modelGivenArguments.Count < targetMethodParameters.Length)
        {
            initAmount = modelGivenArguments.Count;
        }

        for (int i = 0; i < initAmount; i++)
        {
            object? argValue = null;

            if (modelGivenArguments[i].Type == FunctionCall.ArgumentType.String)
            {
                argValue = Encoding.UTF8.GetString(modelGivenArguments[i].RawValue);
            }
            else if (modelGivenArguments[i].Type == FunctionCall.ArgumentType.Boolean)
            {
                // UTF-8 decode the raw value into characters.
                Span<char> boolAsChars = new char[Encoding.UTF8.GetCharCount(modelGivenArguments[i].RawValue)];
                boolAsChars = boolAsChars[..Encoding.UTF8.GetChars(modelGivenArguments[i].RawValue, boolAsChars)];

                // Parse the characters as a bool.
                if (bool.TryParse(boolAsChars, out bool parsedBool))
                    argValue = parsedBool;
            }
            else if (modelGivenArguments[i].Type == FunctionCall.ArgumentType.Number)
            {
                // Encode the raw value into a string.
                string numberAsString = Encoding.UTF8.GetString(modelGivenArguments[i].RawValue);

                // Establish the type of the parameter which is expected by the method.
                Type targetType = targetMethodParameters[i].ParameterType;

                // Assuming the targetType is a number type (int, double, etc), retrieve the static 'Parse' method on that type.
                var parseMethod = targetType.GetMethod("Parse", _stringTypeArrayCache);

                // Invoke the static Parse method on the number type and box its value into the argValue variable.
                argValue = parseMethod?.Invoke(null, new object[] { numberAsString });
            }

            methodParameterObjects[i] = argValue;
        }

        try
        {
            return (string?)(_targetMethod?.DynamicInvoke(methodParameterObjects));
        }
        catch (TargetInvocationException e)
        {
            NLog.LogManager.GetCurrentClassLogger().Debug(
                $"Function call failed '{nameof(TargetInvocationException)}'\nMessage: '{e.Message}'\nInnerException Message: '{e.InnerException?.Message}'\n" +
                $"Model Provided Arguments: {string.Join(',', modelGivenArguments.Select((definition) => $"{{ Name: '{definition.Name}', Value: '{Encoding.UTF8.GetString(definition.RawValue)}', Type: '{definition.Type}' }}"))}");

            return $"Function call failed due to invalid parameters provided by the language model, the error message is: {e.InnerException?.Message}. Please try the function call again.";
        }
    }

    /// <summary>
    /// Used as a cache for <see cref="Type.GetMethod(string, int, Type[])"/> so that we don't keep creating the array.
    /// <para/>
    /// Represents a function that has one parameter of type string.
    /// </summary>
    private readonly Type[] _stringTypeArrayCache = new Type[] { typeof(string) };

    /// <summary>
    /// Creates a JSON Schema of the given function and its parameters.
    /// </summary>
    /// <returns></returns>
    public string ToJsonSchema()
    {
        // Used for sending a function to a model.

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

            InternalWriteParameterJSON(writer, parameter);

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

    /// <summary>
    /// Writes a <see cref="FunctionParameter"/> as a JSON object.
    /// </summary>
    private static void InternalWriteParameterJSON(Utf8JsonWriter writer, FunctionParameter parameter)
    {
        writer.WriteStartObject(parameter._name);

        // A parameter must have a 'type' defined.
        writer.WriteString("type", parameter._type);

        if (!string.IsNullOrEmpty(parameter._description))
            writer.WriteString("description", parameter._description);

        // If this parameter is an enum,
        // we need to write out the possible enum values.
        if (parameter._enumValues != null && parameter._enumValues.Length > 0)
        {
            writer.WriteStartArray("enum");

            var arrayType = parameter._enumValues[0].GetType();
            for (int e = 0; e < parameter._enumValues.Length; e++)
            {
                if (arrayType == typeof(string))
                    writer.WriteStringValue(parameter._enumValues[e].ToString());
                else
                    writer.WriteRawValue(parameter._enumValues[e].ToString() ?? "null");
            }

            writer.WriteEndArray();
        }

        writer.WriteEndObject();
    }
}
