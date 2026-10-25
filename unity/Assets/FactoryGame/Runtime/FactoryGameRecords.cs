using System;
using System.Globalization;
using ProjectPenguin.Domain.Clock;
using UnityEngine;
#if UNITY_IOS && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

namespace ProjectPenguin.FactoryGame
{
    public interface IFactoryRecordReader
    {
        bool Supported { get; }
        void Open(string target, int serial, string date, bool barcode);
        void Cancel(int serial);
    }

    public sealed class FactoryRecordInput
    {
        private readonly string _target;
        private readonly Action<string> _notify;
        private readonly Action<string> _barcode;
        private readonly Action<int> _screen;
        private readonly IFactoryRecordReader _reader;
        private readonly Func<int> _today;
        private readonly Func<float> _clock;
        private int _serial, _day;
        private bool _pending, _isBarcode;
        private float _started;
        public bool Pending => _pending;

        public FactoryRecordInput(string target, Action<string> notify, Action<string> barcode, Action<int> screen,
            IFactoryRecordReader reader = null, Func<int> today = null, Func<float> clock = null)
        {
            _target = target; _notify = notify; _barcode = barcode; _screen = screen;
            _reader = reader ?? new IosFactoryRecordReader(); _today = today ?? (() => FactoryGameDriver.RecordToday);
            _clock = clock ?? (() => Time.realtimeSinceStartup);
        }

        public void Open(bool barcode)
        {
            if (_pending) return;
            if (!_reader.Supported) { _notify("実機の読み取りはiOS専用です。合成入力とは区別しています"); return; }
            _pending = true; _isBarcode = barcode; _day = _today(); _serial++;
            _started = _clock();
            var date = JstCalendar.DateOfDay(_day - 1).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            _reader.Open(_target, _serial, date, barcode);
            _notify(barcode ? "商品バーコードをカメラに向けてください" : "前日のスクリーンタイム画像を選択してください。端末の中だけで読み取ります");
        }

        public void Poll()
        {
            if (_pending && _clock() - _started > 120)
            { Cancel(); _notify("読み取りが時間切れになりました。受領していません。もう一度お試しください"); }
        }

        public void Cancel()
        {
            if (_pending) _reader.Cancel(_serial);
            _pending = false;
        }

        public void Receive(string json)
        {
            RecordResult result;
            if (!_pending) return;
            try
            {
                if (string.IsNullOrEmpty(json) || json.Length > 4096) throw new ArgumentException();
                result = new RecordResult { minutes = -1 };
                JsonUtility.FromJsonOverwrite(json, result);
            }
            catch (ArgumentException) { Cancel(); _notify("読み取り結果を受け取れませんでした。受領していません"); return; }
            if (!_pending || result == null || result.requestId != _serial) return;
            _pending = false;
            if (result.status == "cancelled") { _notify("読み取りを取り消しました。受領していません"); return; }
            if (result.status == "denied") { _notify("カメラの許可がありません。iOSの設定で許可して再試行してください"); return; }
            if (result.status != "ok") { _notify("読み取れませんでした。対象日と画像・商品を確認してください。受領していません"); return; }
            if (_day != _today()) { _notify("読み取り中に日付が変わりました。もう一度読み取ってください"); return; }
            if (_isBarcode && !ProjectPenguin.Domain.Factory.FactorySession.ValidBarcode(result.barcode)
                || !_isBarcode && (result.minutes < 0 || result.minutes > 1440))
            { _notify("読み取った値の確認が必要です。受領していません"); return; }
            if (_isBarcode) _barcode(result.barcode); else _screen(result.minutes);
        }

        [Serializable] private sealed class RecordResult
        { public string status, barcode; public int requestId, minutes; }
    }

    internal sealed class IosFactoryRecordReader : IFactoryRecordReader
    {
#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern void PenguinFactoryRecords_Open(string target, string method, int serial, string date, bool barcode);
        [DllImport("__Internal")] private static extern void PenguinFactoryRecords_Cancel(int serial);
        public bool Supported => true;
        public void Open(string target, int serial, string date, bool barcode) => PenguinFactoryRecords_Open(target, "OnFactoryRecordResult", serial, date, barcode);
        public void Cancel(int serial) => PenguinFactoryRecords_Cancel(serial);
#else
        public bool Supported => false;
        public void Open(string target, int serial, string date, bool barcode) { }
        public void Cancel(int serial) { }
#endif
    }
}
