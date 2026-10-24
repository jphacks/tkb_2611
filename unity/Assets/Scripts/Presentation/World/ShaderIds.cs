using UnityEngine;

namespace ProjectPenguin.Presentation.World
{
    /// <summary>
    /// シェーダーのプロパティ ID (URP Lit / Unlit)。<c>SetColor("_BaseColor", ...)</c> のように文字列で書かず、ここの ID を使う。
    /// 文字列で渡すと呼ぶたびに ID を引き直すため、毎フレーム呼ぶ箇所では特に効く。
    /// </summary>
    public static class ShaderIds
    {
        public static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        public static readonly int BaseMap = Shader.PropertyToID("_BaseMap");
        public static readonly int Smoothness = Shader.PropertyToID("_Smoothness");
        public static readonly int Metallic = Shader.PropertyToID("_Metallic");
    }
}
