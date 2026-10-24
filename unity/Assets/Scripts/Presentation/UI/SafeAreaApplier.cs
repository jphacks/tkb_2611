using UnityEngine;
using UnityEngine.UIElements;

namespace ProjectPenguin.Presentation.UI
{
    /// <summary>
    /// Screen.safeArea をパネル座標に変換し、ノッチ・ジェスチャーバーを避ける余白を UI Toolkit の要素に反映する。
    /// 上・左・右は <see cref="UiClasses.SafeArea.Root"/> の padding に、下は <see cref="UiClasses.SafeArea.Bottom"/> の高さに入れる
    /// (タブバーの背景を画面下端まで伸ばしたまま、ボタンだけをジェスチャーバーの上に置くため)。
    /// 画面ごとに要素を持てるよう、名前ではなくクラスで探して該当する全要素に適用する。
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class SafeAreaApplier : MonoBehaviour
    {

        private UIDocument _document;
        private VisualElement _root;
        private Rect _appliedSafeArea;
        private Vector2Int _appliedScreenSize;

        private void OnEnable()
        {
            _document = GetComponent<UIDocument>();
            _root = _document.rootVisualElement;

            _appliedScreenSize = Vector2Int.zero;
            _root.RegisterCallback<GeometryChangedEvent>(OnRootGeometryChanged);
            Apply();
        }

        private void OnDisable()
        {
            _root?.UnregisterCallback<GeometryChangedEvent>(OnRootGeometryChanged);
        }

        // 回転や解像度変更ではルートの大きさが変わるので、ここで取り直せば毎フレーム監視しなくて済む。
        private void OnRootGeometryChanged(GeometryChangedEvent evt) => Apply();

        private void Apply()
        {
            var panel = _root.panel;
            if (panel == null)
            {
                return;
            }

            var safeArea = Screen.safeArea;
            var screenSize = new Vector2Int(Screen.width, Screen.height);
            if (safeArea == _appliedSafeArea && screenSize == _appliedScreenSize)
            {
                return;
            }

            _appliedSafeArea = safeArea;
            _appliedScreenSize = screenSize;

            var leftTop = RuntimePanelUtils.ScreenToPanel(panel, new Vector2(safeArea.xMin, Screen.height - safeArea.yMax));
            var rightBottom = RuntimePanelUtils.ScreenToPanel(panel, new Vector2(Screen.width - safeArea.xMax, safeArea.yMin));

            // 端末ごとに変わる実測値なので USS では表せず、インラインスタイルで入れる。
            _root.Query(className: UiClasses.SafeArea.Root).ForEach(element =>
            {
                element.style.paddingTop = leftTop.y;
                element.style.paddingLeft = leftTop.x;
                element.style.paddingRight = rightBottom.x;
            });

            _root.Query(className: UiClasses.SafeArea.Bottom).ForEach(element => element.style.height = rightBottom.y);
        }
    }
}
