using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Mono.Cecil;
using REPOJP.StagePhysicsEvents;
using UnityEngine.InputSystem;

int checks = 0;
void Check(bool result, string label)
{
    if (!result) throw new Exception(label);
    checks++;
}
bool Close(float a, float b) => Math.Abs(a - b) < 0.01f;

// Read the installed game's actual Update IL, rather than assuming that
// MenuLib's obsolete scrollSpeed hook runs. Unity objects remain stand-ins.
string gamePath = args.Length > 0 ? args[0] : @"E:\SteamLibrary\steamapps\common\REPO\REPO_Data\Managed\Assembly-CSharp.dll";
using var game = AssemblyDefinition.ReadAssembly(gamePath);
// Confirm the installed Unity UI module normalizes using the same internal
// runtime tick size; a hard-coded Windows delta is no longer a valid contract.
using var input = AssemblyDefinition.ReadAssembly(Path.Combine(Path.GetDirectoryName(gamePath)!, "Unity.InputSystem.dll"));
var tickGetter = input.MainModule.Types.Single(t => t.FullName == "UnityEngine.InputSystem.InputSystem")
    .Methods.Single(m => m.Name == "get_scrollWheelDeltaPerTick");
Check(tickGetter.IsAssembly && tickGetter.IsStatic && tickGetter.ReturnType.FullName == "System.Single",
    "Installed Input System has the internal static float getter used by the cached delegate");
Check(tickGetter.Body.Instructions.Any(i => i.Operand is MethodReference m && m.Name == "get_scrollWheelDeltaPerTick"),
    "Wheel tick size comes from the active input runtime");
var nativeScrollCallback = input.MainModule.Types.Single(t => t.FullName == "UnityEngine.InputSystem.UI.InputSystemUIInputModule")
    .Methods.Single(m => m.Name == "OnScrollCallback");
var nativeTickCall = nativeScrollCallback.Body.Instructions.Single(i => i.Operand is MethodReference m && m.FullName == tickGetter.FullName);
Check(nativeTickCall.Next.Operand is MethodReference divide && divide.Name == "op_Division",
    "Installed Unity UI divides scroll input by the runtime tick size");
var update = game.MainModule.Types.Single(t => t.Name == "MenuScrollBox").Methods.Single(m => m.Name == "Update");
var opcodes = typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static)
    .Where(f => f.FieldType == typeof(OpCode)).Select(f => (OpCode)f.GetValue(null)!)
    .GroupBy(o => o.Value).ToDictionary(g => g.Key, g => g.First());
object? Operand(object? value)
{
    if (value is MethodReference method && method.DeclaringType.Name == "SemiFunc" && method.Name == "InputScrollY")
        return typeof(SemiFunc).GetMethod(method.Name)!;
    if (value is MethodReference sign && sign.DeclaringType.Name == "Mathf" && sign.Name == "Sign")
        return typeof(UnityEngine.Mathf).GetMethod("Sign")!;
    if (value is FieldReference field && field.DeclaringType.Name == "MenuScrollBox")
        return (object?)typeof(MenuScrollBox).GetField(field.Name) ?? value;
    return value;
}
var original = update.Body.Instructions.Select(i => new CodeInstruction(opcodes[i.OpCode.Value], Operand(i.Operand))).ToList();
var patched = EventMenuScroll.Transpiler(original).ToList();
var helper = typeof(EventMenuScroll).GetMethod("WheelStep", BindingFlags.Static | BindingFlags.NonPublic)!;
Check(patched.Count == original.Count + 6, "Installed game matches exactly one wheel calculation");
Check(patched.Count(i => i.Calls(helper)) == 1, "Wheel helper is inserted exactly once");
Check(patched.Where(original.Contains).SequenceEqual(original), "All native gates, keyboard math, bounds and animation instructions are retained in order");
Check(original.All(i => patched.Contains(i) && i.labels.Count == 0), "Original instructions and branch targets are retained");

