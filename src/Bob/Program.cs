
string partnerName = "Alice";
bool cancel = false;
var pipe = new NamedPipeConnection(myName: "Bob", partnerName);
pipe.OnError += (_, err) => Console.WriteLine("ERROR: " + err);
pipe.OnNewMessage += (_, msg) => Console.WriteLine($"Message from {partnerName}: " + msg);
pipe.Connected += (_, msg) => Console.WriteLine(msg);
pipe.Disconnected += (_, msg) =>
{
    Console.WriteLine($"{partnerName} disconnected");
};
pipe.Connect();
Console.CancelKeyPress += Console_CancelKeyPress;

while (!cancel)
{   
    string line =  Console.ReadLine();
    if (line.StartsWith("exit"))
    {
        break;
        pipe.Disconnect();
    }
    await pipe.Send(line);
}


void Console_CancelKeyPress(object? sender, ConsoleCancelEventArgs e)
{
    cancel = true;
    pipe?.Disconnect();
}
