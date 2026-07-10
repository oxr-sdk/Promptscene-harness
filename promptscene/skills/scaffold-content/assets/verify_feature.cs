// scaffold-content Phase E (FEATURES half): contract §5 check, run IN the live networked room (Play mode).
// Run via MCP script-execute (className=SC_VerifyFeature, methodName=Run) after the avatar has spawned.
// Reflection-only (no compile-time dep on PromptScene.Core) — matches the assemble-room verify scripts.
// Writes results to a temp file you then Read. Set FEATURE_ID + OUT.
using UnityEngine;
using System;
using System.Linq;
using System.Text;
using System.IO;
using System.Reflection;
using System.Collections;

public class SC_VerifyFeature {
    const string FEATURE_ID = "__FEATURE_ID__";      // <-- generated feature Id (registry key)
    const string OUT = @"C:\J_0\sc_verify_feature.txt";

    static object Prop(object o, string n){ return o?.GetType().GetProperty(n, BindingFlags.Public|BindingFlags.Instance)?.GetValue(o); }
    static object Field(object o, string n){
        var t=o?.GetType(); while(t!=null){ var f=t.GetField(n, BindingFlags.Public|BindingFlags.Instance); if(f!=null) return f.GetValue(o); t=t.BaseType; } return null; }

    public static void Run(){
        var sb=new StringBuilder();
        sb.AppendLine("isPlaying="+Application.isPlaying);

        // RoomCore.Instance (static)
        var rcType = AppDomain.CurrentDomain.GetAssemblies().SelectMany(a=>{try{return a.GetTypes();}catch{return new Type[0];}})
            .FirstOrDefault(t=>t.FullName=="PromptScene.Core.RoomCore");
        var instance = rcType?.GetProperty("Instance", BindingFlags.Public|BindingFlags.Static)?.GetValue(null);
        sb.AppendLine("RESULT(A) RoomCore.Instance="+(instance!=null));
        if(instance==null){ Fail(sb,"RoomCore.Instance null — is a RoomCore in the live scene?"); return; }

        var registry = Prop(instance, "Contents");
        sb.AppendLine("registry="+(registry!=null));

        // registry.GetById(FEATURE_ID)
        var content = registry?.GetType().GetMethod("GetById")?.Invoke(registry, new object[]{FEATURE_ID});
        sb.AppendLine("RESULT(B) registered id='"+FEATURE_ID+"' -> "+(content!=null?content.GetType().Name:"NOT FOUND"));
        if(content==null){ Fail(sb,"feature did not self-register (check RoomCore present + feature under FEATURES + no compile error)"); return; }

        // toggle: false->true->false->true, each exception-free, IsEnabled tracks
        var setEnabled = content.GetType().GetMethod("SetEnabled", new[]{typeof(bool)});
        bool okOn=false, okOff=false, okReOn=false, enOn=false, enOff=false;
        try{ setEnabled.Invoke(content, new object[]{true});  enOn =(bool)Prop(content,"IsEnabled"); okOn=true; }
        catch(Exception e){ sb.AppendLine("SetEnabled(true) THREW: "+Inner(e)); }
        try{ setEnabled.Invoke(content, new object[]{false}); enOff=(bool)Prop(content,"IsEnabled"); okOff=true; }
        catch(Exception e){ sb.AppendLine("SetEnabled(false) THREW: "+Inner(e)); }
        try{ setEnabled.Invoke(content, new object[]{true}); setEnabled.Invoke(content, new object[]{true}); okReOn=true; } // idempotent double-on
        catch(Exception e){ sb.AppendLine("SetEnabled(true x2) THREW: "+Inner(e)); }
        try{ setEnabled.Invoke(content, new object[]{false}); }catch{}
        sb.AppendLine("RESULT(C) SetEnabled noThrow on="+okOn+" off="+okOff+" idempotent="+okReOn+" | IsEnabled true->"+enOn+" false->"+enOff);

        // Meta valid
        var meta = Prop(content, "Meta");
        var display=Field(meta,"DisplayName") as string; var cat=Field(meta,"Category") as string;
        bool metaOk = !string.IsNullOrEmpty(display) && !string.IsNullOrEmpty(cat);
        sb.AppendLine("RESULT(D) Meta DisplayName='"+display+"' Category='"+cat+"' valid="+metaOk);

        bool pass = content!=null && okOn && okOff && okReOn && enOn && !enOff && metaOk;
        sb.AppendLine("=== §5 FEATURES VERDICT: "+(pass?"PASS":"FAIL")+" ===");
        Write(sb);
    }

    static string Inner(Exception e){ while(e.InnerException!=null) e=e.InnerException; return e.GetType().Name+": "+e.Message; }
    static void Fail(StringBuilder sb, string why){ sb.AppendLine("=== §5 FEATURES VERDICT: FAIL ("+why+") ==="); Write(sb); }
    static void Write(StringBuilder sb){ File.WriteAllText(OUT, sb.ToString()); Debug.Log("[SC_VerifyFeature] written to "+OUT+"\n"+sb); }
}
