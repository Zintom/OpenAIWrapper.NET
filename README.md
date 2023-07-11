# ChatCompletions C# / .NET Wrapper

A C# / .NET wrapper library around the OpenAI ChatCompletions (ChatGPT) API. Easy-to-use with a wide range of functionality. Supports ChatCompletions with function-calling. Supports automatic function-call creation through simply applying attributes to an existing C# method.

[![NuGet](https://img.shields.io/nuget/v/OpenAIWrapper?color=%2327ae60)](https://www.nuget.org/packages/OpenAIWrapper)

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

Other documentation and tutorials:

- [Adding functions for a model to be able to call.](Documentation/function_calling.md)
