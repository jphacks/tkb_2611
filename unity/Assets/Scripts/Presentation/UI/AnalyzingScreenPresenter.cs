using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;

namespace ProjectPenguin.Presentation.UI
{
    /// <summary>
    /// 解析中画面の 3 段階 (受け取り → 見分け → 資源に変える) の進み具合を表示する。
    /// 判定 (<see cref="EcoPhotoFlowPresenter"/>) の応答が来るまでは最後の段階で待ち、
    /// <see cref="Finish"/> で全段階を完了にしてから次へ進む。
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class AnalyzingScreenPresenter : MonoBehaviour
    {

        private static readonly string[] StepNames = { UiNames.Analyzing.StepRead, UiNames.Analyzing.StepClassify, UiNames.Analyzing.StepCalculate };

        [Tooltip("1 段階を見せる最短の秒数")]
        [SerializeField] private float _secondsPerStep = 0.7f;

        private VisualElement[] _steps;
        private Label _caption;
        private Coroutine _routine;
        private int _current = -1;

        private void OnEnable()
        {
            var screen = GetComponent<UIDocument>().rootVisualElement.Q(ScreenNavigator.ElementNameOf(AppScreen.Analyzing));
            _steps = new VisualElement[StepNames.Length];
            for (var i = 0; i < StepNames.Length; i++)
            {
                _steps[i] = screen.Q(StepNames[i]);
            }

            _caption = screen.Q<Label>(UiNames.Analyzing.AnalyzingCaption);
        }

        private void OnDisable() => Cancel();

        /// <summary>最初の段階から進め、最後の段階で <see cref="Finish"/> を待つ。</summary>
        public void Begin()
        {
            Cancel();
            SetCaption("写真を見ています");
            _routine = StartCoroutine(Run());
        }

        /// <summary>残りの段階を完了にし、少し見せてから <paramref name="onCompleted"/> を呼ぶ。</summary>
        public void Finish(Action onCompleted)
        {
            if (_routine != null)
            {
                StopCoroutine(_routine);
            }

            _routine = StartCoroutine(FinishRoutine(onCompleted));
        }

        /// <summary>途中でやめる。表示は最初の状態に戻す。</summary>
        public void Cancel()
        {
            if (_routine != null)
            {
                StopCoroutine(_routine);
                _routine = null;
            }

            ShowStep(-1);
        }

        public void SetCaption(string text)
        {
            if (_caption != null)
            {
                _caption.text = text;
            }
        }

        private IEnumerator Run()
        {
            for (var i = 0; i < _steps.Length; i++)
            {
                ShowStep(i);
                yield return new WaitForSecondsRealtime(_secondsPerStep);
            }

            _routine = null;
        }

        private IEnumerator FinishRoutine(Action onCompleted)
        {
            // 早く応答が来ても、各段階が一瞬で過ぎないように見せる。
            for (var i = Mathf.Max(0, _current); i < _steps.Length; i++)
            {
                ShowStep(i);
                yield return new WaitForSecondsRealtime(_secondsPerStep * 0.5f);
            }

            ShowStep(_steps.Length);
            yield return new WaitForSecondsRealtime(_secondsPerStep * 0.6f);
            _routine = null;
            onCompleted?.Invoke();
        }

        // current より前は完了、current は進行中、それ以降は未着手。-1 ならすべて未着手。
        private void ShowStep(int current)
        {
            _current = current;
            if (_steps == null)
            {
                return;
            }

            for (var i = 0; i < _steps.Length; i++)
            {
                _steps[i].EnableInClassList(UiClasses.Step.Done, current >= 0 && i < current);
                _steps[i].EnableInClassList(UiClasses.Step.Active, i == current);
            }
        }
    }
}
