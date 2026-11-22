using System;
using System.IO;
using System.Linq;
using DeadLetter.Endings;
using DeadLetter.Interaction;
using DeadLetter.Mail;
using DeadLetter.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DeadLetter.Editor
{
    // Ending validation and scene installation utilities.
    [InitializeOnLoad]
    public static class EndingSetup
    {
        static EndingSetup() { EditorApplication.update += Poll; }
        private static void Poll()
        {
            const string request = "Temp/DeadLetterEndingCommand.txt";
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || !File.Exists(request)) return;
            string command;
            try { command = File.ReadAllText(request).Trim(); File.Delete(request); }
            catch (IOException) { return; }
            try
            {
                if (command == "inspect") Inspect();
                else if (command == "setup") Install();
                else if (command == "validate") Validate();
                else if (command == "enter-play")
                {
                    var gameScene=AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/Scenes/SampleScene.unity");
                    if(gameScene==null)throw new InvalidOperationException("SampleScene is missing.");
                    EditorSceneManager.playModeStartScene=gameScene;
                    EditorApplication.delayCall+=()=>EditorApplication.isPlaying=true;
                }
                else if (command == "load-game" && EditorApplication.isPlaying) UnityEngine.SceneManagement.SceneManager.LoadScene("SampleScene");
                else if (command == "normal-preview") EndingPlayVerifier.NormalPreview();
                else if (command == "repeat-normal-preview") EndingPlayVerifier.NormalPreview(true);
                else if (command == "true-preview") EndingPlayVerifier.TruePreview();
                else if (command == "repeat-true-preview") EndingPlayVerifier.TruePreview(true);
                else if (command == "letter-preview") EndingPlayVerifier.LetterPreview();
                else if (command == "play-status") EndingPlayVerifier.Status();
                else if (command == "scene-validate") ValidateScene();
                else if (command == "hold-escape") EndingPlayVerifier.HoldEscape();
                else if (command == "build") BuildValidationPlayer();
                else if (command == "stop-preview") { EndingPlayVerifier.RestoreRecords(); EditorApplication.isPlaying=false; }
            }
            catch (Exception e) { File.WriteAllText("Temp/DeadLetterEndingResult.txt",e.ToString()); Debug.LogException(e); }
        }
        [MenuItem("Tools/DEAD LETTER/Endings/Inspect Scene References")]
        public static void Inspect()
        {
            var objects=UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include);
            var lines=objects.Where(t=>t.name.Contains("액자") || t.name.Contains("Frame") || t.name.Contains("Hidden Room Floor") || t.name.Contains("Counter Floor") || t.name.Contains("Counter Room") || t.name.Contains("Hidden Room Door Pivot"))
                .Select(t=> $"{PathOf(t)} pos={t.position:F4} rot={t.eulerAngles:F2} scale={t.lossyScale:F4} " + (t.TryGetComponent<Renderer>(out var r) ? $"bounds={r.bounds} enabled={r.enabled}" : ""));
            File.WriteAllLines("Temp/DeadLetterEndingResult.txt",lines);
        }
        public static string PathOf(Transform t) => t.parent==null ? t.name : PathOf(t.parent)+"/"+t.name;
        [MenuItem("Tools/DEAD LETTER/Endings/Install in Current Scene")]
        public static void Install()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode first.");
            var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if(scene.path!="Assets/Scenes/SampleScene.unity") throw new InvalidOperationException("Install only in SampleScene.");
            if(GameObject.Find("Ending Flow")!=null) throw new InvalidOperationException("Ending Flow already exists; preserve authored references.");
            var frameObject=GameObject.Find("DEAD LETTER - Graybox/Production Room Art/Counter Production Art/창구 Art Content/Size - 창구-액자/창구-액자");
            var door=GameObject.Find("DEAD LETTER - Graybox/Hidden Room Door Pivot");
            var floor=GameObject.Find("DEAD LETTER - Graybox/Hidden Room Floor");
            if(frameObject==null || door==null || floor==null) throw new InvalidOperationException("An inspected reference is missing. Do not guess replacements.");
            Directory.CreateDirectory("tmp/ending-backup");
            string backup="tmp/ending-backup/SampleScene-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".unity";
            File.Copy(scene.path,backup);
            var root=new GameObject("Ending Flow"); Undo.RegisterCreatedObjectUndo(root,"Install endings");
            var flow=root.AddComponent<EndingFlowController>();
            var marksObject=new GameObject("Hidden tally marks",typeof(MeshFilter),typeof(MeshRenderer),typeof(EndingTallyMarks));
            marksObject.transform.SetParent(root.transform);
            Bounds frameBounds=frameObject.GetComponent<Renderer>().bounds;
            Vector3 normal=Vector3.left; // Inspected existing frame is on Counter's east wall (x=4.45).
            marksObject.transform.SetPositionAndRotation(frameBounds.center + normal*.025f,Quaternion.LookRotation(normal,Vector3.up));
            marksObject.transform.localScale=new Vector3(frameBounds.size.z*.85f,frameBounds.size.y*.85f,1);
            Material chalk=Material("Ending Chalk",new Color(.82f,.81f,.74f),true);
            Material paper=Material("Ending White Letter",new Color(.93f,.91f,.83f),false);
            Material pencil=Material("Ending Scratch Tool",new Color(.15f,.14f,.12f),false);
            marksObject.GetComponent<MeshRenderer>().sharedMaterial=chalk;
            marksObject.GetComponent<MeshRenderer>().enabled=false;
            var landing=new GameObject("Frame Landing"); landing.transform.SetParent(root.transform);
            // Transform origin is displaced in the imported mesh. Translate by the bounds' floor clearance.
            landing.transform.position=new Vector3(frameBounds.center.x,.13f,frameBounds.center.z)+normal*1.05f;
            var interaction=Undo.AddComponent<EndingInteractable>(frameObject); interaction.Configure(flow,false);
            var collider=frameObject.GetComponent<BoxCollider>();
            if(collider==null) collider=Undo.AddComponent<BoxCollider>(frameObject);
            Bounds localBounds=frameObject.GetComponent<MeshFilter>().sharedMesh.bounds;
            collider.center=localBounds.center; collider.size=localBounds.size;
            var letter=new GameObject("Ending White Letter",typeof(BoxCollider)); letter.transform.SetParent(root.transform);
            Bounds floorBounds=floor.GetComponent<Renderer>().bounds;
            Vector3 position=new Vector3(floorBounds.max.x-.9f,floorBounds.max.y+.025f,floorBounds.center.z-.8f);
            letter.transform.position=position;
            letter.GetComponent<BoxCollider>().size=new Vector3(.32f,.025f,.22f);
            var body=GameObject.CreatePrimitive(PrimitiveType.Cube); body.name="Envelope body (placeholder)"; body.transform.SetParent(letter.transform,false);
            body.transform.localScale=new Vector3(.32f,.025f,.22f); body.GetComponent<Renderer>().sharedMaterial=paper;
            UnityEngine.Object.DestroyImmediate(body.GetComponent<Collider>());
            letter.AddComponent<EndingInteractable>().Configure(flow,true);
            var flap=new GameObject("Flap"); flap.transform.SetParent(letter.transform,false); flap.transform.localPosition=new Vector3(0,.014f,.11f);
            var lid=GameObject.CreatePrimitive(PrimitiveType.Cube); lid.name="Envelope lid (placeholder)"; lid.transform.SetParent(flap.transform,false);
            lid.transform.localPosition=new Vector3(0,0,-.055f); lid.transform.localScale=new Vector3(.32f,.004f,.11f);
            lid.GetComponent<Renderer>().sharedMaterial=paper; UnityEngine.Object.DestroyImmediate(lid.GetComponent<Collider>());
            var serialized=new SerializedObject(flow);
            void Set(string key,UnityEngine.Object value) { if(value==null)throw new InvalidOperationException("Missing ending reference: "+key); serialized.FindProperty(key).objectReferenceValue=value; }
            Set("dayCycle",UnityEngine.Object.FindAnyObjectByType<DayCycleManager>());
            Set("tasks",UnityEngine.Object.FindAnyObjectByType<LetterTaskState>());
            Set("customer",UnityEngine.Object.FindAnyObjectByType<TestCustomerNPC>(FindObjectsInactive.Include));
            var player=UnityEngine.Object.FindAnyObjectByType<FirstPersonController>(); Set("player",player);
            Set("interactor",UnityEngine.Object.FindAnyObjectByType<FirstPersonInteractor>());
            Set("view",player.GetComponentInChildren<Camera>());
            Set("hiddenDoor",door.GetComponent<HingedDoor>()); Set("frame",frameObject.transform);
            Set("marks",marksObject.GetComponent<EndingTallyMarks>()); Set("whiteLetter",letter.transform); Set("pencilMaterial",pencil);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.RecordPrefabInstancePropertyModifications(frameObject);
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
            File.WriteAllText("Temp/DeadLetterEndingResult.txt","Installed endings; scene backup: "+backup+"\nFrame: "+PathOf(frameObject.transform)+"\nWhite letter: "+position);
        }
        private static Material Material(string name,Color color,bool unlit)
        {
            Directory.CreateDirectory("Assets/Art/Materials/Endings"); AssetDatabase.Refresh();
            string path="Assets/Art/Materials/Endings/"+name+".mat";
            var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(material!=null)return material;
            material=new Material(Shader.Find(unlit ? "Universal Render Pipeline/Unlit" : "Universal Render Pipeline/Lit"));
            material.SetColor("_BaseColor",color); material.SetFloat("_Smoothness",.1f);
            AssetDatabase.CreateAsset(material,path); return material;
        }
        [MenuItem("Tools/DEAD LETTER/Endings/Build Validation Player")]
        public static void BuildValidationPlayer()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Exit Play Mode first");
            Directory.CreateDirectory("tmp/ending-validation-build");
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes=EditorBuildSettings.scenes.Where(s=>s.enabled).Select(s=>s.path).ToArray(),
                locationPathName="tmp/ending-validation-build/DeadLetter.exe",target=BuildTarget.StandaloneWindows64,options=BuildOptions.None });
            File.WriteAllText("tmp/ending-build-result.txt",$"{report.summary.result}; errors={report.summary.totalErrors}; warnings={report.summary.totalWarnings}; duration={report.summary.totalTime}");
        }
        [MenuItem("Tools/DEAD LETTER/Endings/Validate")]
        public static void Validate()
        {
            int checks=0;
            void Require(bool value,string message) { if(!value)throw new Exception(message); checks++; }
            Require(!EndingStateController.NormalReady(2,5,5,false,false),"DAY2 must not trigger ending");
            Require(!EndingStateController.NormalReady(3,4,5,false,false),"unfinished work");
            Require(!EndingStateController.NormalReady(3,5,5,true,false),"customer still present");
            Require(!EndingStateController.NormalReady(3,5,5,false,true),"dialogue still active");
            Require(EndingStateController.NormalReady(3,5,5,false,false),"ready normal ending");
            var root=new GameObject("Ending validation (temporary)");
            var state=root.AddComponent<EndingStateController>();
            var mesh=new Mesh();
            try
            {
                Require(!state.Discover(false),"unseen normal must lock frame");
                Require(!state.Begin(EndingKind.True,false,false),"true before discovery");
                Require(state.Discover(true),"discovery");
                Require(!state.Discover(true),"duplicate discovery");
                Require(state.Begin(EndingKind.True,false,false) && !state.CanSkip,"first true cannot skip");
                Require(!state.Begin(EndingKind.True,false,true),"duplicate ending");
                state.Finish();
                Require(!state.Begin(EndingKind.True,false,true),"opened letter cannot repeat");
                Require(state.Begin(EndingKind.Normal,true,true) && state.CanSkip,"repeat normal can skip independently");
                EndingTallyMarks.Build(mesh,48,0); int incomplete=mesh.vertexCount;
                EndingTallyMarks.Build(mesh,48,1);
                Require(mesh.vertexCount==incomplete+16,"one diagonal consists of four rough segments");
                Require(mesh.vertexCount==48*5*4*4,"four verticals and diagonal per glyph");
                EndingTallyMarks.Build(mesh,240,1); Require(mesh.vertexCount==240*5*4*4,"hundreds in one mesh");
                var replayRoot=new GameObject("Ending replay validation (temporary)");
                var replayState=replayRoot.AddComponent<EndingStateController>();
                Require(replayState.Discover(true),"repeat true discovery");
                Require(replayState.Begin(EndingKind.True,false,true) && replayState.CanSkip,"repeat true can skip independently");
                UnityEngine.Object.DestroyImmediate(replayRoot);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); UnityEngine.Object.DestroyImmediate(mesh); }
            File.WriteAllText("Temp/DeadLetterEndingResult.txt",$"PASS {checks} ending state and mesh checks. Persistent player records untouched.");
            CustomerDialogueValidator.ValidateAll();
        }
        [MenuItem("Tools/DEAD LETTER/Endings/Validate Scene Connections")]
        public static void ValidateScene()
        {
            var flow=UnityEngine.Object.FindAnyObjectByType<EndingFlowController>();
            if(flow==null)throw new Exception("Ending Flow missing");
            var serialized=new SerializedObject(flow);
            foreach(string key in new[]{"dayCycle","tasks","customer","player","interactor","view","hiddenDoor","frame","marks","whiteLetter","pencilMaterial"})
                if(serialized.FindProperty(key).objectReferenceValue==null)throw new Exception("Missing "+key);
            var frame=(Transform)serialized.FindProperty("frame").objectReferenceValue;
            var letter=(Transform)serialized.FindProperty("whiteLetter").objectReferenceValue;
            var interactor=(FirstPersonInteractor)serialized.FindProperty("interactor").objectReferenceValue;
            if(Mathf.Abs(interactor.InteractionDistance-3)>.001f)throw new Exception("Interaction distance changed");
            Physics.SyncTransforms();
            var frameCollider=frame.GetComponent<BoxCollider>();
            Vector3 center=frameCollider.bounds.center;
            if(!Physics.Raycast(center+Vector3.left*1.5f,Vector3.right,out RaycastHit frameHit,3,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore) || frameHit.collider!=frameCollider)
                throw new Exception("Frame ray blocked by "+(frameHit.collider!=null?PathOf(frameHit.transform):"no hit"));
            var letterCollider=letter.GetComponent<BoxCollider>();
            if(!Physics.Raycast(letter.position+Vector3.up*1.6f,Vector3.down,out RaycastHit letterHit,3,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore) || letterHit.collider!=letterCollider)
                throw new Exception("Letter ray blocked by "+(letterHit.collider!=null?PathOf(letterHit.transform):"no hit"));
            File.WriteAllText("Temp/DeadLetterEndingResult.txt","PASS 11 scene references; 3m interactor unchanged; frame and floor letter physics rays hit their own colliders.\n"+"Frame collider bounds "+frameCollider.bounds+"\nLetter collider bounds "+letterCollider.bounds);
        }
    }
}
