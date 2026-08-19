// multiplayer-check — A(원본=host) 쪽 2클라 신호 수집기. B(클론=client)는 MCP가 없으므로
// 클론 루트의 .promptscene-status 파일로 읽는다(하네스 CloneHarness.cs가 1초마다 덮어씀).
//
// 절차 SSOT: build-studio-room.md §6.6 (2클라 클론 절차) / xumflow-migration.md §17 (실측·트랩표).
// Run via MCP script-execute (className=PS_VerifyMultiplayer). 진행 순서:
//
//   0) MCP: scene-open Assets/App/Scenes/QuickStart.unity Single
//   1) script-execute PS_VerifyMultiplayer.Setup   — QuickTestStarter 스냅샷 + server+host+roomSceneKey=<ROOM>
//   2) script-execute (isPlaying=true) — A를 host로 진입. 12~15s 대기(서버→룸 로드→아바타 스폰)
//   3) (B 무장: 클론 루트에 .promptscene-autoplay 생성 — 셸에서. A가 host로 뜬 뒤에 해야 한다)
//   4) script-execute PS_VerifyMultiplayer.Check   — A쪽 신호를 Temp/ps_mp_result.txt 로 (Read it)
//   5) script-execute PS_VerifyMultiplayer.Nudge   — A 아바타를 고정 좌표로 이동(신호 ③ 위치 전파용)
//   6) (B쪽 .promptscene-status 를 읽어 ②③④ 교차 대조 — 셸에서)
//   7) script-execute (isPlaying=false) → PS_VerifyMultiplayer.Teardown
//
// ⚠ QuickStart 는 메모리에서만 고친다(디스크 저장 금지 — sibling verify_* 스킬과 동일 규약).
//    Play를 끊었다가 다시 들어갈 때마다 Setup을 다시 부를 것: Play 종료 시 씬이 진입 전 스냅샷으로
//    되돌아간다(xumflow-migration §17 / quicktest 함정).
// Reflection-only — App.HotUpdate 에 컴파일 의존하지 않는다(sibling 스크립트와 동일).
using UnityEngine;
using UnityEditor;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine.SceneManagement;

public class PS_VerifyMultiplayer {
    // ---- 실행 전에 채운다 ----------------------------------------------------------
    const string ROOM = "AssembleRoom";   // leaf == Addressables 주소 == roomSceneKey
    // 컴포넌트 파리티(게이트 2)를 볼 때만 채운다. 비우면 게이트 1(2인 스폰)만 판정한다.
    const string PARITY_TYPE = "";        // 예: "ChatChannelView" — 양측에 같은 objId로 보여야 하는 네트워크 View
    // -------------------------------------------------------------------------------

    static string TmpDir => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Temp"));
    static string OrigF  => Path.Combine(TmpDir, "ps_mp_orig.txt");
    static string OutF   => Path.Combine(TmpDir, "ps_mp_result.txt");

    // ── 1) Setup ────────────────────────────────────────────────────────────────────
    public static object Setup() {
        var st = FindStarter();
        if (st == null) return "QuickTestStarter not found — QuickStart.unity 를 먼저 열어라";

        var so = new SerializedObject(st);
        string orig = so.FindProperty("startAsServer").boolValue + "|" +
                      so.FindProperty("hostMode").boolValue + "|" +
                      so.FindProperty("roomSceneKey").stringValue;
        Directory.CreateDirectory(TmpDir);
        if (!File.Exists(OrigF)) File.WriteAllText(OrigF, orig);   // 첫 Setup 만 원본으로 인정

        so.FindProperty("startAsServer").boolValue = true;   // A = 서버
        so.FindProperty("hostMode").boolValue      = true;   // + 로컬 클라(자기 아바타)
        so.FindProperty("roomSceneKey").stringValue = ROOM;
        so.ApplyModifiedPropertiesWithoutUndo();             // 메모리 전용 — scene-save 하지 않는다

        return "Setup ok: startAsServer=true hostMode=true roomSceneKey=" + ROOM + " (orig=" + orig + ", 메모리 전용)";
    }

