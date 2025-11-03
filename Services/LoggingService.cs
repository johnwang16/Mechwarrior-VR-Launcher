using System;
using System.Collections.Generic;

namespace MechwarriorVRLauncher.Services
{
    public class LoggingService
    {
        private readonly List<string> _logBuffer = new List<string>();

        public virtual void LogMessage(string message)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss");
            var formattedMessage = $"[{timestamp}] {message}";
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
    }
}
