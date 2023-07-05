using System;
using System.Collections.Generic;

namespace Zintom.OpenAIWrapper.Models;

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
public partial class FunctionDefinition
{
    public class Builder
    {
        private readonly FunctionDefinition _functionDefinition;

        private readonly List<FunctionParameter> _parameters;

        public Builder(string name, string description)
        {
            _functionDefinition = new FunctionDefinition { _name = name, _description = description };
            _parameters = new List<FunctionParameter>();
        }

        /// <summary>
        /// Adds a standard (non-enum) parameter to the function.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="type"></param>
        /// <param name="description"></param>
        /// <param name="isRequired"></param>
        /// <returns></returns>
        public Builder Add(string name, string type, string description, bool isRequired)
        {
            _parameters.Add(new FunctionParameter(name, type, description, isRequired));

            return this;
        }

        /// <summary>
        /// Adds an enum parameter to the function.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="isRequired"></param>
        /// <param name="enumValues"></param>
        /// <returns></returns>
        public Builder Add(string name, bool isRequired, params string[]? enumValues)
        {
            _parameters.Add(new FunctionParameter(name, isRequired, enumValues));

            return this;
        }

        public FunctionDefinition Build()
        {
            _functionDefinition._parameters = _parameters;

            return _functionDefinition;
        }
    }
}
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member