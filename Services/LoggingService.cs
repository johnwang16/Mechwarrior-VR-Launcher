using System;
using System.Collections.Generic;

namespace MechwarriorVRLauncher.Services
{
    public class LoggingService
    {
        private readonly List<string> _logBuffer = new List<string>();
        private Action<string>? _logHandler = null;

        /// <summary>
        /// Sets a log handler that will receive log messages in real-time.
        /// If set, messages go to both the handler and the buffer.
        /// </summary>
        public void SetLogHandler(Action<string> handler)
        {
            _logHandler = handler;
        }

        /// <summary>
        /// Clears the current log handler.
        /// </summary>
        public void ClearLogHandler()
        {
            _logHandler = null;
        }

        public virtual void LogMessage(string message)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss");
            var formattedMessage = $"[{timestamp}] {message}";

            // If handler is set, send to it immediately
            if (_logHandler != null)
            {
                _logHandler(formattedMessage);
            }

            // Always add to buffer as well
            _logBuffer.Add(formattedMessage);

            // Limit buffer size to prevent memory issues
            if (_logBuffer.Count > Constants.MaxLogBufferSize)
            {
                _logBuffer.RemoveAt(0); // Remove oldest entry
            }
        }

        public virtual void AddToLogBuffer(string formattedMessage)
        {
            _logBuffer.Add(formattedMessage);

            // Limit buffer size to prevent memory issues
            if (_logBuffer.Count > Constants.MaxLogBufferSize)
            {
                _logBuffer.RemoveAt(0); // Remove oldest entry
            }
        }

        public virtual List<string> GetLogBuffer()
        {
            return new List<string>(_logBuffer);
        }

        /// <summary>
        /// Gets the buffer contents and clears it.
        /// Useful for flushing buffered logs to a window.
        /// </summary>
        public virtual List<string> FlushBuffer()
        {
            var bufferedLogs = new List<string>(_logBuffer);
            _logBuffer.Clear();
            return bufferedLogs;
        }
    }
}
