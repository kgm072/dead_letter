using System;
using System.IO;
using System.Linq;
using DeadLetter.Endings;
using DeadLetter.Interaction;
using DeadLetter.Mail;
using DeadLetter.Player;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace DeadLetter.Editor
{
    // Editor-only preview. Saves/restores the user's persistent ending records, including absent keys.
    [InitializeOnLoad]
    public static class EndingPlayVerifier
    {
        private const string Snapshot="DeadLetter.EndingPreview.";
        private static double holdUntil;
        private static string capturedStage;
        static EndingPlayVerifier()
        {
            EditorApplication.playModeStateChanged += mode=> { if(mode==PlayModeStateChange.EnteredEditMode) RestoreRecords(); };
            EditorApplication.update += ()=> { if(EditorApplication.isPlaying && SceneManager.GetActiveScene().name=="MainMenu") RestoreRecords(); };
            EditorApplication.update += ()=> {
                if(EditorApplication.isPlaying && SessionState.GetBool(Snapshot+"active",false))
                {
                    var flow=UnityEngine.Object.FindAnyObjectByType<EndingFlowController>();
                    if(flow!=null && flow.SequenceStage!=capturedStage && (flow.SequenceStage.EndsWith("ending card") || flow.SequenceStage=="Final diagonal"))
                    {
                        capturedStage=flow.SequenceStage;
                        ScreenCapture.CaptureScreenshot("tmp/ending-"+capturedStage.Replace(' ','-')+".png");
                    }
                }
                if(holdUntil<=0 || Keyboard.current==null)return;
                bool holding=EditorApplication.isPlaying && EditorApplication.timeSinceStartup<holdUntil;
                InputSystem.QueueStateEvent(Keyboard.current,holding ? new KeyboardState(Key.Escape) : new KeyboardState());
                if(!holding)holdUntil=0;
            };
        }
        private static void BackupRecords()
        {
            if(SessionState.GetBool(Snapshot+"active",false))return;
            foreach(string key in new[]{EndingRecords.NormalKey,EndingRecords.TrueKey})
            { SessionState.SetBool(Snapshot+key+"exists",PlayerPrefs.HasKey(key)); SessionState.SetInt(Snapshot+key,PlayerPrefs.GetInt(key)); }
            SessionState.SetBool(Snapshot+"active",true);
        }
        public static void RestoreRecords()
        {
            if(!SessionState.GetBool(Snapshot+"active",false))return;
            File.AppendAllText("tmp/ending-play-results.txt",$"Before test restore: scene={SceneManager.GetActiveScene().name}, normalSeen={EndingRecords.HasSeen(EndingKind.Normal)}, trueSeen={EndingRecords.HasSeen(EndingKind.True)}\n");
            foreach(string key in new[]{EndingRecords.NormalKey,EndingRecords.TrueKey})
                if(SessionState.GetBool(Snapshot+key+"exists",false)) PlayerPrefs.SetInt(key,SessionState.GetInt(Snapshot+key,0)); else PlayerPrefs.DeleteKey(key);
            PlayerPrefs.Save(); SessionState.SetBool(Snapshot+"active",false);
            File.AppendAllText("tmp/ending-play-results.txt","Persistent ending records restored. Scene="+SceneManager.GetActiveScene().name+"\n");
        }
        [MenuItem("Tools/DEAD LETTER/Endings/Preview Normal (Play Mode)")]
        public static void NormalPreviewMenu() => NormalPreview();
        public static void HoldEscape() { holdUntil=EditorApplication.timeSinceStartup+3; }
        public static void NormalPreview(bool repeat=false)
        {
            if(!Application.isPlaying)throw new Exception("Play Mode required");
            BackupRecords(); PlayerPrefs.SetInt(EndingRecords.NormalKey,repeat?1:0);
            var manager=UnityEngine.Object.FindAnyObjectByType<DayCycleManager>();
            var flow=UnityEngine.Object.FindAnyObjectByType<EndingFlowController>();
            var npc=UnityEngine.Object.FindAnyObjectByType<TestCustomerNPC>(FindObjectsInactive.Include);
            void Check(bool ok,string label) { if(!ok)throw new Exception("Preview prerequisite failed: "+label); }
            Check(!flow.TryStartNormal(),"normal blocked during DAY1");
            Check(manager.DebugStartDay(3),"DAY3 start");
            foreach(var request in manager.DayThreeOrder.ToArray())
            {
                var d=request.Definition;
                Check(manager.DebugSelectDayThreeRequest(d.RequestId),"select "+d.RequestId);
                Check(manager.DebugAcceptCurrentRequest(),"accept");
                Check(manager.DebugSecureAllCurrentItems(),"items"); Check(manager.DebugSecureCurrentBox(),"box");
                Check(manager.DebugPackCurrentRequest(),"pack"); Check(manager.DebugApplyCurrentBoxPictograms(),"stamps"); Check(manager.DebugProcessCurrentLetter(),"letter");
                if(d.RequestType==MailRequestType.Dispatch) { Check(manager.DebugDispatchCurrentRequest(),"dispatch"); Check(manager.DebugRecordCurrentCompletionNotice(),"notice"); }
                else Check(manager.DebugDeliverCurrentRequest(d.CustomerId),"deliver");
                Check(!flow.NormalReady,"still present before departure");
                npc.HideUntilNextCustomer(); manager.NotifyCustomerDeparted();
            }
            Check(flow.NormalReady,"normal ready after all departures");
            Check(!flow.IsBusy,"no automatic ending on last departure");
            var serialized=new SerializedObject(flow); serialized.FindProperty("cardSeconds").floatValue=18; serialized.FindProperty("scratchSeconds").floatValue=8; serialized.ApplyModifiedPropertiesWithoutUndo();
            UnityEngine.Object.FindAnyObjectByType<DaySleepSofa>().Interact(UnityEngine.Object.FindAnyObjectByType<FirstPersonController>().gameObject);
            Check(flow.IsBusy,"sofa triggered sequence");
            Check(flow.GetComponent<EndingStateController>().CanSkip==repeat,"per-ending viewing skip policy");
            File.AppendAllText("tmp/ending-play-results.txt",$"PASS normal prerequisite sequence using debug gameplay transitions; sofa starts; repeat={repeat}; skip allowed={flow.GetComponent<EndingStateController>().CanSkip}. Dialogue/balloon deliberately bypassed by preview.\n");
        }
        [MenuItem("Tools/DEAD LETTER/Endings/Preview Frame Discovery (Play Mode)")]
        public static void TruePreviewMenu() => TruePreview();
        public static void TruePreview(bool repeat=false)
        {
            if(!Application.isPlaying)throw new Exception("Play Mode required");
            BackupRecords();
            var flow=UnityEngine.Object.FindAnyObjectByType<EndingFlowController>();
            PlayerPrefs.SetInt(EndingRecords.NormalKey,0);
            if(flow.TryLowerFrame() || flow.TryStartTrue())throw new Exception("True ending unlocked before normal");
            PlayerPrefs.SetInt(EndingRecords.NormalKey,1); PlayerPrefs.SetInt(EndingRecords.TrueKey,repeat?1:0);
            var manager=UnityEngine.Object.FindAnyObjectByType<DayCycleManager>();
            if(manager==null || !manager.DebugStartDay(1))throw new Exception("Replay DAY1 setup failed");
            var npc=UnityEngine.Object.FindAnyObjectByType<TestCustomerNPC>(FindObjectsInactive.Include); npc.HideUntilNextCustomer();
            var serialized=new SerializedObject(flow);
            var marks=(EndingTallyMarks)serialized.FindProperty("marks").objectReferenceValue;
            var camera=(Camera)serialized.FindProperty("view").objectReferenceValue;
            camera.transform.position=marks.transform.position+marks.transform.forward*1.6f;
            camera.transform.rotation=Quaternion.LookRotation(-marks.transform.forward,Vector3.up);
            serialized.FindProperty("cardSeconds").floatValue=18; serialized.ApplyModifiedPropertiesWithoutUndo();
            if(!flow.TryLowerFrame())
            {
                var state=flow.GetComponent<EndingStateController>();
                var taskState=(LetterTaskState)serialized.FindProperty("tasks").objectReferenceValue;
                string missing=string.Join(",",new[]{"frame","marks","view","player","hiddenDoor","whiteLetter"}
                    .Where(key=>serialized.FindProperty(key).objectReferenceValue==null));
                throw new Exception($"Frame did not lower on replay DAY1: canLower={flow.CanLowerFrame}, day={taskState?.CurrentDay}, normalSeen={EndingRecords.HasSeen(EndingKind.Normal)}, busy={flow.IsBusy}, playing={state.Playing}, stage={flow.SequenceStage}, frameLowered={state.FrameLowered}, missing=[{missing}]");
            }
            File.AppendAllText("tmp/ending-play-results.txt","PASS first-run true locked; replay DAY1 frame interaction starts.\n");
        }
        [MenuItem("Tools/DEAD LETTER/Endings/Preview White Letter (Play Mode)")]
        public static void LetterPreview()
        {
            var flow=UnityEngine.Object.FindAnyObjectByType<EndingFlowController>();
            var serialized=new SerializedObject(flow);
            var door=(HingedDoor)serialized.FindProperty("hiddenDoor").objectReferenceValue;
            if(!flow.CanOpenLetter || door.IsLocked) throw new Exception("Discovery did not unlock hidden door and letter");
            var letter=(Transform)serialized.FindProperty("whiteLetter").objectReferenceValue;
            letter.GetComponent<EndingInteractable>().Interact(UnityEngine.Object.FindAnyObjectByType<FirstPersonController>().gameObject);
            if(!flow.IsBusy || flow.GetComponent<EndingStateController>().CanSkip!=EndingRecords.HasSeen(EndingKind.True))throw new Exception("True start / independent skip policy failed");
            if(flow.TryStartTrue())throw new Exception("duplicate true ending");
            File.AppendAllText("tmp/ending-play-results.txt",$"PASS discovered marks automatically unlock door; white letter starts true; duplicate rejected; canSkip={flow.GetComponent<EndingStateController>().CanSkip}.\n");
        }
        public static void Status()
        {
            var flow=UnityEngine.Object.FindAnyObjectByType<EndingFlowController>();
            File.WriteAllText("Temp/DeadLetterEndingResult.txt",flow==null ? "Scene="+SceneManager.GetActiveScene().name : $"Stage={flow.SequenceStage} Busy={flow.IsBusy} Ready={flow.NormalReady} Letter={flow.CanOpenLetter}");
        }
    }
}
