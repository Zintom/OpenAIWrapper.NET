using System;
using System.Collections.Generic;
using System.IO;
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

    /// <summary>
    /// Holds the details regarding a parameter for a function.
    /// </summary>
    private sealed class FunctionParameter
    {
        internal readonly string _name;
        internal readonly string _type;
        internal readonly string? _description;
        internal readonly string[]? _enumValues;
        internal readonly bool _required;

        /// <summary>
        /// Create a function parameter which is a standard (non-enum) type.
        /// </summary>
        /// <param name="name">The name of the parameter.</param>
        /// <param name="type">The type of the parameter, such as 'integer', or 'string'.</param>
        /// <param name="description">The description of the parameter.</param>
        /// <param name="isRequired">Dictates whether this parameter is mandatory.</param>
        internal FunctionParameter(string name, string type, string description, bool isRequired)
        {
            _name = name;
            _type = type;
            _description = description;
            _required = isRequired;
        }

        /// <summary>
        /// Create a function parameter which is an enum.
        /// </summary>
        /// <param name="name">The name of the parameter.</param>
        /// <param name="isRequired">Dictates whether this parameter is mandatory.</param>
        /// <param name="enumValues"></param>
        internal FunctionParameter(string name, bool isRequired, params string[]? enumValues)
        {
            _name = name;
            _type = "string";
            _enumValues = enumValues;
            _required = isRequired;
        }
    }

    /// <summary>
    /// Runs the function which was bound to this definition using the given <paramref name="arguments"/>.
    /// </summary>
    /// <param name="arguments"></param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    public string? RunFunction(List<FunctionCall.ArgumentDefinition> arguments)
    {
        if (_method == null)
            throw new InvalidOperationException("This function-definition does not have a defined function (delegate) to run.");

        // Create an array of objects to hold the arguments for the dynamic function call.
        // We use the number of parameters the function is EXPECTING, not the amount provided by the model.
        object?[] argumentObjects = new object[_method.Method.GetParameters().Length];

        // Deliberately leaves any non-set parameters to null (they are optional, but must be provided to dynamic invoke or the call will fail).
        for (int i = 0; i < arguments.Count; i++)
        {
            argumentObjects[i] = arguments[i].Value;
        }

        return (string?)(_method?.DynamicInvoke(argumentObjects));
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
        using Utf8JsonWriter writer = new(stream, new JsonWriterOptions() { Indented = true });

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
            if (parameter._enumValues == null)
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