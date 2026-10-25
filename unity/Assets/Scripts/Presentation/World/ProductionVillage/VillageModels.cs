using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectPenguin.Presentation.World.ProductionVillage
{
    /// <summary>村の 3D 表示で共通に使う、モデルの置き方・色の付け方・盤面に貼る矩形の作り方。</summary>
    public static class VillageModels
    {
        private static readonly int BaseColor=Shader.PropertyToID("_BaseColor");
        private static readonly int LegacyColor=Shader.PropertyToID("_Color");
        private static MaterialPropertyBlock _block;

        /// <summary>
        /// プレハブを横幅 <paramref name="width"/> に合わせて置く。足元が親の原点に来るように中心を寄せる。
        /// 演出用の部品 (ProductionIslandMotion など) 以外のスクリプトは止める。
        /// </summary>
        public static GameObject Spawn(GameObject prefab,float width,Transform parent,string name)
        {
            var holder=new GameObject(name);holder.transform.SetParent(parent,false);
            if(prefab==null)return holder;
            var instance=Object.Instantiate(prefab,holder.transform);
            foreach(var b in instance.GetComponentsInChildren<MonoBehaviour>(true))b.enabled=b is ProductionIslandMotion||b is ProductionIslandPenguinView;
            foreach(var a in instance.GetComponentsInChildren<Animator>(true))a.enabled=false;
            foreach(var c in instance.GetComponentsInChildren<Collider>(true))c.enabled=false;
            var renderers=instance.GetComponentsInChildren<Renderer>();if(renderers.Length==0)return holder;
            var bounds=renderers[0].bounds;foreach(var r in renderers.Skip(1))bounds.Encapsulate(r.bounds);
            instance.transform.localScale*=width/Mathf.Max(.001f,Mathf.Max(bounds.size.x,bounds.size.z));
            bounds=renderers[0].bounds;foreach(var r in renderers.Skip(1))bounds.Encapsulate(r.bounds);
            instance.transform.localPosition-=new Vector3(bounds.center.x-holder.transform.position.x,bounds.min.y-holder.transform.position.y,bounds.center.z-holder.transform.position.z);
            foreach(var r in renderers){r.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;}
            return holder;
        }

        /// <summary>マテリアルを複製せずに色を掛ける (URP Lit の Base Color)。白で元の色に戻る。</summary>
        public static void Tint(GameObject root,Color color)
        {
            _block??=new MaterialPropertyBlock();
            foreach(var r in root.GetComponentsInChildren<Renderer>())
            {
                r.GetPropertyBlock(_block);_block.SetColor(BaseColor,color);_block.SetColor(LegacyColor,color);r.SetPropertyBlock(_block);
            }
        }

        /// <summary>盤面のキャンバス (地面に寝かせた World Space Canvas) に色付きの矩形を貼る。</summary>
        public static Image Rect(string name,Transform parent,Vector2 position,Vector2 size,Color color)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(Image));go.transform.SetParent(parent,false);
            var rt=(RectTransform)go.transform;rt.anchoredPosition=position;rt.sizeDelta=size;
            var i=go.GetComponent<Image>();i.color=color;i.raycastTarget=false;return i;
        }

        /// <summary>a から b へ伸びる線として矩形を置く。</summary>
        public static void Line(Image image,Vector2 a,Vector2 b,float width)
        {
            var rt=image.rectTransform;var delta=b-a;rt.pivot=new Vector2(0,.5f);rt.anchoredPosition=a;rt.sizeDelta=new Vector2(delta.magnitude,width);
            rt.localRotation=Quaternion.Euler(0,0,Mathf.Atan2(delta.y,delta.x)*Mathf.Rad2Deg);
        }
    }
}
