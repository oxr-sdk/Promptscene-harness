#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using FishNet;
using FishNet.Object;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PromptScene.Harness
{
    /// <summary>
    /// 클론(B) 전용 하네스 — 원본(A)에서는 완전히 불활성.
    ///
    /// B 는 MCP 가 없다(§4: MCP 는 원본 A 에만). 그래서 B 와의 통신은 파일·로그로 한다:
    ///   · 판정 채널(B→나): Debug.Log → Unity 의 -logFile
    ///   · 명령 채널(나→B): 프로젝트 루트의 .promptscene-cmd 파일(한 줄 = 한 명령, 실행 후 삭제)
    ///   · 자동 Play : 프로젝트 루트의 .promptscene-autoplay 파일이 있을 때만
    ///
    /// 두 파일 모두 프로젝트 "루트"에 둔다 — Assets/ 는 A 와 심링크로 공유되므로
    /// Assets 안에 두면 A 까지 무장(arm)돼 버린다. 루트는 클론마다 독립이다.
    ///
    /// fail-closed 2중: 역할이 client 가 아니거나(=원본) 파일이 없으면 아무 일도 하지 않는다.
    /// </summary>
    [InitializeOnLoad]
    internal static class CloneHarness
    {
        private const string ArmFileName = ".promptscene-autoplay";
        private const string CmdFileName = ".promptscene-cmd";
        private const string LogTag      = QuickTestRoleBridge.LogTag;

        private static double _nextProbe;
        private static double _nextCmdPoll;
        private static bool   _playRequested;

        private static string ProjectRoot => Path.GetDirectoryName(Application.dataPath);

        private static bool IsCloneRole =>
            SessionState.GetString(QuickTestRoleBridge.SessionKey, QuickTestRoleBridge.RoleUnset)
                == QuickTestRoleBridge.RoleClient;

        static CloneHarness()
        {
            if (!IsCloneRole) return;   // 원본: 구독조차 하지 않는다
            EditorApplication.update += Tick;
            Debug.Log(LogTag + " clone-harness armed root=" + ProjectRoot +
                      " autoplay=" + File.Exists(Path.Combine(ProjectRoot, ArmFileName)));
        }

        private static void Tick()
        {
            double now = EditorApplication.timeSinceStartup;

            if (!EditorApplication.isPlaying)
            {
                TryAutoPlay(now);
                return;
            }

            _playRequested = false;

            if (now >= _nextCmdPoll) { _nextCmdPoll = now + 0.5; PollCommand(); }
            if (now >= _nextProbe)   { _nextProbe   = now + 1.0; Probe("tick"); }
        }

        // 자동 Play
        private static void TryAutoPlay(double now)
        {
            if (_playRequested) return;
            if (!File.Exists(Path.Combine(ProjectRoot, ArmFileName))) return;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (now < 20.0) return;   // 콜드 오픈 임포트가 가라앉을 시간

            // QuickTest 모드의 EditorPrefs 는 프로젝트 경로별이라 클론엔 없다 →
            // playModeStartScene 을 여기서 직접 지정한다(에디터 메모리 전용, 공유 자산 무수정).
            var scene = AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/App/Scenes/QuickStart.unity");
            if (scene != null) EditorSceneManager.playModeStartScene = scene;

            _playRequested = true;
            Debug.Log(LogTag + " clone auto-play ENTER (armed) startScene=" + (scene != null ? "QuickStart" : "<none>"));
            EditorApplication.isPlaying = true;
        }

        // 명령 채널
        private static void PollCommand()
        {
            string path = Path.Combine(ProjectRoot, CmdFileName);
            if (!File.Exists(path)) return;

            string line;
            try { line = File.ReadAllText(path, Encoding.UTF8).Trim(); }
            catch { return; }                       // 쓰는 중 — 다음 폴에서 다시
            try { File.Delete(path); } catch { }

            if (string.IsNullOrEmpty(line)) return;
            Debug.Log(LogTag + " cmd-recv " + line);

            string[] a = line.Split(new[] { ' ' }, 2);
            string verb = a[0].ToLowerInvariant();
            string rest = a.Length > 1 ? a[1] : "";

            try
            {
                switch (verb)
                {
                    case "probe": Probe("cmd"); break;
                    case "chat":  Chat(rest); break;
                    case "move":  Move(rest); break;
                    case "stop":  Debug.Log(LogTag + " cmd stop"); EditorApplication.isPlaying = false; break;
                    case "quit":  Debug.Log(LogTag + " cmd quit"); EditorApplication.Exit(0); break;
                    default:      Debug.LogWarning(LogTag + " cmd-unknown " + verb); break;
                }
            }
            catch (Exception e)
            {
                Debug.LogError(LogTag + " cmd-threw " + verb + ": " + e.GetType().Name + " " + e.Message);
            }
        }

        private static void Chat(string text)
        {
            Type viewType = FindType("ChatChannelView");
            if (viewType == null) { Debug.LogWarning(LogTag + " chat FAIL: ChatChannelView type not loaded"); return; }

            var view = UnityEngine.Object.FindFirstObjectByType(viewType);
            if (view == null) { Debug.LogWarning(LogTag + " chat FAIL: no ChatChannelView instance in scene"); return; }

            MethodInfo send = viewType.GetMethod("Send", BindingFlags.Public | BindingFlags.Instance);
            if (send == null) { Debug.LogWarning(LogTag + " chat FAIL: Send(string) missing"); return; }

            send.Invoke(view, new object[] { text });
            Debug.Log(LogTag + " chat SENT " + text);
        }

        private static void Move(string args)
        {
            var p = args.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (p.Length < 3) { Debug.LogWarning(LogTag + " move FAIL: need 'move dx dy dz'"); return; }

            var own = OwnedObjects().FirstOrDefault();
            if (own == null) { Debug.LogWarning(LogTag + " move FAIL: no owned NetworkObject"); return; }

            Vector3 d = new Vector3(F(p[0]), F(p[1]), F(p[2]));
            Vector3 before = own.transform.position;
            own.transform.position = before + d;
            Debug.Log(LogTag + " move id=" + own.ObjectId + " " + V(before) + " -> " + V(own.transform.position));
        }

        // 판정 채널
        private static void Probe(string why)
        {
            var nm = InstanceFinder.NetworkManager;
            var sb = new StringBuilder();
            sb.Append(LogTag + " probe[" + why + "] ");
            sb.Append("srv=" + (nm != null ? nm.IsServerStarted.ToString() : "?") + " ");
            sb.Append("cli=" + (nm != null ? nm.IsClientStarted.ToString() : "?") + " ");
            sb.Append("cid=" + (nm != null && nm.ClientManager.Connection.IsValid
                                    ? nm.ClientManager.Connection.ClientId : -1) + " ");
            sb.Append("scenes=[" + string.Join(",", Enumerable
                .Range(0, UnityEngine.SceneManagement.SceneManager.sceneCount)
                .Select(i => UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).name)) + "] ");

            var objs = UnityEngine.Object.FindObjectsByType<NetworkObject>(FindObjectsSortMode.None)
                                         .OrderBy(o => o.ObjectId).ToArray();
            sb.Append("nobs=" + objs.Length + " ");
            foreach (var o in objs)
            {
                sb.Append("| id=" + o.ObjectId + " " + o.name +
                          " owner=" + o.IsOwner +
                          " ownerCid=" + (o.Owner != null && o.Owner.IsValid ? o.Owner.ClientId : -1) +
                          " pos=" + V(o.transform.position) + " ");
            }

            sb.Append("| chatLog=" + ChatLogSnapshot());
            Debug.Log(sb.ToString());
        }

        private static string ChatLogSnapshot()
        {
            Type t = FindType("ChatChannelView");
            FieldInfo logField = t == null ? null : t.GetField("Log", BindingFlags.Public | BindingFlags.Static);
            if (logField == null) return "<n/a>";

            var log = logField.GetValue(null) as System.Collections.IEnumerable;
            if (log == null) return "<null>";

            var parts = new List<string>();
            foreach (var m in log)
            {
                Type mt = m.GetType();
                var sidF = mt.GetField("SenderId");
                var txtF = mt.GetField("Text");
                parts.Add((sidF == null ? "?" : sidF.GetValue(m).ToString()) + ":" +
                          (txtF == null ? "?" : txtF.GetValue(m) as string));
            }
            return parts.Count + "[" + string.Join(" / ", parts) + "]";
        }

        // 유틸
        private static IEnumerable<NetworkObject> OwnedObjects()
        {
            return UnityEngine.Object.FindObjectsByType<NetworkObject>(FindObjectsSortMode.None)
                                     .Where(o => o.IsOwner);
        }

        private static string V(Vector3 v)
        {
            return v.x.ToString("F2", CultureInfo.InvariantCulture) + "," +
                   v.y.ToString("F2", CultureInfo.InvariantCulture) + "," +
                   v.z.ToString("F2", CultureInfo.InvariantCulture);
        }

        private static float F(string s)
        {
            float f;
            return float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out f) ? f : 0f;
        }

        private static Type FindType(string name)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types;
                try { types = asm.GetTypes(); } catch { continue; }
                foreach (var t in types) if (t.Name == name) return t;
            }
            return null;
        }
    }
}
#endif
