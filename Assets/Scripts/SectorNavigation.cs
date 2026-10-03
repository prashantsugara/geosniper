using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using Unity.AI.Navigation;

namespace GeoSniper
{
    public sealed class SectorNavigation : MonoBehaviour
    {
        NavMeshSurface surface;
        void Configure()
        {
            if(surface==null) surface=GetComponent<NavMeshSurface>() ?? gameObject.AddComponent<NavMeshSurface>();
            surface.collectObjects=CollectObjects.Children;
            surface.useGeometry=NavMeshCollectGeometry.PhysicsColliders;
            surface.layerMask=~0;
        }
        public void Build()
        {
            Configure();
            surface.RemoveData();
            surface.BuildNavMesh();
        }
        public IEnumerator BuildAsync()
        {
            Configure();
            surface.RemoveData();
            var data=new NavMeshData(surface.agentTypeID) { name=gameObject.name };
            surface.navMeshData=data;
            // CollectSources still runs here, but the expensive tile bake runs off
            // the main thread so a streamed sector does not freeze gameplay.
            var operation=surface.UpdateNavMesh(data);
            if(operation!=null) yield return operation;
            if(surface!=null && surface.isActiveAndEnabled) surface.AddData();
        }
        public bool TryPath(Vector3 from,Vector3 to,out UnityEngine.AI.NavMeshPath path)
        {
            path=new UnityEngine.AI.NavMeshPath();
            return UnityEngine.AI.NavMesh.CalculatePath(from,to,UnityEngine.AI.NavMesh.AllAreas,path) && path.status==UnityEngine.AI.NavMeshPathStatus.PathComplete;
        }
    }
}
