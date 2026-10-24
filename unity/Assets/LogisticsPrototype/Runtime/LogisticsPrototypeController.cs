using ProjectPenguin.Domain.LogisticsPrototype;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectPenguin.LogisticsPrototype
{
    /// <summary>Disposable scene-only clock and commands. Never reads or writes the product save.</summary>
    public sealed class LogisticsPrototypeController : MonoBehaviour
    {
        [SerializeField, Min(.1f)] private float _deliverySeconds = 1.5f;
        [SerializeField, Min(.1f)] private float _processingSeconds = 2f;
        [SerializeField] private Camera _camera;
        [SerializeField] private Transform[] _facilities;
        [SerializeField] private Font _font;
        private LogisticsPrototypeHud _hud;
        private double _accumulator;
        private int _selected = -1;
        private bool _paused;
        private string _message = "施設を送り元 → 届け先の順にタップして接続";
        public LogisticsChain Chain { get; private set; }
        public bool Paused => _paused;
        public string Message => _message;
        public int Selected => _selected;

        private void Awake()
        {
            ResetChain();
            _hud = new LogisticsPrototypeHud(transform, _camera, _facilities, _font, this);
        }
        private void Update()
        {
            if (_paused) return;
            // A short fixed step makes phase transitions observable and keeps work per frame bounded.
            // Long editor stalls / mobile suspension are not offline progression in this disposable trial.
            _accumulator += Mathf.Min(Time.deltaTime, .5f);
            while (_accumulator >= .05) { Chain.Advance(.05); _accumulator -= .05; }
        }
        private void LateUpdate() => _hud?.Refresh();
        public void ResetChain()
        {
            Chain = new LogisticsChain(_deliverySeconds, _processingSeconds);
            _accumulator = 0; _selected = -1; _paused = false;
            _message = "初期接続あり · 魚12個からスタート";
        }
        public void SelectFacility(int index)
        {
            if (index < 0 || index > 2) return;
            if (_selected < 0)
            {
                _selected = index; _message = "届け先の施設をタップ（漁場 → さばき場 → 倉庫）"; return;
            }
            _message = Chain.Connect(_selected, index) ? "接続しました" : "接続不可：順方向のみ・重複接続なし";
            _selected = -1;
        }
        public void DisconnectAll()
        {
            Chain.DisconnectAll(); _selected = -1;
            _message = "線を外しました · 配送済みの荷物は最後まで届きます";
        }
        public void TogglePause() { _paused = !_paused; }
        public string TimingLabel => $"配送 {_deliverySeconds:0.#}秒 / 加工 {_processingSeconds:0.#}秒 · 試作値";
    }
}
