using System.IO.Pipes;
using System.Text;

namespace ServerGUI;

public partial class Form1 : Form
{
    NamedPipeConnection _pipe;
    static string NL = Environment.NewLine;
    readonly string _partnerName = "Bob";

    public Form1()
    {
        InitializeComponent();      
    }


    private void Form1_Load(object sender, EventArgs e)
    {
        button1.Text = $"Send to {_partnerName}";

        _pipe = new NamedPipeConnection("Alice", _partnerName);
        _pipe.Connected += _namedPipeServer_Connected;
        _pipe.OnError += _namedPipeServer_OnError;
        _pipe.Disconnected += _namedPipePeer_Disconnected;
        _pipe.OnNewMessage += _namedPipePeer_OnNewMessage;
        _pipe.Connect();
    }


    private void _namedPipePeer_OnNewMessage(object? sender, string msg)
    {
        Invoke(() =>
        {
            textBoxMessages.AppendText($"Message from {_partnerName}: ");
            textBoxMessages.AppendText(msg);
            textBoxMessages.AppendText(NL);
        });
    }

    private void _namedPipePeer_Disconnected(object? sender, string msg)
    {
        Invoke(() =>
        {
            textBoxMessages.AppendText($"{_partnerName} disconnected");
            textBoxMessages.AppendText(NL);
            _pipe.Connect();
        });
    }

    private void _namedPipeServer_OnError(object? sender, string error)
    {
        Invoke(() =>
        {
            textBoxMessages.AppendText("Error:");
            textBoxMessages.AppendText(error);
            textBoxMessages.AppendText(NL);
        });
    }

    private void _namedPipeServer_Connected(object? sender, string msg)
    {
        Invoke(() =>
        {
            textBoxMessages.AppendText("Connected with: " + msg);
            textBoxMessages.AppendText(NL);
        });
    }

    private void Form1_FormClosing(object sender, FormClosingEventArgs e)
    {
        _pipe.Disconnect();
    }

    private async void button1_Click(object sender, EventArgs e)
    {
        string message = textBox1.Text;
        textBoxMessages.AppendText("Sending: ");
        textBoxMessages.AppendText(message);
        textBoxMessages.AppendText(NL);
        await _pipe.Send(message);
        textBox1.Text = "";
    }

   
}
