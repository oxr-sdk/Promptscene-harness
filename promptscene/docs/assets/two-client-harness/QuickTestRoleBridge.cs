#if UNITY_EDITOR
using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace PromptScene.Harness
{
    /// <summary>
    /// 2클라(A=host / B=client) 역할 자동 분기 브리지 — PromptScene 소유, shipped 무수정.
    ///
    /// 왜 필요한가:
    ///   ParrelSync 클론은 Assets/ProjectSettings 를 심링크로 "공유"한다.
    ///   QuickTest 의 startAsServer/hostMode 값은 코드가 아니라 씬 에셋
    ///   (Assets/App/Scenes/QuickStart.unity 의 Starter)에 저장되므로
    ///   A 와 B 가 같은 값을 읽는다 → 둘 다 host 가 되어 2인 검증이 불가능하다.
    ///   따라서 역할은 반드시 "런타임 분기"여야 한다.
    ///
    /// 어떻게:
    ///   ParrelSync.ClonesManager 는 Editor 어셈블리라 App.HotUpdate 에서 참조할 수 없다.
    ///   이 훅은 Assembly-CSharp-Editor 에 살면서(폴더명 Editor, asmdef 없음)
    ///   [InitializeOnLoad] 로 역할을 판정해 SessionState 에 적어두고,
    ///   [RuntimeInitializeOnLoadMethod(AfterSceneLoad)] 로 플레이 진입 직후
    ///   (모든 Awake 후 / 모든 Start 전) shipped QuickTestStarter 의 private 필드를
    ///   리플렉션으로 덮어쓴다. shipped 스크립트도 shipped 씬도 수정하지 않는다.
    ///
    /// fail-closed:
    ///   ParrelSync 부재 / IsClone()==false / QuickTestStarter 부재 중 어느 것이든
    ///   → 아무것도 하지 않는다 = 원본은 지금까지와 완전히 동일하게 동작한다.
    /// </summary>
    [InitializeOnLoad]
    internal static class QuickTestRoleBridge
    {
        internal const string SessionKey = "PromptScene.Harness.QuickTestRole";
        /// <summary>훅이 "실제로 돌았는지"를 로그와 무관하게 증명하는 마커(원본은 MCP로 읽는다).</summary>
        internal const string ApplyKey   = "PromptScene.Harness.ApplyMarker";
        internal const string RoleUnset  = "";        // 원본 = shipped 동작 그대로
        internal const string RoleClient = "client";  // 클론 = FishNet 클라이언트

        internal const string LogTag = "[PromptSceneHarness]";

        static QuickTestRoleBridge()
        {
            string role = DetectRole(out string how);
            SessionState.SetString(SessionKey, role);
            Debug.Log($"{LogTag} role-detect role='{(role == RoleUnset ? "unset(original)" : role)}' via {how}");
        }

        /// <summary>ParrelSync 를 리플렉션으로만 만진다(미설치여도 컴파일·동작 유지).</summary>
        private static string DetectRole(out string how)
        {
            Type cm = FindType("ParrelSync.ClonesManager");
            if (cm == null) { how = "ParrelSync-absent"; return RoleUnset; }

            MethodInfo isClone = cm.GetMethod("IsClone", BindingFlags.Public | BindingFlags.Static);
            if (isClone == null) { how = "IsClone-missing"; return RoleUnset; }

            try
            {
                bool clone = (bool)isClone.Invoke(null, null);
                how = "ClonesManager.IsClone()=" + clone;
                return clone ? RoleClient : RoleUnset;
            }
            catch (Exception e)
            {
                how = "IsClone-threw:" + e.GetType().Name;
                return RoleUnset;
            }
        }

        /// <summary>
        /// AfterSceneLoad = 첫 씬의 모든 Awake 이후, 모든 Start 이전.
        /// QuickTestStarter.Start() 는 코루틴이고 필드를 읽기 전에 최대 10초 대기하므로
        /// 여유는 충분하다.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void ApplyRole()
        {
            string role = SessionState.GetString(SessionKey, RoleUnset);

            if (role != RoleClient)
            {
                Report("role=unset(original) applied=False (shipped values untouched)");
                return;
            }

            var starter = UnityEngine.Object.FindFirstObjectByType(FindType("QuickTestStarter") ?? typeof(MonoBehaviour));
            if (starter == null || starter.GetType().Name != "QuickTestStarter")
            {
                Report("role=client applied=False reason=QuickTestStarter-not-found");
                return;
            }

            bool okServer = SetPrivateBool(starter, "startAsServer", false, out bool wasServer);
            bool okHost   = SetPrivateBool(starter, "hostMode",      false, out bool wasHost);

            Report($"role=client applied={(okServer && okHost)} " +
                   $"startAsServer:{wasServer}->false hostMode:{wasHost}->false " +
                   "(shipped scene asset NOT modified - runtime-only override)");
        }

        /// <summary>A는 SessionState(MCP)로, B는 -logFile 의 stdout 로 같은 사실을 읽는다.</summary>
        private static void Report(string detail)
        {
            SessionState.SetString(ApplyKey, detail);
            Debug.Log($"{LogTag} apply {detail}");
        }

        private static bool SetPrivateBool(object target, string field, bool value, out bool previous)
        {
            previous = false;
            FieldInfo f = target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic);
            if (f == null || f.FieldType != typeof(bool)) return false;
            previous = (bool)f.GetValue(target);
            f.SetValue(target, value);
            return true;
        }

        private static Type FindType(string fullName)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type t;
                try { t = asm.GetType(fullName, false); }
                catch { continue; }
                if (t != null) return t;
            }
            return null;
        }
    }
}
#endif
