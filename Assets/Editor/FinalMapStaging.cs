using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using System.Collections.Generic;
using DeadLetter.Interaction;

namespace DeadLetter.Editor
{
    public static class FinalMapStaging
    {
        public const string TestScene = "Assets/Scenes/SampleScene_FinalMap_Test.unity";
        private const string StorageOpeningMesh = "Assets/Art/Models/FinalMap_Storage_WithExteriorOpening.asset";
        public static void BuildBatch()
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
            var gray = GameObject.Find("DEAD LETTER - Graybox").transform;
            var art = gray.Find("Final Map Visuals (Visual Only)");
            if (art == null) throw new InvalidOperationException("Final map visual layer missing");
            if(gray.Find("Final Map Storage Exterior Opening") != null)
            {
                Debug.Log("[FinalCandidate] Final map is already applied; running passage validation without changing transforms.");
                Physics.SyncTransforms();
                InspectPassages(art);
                TestCharacterPassages();
                return;
            }
            var hall = art.Find("복도");
            // Use the authored hall junction, not the total bounds (which include an outdoor apron).
            Vector3 origin = hall.position; origin.y = 0;
            art.RotateAround(origin, Vector3.up, 180f);
            art.position -= origin;
            var structures = new HashSet<string>{"복도","비밀의방","작업실","창고","창구","탕비실"};
            foreach(var r in art.GetComponentsInChildren<Renderer>(true))
            {
                r.enabled = structures.Contains(r.name);
                if (!r.enabled) continue;
                var mc = r.GetComponent<MeshCollider>();
                if (mc == null) mc = r.gameObject.AddComponent<MeshCollider>();
                mc.sharedMesh = r.GetComponent<MeshFilter>().sharedMesh;
                mc.convex = false;
            }
            foreach(var r in gray.GetComponentsInChildren<Renderer>(true))
            {
                if(r.transform.IsChildOf(art)) continue;
                if(r.name.Contains("Wall") || r.name.EndsWith("Floor") || r.name.EndsWith("Ceiling"))
                {
                    r.enabled=false;
                    foreach(var c in r.GetComponents<Collider>()) c.enabled=false;
                }
            }
            Vector3 storage = art.Find("창고").GetComponent<Renderer>().bounds.center; storage.y=0;
            Vector3 workshop = art.Find("작업실").GetComponent<Renderer>().bounds.center; workshop.y=0;
            var transforms = gray.Cast<Transform>().Concat(gray.Find("Production Room Art").Cast<Transform>())
                .Concat(GameObject.Find("DAY 2 Gameplay Objects").transform.Cast<Transform>()).ToArray();
            foreach(var t in transforms)
            {
                string n=t.name;
                if(n.StartsWith("Storage") || n.StartsWith("Empty Box"))
                    MoveRoom(t,new Vector3(0,0,11.5f),storage,90);
                else if(n.StartsWith("Workshop") || n.StartsWith("DAY2 ") || n.StartsWith("Box Stamp"))
                    MoveRoom(t,new Vector3(11.5f,0,0),workshop,-90);
            }
            Physics.SyncTransforms();
            AlignDoor(gray,art,"Counter Hall Door","창구 안쪽 문");
            AlignDoor(gray,art,"Counter Exterior Door","창구 바깥 문");
            AlignDoor(gray,art,"Storage Hall Door","창고 문");
            AlignDoor(gray,art,"Workshop Hall Door","작업실 문");
            AlignDoor(gray,art,"Pantry Hall Door","탕비실 문");
            AlignDoor(gray,art,"Hidden Room Door","비밀의방 문");
            // The FBX has six authored openings while the live game owns seven functional doors.
            // Preserve the storage exterior door and derive a matching opening without altering the FBX source.
            BuildStorageExteriorOpening(gray, art.Find("창고").GetComponent<MeshRenderer>());
            // Structural mesh colliders already bound each authored opening. Legacy lintel
            // colliders are redundant and touched the real controller skin at two thresholds.
            foreach(Transform child in gray)
                if(child.name.EndsWith(" Door Lintel"))
                    foreach(var collider in child.GetComponents<Collider>()) collider.enabled=false;
            Physics.SyncTransforms();
            foreach(var root in scene.GetRootGameObjects())
                foreach(var component in root.GetComponentsInChildren<Component>(true))
                    if(component != null && PrefabUtility.IsPartOfPrefabInstance(component))
                        PrefabUtility.RecordPrefabInstancePropertyModifications(component);
            EditorSceneManager.SaveScene(scene,TestScene);
            EditorSceneManager.OpenScene(TestScene);
            art=GameObject.Find("Final Map Visuals (Visual Only)").transform;
            Physics.SyncTransforms();
            Debug.Log("[FinalCandidate] Saved " + TestScene);
            InspectPassages(art);
            TestCharacterPassages();
        }
        private static void BuildStorageExteriorOpening(Transform gray, MeshRenderer storageRenderer)
        {
            var storageFilter=storageRenderer.GetComponent<MeshFilter>();
            var source=storageFilter.sharedMesh;
            var vertices=source.vertices;
            var sourceTriangles=source.triangles;
            float wallX=source.bounds.max.x;
            var kept=new List<int>(sourceTriangles.Length);
            int removed=0;
            for(int i=0;i<sourceTriangles.Length;i+=3)
            {
                Vector3 a=vertices[sourceTriangles[i]], b=vertices[sourceTriangles[i+1]], c=vertices[sourceTriangles[i+2]];
                float minX=Mathf.Min(a.x,Mathf.Min(b.x,c.x));
                float maxY=Mathf.Max(a.y,Mathf.Max(b.y,c.y));
                float minY=Mathf.Min(a.y,Mathf.Min(b.y,c.y));
                float maxZ=Mathf.Max(a.z,Mathf.Max(b.z,c.z));
                float minZ=Mathf.Min(a.z,Mathf.Min(b.z,c.z));
                bool exteriorWallFace=minX>wallX-.004f && maxY-minY>.02f && maxZ-minZ>.08f;
                if(exteriorWallFace){removed++; continue;}
                kept.Add(sourceTriangles[i]); kept.Add(sourceTriangles[i+1]); kept.Add(sourceTriangles[i+2]);
            }
            if(removed!=4) throw new InvalidOperationException($"Storage exterior wall extraction expected 4 triangles, got {removed}.");
            var derived=UnityEngine.Object.Instantiate(source);
            derived.name="FinalMap Storage With Exterior Opening";
            derived.triangles=kept.ToArray();
            derived.RecalculateBounds();
            AssetDatabase.DeleteAsset(StorageOpeningMesh);
            AssetDatabase.CreateAsset(derived,StorageOpeningMesh);
            storageFilter.sharedMesh=derived;
            storageRenderer.GetComponent<MeshCollider>().sharedMesh=derived;

            var previous=gray.Find("Final Map Storage Exterior Opening");
            if(previous!=null) UnityEngine.Object.DestroyImmediate(previous.gameObject);
            var root=new GameObject("Final Map Storage Exterior Opening").transform;
            root.SetParent(gray,false);
            var leaf=gray.Find("Storage Exterior Door Pivot").GetComponentsInChildren<BoxCollider>(true)
                .First(c=>c.name.EndsWith(" Leaf"));
            Physics.SyncTransforms();
            Bounds room=storageRenderer.bounds;
            Bounds door=leaf.bounds;
            float thickness=.18f;
            float wallCenterX=room.max.x-thickness*.5f;
            float openMin=door.min.z-.10f, openMax=door.max.z+.10f;
            float openTop=door.max.y+.08f;
            CreateWallPiece(root,"Storage Exterior Wall Near",
                new Vector3(wallCenterX,(room.min.y+room.max.y)*.5f,(room.min.z+openMin)*.5f),
                new Vector3(thickness,room.size.y,openMin-room.min.z),storageRenderer.sharedMaterial);
            CreateWallPiece(root,"Storage Exterior Wall Far",
                new Vector3(wallCenterX,(room.min.y+room.max.y)*.5f,(openMax+room.max.z)*.5f),
                new Vector3(thickness,room.size.y,room.max.z-openMax),storageRenderer.sharedMaterial);
            CreateWallPiece(root,"Storage Exterior Wall Lintel",
                new Vector3(wallCenterX,(openTop+room.max.y)*.5f,(openMin+openMax)*.5f),
                new Vector3(thickness,room.max.y-openTop,openMax-openMin),storageRenderer.sharedMaterial);
            CreateWallPiece(root,"Storage Exterior Landing",
                new Vector3(room.max.x+.70f,room.min.y-.05f,(openMin+openMax)*.5f),
                new Vector3(1.40f,.10f,openMax-openMin+.30f),storageRenderer.sharedMaterial);
            Debug.Log($"[StorageOpening] derived mesh saved, opening z={openMin:F2}..{openMax:F2} top={openTop:F2}");
        }
        private static void CreateWallPiece(Transform parent,string name,Vector3 position,Vector3 size,Material material)
        {
            var piece=GameObject.CreatePrimitive(PrimitiveType.Cube);
            piece.name=name;
            piece.transform.SetParent(parent,true);
            piece.transform.position=position;
            piece.transform.rotation=Quaternion.identity;
            piece.transform.localScale=size;
            piece.GetComponent<MeshRenderer>().sharedMaterial=material;
        }
        private static void MoveRoom(Transform t,Vector3 from,Vector3 to,float yaw)
        {
            Quaternion q=Quaternion.Euler(0,yaw,0);
            t.SetPositionAndRotation(to+q*(t.position-from),q*t.rotation);
        }
        private static void AlignDoor(Transform gray,Transform art,string prefix,string marker)
        {
            var pivot=gray.Find(prefix+" Pivot");
            var leaf=pivot.GetComponentsInChildren<BoxCollider>(true).First(c=>c.name.EndsWith(" Leaf"));
            Vector3 delta=art.Find(marker).GetComponent<Renderer>().bounds.center-leaf.bounds.center;
            delta.y=0;
            foreach(Transform t in gray)
                if(t.name.StartsWith(prefix)) t.position+=delta;
            Debug.Log($"[DoorAligned] {prefix} delta={delta:F3}");
        }
        private static void InspectPassages(Transform art)
        {
            foreach(var door in UnityEngine.Object.FindObjectsByType<HingedDoor>(FindObjectsSortMode.None))
            {
                var leaf=door.GetComponentsInChildren<BoxCollider>().First(c=>c.name.EndsWith(" Leaf"));
                var own=new HashSet<Collider>(door.GetComponentsInChildren<Collider>());
                Vector3 p=leaf.bounds.center; p.y=.23f;
                var hits=Physics.OverlapCapsule(p+Vector3.up*.4f,p+Vector3.up*1.5f,.29f,~0,QueryTriggerInteraction.Ignore)
                    .Where(c=>!own.Contains(c)).ToArray();
                Debug.Log($"[CandidatePassage] {door.name} center={p:F3} blockers={string.Join(",",hits.Select(c=>c.name))}");
            }
        }
        private static void TestCharacterPassages()
        {
            var probe = new GameObject("Temporary movement verification");
            var cc=probe.AddComponent<CharacterController>();
            cc.radius=.32f; cc.height=1.8f; cc.center=Vector3.up*.9f;
            cc.stepOffset=.3f; cc.skinWidth=.08f;
            var failed=new List<string>();
            try
            {
                foreach(var door in UnityEngine.Object.FindObjectsByType<HingedDoor>(FindObjectsSortMode.None))
                {
                    var leaf=door.GetComponentsInChildren<BoxCollider>().First(c=>c.name.EndsWith(" Leaf"));
                    Vector3 center=leaf.bounds.center; center.y=.03f;
                    Vector3 axis=leaf.bounds.size.x<leaf.bounds.size.z?Vector3.right:Vector3.forward;
                    Quaternion closed=door.transform.localRotation;
                    try
                    {
                        for(int side=-1;side<=1;side+=2)
                        {
                            float best=0;
                            foreach(float angle in new[]{90f,-90f})
                            {
                                door.transform.localRotation=closed*Quaternion.Euler(0,angle,0);
                                cc.enabled=false; probe.transform.position=center+axis*side*.60f; cc.enabled=true;
                                Physics.SyncTransforms();
                                Vector3 start=probe.transform.position;
                                for(int i=0;i<45;i++) cc.Move(-axis*side*.04f+Vector3.down*.03f);
                                best=Mathf.Max(best,Vector3.Dot(probe.transform.position-start,-axis*side));
                            }
                            bool passed=best>.75f;
                            Debug.Log($"[CharacterPassage] {door.name} side={side} distance={best:F3} passed={passed}");
                            if(!passed)
                            {
                                failed.Add(door.name+" side="+side);
                                Vector3 p=probe.transform.position-axis*side*.1f;
                                Debug.Log($"[BlockedDetail] {door.name} foot={probe.transform.position:F3} near={string.Join(",",Physics.OverlapCapsule(p+Vector3.up*.32f,p+Vector3.up*1.48f,.32f).Where(c=>c!=cc).Select(c=>c.name))}");
                            }
                        }
                    }
                    finally {door.transform.localRotation=closed; Physics.SyncTransforms();}
                }
            }
            finally {UnityEngine.Object.DestroyImmediate(probe);}
            if(failed.Count>0) throw new InvalidOperationException("Character passage failures: "+string.Join(", ",failed));
        }
        public static void InspectBatch()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
            foreach (var root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
                Dump(root.transform, 0);
        }
        private static void Dump(Transform t, int depth)
        {
            if (depth <= 2) Debug.Log($"[Layout] {t.parent?.name}/{t.name} pos={t.position:F3} rot={t.eulerAngles:F1} components={string.Join(",",t.GetComponents<Component>().Where(c=>c!=null).Select(c=>c.GetType().Name))}");
            if (t.name == "Final Map Visuals (Visual Only)")
                foreach(var r in t.GetComponentsInChildren<Renderer>(true)) Debug.Log($"[FBXLayout] {r.name} enabled={r.enabled} center={r.bounds.center:F3} size={r.bounds.size:F3}");
            foreach(Transform child in t) Dump(child, depth+1);
        }
    }
}
