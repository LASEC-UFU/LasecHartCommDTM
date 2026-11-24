using System;
using System.Collections.Generic;
using System.IO;

namespace HartEngine
{
    public enum LogKind { Info, Tx, Rx, Error }

    public sealed class LogEntry
    {
        public DateTime Time { get; set; }
        public LogKind Kind { get; set; }
        public string Text { get; set; }
        public override string ToString() => $"{Time:HH:mm:ss.fff} {Kind,-5} {Text}";
    }

    /// <summary>
    /// Registro da comunicação (quadros enviados e recebidos, erros). Mantém as últimas entradas em memória para a
    /// tela de log e, opcionalmente, grava em arquivo.
    /// </summary>
    public sealed class HartCommLog
    {
        private readonly LinkedList<LogEntry> _entries = new LinkedList<LogEntry>();
        private readonly object _lock = new object();

        public int Capacity { get; set; } = 2000;
        /// <summary>Arquivo de log (null = só memória).</summary>
        public string FilePath { get; set; }

        /// <summary>Disparado na thread que registrou a entrada.</summary>
        public event Action<LogEntry> Added;

        public void Info(string text) => Add(LogKind.Info, text);
        public void Error(string text) => Add(LogKind.Error, text);
        public void Tx(byte[] raw, string what) => Add(LogKind.Tx, $"{what}  {Hex.Of(raw)}");
        public void Rx(byte[] raw, string what) => Add(LogKind.Rx, $"{what}  {Hex.Of(raw)}");

        public void Add(LogKind kind, string text)
        {
            var e = new LogEntry { Time = DateTime.Now, Kind = kind, Text = text };
            lock (_lock)
            {
                _entries.AddLast(e);
                while (_entries.Count > Capacity) _entries.RemoveFirst();
                if (FilePath != null)
                    try { File.AppendAllText(FilePath, e + Environment.NewLine); } catch { /* log nunca derruba a comunicação */ }
            }
            try { Added?.Invoke(e); } catch { }
        }

        public List<LogEntry> Snapshot()
        {
            lock (_lock) return new List<LogEntry>(_entries);
        }

        public void Clear()
        {
            lock (_lock) _entries.Clear();
        }
    }
}
