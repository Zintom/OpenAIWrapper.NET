using System;
using System.Collections.Generic;

namespace Zintom.OpenAIWrapper.Models;

public partial class FunctionDefinition
{
    /// <summary>
    /// Builder class to create a <see cref="FunctionDefinition"/>.
    /// </summary>
    public class Builder
    {
        private readonly FunctionDefinition _functionDefinition;

        private readonly List<FunctionParameter> _parameters;

        private Delegate? _method;

        /// <summary>
        /// Creates a builder for a function with the given <paramref name="name"/> and <paramref name="description"/>.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="description"></param>
        public Builder(string name, string description)
        {
            _functionDefinition = new FunctionDefinition { _name = name, _description = description };
            _parameters = new List<FunctionParameter>();
        }

        /// <summary>
        /// Sets the method that will be executed if the model decides to use this function.
        /// </summary>
        /// <param name="method">Must have a return type of <see cref="string"/> so that the model can interpret the result.</param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        public Builder SetMethod(Delegate method)
        {
            if (method.Method.ReturnType != typeof(string))
            {
                throw new ArgumentException($"The method must return type of {nameof(String)}, so that the model can read it.");
            }

            _method = method;

            return this;
        }

        /// <summary>
        /// Adds a standard (non-enum) parameter to the function.
        /// </summary>
        /// <param name="name">The name of the parameter.</param>
        /// <param name="type">One of <see cref="FunctionCall.ArgumentType"/></param>
        /// <param name="description">The description of the parameter.</param>
        /// <param name="isRequired">Indicates to the model if the parameter is required.</param>
        /// <returns></returns>
        public Builder AddParameter(string name, string type, string description, bool isRequired)
        {
            _parameters.Add(new FunctionParameter(name, type, description, isRequired));

            return this;
        }

        /// <summary>
        /// Adds an enum parameter to the function.
        /// </summary>
        /// <param name="name">The name of the parameter.</param>
        /// <param name="isRequired">Indicates to the model if the parameter is required.</param>
        /// <param name="enumValues">The possible values of the parameter, such as { "Yes", "No" }.</param>
        /// <returns></returns>
        public Builder AddParameter(string name, bool isRequired, params string[]? enumValues)
        {
            _parameters.Add(new FunctionParameter(name, isRequired, enumValues));

            return this;
        }

        /// <summary>
        /// Creates a <see cref="FunctionDefinition"/> based on the components of this builder.
        /// </summary>
        /// <returns></returns>
        public FunctionDefinition Build()
        {
            _functionDefinition._parameters = _parameters;
            _functionDefinition._method = _method;

            return _functionDefinition;
        }
    }
}