// Execute the actual wheel-expression IL after applying the production
// transpiler. This catches a patch that changes an unused speed property.
int helperIndex = patched.FindIndex(i => i.Calls(helper));
var dynamic = new DynamicMethod("PatchedNativeWheel", typeof(float), new[] { typeof(MenuScrollBox) }, typeof(Program).Module, true);
var il = dynamic.GetILGenerator();
// The native expression starts with a wheel local; load its input directly.
il.Emit(OpCodes.Call, typeof(SemiFunc).GetMethod("InputScrollY")!);
foreach (var instruction in patched.GetRange(helperIndex - 14, 15))
{
    switch (instruction.operand)
    {
        case null: il.Emit(instruction.opcode); break;
        case float number: il.Emit(instruction.opcode, number); break;
        case FieldInfo field: il.Emit(instruction.opcode, field); break;
        case MethodInfo method: il.Emit(instruction.opcode, method); break;
        default: throw new Exception("Unexpected operand in installed wheel expression");
    }
}
il.Emit(OpCodes.Ret);
var wheelStep = dynamic.CreateDelegate<Func<MenuScrollBox, float>>();
var box = EventMenu.Box;
foreach (float unitsPerTick in new[] { 1f, 120f, 10f })
foreach (float notches in new[] { 0.01f, 0.5f, 1f, 5f, -0.01f, -0.5f, -1f, -5f })
foreach (var geometry in new[] { (1000f, 50000f), (320f, 4000f), (250f, 100f) })
foreach (float staleHeight in new[] { 200f, 4000f, 100000f })
{
    InputSystem.scrollWheelDeltaPerTick = unitsPerTick;
    SemiFunc.Scroll = notches * unitsPerTick;
    box.scrollHeight = staleHeight;
    box.scrollerStartPosition = geometry.Item2;
    box.scrollBarBackground.rect = new UnityEngine.Rect { height = geometry.Item1 };
    float delta = wheelStep(box);
    float contentDistance = delta * geometry.Item2 / (geometry.Item1 - box.scrollHandle.sizeDelta.y);
    Check(Close(contentDistance, notches * 97.5f), "Actual patched expression gives three body lines per detent despite stale native height or page geometry");
}

InputSystem.scrollWheelDeltaPerTick = 1f;
SemiFunc.Scroll = 1;
float single = wheelStep(box);
SemiFunc.Scroll = 5;
Check(Close(wheelStep(box), single * 5), "Five detents in one low-FPS input frame equal five separate detents");
SemiFunc.Scroll = 0.5f;
Check(Close(wheelStep(box) * 2, single), "Fractional high-resolution input retains its accumulated distance");

SemiFunc.Scroll = 1;
foreach (float invalid in new[] { 0f, float.NaN, float.PositiveInfinity, float.NegativeInfinity })
{
    SemiFunc.Scroll = invalid;
    Check(wheelStep(box) == 0, "Idle or invalid input adds no movement");
}
SemiFunc.Scroll = 1;
foreach (float invalidTick in new[] { 0f, -1f, float.NaN, float.PositiveInfinity, float.NegativeInfinity })
{
    InputSystem.scrollWheelDeltaPerTick = invalidTick;
    Check(wheelStep(box) == 0, "Invalid runtime tick size adds no movement");
}
InputSystem.scrollWheelDeltaPerTick = 120f;
SemiFunc.Scroll = 120f;
Check(Close(wheelStep(box), single), "A runtime unit change preserves the distance of one detent");
InputSystem.scrollWheelDeltaPerTick = 1f;
SemiFunc.Scroll = 1f;
Check(Close(wheelStep(box), single), "Switching back to normalized input preserves distance");
var otherBox = new MenuScrollBox();
Check(Close(wheelStep(otherBox), otherBox.scrollWheelStep / (otherBox.scrollHeight * 0.01f)), "Unrelated menus keep the native wheel calculation");
EventMenu.Open = false;
Check(Close(wheelStep(box), box.scrollWheelStep / (box.scrollHeight * 0.01f)), "Closed menu keeps the native calculation");
EventMenu.Open = true;
box.scrollerStartPosition = 0;
Check(wheelStep(box) == 0, "Content with no travel adds no movement");
box.scrollerStartPosition = 100;
box.scrollBarBackground.rect = new UnityEngine.Rect { height = 40 };
Check(wheelStep(box) == 0, "A full-height handle adds no movement");
bool rejected = false;
try { EventMenuScroll.Transpiler(new[] { new CodeInstruction(OpCodes.Ret) }).ToList(); }
catch (InvalidOperationException) { rejected = true; }
Check(rejected, "Unsupported game IL fails explicitly instead of silently installing an ineffective patch");

