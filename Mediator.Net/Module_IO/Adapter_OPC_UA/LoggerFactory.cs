using Microsoft.Extensions.Logging;
using System;
using System.IO;

namespace Ifak.Fast.Mediator.IO.Adapter_OPC_UA;

public sealed class LoggerFactory : ILoggerFactory {

    private readonly string prefix;
    private readonly LogLevel logLevel;

    public LoggerFactory(string prefix, LogLevel logLevel) {
        this.prefix = prefix;
        this.logLevel = logLevel;
    }

    public Logger CreateLogger() {
        return new Logger(prefix, logLevel);
    }

    public ILogger CreateLogger(string categoryName) {
        return new Logger(prefix, logLevel);
    }

    public void AddProvider(ILoggerProvider provider) { }

    public void Dispose() { }
}

public sealed class Logger : ILogger {

    private readonly string prefix;
    private readonly LogLevel logLevel;

    public Logger() { 
        this.prefix = "";
        this.logLevel = LogLevel.Debug;
    }

    public Logger(string prefix, LogLevel logLevel) {
        this.prefix = prefix;
        this.logLevel = logLevel;
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull {
        return new Scope();
    }

    public bool IsEnabled(LogLevel logLevel) {
        return logLevel >= this.logLevel;
    }

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) {
        if (logLevel < this.logLevel) return;
        TextWriter writer = logLevel == LogLevel.Error || logLevel == LogLevel.Critical ? Console.Error : Console.Out;
        string fmt = formatter(state, exception);
        writer.WriteLine($"{prefix} {fmt}");
    }

    private sealed class Scope : IDisposable {
        public void Dispose() { }
    }
}
