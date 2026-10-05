using System.IO.Ports;
using System.Text;
using SharpCAT2.Core.Serial;
using SharpCAT2.ServerLibrary.Serial;
using Xunit;

namespace SharpCAT2.Tests.Serial;

public sealed class LegacySerialByteTransportTests
{
    [Fact]
    public async Task WriteAsyncPreservesAsciiExactlyOnLegacyPort()
    {
        var port = new InstrumentedSerialPort();
        await using var transport = new LegacySerialByteTransport(port);
        await transport.OpenAsync();

        await transport.WriteAsync(Encoding.ASCII.GetBytes("FA014250000;"));

        Assert.Equal("FA014250000;", port.WrittenText);
        Assert.Equal(1, port.WriteCount);
    }

    [Fact]
    public async Task WriteAsyncRejectsNonAsciiWithoutWritingOnLegacyPort()
    {
        var port = new InstrumentedSerialPort();
        await using var transport = new LegacySerialByteTransport(port);
        await transport.OpenAsync();

        await Assert.ThrowsAsync<NotSupportedException>(async () =>
            await transport.WriteAsync(new byte[] { 0x46, 0x41, 0x80 }));

        Assert.Equal(0, port.WriteCount);
        Assert.Equal(string.Empty, port.WrittenText);
    }

    [Fact]
    public async Task ReadAsyncReturnsPartialChunksWithoutLosingTail()
    {
        var port = new InstrumentedSerialPort { ReadChunkLimit = 2 };
        port.EnqueueReceived(0x41, 0x42, 0x43, 0x44);
        await using var transport = new LegacySerialByteTransport(port);
        await transport.OpenAsync();
        var buffer = new byte[4];

        var firstRead = await transport.ReadAsync(buffer);
        var firstText = Encoding.ASCII.GetString(buffer, 0, firstRead);
        var secondRead = await transport.ReadAsync(buffer);
        var secondText = Encoding.ASCII.GetString(buffer, 0, secondRead);

        Assert.Equal(2, firstRead);
        Assert.Equal("AB", firstText);
        Assert.Equal(2, secondRead);
        Assert.Equal("CD", secondText);
    }

    [Fact]
    public async Task AdapterDoesNotSubscribeToDataReceived()
    {
        var port = new InstrumentedSerialPort();
        await using var transport = new LegacySerialByteTransport(port);

        Assert.Equal(0, port.DataReceivedSubscriberCount);
    }

    [Fact]
    public async Task ReadAsyncCancelsWhilePollingAnIdleOpenPort()
    {
        var port = new InstrumentedSerialPort();
        await using var transport = new LegacySerialByteTransport(port);
        await transport.OpenAsync();
        using var cancellation = new CancellationTokenSource();
        var read = transport.ReadAsync(new byte[8], cancellation.Token).AsTask();

        await Task.Delay(30);
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => read);
    }

    [Fact]
    public async Task ReadAsyncReturnsEndOfTransportAfterClose()
    {
        var port = new InstrumentedSerialPort();
        await using var transport = new LegacySerialByteTransport(port);
        await transport.OpenAsync();
        await transport.CloseAsync();

        var count = await transport.ReadAsync(new byte[4]);

        Assert.Equal(0, count);
        Assert.False(transport.IsOpen);
    }

    [Fact]
    public async Task ReadAsyncRejectsInvalidReadCountFromLegacyPort()
    {
        var port = new InstrumentedSerialPort { ReportedReadCount = 3 };
        port.EnqueueReceived(0x41, 0x42);
        await using var transport = new LegacySerialByteTransport(port);
        await transport.OpenAsync();

        await Assert.ThrowsAsync<IOException>(async () => await transport.ReadAsync(new byte[2]));
    }

    [Fact]
    public async Task DisposeAsyncClosesAndDisposesOwnedPortOnce()
    {
        var port = new InstrumentedSerialPort();
        var transport = new LegacySerialByteTransport(port);
        await transport.OpenAsync();

        await transport.DisposeAsync();
        await transport.DisposeAsync();

        Assert.False(port.IsOpen);
        Assert.Equal(1, port.CloseCount);
        Assert.Equal(1, port.DisposeCount);
    }

    [Fact]
    public async Task ConstructorClampsLegacyTimeoutsToBoundedDefaults()
    {
        var port = new InstrumentedSerialPort { ReadTimeout = 120000, WriteTimeout = Timeout.Infinite };
        await using var transport = new LegacySerialByteTransport(port);

        Assert.Equal(500, port.ReadTimeout);
        Assert.Equal(500, port.WriteTimeout);
    }

    private sealed class InstrumentedSerialPort : ISerialPort
    {
        private readonly Queue<byte> _received = new();
        private bool _isOpen;

        public bool IsOpen => _isOpen;
        public string PortName => "TEST";
        public int BaudRate => 9600;
        public int BytesToRead => _received.Count;
        public int ReadTimeout { get; set; } = 500;
        public int WriteTimeout { get; set; } = 500;
        public string WrittenText { get; private set; } = string.Empty;
        public int WriteCount { get; private set; }
        public int CloseCount { get; private set; }
        public int DisposeCount { get; private set; }
        public int ReadChunkLimit { get; set; } = int.MaxValue;
        public int? ReportedReadCount { get; set; }

        private SerialDataReceivedEventHandler? _dataReceived;

        public event SerialDataReceivedEventHandler? DataReceived
        {
            add => _dataReceived += value;
            remove => _dataReceived -= value;
        }

        public int DataReceivedSubscriberCount => _dataReceived?.GetInvocationList().Length ?? 0;

        public void Open() => _isOpen = true;

        public void Close()
        {
            CloseCount++;
            _isOpen = false;
        }

        public void Write(string data)
        {
            if (!_isOpen)
            {
                throw new InvalidOperationException("Port is closed.");
            }

            WriteCount++;
            WrittenText += data;
        }

        public void WriteLine(string data) => Write(data + Environment.NewLine);
        public string ReadExisting() => throw new NotSupportedException();
        public string ReadLine() => throw new NotSupportedException();

        public int Read(byte[] buffer, int offset, int count)
        {
            if (!_isOpen)
            {
                throw new InvalidOperationException("Port is closed.");
            }

            var toRead = Math.Min(Math.Min(count, ReadChunkLimit), _received.Count);
            for (var index = 0; index < toRead; index++)
            {
                buffer[offset + index] = _received.Dequeue();
            }

            return ReportedReadCount ?? toRead;
        }

        public void DiscardInBuffer() => _received.Clear();
        public void DiscardOutBuffer() => WrittenText = string.Empty;

        public void EnqueueReceived(params byte[] bytes)
        {
            foreach (var value in bytes)
            {
                _received.Enqueue(value);
            }
        }

        public void RaiseDataReceived() => _dataReceived?.Invoke(this, null!);

        public void Dispose()
        {
            DisposeCount++;
            _isOpen = false;
        }
    }
}
