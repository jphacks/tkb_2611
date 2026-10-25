using System;
using ProjectPenguin.Domain.Clock;
using ProjectPenguin.Domain.Reality;
using ProjectPenguin.Systems;
using UnityEngine;

namespace ProjectPenguin.Presentation.UI.ProductionVillage
{
    /// <summary>
    /// 村のスクリーンタイム: 設定アプリのスクリーンタイム (「日」の表示) のスクショを 1 枚選んでもらい、端末の中で読んで
    /// (<see cref="IScreenTimeSource"/>・<see cref="ScreenTimeOcrParser"/>)、昨日の合計を村の規則 (<c>VillageGame.AcceptScreen</c>) で電気にする。
    /// 画像と読んだ文字は端末の外に出さず、解析したら捨てる。保存もログ出力もしない (.claude/rules/privacy.md)。
    /// 同じ画像は記録の id (画像のハッシュ) で、同じ日は村の規則で 2 回受け取らない。
    /// </summary>
    public sealed class VillageScreenTimePresenter : MonoBehaviour
    {
        [SerializeField] private ProductionVillageSession _session;

        private IScreenTimeSource _source;
        private ProductionNavigator _navigator;
        private bool _waiting;

        public bool IsSupported => _source != null && _source.IsSupported;

        private void Awake()
        {
            _source = ScreenTimeSources.Create();
            _navigator = GetComponent<ProductionNavigator>();
        }

        private void OnEnable()
        {
            if (_session == null)
            {
                Debug.LogError($"{nameof(VillageScreenTimePresenter)}: 参照が未設定です。", this);
                enabled = false;
            }
        }

        /// <summary>スクショを選ぶ画面を出す。選んでいる間に呼んでも何もしない。</summary>
        public void Open()
        {
            if (_waiting || !IsSupported)
            {
                return;
            }

            if (_source is FakeScreenTimeSource fake)
            {
                // Editor など: 昨日の「日」の表示を読んだことにする (同じ見本は 2 回目に受け取り済みになる)。
                var yesterday = JstCalendar.ToJst(DateTimeOffset.UtcNow).Date.AddDays(-1);
                fake.Next = FakeScreenTimeSource.Sample(yesterday, 4 * 60 + 2);
            }

            _waiting = true;
            _source.RequestPick();
        }

        private void Update()
        {
            if (!_waiting || !_source.TryTake(out var status, out var capture))
            {
                return;
            }

            _waiting = false;
            switch (status)
            {
                case ScreenTimePickStatus.Cancelled:
                    return;
                case ScreenTimePickStatus.Failed:
                    Toast("画像を読めなかったよ。もう一度選んでみてね");
                    return;
            }

            var now = DateTimeOffset.UtcNow;
            // 読んだ行 (端末名・アプリ名を含む) はここで解析したら捨てる。
            var result = ScreenTimeOcrParser.Parse(capture.Lines, now);
            Toast(result.Succeeded ? Accept(result, capture.ImageHash, now) : Hint(result.Failure));
        }

        private string Accept(ScreenTimeParseResult result, string imageHash, DateTimeOffset now)
        {
            var reading = result.Reading;
            var today = JstCalendar.DayNumber(now);
            var total = Duration(reading.TotalMinutes);
            if (result.IsToday)
            {
                return $"今日の分 ({total}) はまだ途中だよ。あしたになったら、きのうの分を選んでね";
            }

            if (reading.Day != today - 1)
            {
                var yesterday = JstCalendar.ToJst(now).Date.AddDays(-1);
                return $"受け取れるのはきのう ({yesterday.Month}月{yesterday.Day}日) の分だけだよ";
            }

            if (string.IsNullOrEmpty(imageHash))
            {
                return "画像を読めなかったよ。もう一度選んでみてね";
            }

            var core = _session.Core;
            var accepted = core.AcceptScreen("screen:" + imageHash, reading.Day, reading.TotalMinutes, today, true, true, ProductionVillageSession.UtcNow);
            var message = accepted || core.Error.Length == 0 ? core.Notice : core.Error;
            return $"きのうは {total}。{message}";
        }

        private static string Hint(ScreenTimeParseFailure failure) => failure switch
        {
            ScreenTimeParseFailure.NotScreenTime => "スクリーンタイムの画面ではないみたい。設定 > スクリーンタイム の画面を撮ってね",
            ScreenTimeParseFailure.WeekView => "「週」の画面だよ。上の「日」を選んで、きのうの棒を押してから撮ってね",
            ScreenTimeParseFailure.DateMissing => "日付が読めなかったよ。「日」の画面を、日付が入るように撮ってね",
            ScreenTimeParseFailure.TotalMissing => "合計の時間が読めなかったよ。日付のすぐ下の時間が入るように撮ってね",
            ScreenTimeParseFailure.TooOld => "古すぎる日だよ。きのうの分を選んでね",
            _ => "うまく読めなかったよ。もう一度選んでみてね",
        };

        private static string Duration(int minutes) => minutes >= 60 ? $"{minutes / 60}時間{minutes % 60}分" : $"{minutes}分";

        private void Toast(string message)
        {
            if (_navigator != null)
            {
                _navigator.ShowToast(message);
            }
        }
    }
}
