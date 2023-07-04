using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Zintom.OpenAIWrapper.Models;

public static class FunctionBuilder
{

    /// <summary>
    /// Creates a JSON Schema of the given function and its parameters.
    /// </summary>
    /// <param name="name">The name of the function.</param>
    /// <param name="description">The description of the function.</param>
    /// <param name="parameters">The parameters of the function.</param>
    /// <returns></returns>
    public static string Create(string name, string description, params FunctionParameter[] parameters)
    {
        // https://json-schema.org/understanding-json-schema/

        using MemoryStream stream = new();
        using Utf8JsonWriter writer = new(stream, new JsonWriterOptions() { Indented = true });

        writer.WriteStartArray();

        writer.WriteStartObject();
        writer.WriteString("name", name);
        writer.WriteString("description", description);

        // "parameters" is the JSON object which holds the nested object of parameters, and the specification of any 'required' parameters.
        writer.WriteStartObject("parameters");

        writer.WriteString("type", "object");

        // "properties" is the object which holds the actual definitions of the parameters, consisting of a 'type', and a 'description' or 'enum'.
        writer.WriteStartObject("properties");

        List<string> requiredParameterNames = new();

        for (int i = 0; i < parameters.Length; i++)
        {
            FunctionParameter parameter = parameters[i];

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

        writer.WriteEndArray();

        writer.Flush();

        using (var sr = new StreamReader(stream))
        {
            stream.Position = 0;
            return sr.ReadToEnd();
        }
    }

    public class FunctionParameter
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
        public FunctionParameter(string name, string type, string description, bool isRequired)
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
        public FunctionParameter(string name, bool isRequired, params string[]? enumValues)
        {
            _name = name;
            _type = "string";
            _enumValues = enumValues;
            _required = isRequired;
        }
    }

}
