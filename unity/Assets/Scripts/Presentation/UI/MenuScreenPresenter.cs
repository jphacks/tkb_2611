using System;
using ProjectPenguin.Systems;
using UnityEngine;
using UnityEngine.UIElements;

namespace ProjectPenguin.Presentation.UI
{
    /// <summary>メニュー画面のうち、画面遷移以外の操作 (遊び方・データを消す) と見出しを受け持つ。</summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class MenuScreenPresenter : MonoBehaviour
    {
        private const string HelpText =
            "・毎日 1 枚、エコな行動の写真を撮ろう。電気・水がもらえて、今日の記録になるよ。\n" +
            "・記録すると氷が増え、休むと少し溶ける。氷が足りないと、広げた島の端から沈むよ。\n" +
            "・歩くとガス、ミッションの振り返りでオールマイティがもらえるよ。\n" +
            "・施設を建ててペンギンに働いてもらい、港の注文に品物を届けるとお魚が増えるよ。";

        [SerializeField] private GameStateSystem _game;
        [SerializeField] private ScreenNavigator _navigator;
        [SerializeField] private OnboardingPresenter _onboarding;

        private VisualElement _screen;
        private VisualElement _modal;
        private Label _modalTitle;
        private Label _modalText;
        private Button _ok;
        private Button _cancel;
        private Button _help;
        private Button _reset;
        private Action _onOk;

        private void OnEnable()
        {
            if (_game == null || _navigator == null)
            {
                Debug.LogError($"{nameof(MenuScreenPresenter)}: 参照が未設定です。", this);
                enabled = false;
                return;
            }

            _screen = GetComponent<UIDocument>().rootVisualElement.Q(ScreenNavigator.ElementNameOf(AppScreen.Menu));
            _modal = _screen.Q(UiNames.Menu.MenuModal);
            _modalTitle = _screen.Q<Label>(UiNames.Menu.MenuModalTitle);
            _modalText = _screen.Q<Label>(UiNames.Menu.MenuModalText);
            _ok = _screen.Q<Button>(UiNames.Menu.MenuModalOk);
            _cancel = _screen.Q<Button>(UiNames.Menu.MenuModalCancel);
            _help = _screen.Q<Button>(UiNames.Menu.MenuHelp);
            _reset = _screen.Q<Button>(UiNames.Menu.MenuReset);
            _ok.clicked += OnOk;
            _cancel.clicked += Close;
            _help.clicked += ShowHelp;
            _reset.clicked += ConfirmReset;
            _navigator.ScreenShown += OnScreenShown;
        }

        private void OnDisable()
        {
            if (_ok != null)
            {
                _ok.clicked -= OnOk;
                _cancel.clicked -= Close;
                _help.clicked -= ShowHelp;
                _reset.clicked -= ConfirmReset;
            }

            if (_navigator != null)
            {
                _navigator.ScreenShown -= OnScreenShown;
            }
        }

        private void OnScreenShown(AppScreen screen, AppScreen previous)
        {
            if (screen != AppScreen.Menu)
            {
                return;
            }

            var name = string.IsNullOrEmpty(_game.PlayerName) ? "ペンギンの島" : $"{_game.PlayerName}島";
            _screen.Q<Label>(UiNames.Menu.MenuPlayer).text = $"{name}・レベル {_game.Island.Level}";
            Close();
        }

        private void ShowHelp() => Open("遊び方", HelpText, "わかった", null, false);

        private void ConfirmReset() => Open("データを消す？", "島・資源・記録がすべて消えて、最初からになります。元には戻せません。", "消す", () =>
        {
            _game.ResetAll();
            _navigator.Show(AppScreen.Home);
            _onboarding?.ShowIfNeeded();
        }, true);

        private void Open(string title, string text, string ok, Action onOk, bool cancellable)
        {
            _modalTitle.text = title;
            _modalText.text = text;
            _ok.text = ok;
            _onOk = onOk;
            IslandUi.SetVisible(_cancel, cancellable);
            IslandUi.SetVisible(_modal, true);
        }

        private void OnOk()
        {
            var a = _onOk;
            Close();
            a?.Invoke();
        }

        private void Close()
        {
            _onOk = null;
            IslandUi.SetVisible(_modal, false);
        }
    }
}
