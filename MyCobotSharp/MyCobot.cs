using System.Buffers;
using System.Buffers.Binary;
using System.IO.Pipelines;

namespace MyCobotSharp;

public class MyCobot : IAsyncDisposable
{
    private readonly PipeReader _reader;

    private readonly PipeWriter _writer;

    public MyCobot(Stream inputStream, Stream outputStream)
    {
        _reader = PipeReader.Create(inputStream);
        _writer = PipeWriter.Create(outputStream);
        _parserTask = Task.Factory.StartNew(ParseFrames, _parserCancellation.Token, TaskCreationOptions.LongRunning);
    }

    private readonly CancellationTokenSource _parserCancellation = new();

    private readonly Task _parserTask;

    /// <summary>
    /// In several connection methods such as serial port, you may need to wait for the controller board to boot up.
    /// </summary>
    /// <exception cref="Exception"></exception>
    public async Task WaitForReady()
    {
        var cancellation = new CancellationTokenSource();
        var sendingStatusQuery = Task.Run(async () =>
        {
            var token = cancellation.Token;
            while (!token.IsCancellationRequested)
            {
                await WriteFrame(MyCobotCommand.IsPowerOn, Memory<byte>.Empty);
                await Task.Delay(300, token);
            }
        }, cancellation.Token);
        var powered = (await WaitResponse(MyCobotCommand.IsPowerOn)).Span[0] == 0x01;
        await cancellation.CancelAsync();
        if (!powered)
            throw new Exception("Atom is not powered on.");
    }

    public async ValueTask DisposeAsync()
    {
        await _parserCancellation.CancelAsync();
        _parserTask.Dispose();
        await _reader.CompleteAsync();
        await _writer.CompleteAsync();
    }

    private SequencePosition? FindDataSegment(ReadOnlySequence<byte> sequence)
    {
        var reader = new SequenceReader<byte>(sequence);
        while (reader.TryAdvanceTo((byte)MyCobotCommand.Header))
        {
            if (reader.IsNext((byte)MyCobotCommand.Header, true))
                return reader.Position;
        }

        return null;
    }

    private async Task ParseFrames(object? cancellationToken)
    {
        var cancellation = (CancellationToken?)cancellationToken ?? new CancellationToken(false);
        while (!cancellation.IsCancellationRequested)
        {
            var result = await _reader.ReadAtLeastAsync(2, cancellation);
            var dataPosition = FindDataSegment(result.Buffer);
            if (dataPosition == null)
            {
                _reader.AdvanceTo(result.Buffer.End);
                continue;
            }

            _reader.AdvanceTo(dataPosition.Value);

            byte commandId = 0;
            byte dataLength = 0;
            // Read the head and decode it.
            using (var buffer = MemoryPool<byte>.Shared.Rent(2))
            {
                result = await _reader.ReadAtLeastAsync(2, cancellation);
                result.Buffer.CopyTo(buffer.Memory.Span);
                dataLength = (byte)(buffer.Memory.Span[0] - 2);
                commandId = buffer.Memory.Span[1];
                _reader.AdvanceTo(result.Buffer.GetPosition(2));
            }

            // Read the body and the tail.
            result = await _reader.ReadAtLeastAsync(dataLength + 1, cancellation);

            // Skip the body and the tail if no corresponding handler is found.
            if (_handlers.Remove(commandId, out var completion))
            {
                // Resume the suspending handlers.
                using var buffer = MemoryPool<byte>.Shared.Rent(dataLength + 1);
                result.Buffer.CopyTo(buffer.Memory.Span);
                // Check tail mark.
                if (buffer.Memory.Span[dataLength] != (byte)MyCobotCommand.Footer)
                    completion.SetException(new IOException("Tail mark of the packet is invalid."));
                else completion.SetResult(buffer.Memory[.. dataLength]);
            }

            _reader.AdvanceTo(result.Buffer.GetPosition(dataLength + 1));
        }
    }

    private readonly Dictionary<byte, TaskCompletionSource<Memory<byte>>> _handlers = new();

    private Task<Memory<byte>> WaitResponse(MyCobotCommand command)
    {
        if (_handlers.TryGetValue((byte)command, out var notifier))
            return notifier.Task;
        notifier = new TaskCompletionSource<Memory<byte>>();
        _handlers[(byte)command] = notifier;

        return notifier.Task;
    }

    private async Task WriteFrame(MyCobotCommand command, Memory<byte> data)
    {
        var memory = _writer.GetMemory(5 + data.Length);
        memory.Span[0] = (byte)MyCobotCommand.Header;
        memory.Span[1] = (byte)MyCobotCommand.Header;
        // Data length includes this length field and the command field.
        memory.Span[2] = (byte)(data.Length + 2);
        memory.Span[3] = (byte)command;

        if (!data.IsEmpty)
            data.Span.CopyTo(memory.Span[4..]);

        memory.Span[4 + data.Length] = (byte)MyCobotCommand.Footer;

        _writer.Advance(5 + data.Length);
        await _writer.FlushAsync();
    }

    public double[] Angles { get; } = new double[6];

    public async Task PushAngles(byte speed)
    {
        using var buffer = MemoryPool<byte>.Shared.Rent(13);
        for (var index = 0; index < Angles.Length; ++index)
        {
            BinaryPrimitives.WriteInt16BigEndian(buffer.Memory.Span.Slice(index * 2, 2),
                (short)(Angles[index] * 100));
        }

        buffer.Memory.Span[12] = speed;

        await WriteFrame(MyCobotCommand.SendAngles, buffer.Memory[..13]);
    }

    public async Task PullAngles()
    {
        await WriteFrame(MyCobotCommand.GetAngles, Memory<byte>.Empty);

        var response = await WaitResponse(MyCobotCommand.GetAngles);
        for (var index = 0; index < Angles.Length; ++index)
        {
            Angles[index] = BinaryPrimitives.ReadInt16BigEndian(
                response.Span.Slice(index * 2, 2)) / 100.0;
        }
    }
}