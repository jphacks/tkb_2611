using System;
using System.Collections.Generic;
using ProjectPenguin.Domain.Clock;
using ProjectPenguin.Domain.Reality;
#if UNITY_IOS && !UNITY_EDITOR
using System.Runtime.InteropServices;
using UnityEngine;
#endif

namespace ProjectPenguin.Systems
{
    /// <summary>スクショを選ぶ操作の結果。</summary>
    public enum ScreenTimePickStatus
    {
        /// <summary>画像を読めた。<see cref="ScreenTimeCapture"/> を <see cref="ScreenTimeOcrParser"/> に渡す。</summary>
        Captured,

        /// <summary>選ばずに閉じた。</summary>
        Cancelled,

        /// <summary>画像を読めなかった。手入力に回す。</summary>
        Failed,
    }

    /// <summary>
    /// 選ばれたスクショを端末の中で読んだ結果。画像そのものは持たない (ネイティブ側で捨てる)。
    /// <see cref="Lines"/> には端末名 (利用者の名前) の行も入るので、解析が済んだら捨て、保存もログもしない (.claude/rules/privacy.md)。
    /// </summary>
    public sealed class ScreenTimeCapture
    {
        public ScreenTimeCapture(string imageHash, IReadOnlyList<OcrLine> lines)
        {
            ImageHash = imageHash ?? "";
            Lines = lines ?? Array.Empty<OcrLine>();
        }

        /// <summary>画像の SHA-256 (hex)。同じ画像の取り込みを弾くために使う。</summary>
        public string ImageHash { get; }

        public IReadOnlyList<OcrLine> Lines { get; }
    }

    /// <summary>
    /// スクリーンタイムのスクショの取り出し口 (project-penguin の docs/reality-inputs.md の「2」から写した)。<see cref="IStepSource"/> と同じく、
    /// 要求 (<see cref="RequestPick"/>) してから <see cref="TryTake"/> で受け取る。
    /// </summary>
    public interface IScreenTimeSource
    {
        bool IsSupported { get; }

        /// <summary>スクリーンショットを 1 枚選ぶ画面を出す。選んでいる間に呼んでも何もしない。</summary>
        void RequestPick();

        /// <summary>選ぶ操作が終わっていれば true。<paramref name="capture"/> は <see cref="ScreenTimePickStatus.Captured"/> のときだけ入る。</summary>
        bool TryTake(out ScreenTimePickStatus status, out ScreenTimeCapture capture);
    }

    public static class ScreenTimeSources
    {
        /// <summary>実行環境に合った取り出し口。</summary>
        public static IScreenTimeSource Create()
        {
#if UNITY_IOS && !UNITY_EDITOR
            return new IosScreenTimeSource();
#else
            return new FakeScreenTimeSource();
#endif
        }
    }

    /// <summary>
    /// Editor と iOS 以外。選ぶ操作の代わりに <see cref="Next"/> を返す。
    /// 既定は iOS 26 の「日」の表示を読んだときの行 (端末名とアプリ名は架空)。7 日前から昨日へ 1 日ずつ進み、少しずつ短くなる。
    /// </summary>
    public sealed class FakeScreenTimeSource : IScreenTimeSource
    {
        private bool _pending;

        private int _daysAgo = 7;

        /// <summary>次に返す結果。null なら日をずらした見本 (<see cref="Sample"/>) を返す。</summary>
        public ScreenTimeCapture Next { get; set; }

        /// <summary>見本の日付を決める今の時刻。Editor では <c>GameStateSystem.Now</c> (時間を進めた分を含む) を入れる。</summary>
        public DateTimeOffset Now { get; set; } = DateTimeOffset.UtcNow;

        public bool IsSupported => true;

        public void RequestPick()
        {
            _pending = true;
        }

        public bool TryTake(out ScreenTimePickStatus status, out ScreenTimeCapture capture)
        {
            status = ScreenTimePickStatus.Cancelled;
            capture = null;
            if (!_pending)
            {
                return false;
            }

            _pending = false;
            status = ScreenTimePickStatus.Captured;
            if (Next != null)
            {
                capture = Next;
                return true;
            }

            // 最初の日が基準になり、2 回目からは基準より減った分が出るよう、日ごとに 20 分ずつ短くする。
            var date = JstCalendar.ToJst(Now).Date.AddDays(-_daysAgo);
            capture = Sample(date, 300 - 20 * (8 - _daysAgo));
            _daysAgo = _daysAgo <= 1 ? 7 : _daysAgo - 1;
            return true;
        }

