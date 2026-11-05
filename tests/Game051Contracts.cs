using Mono.Cecil;
using Mono.Cecil.Cil;

internal static class Game051Contracts
{
    internal static void Run(string root, string gamePath, Action<bool, string> check)
    {
        using var game = ModuleDefinition.ReadModule(gamePath);
        using var mod = ModuleDefinition.ReadModule(Path.Combine(root, "bin/Release/netstandard2.1/StagePhysicsEvents.dll"));
        TypeDefinition Native(string name) => game.GetTypes().Single(type => type.FullName == name);
        MethodDefinition Method(string type, string name, string result, params string[] arguments)
        {
            var matches = Native(type).Methods.Where(method => method.Name == name && method.ReturnType.FullName == result &&
                method.Parameters.Select(parameter => parameter.ParameterType.FullName).SequenceEqual(arguments)).ToArray();
            check(matches.Length == 1, "0.5.1 complete native signature: " + type + "." + name);
            return matches.Single();
        }
        void Field(string type, string name, string expected)
        {
            check(Native(type).Fields.Any(field => field.Name == name && field.FieldType.FullName == expected && !field.IsStatic),
                "0.5.1 reflected field type: " + type + "." + name);
        }

        // Existence alone did not detect the obsolete one-argument reflection call.
        var open = Method("PhysGrabHinge", "OpenImpulse", "System.Void");
        var close = Method("PhysGrabHinge", "CloseImpulse", "System.Void", "System.Boolean", "System.Boolean");
        var closeRpc = Method("PhysGrabHinge", "CloseImpulseRPC", "System.Void", "System.Boolean", "System.Boolean", "Photon.Pun.PhotonMessageInfo");
        var openRpc = Method("PhysGrabHinge", "OpenImpulseRPC", "System.Void", "Photon.Pun.PhotonMessageInfo");
        foreach (var rpc in new[] { openRpc, closeRpc })
        {
            check(rpc.CustomAttributes.Any(attribute => attribute.AttributeType.FullName == "Photon.Pun.PunRPC"), "Door operation remains a vanilla RPC: " + rpc.Name);
            check(rpc.Body.Instructions.Any(instruction => instruction.Operand is MethodReference call &&
                call.DeclaringType.Name == "SemiFunc" && call.Name == "MasterOnlyRPC"), "Door RPC retains host authority: " + rpc.Name);
        }
        check(close.Body.Instructions.Any(instruction => instruction.Operand is MethodReference call &&
            call.DeclaringType.FullName == "Photon.Pun.PhotonView" && call.Name == "RPC"), "Native close forwards to vanilla clients");
        var adapter = mod.GetTypes().Single(type => type.Name == "StageWorldEventAdapter");
        var drive = adapter.Methods.Single(method => method.Name == "DriveHinge");
        var calls = drive.Body.Instructions.Where(instruction => instruction.Operand is MethodReference).ToArray();
        check(calls.Count(instruction => ((MethodReference)instruction.Operand).FullName == open.FullName) == 1 &&
            calls.Count(instruction => ((MethodReference)instruction.Operand).FullName == close.FullName) == 1,
            "Door Chaos uses compile-checked native open and two-argument close exactly once");
        check(!calls.Any(instruction => ((MethodReference)instruction.Operand).Name == "Invoke"), "No runtime parameter-count reflection remains in DriveHinge");
        var closeCall = calls.Single(instruction => ((MethodReference)instruction.Operand).FullName == close.FullName);
        check(closeCall.Previous.OpCode.Code == Code.Ldc_I4_1 && closeCall.Previous.Previous.OpCode.Code == Code.Ldc_I4_0,
            "Native close uses light impulse and audible close; configured angular force is preserved");
        check(calls.Any(instruction => ((MethodReference)instruction.Operand).Name == "set_angularVelocity"), "Configured Door Chaos force still follows native close");

        Method("EnemyParent", "Spawn", "System.Void");
        Method("ValuableCar", "UpdateState", "System.Void", "ValuableCar/State");
        Method("ValuablePlane", "UpdateState", "System.Void", "ValuablePlane/State");
        Method("PhysGrabObjectImpactDetector", "HealLogic", "System.Void", "System.Single", "UnityEngine.Vector3");
        Method("PlayerVoiceChat", "OverrideDisableEnemyInvestigate", "System.Void", "System.Single");
        Method("SemiAreaOfEffect", "CreateAffectsRPC", "System.Void", "System.Int32[]", "System.Single[]", "System.Int32[]", "System.Double[]", "UnityEngine.Vector3", "UnityEngine.Vector3", "Photon.Pun.PhotonMessageInfo");

        // The native mute fix must not turn notification filtering into microphone muting.
        Field("PlayerVoiceChat", "clipLoudnessNoTTS", "System.Single");
        Field("PlayerVoiceChat", "investigateTimer", "System.Single");
        Field("PlayerVoiceChat", "clipLoudnessCrawlingCounter", "System.Int32");
        Field("PlayerVoiceChat", "playerAvatar", "PlayerAvatar");
        Field("PlayerVoiceChat", "ttsAudioSource", "UnityEngine.AudioSource");
        Field("PlayerAvatar", "voiceChat", "PlayerVoiceChat");
        Field("PlayerAvatar", "playerDeathHead", "PlayerDeathHead");
        foreach (string field in new[] { "isDisabled", "isCrawling", "isCrouching", "deadSet", "spectating", "isGrounded", "isTumbling" })
            Field("PlayerAvatar", field, "System.Boolean");
        Field("PlayerDeathHead", "spectated", "System.Boolean");
        Field("PlayerDeathHead", "physGrabObject", "PhysGrabObject");
        Field("EnemyTickHealAura", "totalDuration", "System.Single");
        Field("EnemyParent", "SetupDone", "System.Boolean");
        Field("EnemyParent", "Spawned", "System.Boolean");
        Field("EnemyParent", "Enemy", "Enemy");
        Field("SemiAreaOfEffect", "targetPhysObjectsForSingleplayer", "System.Collections.Generic.List`1<PhysGrabObject>");
        Field("SemiAreaOfEffect", "playerAvatars", "System.Collections.Generic.List`1<PlayerAvatar>");
        Field("MenuButton", "hovering", "System.Boolean");
        string guard = File.ReadAllText(Path.Combine(root, "NotificationEnemyReactionGuard.cs"));
        check(guard.Contains("ClipLoudnessNoTtsField") && guard.Contains("RestoreMicrophoneInvestigation") &&
            !guard.Contains("OverrideMute(") && !guard.Contains("TransmitEnabled =") && !guard.Contains(".volume ="),
            "Notification filtering does not override the game's 0.5.1 TTS mute fix");
        check(!mod.GetTypes().SelectMany(type => type.Methods).Any(method => method.CustomAttributes.Any(attribute =>
            attribute.AttributeType.FullName == "Photon.Pun.PunRPC")), "No mod-only RPC required by vanilla participants");
    }
}
