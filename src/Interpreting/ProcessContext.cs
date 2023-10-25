using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Threading;
using Elk.Interpreting.Exceptions;
using Elk.Std.DataTypes;

namespace Elk.Interpreting;

public class ProcessContext : IEnumerable<string>
{
    public bool Success
        => _exitCode == 0 || _allowNonZeroExit;

    private Process? _process;
    private readonly RuntimeObject? _pipedValue;
    private readonly BlockingCollection<string> _buffer = new(new ConcurrentQueue<string>());
    private bool _allowNonZeroExit;
    private int _exitCode;
    private int _openPipeCount;
    private bool _disposeOutput;
    private bool _disposeError;

    public ProcessContext(Process process, RuntimeObject? pipedValue)
    {
        _process = process;
        _pipedValue = pipedValue;
    }

    public IEnumerator<string> GetEnumerator()
        => _buffer.GetConsumingEnumerable().GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator()
        => GetEnumerator();

    public int Start()
    {
        try
        {
            _process!.Start();
        }
        catch (Win32Exception)
        {
            throw new RuntimeNotFoundException(_process!.StartInfo.FileName);
        }

        if (_pipedValue != null)
            Read(_pipedValue);

        _process.WaitForExit();
        var exitCode = _process.ExitCode;
        CloseProcess();
        Environment.SetEnvironmentVariable("?", exitCode.ToString());

        return exitCode;
    }

    public void StartWithRedirect()
    {
        if (!_disposeOutput)
        {
            _process!.OutputDataReceived += Process_DataReceived;
        }

        if (!_disposeError)
            _process!.ErrorDataReceived += Process_DataReceived;

        _process!.Exited += (_, _)
            => CloseProcess();

        if (_disposeOutput)
            _process.StartInfo.RedirectStandardOutput = true;

        if (_disposeError)
            _process.StartInfo.RedirectStandardError = true;

        _allowNonZeroExit = _process.StartInfo.RedirectStandardError;
        _process.EnableRaisingEvents = true;

        try
        {
            _process.Start();
        }
        catch (Win32Exception)
        {
            throw new RuntimeNotFoundException(_process.StartInfo.FileName);
        }

        if (_process?.StartInfo.RedirectStandardOutput is true && !_disposeOutput)
        {
            _process?.BeginOutputReadLine();
            _openPipeCount++;
        }

        if (_process?.StartInfo.RedirectStandardError is true && !_disposeError)
        {
            _process?.BeginErrorReadLine();
            _openPipeCount++;
        }

        if (_openPipeCount == 0)
            _buffer.CompleteAdding();

        if (_pipedValue != null)
            Read(_pipedValue);
    }

    public void Stop()
    {
        _process?.Kill();
    }

    public void EnableDisposeOutput()
    {
        _disposeOutput = true;
    }

    public void EnableDisposeError()
    {
        _disposeError = true;
    }

    private void Process_DataReceived(object sender, DataReceivedEventArgs eventArgs)
    {
        if (eventArgs.Data == null)
        {
            if (Interlocked.Decrement(ref _openPipeCount) == 0)
                _buffer.CompleteAdding();
        }
        else
        {
            _buffer.Add(eventArgs.Data);
        }
    }

    private void Read(RuntimeObject value)
    {
        try
        {
            using var streamWriter = _process!.StandardInput;
            if (value is RuntimePipe runtimePipe)
            {
                while (runtimePipe.StreamEnumerator.MoveNext())
                    streamWriter.WriteLine(runtimePipe.StreamEnumerator.Current);
            }
            else if (value is RuntimeList runtimeList)
            {
                foreach (var item in runtimeList)
                    streamWriter.WriteLine(item);
            }
            else
            {
                streamWriter.Write(value);
            }
        }
        catch (IOException)
        {
            if (value is RuntimePipe runtimePipe)
                runtimePipe.Stop();
        }
    }

    private void CloseProcess()
    {
        if (_process == null)
            return;

        _process.WaitForExit();
        _exitCode = _process.ExitCode;
        _process.Dispose();
        _process = null;

        Environment.SetEnvironmentVariable("?", _exitCode.ToString());
    }
}