// Verify why the previous implementation was ineffective with the pinned DLL.
using var menuLib = AssemblyDefinition.ReadAssembly(Path.GetFullPath("lib/MenuLib.dll"));
var entry = menuLib.MainModule.Types.Single(t => t.FullName == "MenuLib.Entry");
var awake = entry.Methods.Single(m => m.Name == "Awake");
Check(entry.Methods.Any(m => m.Name == "MenuScrollBox_UpdateILHook"), "Obsolete MenuLib hook still exists");
Check(!awake.Body.Instructions.Any(i => i.Operand is MethodReference m && m.Name == "MenuScrollBox_UpdateILHook"), "Installed MenuLib does not register the obsolete scrollSpeed hook");
// The two mods decorate the same native expression. Apply both orders, then
// execute the combined IL for Events, the peer menu, and an unrelated menu.
var peerHelper = typeof(PeerMenuScroll).GetMethod(nameof(PeerMenuScroll.WheelStep))!;
List<CodeInstruction> Decorate(List<CodeInstruction> inputCode, bool peer)
{
    var outputCode = EventMenuScroll.Transpiler(inputCode).ToList();
    if (peer) outputCode.First(i => i.Calls(helper)).operand = peerHelper;
    return outputCode;
}
foreach (bool peerFirst in new[] { true, false })
{
    var combined = Decorate(Decorate(original, peerFirst), !peerFirst);
    Check(combined.Count(i => i.Calls(helper)) == 1 && combined.Count(i => i.Calls(peerHelper)) == 1,
        "Both patch orders install each menu helper once");
    int firstHelper = combined.FindIndex(i => i.Calls(helper) || i.Calls(peerHelper));
    var both = new DynamicMethod("BothMenusWheel", typeof(float), new[] { typeof(MenuScrollBox) }, typeof(Program).Module, true);
    var emit = both.GetILGenerator();
    emit.Emit(OpCodes.Call, typeof(SemiFunc).GetMethod("InputScrollY")!);
    foreach (var instruction in combined.GetRange(firstHelper - 14, 21))
    {
        switch (instruction.operand)
        {
            case null: emit.Emit(instruction.opcode); break;
            case float number: emit.Emit(instruction.opcode, number); break;
            case FieldInfo field: emit.Emit(instruction.opcode, field); break;
            case MethodInfo method: emit.Emit(instruction.opcode, method); break;
            default: throw new Exception("Unexpected combined expression operand");
        }
    }
    emit.Emit(OpCodes.Ret);
    var scrollBoth = both.CreateDelegate<Func<MenuScrollBox, float>>();
    foreach (var target in new[] { EventMenu.Box, PeerMenuScroll.Box, new MenuScrollBox() })
    foreach (float notches in new[] { 0.5f, 1f, 5f, -1f })
    {
        target.scrollerStartPosition = 1000; target.scrollerEndPosition = 0;
        target.scrollBarBackground.rect = new UnityEngine.Rect { height = 500 };
        target.scrollHandle.sizeDelta = new UnityEngine.Vector2 { y = 40 };
        InputSystem.scrollWheelDeltaPerTick = 120; SemiFunc.Scroll = notches * 120;
        float expected = ReferenceEquals(target, EventMenu.Box) || ReferenceEquals(target, PeerMenuScroll.Box)
            ? notches * 97.5f * 460 / 1000
            : Math.Sign(notches) * target.scrollWheelStep / (target.scrollHeight * 0.01f);
        Check(Close(scrollBoth(target), expected), "Combined patches never double-scale either menu or alter unrelated menus");
    }
}
Console.WriteLine($"PASS: {checks} scroll checks using installed-game IL and the production transpiler, including both peer-patch orders.");
Console.WriteLine("Unity input polling, patch installation, rendering and scene transitions still require in-game verification.");
