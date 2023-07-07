# ChatCompletions C# Wrapper

A C# wrapper around the OpenAI ChatCompletions API.

## Basic Usage
```c#
var gpt = new ChatGPT("your_secret_key");

var response = await gpt.GetChatCompletion(
                          messages: new List<Zintom.OpenAIWrapper.Models.Message> {
                            new Zintom.OpenAIWrapper.Models.Message() { Role = "user", Content = "What is 9 + 900?" }
                          },
                          options: new ChatGPT.ChatCompletionOptions() { Model = LanguageModels.GPT_3_5_Turbo });

Console.WriteLine(response?.Choices?[0].Message?.Content);

```

## Function Calling
Define a function, I create a basic "Add" function here:
```c#
private static string Add(double a, double b)
{
  return (a + b).ToString();
}
```
Define your function definition:
```c#
FunctionDefinition additionFunction = new FunctionDefinition.Builder("Add", "Adds 'b' to 'a'.")
  .AddParameter("a", "integer", "The first value", true)
  .AddParameter("b", "integer", "The second value", true)
  .SetMethod(Add)
  .Build();
```
Call the ChatCompletions API with your function
*(note you must use the 0613 variants of both GPT 3.5 Turbo and GPT-4 as of 7th July 2023 because the default models do not currently support functions)*:
```c#
var response = await gpt.GetChatCompletion(
                          messages: new List<Zintom.OpenAIWrapper.Models.Message> { new Zintom.OpenAIWrapper.Models.Message() { Role = "user", Content = "What is 9 + 900?" } },
                          options: new ChatGPT.ChatCompletionOptions() { Model = LanguageModels.GPT_3_5_Turbo_0613 },
                          functions: additionFunction);
```
