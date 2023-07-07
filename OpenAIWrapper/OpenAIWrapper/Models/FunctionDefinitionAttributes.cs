using System;

namespace Zintom.OpenAIWrapper.Models;

/// <summary>
/// The description of this function to a language model.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public class FunctionDescriptionAttribute : Attribute
{
    internal readonly string _description;

    /// <summary>
    /// The description of this function to a language model.
    /// </summary>
    /// <param name="description"></param>
    public FunctionDescriptionAttribute(string description)
    {
        _description = description;
    }
}

/// <summary>
/// The description of this parameter to a language model.
/// </summary>
[AttributeUsage(AttributeTargets.Parameter)]
public class ParamDescriptionAttribute : Attribute
{
    internal readonly string _description;
    internal readonly bool _required;

    /// <summary>
    /// The description of this parameter to a language model.
    /// </summary>
    /// <param name="description">The description of this parameter to the model.</param>
    /// <param name="required">Indicates to the model if this parameter is required.</param>
    public ParamDescriptionAttribute(string description, bool required = false)
    {
        _description = description;
        _required = required;
    }
}

/// <summary>
/// Defines the possible enum values that a language model can use for this parameter.
/// <para/>
/// For example 'celsius' and 'fahrenheit'.
/// </summary>
[AttributeUsage(AttributeTargets.Parameter)]
public class EnumValuesAttribute : Attribute
{
    internal readonly string[] _possibleEnumValues;

    /// <summary>
    /// Defines the possible enum values that a language model can use for this parameter.
    /// </summary>
    public EnumValuesAttribute(params string[] enumValues)
    {
        _possibleEnumValues = enumValues;
    }
}