    // ── 4) Check — A쪽 신호 ─────────────────────────────────────────────────────────
    public static object Check() {
        var sb = new StringBuilder();
        sb.AppendLine("ROOM=" + ROOM + " PARITY_TYPE=" + (PARITY_TYPE == "" ? "<none>" : PARITY_TYPE));

        var nm = NetworkManager();
        bool srv = nm != null && (bool)Prop(nm, "IsServerStarted");
        bool cli = nm != null && (bool)Prop(nm, "IsClientStarted");

        var loaded = Enumerable.Range(0, SceneManager.sceneCount).Select(i => SceneManager.GetSceneAt(i).name).ToList();
        bool roomLoaded = loaded.Contains(ROOM);
        sb.AppendLine("S1 room loaded (" + ROOM + ")=" + roomLoaded + "  [scenes: " + string.Join(",", loaded) + "]");
        sb.AppendLine("S2 A host up: IsServerStarted=" + srv + " IsClientStarted=" + cli);

        int conns = 0; string connIds = "";
        if (nm != null) {
            var clients = Prop(Prop(nm, "ServerManager"), "Clients") as System.Collections.IDictionary;
            if (clients != null) { conns = clients.Count; connIds = string.Join(",", clients.Keys.Cast<object>().Select(k => k.ToString())); }
        }
        sb.AppendLine("S3 connections=" + conns + " [" + connIds + "]   (host=0, 게스트 B가 붙으면 2)");

        var objs = Objects();
        sb.AppendLine("S4 networkObjects=" + objs.Count);
        foreach (var o in objs) sb.AppendLine("   " + Describe(o));

        // ① A 자기 아바타 IsOwner=True
        var mine = objs.FirstOrDefault(o => Name(o).StartsWith("Desktop") && (bool)Prop(o, "IsOwner"));
        sb.AppendLine("M1 A 자기 아바타 IsOwner=True -> " + (mine != null ? "PASS (objId=" + Prop(mine, "ObjectId") + ")" : "FAIL (없음)"));

        // 게스트 아바타(= B 소유) 존재 — B쪽 ②의 A측 대응 신호
        var guest = objs.FirstOrDefault(o => Name(o).StartsWith("Desktop") && !(bool)Prop(o, "IsOwner"));
        sb.AppendLine("M2(A측) 게스트 아바타(원격 소유) -> " + (guest != null
            ? "PASS (objId=" + Prop(guest, "ObjectId") + " ownerCid=" + OwnerCid(guest) + ")"
            : "FAIL (B 미접속이거나 스폰 전)"));

        // ④ 교차 대조용 objId 목록 — B의 .promptscene-status 와 집합 비교한다
        sb.AppendLine("M4(A측) objIds=" + string.Join(",", objs.Select(o => Prop(o, "ObjectId").ToString())));

        if (PARITY_TYPE != "") {
            var p = objs.FirstOrDefault(o => Comp(o, PARITY_TYPE) != null);
            sb.AppendLine("P1(A측) 파리티 대상 '" + PARITY_TYPE + "' -> " + (p != null
                ? "PASS (objId=" + Prop(p, "ObjectId") + " IsSpawned=" + Prop(p, "IsSpawned") + ")"
                : "FAIL (씬에 없음 — FEATURE가 켜져 있는지/스폰됐는지 확인)"));
        }

        bool pass = roomLoaded && srv && cli && mine != null && guest != null;
        sb.AppendLine("=== 2CLIENT GATE-1 (A측): " + (pass ? "PASS" : "FAIL") + " ===  (②③④ 최종 판정은 B의 .promptscene-status 와 교차 대조)");

        Directory.CreateDirectory(TmpDir);
        File.WriteAllText(OutF, sb.ToString());
        return sb.ToString();
    }

