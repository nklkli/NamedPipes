using System.IO.Pipes;
using System.Text;
#nullable disable


public class NamedPipeConnection : IDisposable
{
    private readonly string _myName;
    private readonly string _partnerName;
    private bool _connectionToPartnerEstablashed = false;
    NamedPipeClientStream _inPipe;
    NamedPipeServerStream _outPipe;
    CancellationTokenSource _cancellationTokenSource;
    object _lock = new object();

    /// <summary>
    /// Automatically re-connect after partner disconnects?
    /// </summary>
    public bool AutoReconnect { get; set; } = true;

    public event EventHandler<string> Connecting;

    /// <summary>
    /// Fires if the connection with the peer was established.
    /// </summary>
    public event EventHandler<string> Connected;

    /// <summary>
    /// Fires if the connectin with the peer was disconnected.
    /// </summary>
    public event EventHandler<string> Disconnected;

    public event EventHandler<string> OnError;

    public event EventHandler<string> OnNewMessage;

    public NamedPipeConnection(string myName, string partnerName)
    {
        this._myName = myName;
        this._partnerName = partnerName;
    }


    public void Connect()
    {
        _cancellationTokenSource = new CancellationTokenSource();
        Connecting?.Invoke(this, $"{_myName} starts connecting...");
        _connectionToPartnerEstablashed = false;
        _ = Task.Run(ConnectAsServer);
        _ = Task.Run(ConnecstAsClient);
    }


    async void ConnectAsServer()
    {
        try
        {
            _outPipe = new NamedPipeServerStream(
               pipeName: _myName,
               direction: PipeDirection.InOut,
               maxNumberOfServerInstances: 1,
               transmissionMode: PipeTransmissionMode.Message,
               options: PipeOptions.None);
            await _outPipe.WaitForConnectionAsync(_cancellationTokenSource.Token);
            Connecting?.Invoke(this, "Server pipe accepted connection from client.");
            FireConnectedEvent();          
        }
        catch (Exception ex)
        {
            OnError?.Invoke(this, "Failed to accept client connection: " + ex.ToString());
        }
    }


    async void ConnecstAsClient()
    {
        try
        {
            _inPipe = new NamedPipeClientStream(
                serverName: ".",
                pipeName: _partnerName,
                direction: PipeDirection.InOut);
            await _inPipe.ConnectAsync(_cancellationTokenSource.Token);
            _inPipe.ReadMode = PipeTransmissionMode.Message;            
            Connecting?.Invoke(this, "Client pipe connected to server pipe.");
            FireConnectedEvent();
            _ = Task.Run(ReadUntilDisconnected);
        }
        catch (Exception ex)
        {
            OnError(this, "Client failed to connect: " + ex.ToString());
        }
    }

    private void FireConnectedEvent()
    {
        lock (_lock)
        {
            if (!_connectionToPartnerEstablashed && (_inPipe?.IsConnected ?? false) && (_outPipe?.IsConnected ?? false))
            {
                Connected?.Invoke(this, $"Connection to '{_partnerName}' established.");
                _connectionToPartnerEstablashed = true;
            }
        }
    }


    public void Disconnect()
    {
        _cancellationTokenSource.Cancel();
    }


    public async Task Send(string message)
    {
        try
        {
            byte[] msg = Encoding.UTF8.GetBytes(message);
            await _outPipe.WriteAsync(msg, _cancellationTokenSource.Token);
        }
        catch (Exception ex)
        {
            OnError?.Invoke(this, "Failed to send message: " + ex.ToString());
        }

    }


    async void ReadUntilDisconnected()
    {
        try
        {
            while (true)
            {
                string message = Encoding.UTF8.GetString(await ReadMessage());
                if (_inPipe.IsConnected)
                    OnNewMessage?.Invoke(this, message);
                else
                {                   
                    await _outPipe.DisposeAsync();
                    await _inPipe.DisposeAsync();
                    Disconnected?.Invoke(this, _partnerName);
                    if(AutoReconnect && !_cancellationTokenSource.IsCancellationRequested) 
                        _ = Task.Run(Connect);
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            await _outPipe.DisposeAsync();
            await _inPipe.DisposeAsync();
            Disconnected?.Invoke(this, ex.ToString());
        }
    }


    async Task<byte[]> ReadMessage()
    {
        MemoryStream ms = new MemoryStream();
        byte[] buffer = new byte[0x1000]; // Read in 4 KB blocks
        do
        {
            ms.Write(buffer, 0, await _inPipe.ReadAsync(buffer, 0, buffer.Length, _cancellationTokenSource.Token));
        }
        while (!_inPipe.IsMessageComplete);
        return ms.ToArray();
    }



    public void Dispose()
    {
        _inPipe?.Dispose();
        _outPipe?.Dispose();
    }
}
