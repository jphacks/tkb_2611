namespace ProjectPenguin.Domain.Reality
{
    /// <summary>
    /// backend が確かめたことの 1 行 (契約の <c>Check</c>)。画面は中身を解釈せずに並べる。
    /// 水の写真の見積もりなど、現実の入力の結果に付く。
    /// </summary>
    public sealed class InputCheck
    {
        public InputCheck(string id, string label, bool passed, string detail)
        {
            Id = id ?? "";
            Label = label ?? "";
            Passed = passed;
            Detail = detail ?? "";
        }

        public string Id { get; }
        public string Label { get; }
        public bool Passed { get; }

        /// <summary>補足。無ければ空。</summary>
        public string Detail { get; }
    }
}