    // ── 5) Nudge — 신호 ③ 위치 전파 ─────────────────────────────────────────────────
    public static object Nudge() {
        var mine = Objects().FirstOrDefault(o => Name(o).StartsWith("Desktop") && (bool)Prop(o, "IsOwner"));
        if (mine == null) return "A 소유 아바타 없음";
        var tr = (mine as Component).transform;
        Vector3 before = tr.position;
        var cc = (mine as Component).GetComponentInChildren<CharacterController>();
        if (cc != null) cc.enabled = false;
        tr.position = new Vector3(5f, 0f, 3f);      // 고정 좌표 — B에서 같은 값이 관측돼야 한다
        if (cc != null) cc.enabled = true;
        return "M3 A 아바타 objId=" + Prop(mine, "ObjectId") + " " + V(before) + " -> " + V(tr.position) +
               "  (B의 .promptscene-status 에서 같은 objId 의 pos=5.00,0.00,3.00 확인)";
    }

    // ── 7) Teardown ─────────────────────────────────────────────────────────────────
    public static object Teardown() {
        if (EditorApplication.isPlaying) return "아직 Play 중 — isPlaying=false 를 먼저 확인하고 다시 부를 것";
        if (!File.Exists(OrigF)) return "원본 스냅샷 없음 (Setup 미실행?)";
        var st = FindStarter();
        if (st == null) return "QuickTestStarter not found";

        var p = File.ReadAllText(OrigF).Split('|');
        var so = new SerializedObject(st);
        so.FindProperty("startAsServer").boolValue  = bool.Parse(p[0]);
        so.FindProperty("hostMode").boolValue       = bool.Parse(p[1]);
        so.FindProperty("roomSceneKey").stringValue = p[2];
        so.ApplyModifiedPropertiesWithoutUndo();
        File.Delete(OrigF);
        return "Teardown ok: 복원 startAsServer=" + p[0] + " hostMode=" + p[1] + " roomSceneKey=" + p[2] + " (메모리 전용)";
    }

    // ── helpers (reflection) ────────────────────────────────────────────────────────
    static MonoBehaviour FindStarter() {
        var t = Type("QuickTestStarter");
        return t == null ? null : UnityEngine.Object.FindFirstObjectByType(t) as MonoBehaviour;
    }
    static object NetworkManager() {
        var f = Type("FishNet.InstanceFinder");
        return f == null ? null : f.GetProperty("NetworkManager", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
    }
    static List<object> Objects() {
        var t = Type("NetworkObject");
        if (t == null) return new List<object>();
        return UnityEngine.Object.FindObjectsByType(t, FindObjectsSortMode.None)
                 .Cast<object>().OrderBy(o => (int)Prop(o, "ObjectId")).ToList();
    }
    static string Describe(object o) =>
        "id=" + Prop(o, "ObjectId") + " " + Name(o) + " owner=" + Prop(o, "IsOwner") +
        " ownerCid=" + OwnerCid(o) + " pos=" + V((o as Component).transform.position);
    static string OwnerCid(object o) {
        var c = Prop(o, "Owner");
        if (c == null) return "-1";
        var valid = Prop(c, "IsValid");
        return (valid is bool b && b) ? Prop(c, "ClientId").ToString() : "-1";
    }
    static string Name(object o) => (o as UnityEngine.Object).name;
    static Component Comp(object o, string typeName) {
        var t = Type(typeName);
        return t == null ? null : (o as Component).GetComponent(t);
    }
    static object Prop(object o, string n) =>
        o == null ? null : o.GetType().GetProperty(n, BindingFlags.Public | BindingFlags.Instance)?.GetValue(o);
    static string V(Vector3 v) =>
        v.x.ToString("F2", CultureInfo.InvariantCulture) + "," +
        v.y.ToString("F2", CultureInfo.InvariantCulture) + "," +
        v.z.ToString("F2", CultureInfo.InvariantCulture);
    static Type Type(string name) {
        foreach (var a in AppDomain.CurrentDomain.GetAssemblies()) {
            Type[] ts; try { ts = a.GetTypes(); } catch { continue; }
            foreach (var t in ts) if (t.Name == name || t.FullName == name) return t;
        }
        return null;
    }
}
