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

Other documentation and tutorials:

- [Adding functions for a model to be able to call.](Documentation/function_calling.md)
