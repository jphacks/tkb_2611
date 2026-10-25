using System;
using ProjectPenguin.Domain.Island;
using ProjectPenguin.Domain.VillageSandbox;
using UnityEngine;

namespace ProjectPenguin.VillageUiPrototype
{
    public sealed class VillageDriver : MonoBehaviour
    {
        [SerializeField] private Camera _camera;
        [SerializeField] private Font _font;
        [SerializeField] private GameObject[] _prefabs;
        [SerializeField] private Transform _plot;
        [SerializeField] private GameObject _harborPrefab;
        [Header("開発用・Releaseでは無効")]
        [SerializeField] private bool _infiniteResources=true;
        [SerializeField] private bool _running=true;
        [SerializeField,Range(0,20)] private float _timeScale=1;
        [SerializeField,Range(.1f,10)] private float _productionSpeed=1;
        [SerializeField,Range(.1f,10)] private float _deliverySeconds=1.5f;
        [SerializeField,Min(0)] private float _gas=100;
        [SerializeField,Min(0)] private float _electricity=100;
        [SerializeField,Min(0)] private float _water=100;
        private double _clock;
        private float _nextRender;
        private int _selected;
        private string _notice="建てるから好きな施設を村へ。建物は長押しで移動できます";
        public VillageSandboxFacade Factory {get;private set;}
        public VillageInteraction Interaction {get;private set;}
        public VillageWorld World {get;private set;}
        public VillageHud Hud {get;private set;}
        public Camera ViewCamera=>_camera;
        public bool DeveloperMode=>Application.isEditor||Debug.isDebugBuild;
        public bool ResourcesAreInfinite=>DeveloperMode&&_infiniteResources;
        public SandboxView View=>Factory?.CreateView();
        public int SelectedId=>_selected;
        public string Notice=>_notice;

        private void Start()
        {
            Factory=new VillageSandboxFacade(ResourcesAreInfinite);
            Factory.SetResources(_gas,_electricity,_water);
            World=new VillageWorld(transform,_camera,_prefabs,_harborPrefab);
            Hud=new VillageHud(transform,_font,this);
            Interaction=gameObject.AddComponent<VillageInteraction>();Interaction.Initialize(this);
            Factory.Place(FacilityKind.Fishery,8,10);Factory.Place(FacilityKind.Preparation,11,10);Factory.Place(FacilityKind.Warehouse,14,10);
            Render();
        }
        private void Update()
        {
            if(Factory==null)return;
            Factory.Configure(ResourcesAreInfinite,_productionSpeed,Math.Max(100,(int)(_deliverySeconds*1000)));
            if(_running){_clock+=Math.Min(Time.unscaledDeltaTime,.5f)*1000*(DeveloperMode?_timeScale:1);Factory.AdvanceTo((long)_clock);}
            if(Time.unscaledTime>=_nextRender){Render();_nextRender=Time.unscaledTime+.1f;}
            World.UpdateCargo(View);
        }
        public void Render()
        {if(Factory==null||World==null||Hud==null)return;var v=View;World.Render(v,_selected);Hud.Render(v,_selected,_notice);}
        public void Select(int id){_selected=id;Hud.CloseBuild();Render();Hud.ShowFacility(id);}
        public void ShowNotice(string message){_notice=message;Render();}
        public void BeginBuild(FacilityKind kind){Interaction.Choose(kind);Hud.CloseBuild();ShowNotice("村の空き地を選んで配置。戻る操作でキャンセルできます");}
        public void Place(FacilityKind kind,int x,int y)
        {var r=Factory.Place(kind,x,y);_notice=r.Reason;if(r.Success)_selected=r.Id;Render();}
        public void Move(int id,int x,int y){var r=Factory.Move(id,x,y);_notice=r.Reason;Render();}
        public void RemoveSelected(){var r=Factory.Remove(_selected);_notice=r.Reason;if(r.Success){_selected=0;Hud.CloseSheet();}Render();}
        public void ApplyInspectorResources(){if(DeveloperMode&&Factory!=null){Factory.SetResources(_gas,_electricity,_water);Render();}}
        public void AdvanceSeconds(float seconds)
        {if(!DeveloperMode||Factory==null)return;_clock+=Math.Max(0,Math.Min(seconds,3600))*1000;Factory.AdvanceTo((long)_clock);Render();}
        public void ToggleClock(){if(DeveloperMode)_running=!_running;}
        public void ClearVillage()
        {
            if(!DeveloperMode)return;
            Factory=new VillageSandboxFacade(ResourcesAreInfinite);Factory.SetResources(_gas,_electricity,_water);
            _clock=0;_selected=0;Interaction.Cancel();Hud.CloseSheet();_notice="空き地から自由に村を作れます";Render();
        }
        private void OnApplicationPause(bool paused){if(paused)Interaction?.Cancel();}
        private void OnApplicationFocus(bool focused){if(!focused)Interaction?.Cancel();}
    }
}
