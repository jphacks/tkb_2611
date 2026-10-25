using System;

namespace ProjectPenguin.Domain.LogisticsPrototype
{
    public sealed class LogisticsChain
    {
        public const int InputCapacity = 2;
        public const int OutputCapacity = 6;
        private readonly double _deliverySeconds;
        private readonly double _processingSeconds;
        private readonly int _warehouseCapacity;
        private readonly bool[] _connections = { true, true };
        private readonly bool[] _flying = new bool[2];
        private readonly double[] _remaining = new double[2];
        private double _processRemaining;

        public LogisticsChain(double deliverySeconds = 1.5, double processingSeconds = 2, int raw = 12, int warehouseCapacity = 20)
        {
            RequirePositive(deliverySeconds); RequirePositive(processingSeconds);
            if (raw < 0 || warehouseCapacity < 1) throw new ArgumentOutOfRangeException();
            _deliverySeconds = deliverySeconds; _processingSeconds = processingSeconds;
            _warehouseCapacity = warehouseCapacity; Raw = raw;
        }
        public int Raw { get; private set; }
        public int Input { get; private set; }
        public int Output { get; private set; }
        public int Stored { get; private set; }
        public bool Processing { get; private set; }
        public double ProcessingProgress => Processing ? 1 - _processRemaining / _processingSeconds : 0;
        public int InTransit => (_flying[0] ? 1 : 0) + (_flying[1] ? 1 : 0);
        public bool Connected(int from) => from >= 0 && from < 2 && _connections[from];
        public bool Connect(int from, int to)
        {
            if (from < 0 || from > 1 || to != from + 1 || _connections[from]) return false;
            _connections[from] = true; return true;
        }
        // A removed edge stops future dispatches. Cargo already on the way still owns its reservation.
        public void DisconnectAll() { _connections[0] = false; _connections[1] = false; }
        public bool Flying(int from) => from >= 0 && from < 2 && _flying[from];
        public double DeliveryProgress(int from) => Flying(from) ? 1 - _remaining[from] / _deliverySeconds : 0;

        public void Advance(double seconds)
        {
            if (double.IsNaN(seconds) || double.IsInfinity(seconds) || seconds < 0) throw new ArgumentOutOfRangeException(nameof(seconds));
            if (seconds == 0) return;
            // Eligibility is captured before arrivals/completions: no same-update multihop traversal.
            var dispatchRaw = _connections[0] && !_flying[0] && Raw > 0 && Input < InputCapacity;
            var dispatchOutput = _connections[1] && !_flying[1] && Output > 0 && Stored < _warehouseCapacity;
            var startProcessing = !Processing && Input > 0 && Output < OutputCapacity;
            for (var i = 0; i < 2; i++)
            {
                if (!_flying[i]) continue;
                _remaining[i] = Math.Max(0, _remaining[i] - seconds);
                if (_remaining[i] > 1e-9) continue;
                _flying[i] = false;
                if (i == 0) Input++; else Stored++;
            }
            if (Processing)
            {
                _processRemaining = Math.Max(0, _processRemaining - seconds);
                if (_processRemaining <= 1e-9) { Processing = false; Output++; }
            }
            if (startProcessing) { Input--; Processing = true; _processRemaining = _processingSeconds; }
            if (dispatchRaw) { Raw--; Dispatch(0); }
            if (dispatchOutput) { Output--; Dispatch(1); }
        }
        private void Dispatch(int from) { _flying[from] = true; _remaining[from] = _deliverySeconds; }
        private static void RequirePositive(double seconds)
        {
            if (double.IsNaN(seconds) || double.IsInfinity(seconds) || seconds <= 0) throw new ArgumentOutOfRangeException(nameof(seconds));
        }
    }
}
