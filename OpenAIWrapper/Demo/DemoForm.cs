using Zintom.OpenAIWrapper;

namespace Demo;

public partial class DemoForm : Form
{
    public DemoForm()
    {
        InitializeComponent();
    }

    private void Form1_Load(object sender, EventArgs e)
    {
        Text = "";

        var gpt = new GPT(Environment.GetEnvironmentVariable("API_KEY"), null);
        ThreadPool.QueueUserWorkItem((_) =>
        {
            gpt.GetStreamingChatCompletion(new Zintom.OpenAIWrapper.Models.Message[] { new Zintom.OpenAIWrapper.Models.Message() { Role = "user", Content = "Hello there!" } },
                (cp) =>
                {
                    Invoke(() => { Text += cp?.Choices?[0]?.Delta?.Content; });
                });
        });
    }
}