        /// <summary>iOS 26 の「日」の表示を OCR したときの行を作る (Editor とテストでの確認用)。</summary>
        public static ScreenTimeCapture Sample(DateTime date, int totalMinutes, bool isToday = false)
        {
            var total = totalMinutes >= 60 ? $"{totalMinutes / 60}時間{totalMinutes % 60}分" : $"{totalMinutes}分";
            var dayWord = isToday ? "今日" : $"{"日月火水木金土"[(int)date.DayOfWeek]}曜日";
            var lines = new[]
            {
                new OcrLine("22:16", 0.13, 0.025, 0.11, 0.02),
                new OcrLine("Editor の iPhone15", 0.32, 0.085, 0.36, 0.02),
                new OcrLine("週", 0.27, 0.16, 0.03, 0.015),
                new OcrLine("日", 0.69, 0.16, 0.03, 0.015),
                new OcrLine("スクリーンタイム", 0.08, 0.225, 0.29, 0.02),
                new OcrLine($"{dayWord}、{date.Month}月{date.Day}日", 0.08, 0.278, 0.27, 0.02),
                new OcrLine(total, 0.08, 0.31, 0.36, 0.035),
                new OcrLine("14時間", 0.82, 0.355, 0.10, 0.015),
                new OcrLine("平均", 0.82, 0.41, 0.06, 0.015),
                new OcrLine("前回のアップデート：今日 22:15", 0.08, 0.74, 0.42, 0.015),
            };
            return new ScreenTimeCapture($"fake-{date:yyyyMMdd}-{totalMinutes}", lines);
        }
    }

#if UNITY_IOS && !UNITY_EDITOR
    /// <summary>iOS の PHPicker と Vision (Assets/Plugins/iOS/PenguinScreenTime.swift)。</summary>
    public sealed class IosScreenTimeSource : IScreenTimeSource
    {
        private const int StateBusy = 1;
        private const int StateDone = 2;
        private const int StateCancelled = 3;

        [DllImport("__Internal")]
        private static extern void PenguinScreenTime_Pick();

        [DllImport("__Internal")]
        private static extern int PenguinScreenTime_State();

        [DllImport("__Internal")]
        private static extern string PenguinScreenTime_TakeResult();

        private bool _pending;

        public bool IsSupported => true;

        public void RequestPick()
        {
            if (_pending)
            {
                return;
            }

            _pending = true;
            PenguinScreenTime_Pick();
        }

        public bool TryTake(out ScreenTimePickStatus status, out ScreenTimeCapture capture)
        {
            status = ScreenTimePickStatus.Failed;
            capture = null;
            if (!_pending)
            {
                return false;
            }

            var state = PenguinScreenTime_State();
            if (state == StateBusy)
            {
                return false;
            }

            _pending = false;
            if (state == StateCancelled)
            {
                status = ScreenTimePickStatus.Cancelled;
                return true;
            }

            if (state != StateDone)
            {
                return true;
            }

            capture = Parse(PenguinScreenTime_TakeResult());
            status = capture != null ? ScreenTimePickStatus.Captured : ScreenTimePickStatus.Failed;
            return true;
        }

        [Serializable]
        private sealed class ResultDto
        {
            public string hash;
            public LineDto[] lines;
        }

        [Serializable]
        private sealed class LineDto
        {
            public string text;
            public double x;
            public double y;
            public double w;
            public double h;
        }

        private static ScreenTimeCapture Parse(string json)
        {
            if (string.IsNullOrEmpty(json))
            {
                return null;
            }

            ResultDto dto;
            try
            {
                dto = JsonUtility.FromJson<ResultDto>(json);
            }
            catch (ArgumentException)
            {
                // 中身 (読んだ文字) は例外の文にもログにも出さない。
                return null;
            }

            if (dto?.lines == null)
            {
                return null;
            }

            var lines = new OcrLine[dto.lines.Length];
            for (var i = 0; i < lines.Length; i++)
            {
                var l = dto.lines[i];
                lines[i] = new OcrLine(l.text, l.x, l.y, l.w, l.h);
            }

            return new ScreenTimeCapture(dto.hash, lines);
        }
    }
#endif
}
