// compose-room VERIFY (FEATURES half): run the contract §5 check for EVERY feature in the composition,
// IN the live networked room (Play mode). Run via MCP script-execute (className=CR_VerifyComposition,
// methodName=Run) after the avatar has spawned. Set FEATURE_IDS + OUT.
//
// This is scaffold-content's verify_feature.cs (SC_VerifyFeature) generalized from ONE feature id to N.
// The §5 pass conditions (A self-registered, B GetById, C SetEnabled exception-free + IsEnabled tracks,
// D Meta valid) are owned by the contract §5 / scaffold-content Phase E — not re-specified here.
// SYSTEMS §6.5 is verified separately by assemble-room's verify_client.cs (feature-agnostic, reused as-is).
// Reflection-only (no compile-time dep on PromptScene.Core). Writes results to a temp file you then Read.
using UnityEngine;
using System;
using System.Linq;
using System.Text;
using System.IO;
using System.Reflection;

public class CR_VerifyComposition {
    static readonly string[] FEATURE_IDS = new[]{ "ruler" };   // <-- plan's feature Ids (registry keys), in order
    const string OUT = @"C:\J_0\cr_verify_composition.txt";

    static object Prop(object o, string n){ return o?.GetType().GetProperty(n, BindingFlags.Public|BindingFlags.Instance)?.GetValue(o); }
    static object Field(object o, string n){
        var t=o?.GetType(); while(t!=null){ var f=t.GetField(n, BindingFlags.Public|BindingFlags.Instance); if(f!=null) return f.GetValue(o); t=t.BaseType; } return null; }
    static string Inner(Exception e){ while(e.InnerException!=null) e=e.InnerException; return e.GetType().Name+": "+e.Message; }

    public static void Run(){
        var sb=new StringBuilder();
        sb.AppendLine("isPlaying="+Application.isPlaying);

        var rcType = AppDomain.CurrentDomain.GetAssemblies().SelectMany(a=>{try{return a.GetTypes();}catch{return new Type[0];}})
            .FirstOrDefault(t=>t.FullName=="PromptScene.Core.RoomCore");
        var instance = rcType?.GetProperty("Instance", BindingFlags.Public|BindingFlags.Static)?.GetValue(null);
        sb.AppendLine("RESULT(A) RoomCore.Instance="+(instance!=null)+"  (shared by all features)");
        if(instance==null){ sb.AppendLine("=== §5 COMPOSITION VERDICT: FAIL (RoomCore.Instance null — is a RoomCore in the live scene?) ==="); Write(sb); return; }

        var registry = Prop(instance, "Contents");
        sb.AppendLine("registry="+(registry!=null));
        sb.AppendLine();

        bool allPass = true;
        foreach(var id in FEATURE_IDS){
            sb.AppendLine("---- feature id='"+id+"' ----");
            var content = registry?.GetType().GetMethod("GetById")?.Invoke(registry, new object[]{id});
            sb.AppendLine("  RESULT(B) registered -> "+(content!=null?content.GetType().Name:"NOT FOUND"));
            if(content==null){ sb.AppendLine("  §5("+id+"): FAIL (did not self-register — RoomCore present? feature under FEATURES? compiled?)"); allPass=false; sb.AppendLine(); continue; }

            var setEnabled = content.GetType().GetMethod("SetEnabled", new[]{typeof(bool)});
            bool okOn=false, okOff=false, okReOn=false, enOn=false, enOff=false;
            try{ setEnabled.Invoke(content, new object[]{true});  enOn =(bool)Prop(content,"IsEnabled"); okOn=true; }
            catch(Exception e){ sb.AppendLine("  SetEnabled(true) THREW: "+Inner(e)); }
            try{ setEnabled.Invoke(content, new object[]{false}); enOff=(bool)Prop(content,"IsEnabled"); okOff=true; }
            catch(Exception e){ sb.AppendLine("  SetEnabled(false) THREW: "+Inner(e)); }
            try{ setEnabled.Invoke(content, new object[]{true}); setEnabled.Invoke(content, new object[]{true}); okReOn=true; }
            catch(Exception e){ sb.AppendLine("  SetEnabled(true x2) THREW: "+Inner(e)); }
            try{ setEnabled.Invoke(content, new object[]{false}); }catch{}
            sb.AppendLine("  RESULT(C) noThrow on="+okOn+" off="+okOff+" idempotent="+okReOn+" | IsEnabled true->"+enOn+" false->"+enOff);

            var meta = Prop(content, "Meta");
            var display=Field(meta,"DisplayName") as string; var cat=Field(meta,"Category") as string;
            bool metaOk = !string.IsNullOrEmpty(display) && !string.IsNullOrEmpty(cat);
            sb.AppendLine("  RESULT(D) Meta DisplayName='"+display+"' Category='"+cat+"' valid="+metaOk);

            bool pass = okOn && okOff && okReOn && enOn && !enOff && metaOk;
            sb.AppendLine("  §5("+id+"): "+(pass?"PASS":"FAIL"));
            sb.AppendLine();
            if(!pass) allPass=false;
        }

        sb.AppendLine("=== §5 COMPOSITION VERDICT: "+(allPass?"PASS":"FAIL")+" ("+FEATURE_IDS.Length+" features) ===");
        Write(sb);
    }

    static void Write(StringBuilder sb){ File.WriteAllText(OUT, sb.ToString()); Debug.Log("[CR_VerifyComposition] written to "+OUT+"\n"+sb); }
}
