using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Collections.Immutable;
using REPOJP.StagePhysicsEvents;

int checks = 0;
void Check(bool condition, string message)
{
    checks++;
    if (!condition) throw new InvalidOperationException(message);
}
Check(ExtendedEventPolicy.RepairAmount(100, 100, 5) == 0, "Undamaged valuable must not be repaired");
Check(ExtendedEventPolicy.RepairAmount(50, 50, 5) == 0, "Value Crash alone is not damage");
Check(ExtendedEventPolicy.RepairAmount(200, 200, 5) == 0, "Value Surge alone is not damage");
Check(ExtendedEventPolicy.RepairAmount(50, 40, 10) == 5, "Repair must keep the Crash scale");
Check(ExtendedEventPolicy.RepairAmount(200, 160, 10) == 20, "Repair must keep the Surge scale");
Check(ExtendedEventPolicy.RepairAmount(100, 99, 10) == 1, "Repair cannot exceed full value");
Check(ExtendedEventPolicy.RepairAmount(100, 105, 10) == 0, "Overfull value must not be reduced");
Check(ExtendedEventPolicy.RepairAmount(100, -1, 5) == 0, "Invalid value skipped");
Check(ExtendedEventPolicy.RepairAmount(float.NaN, 1, 5) == 0, "NaN skipped");
Check(ExtendedEventPolicy.DrainCharge(100, 5, 0) == 95, "Five exact percentage points");
Check(ExtendedEventPolicy.DrainCharge(21, 5, 20) == 20, "Battery minimum");
Check(ExtendedEventPolicy.DrainCharge(10, 5, 20) == 10, "Minimum must not charge an empty battery");
Check(ExtendedEventPolicy.DrainCharge(3, 5, 0) == 0, "Battery cannot become negative");
Check(ExtendedEventPolicy.SharedDamage(40, 25, 25, 100, false) == 10, "Shared damage ratio");
Check(ExtendedEventPolicy.SharedDamage(1000, 25, 25, 100, true) == 25, "Shared damage cap");
Check(ExtendedEventPolicy.SharedDamage(100, 100, 100, 1, false) == 0, "Do not deliberately kill at one HP");
Check(ExtendedEventPolicy.SharedDamage(40, 25, 25, 5, false) == 4, "Nonlethal host-side cap");
Check(ExtendedEventPolicy.SharedDamage(40, 25, 25, 5, true) == 10, "Lethal option");
Check(ExtendedEventPolicy.SharedDamage(0, 25, 25, 100, false) == 0, "No loss means no propagation");
Check(ExtendedEventPolicy.SharedDamage(int.MaxValue, 100, 100, 100, true) == 100, "Shared damage cannot overflow");
Check(ExtendedEventPolicy.EnemyDamage(20, 50) == 10, "Armor scales once");
Check(ExtendedEventPolicy.EnemyDamage(20, 200) == 40, "Vulnerability scales once");
Check(ExtendedEventPolicy.EnemyDamage(0, 200) == 0, "Zero damage stays zero");
Check(ExtendedEventPolicy.EnemyDamage(1, 50) == 1, "Small hits stay nonzero");
Check(ExtendedEventPolicy.EnemyDamage(int.MaxValue, 500) == int.MaxValue, "Enemy damage cannot overflow");

string root = Path.GetFullPath(args[0]);
Check(!EventMenuPolicy.CanEdit(false, false, false, false), "Menu has no authority before the game is ready");
Check(EventMenuPolicy.CanEdit(true, false, false, false), "Single player can edit");
Check(EventMenuPolicy.CanEdit(true, true, true, true), "Host can edit");
Check(!EventMenuPolicy.CanEdit(true, true, true, false), "Guests cannot edit");
Check(!EventMenuPolicy.CanEdit(true, true, false, true), "Disconnected multiplayer cannot edit");
long allEventBits = (1L << 46) - 1;
Check(EventMenuPolicy.TryDecode(EventMenuPolicy.Encode(3, allEventBits, true), 3, out long enabledMask, out bool enabled) &&
    enabled && enabledMask == allEventBits, "All 46 event settings round-trip without truncation");
Check(EventMenuPolicy.TryDecode("1:3:0:0", 3, out enabledMask, out enabled) && enabledMask == 0 && !enabled, "All-off is distinct from unavailable");
foreach (string? bad in new[] { null, "", "2:3:1:1", "1:2:1:1", "1:3:-1:1", "1:3:1:2", "1:3:NaN:1", "1:3:9223372036854775808:1", "1:3:1:1:extra" })
    Check(!EventMenuPolicy.TryDecode(bad, 3, out _, out _), "Reject malformed, foreign-host or unsupported settings: " + bad);
Check(!EventMenuPolicy.TryDecode("1:3:1:1", 0, out _, out _), "No master means unavailable");
string menuSource = File.ReadAllText(Path.Combine(root, "EventMenu.cs"));
foreach (string action in new[] { "Events", "EVENT GUIDE", "EVENT SETTINGS", "REFRESH DISPLAY DATA", "HUD EDITOR", "REPORT A PROBLEM", "COPY REPORT", "OPEN SAVED REPORT" })
    Check(menuSource.Contains("\"" + action + "\""), "Events UI provides " + action);
string guideSource = File.ReadAllText(Path.Combine(root, "EventGuideCatalog.cs"));
Check(System.Text.RegularExpressions.Regex.Matches(guideSource, @"StageEffect\.\w+ =>").Count == 46, "Guide has all 46 event descriptions");
string dll = Path.Combine(root, "bin/Release/netstandard2.1/StagePhysicsEvents.dll");
using var pe = new PEReader(File.OpenRead(dll));
MetadataReader md = pe.GetMetadataReader();
Check(md.GetAssemblyDefinition().Version == new Version(4, 3, 0, 0), "Release version");
string[] forbidden = { "StageRoles", "RoleShuffle", "EliteEnemyVariants" };
foreach (AssemblyReferenceHandle reference in md.AssemblyReferences)
    Check(!forbidden.Contains(md.GetString(md.GetAssemblyReference(reference).Name)), "No mandatory optional-mod references");
string models = File.ReadAllText(Path.Combine(root, "EventModels.cs"));
string controller = File.ReadAllText(Path.Combine(root, "StagePhysicsEventController.cs"));
string config = File.ReadAllText(Path.Combine(root, "ExtendedEventConfig.cs"));
string[] events = { "Restoration", "BatteryDrain", "HeavyCargo", "Butterfingers", "EnemyBlindness", "EnemyArmor", "EnemyVulnerability", "SupplyDrop", "PlayerSwap", "SharedPain" };
foreach (string name in events)
{
    Check(models.Contains(name + " = 1L <<"), $"{name}: unique 64-bit flag");
    Check(controller.Contains("StageEffect." + name), $"{name}: availability wired");
    Check(md.ManifestResources.Any(handle => md.GetString(md.GetManifestResource(handle).Name).EndsWith("Runtime." + name + ".png")), $"{name}: embedded HUD icon");
}
Check(!models.Contains("EmergencyRecall"), "Emergency Recall excluded");
Check(!models.Contains("SharedRecovery"), "Shared Recovery excluded");
Check(config.Contains("DefaultEventChancePercent"), "New chances use existing six-percent default");
Check(controller.Contains("_extendedEvents?.ResetStage()"), "Stage-end cleanup");
Check(controller.Contains("_extendedEvents.ResetStage()"), "New-stage supply budget reset");
Check(controller.Contains("_extendedEvents?.Stop()"), "Event-end cleanup");
string originalConfig = File.ReadAllText(Path.Combine(root, "StagePhysicsConfig.cs"));
foreach (System.Text.RegularExpressions.Match match in System.Text.RegularExpressions.Regex.Matches(guideSource, @"StageEffect\.(\w+) =>"))
{
    string effectName = match.Groups[1].Value;
    var display = System.Text.RegularExpressions.Regex.Match(models, "StageEffect\\." + effectName + " => \"([^\"]+)\"");
    string section = display.Success ? display.Groups[1].Value : effectName;
    Check(originalConfig.Contains("\"" + section + "\", \"Enabled\"") || events.Contains(effectName), "Menu binds an existing setting: " + section);
}
foreach (string pair in new[] { "StageEffect.Feather, StageEffect.HeavyCargo", "StageEffect.Battery, StageEffect.BatteryDrain", "StageEffect.EnemyArmor, StageEffect.EnemyVulnerability" })
    Check(originalConfig.Contains("IsPair(candidate, active, " + pair + ")"), "Logical exclusion: " + pair);
string adapter = File.ReadAllText(Path.Combine(root, "ExtendedEventAdapter.cs"));
Check(adapter.Contains("StagePhysicsEffectCarrierUtility.IsInternalCarrier(component)"), "All scene queries exclude hierarchical internal carriers");
Check(adapter.Contains("_config.TargetValuables.Value && held.GetComponent<ValuableObject>()"), "Dropped valuable protection respects Targets");
Check(adapter.Contains("_config.TargetValuables.Value && target.GetComponent<ValuableObject>()"), "Heavy valuable protection respects Targets");
Check(adapter.Contains("_generation++") && adapter.Contains("generation == _generation"), "Stale supply coroutines cannot spawn into the next event");
Check(adapter.Contains("HurtOther(damage, Vector3.zero, !_config.SharedPainCanKill.Value, -1, true)"), "Shared hits retain their asynchronous anti-recursion tag");
string hooks = File.ReadAllText(Path.Combine(root, "ExtendedEventPatches.cs"));
Check(hooks.Contains("accepted && effect && !hurtByHeal"), "Remote shared damage and donations do not recurse");
Check(hooks.Contains("Record(__instance, __state, !hurtByHeal)"), "Local shared damage and donations do not recurse");

var effectType = md.GetTypeDefinition(md.TypeDefinitions.Single(handle => md.GetString(md.GetTypeDefinition(handle).Name) == "StageEffect"));
var bits = effectType.GetFields().Select(md.GetFieldDefinition).Where(field => !field.GetDefaultValue().IsNil)
    .Select(field => md.GetBlobReader(md.GetConstant(field.GetDefaultValue()).Value).ReadInt64()).Where(value => value != 0).ToList();
Check(bits.Count == 46 && bits.Distinct().Count() == 46 && bits.All(value => (value & (value - 1)) == 0), "All 46 event bits are distinct");
Check(File.ReadAllText(Path.Combine(root, "package/README.md")).Contains("All 46 events"), "English catalog count");
Check(File.ReadAllText(Path.Combine(root, "package/README.md")).Contains("全46イベント"), "Japanese catalog count");

// Verify methods against the installed, non-publicized game assembly, not only stubs.
using var gamePe = new PEReader(File.OpenRead(args[1]));
MetadataReader game = gamePe.GetMetadataReader();
void Contract(string typeName, string methodName, string returns, bool isPublic, params string[] parameters)
{
    TypeDefinition type = game.GetTypeDefinition(game.TypeDefinitions.Single(handle => game.GetString(game.GetTypeDefinition(handle).Name) == typeName));
    var methods = type.GetMethods().Select(game.GetMethodDefinition).Where(method => game.GetString(method.Name) == methodName).ToList();
    var provider = new SignatureNames();
    Check(methods.Any(method => {
        var signature = method.DecodeSignature(provider, 0);
        return signature.ReturnType == returns && signature.ParameterTypes.SequenceEqual(parameters) &&
            (!isPublic || (method.Attributes & MethodAttributes.MemberAccessMask) == MethodAttributes.Public);
    }), $"Installed-game contract: {typeName}.{methodName}");
}
Contract("EnemyHealth", "Hurt", "Void", true, "Int32", "Vector3");
Contract("EnemyVision", "Vision", "IEnumerator", false);
Contract("PlayerHealth", "Hurt", "Void", true, "Int32", "Boolean", "Int32", "Boolean");
Contract("PlayerHealth", "UpdateHealthRPC", "Void", true, "Int32", "Int32", "Boolean", "Boolean", "PhotonMessageInfo");
Contract("PlayerHealth", "HurtOther", "Void", true, "Int32", "Vector3", "Boolean", "Int32", "Boolean");
Contract("PhysGrabObjectImpactDetector", "HealLogic", "Void", false, "Single", "Vector3");
Contract("PhysGrabObjectImpactDetector", "HealRPC", "Void", false, "Single", "Vector3", "PhotonMessageInfo");
Contract("ItemBattery", "BatteryFullPercentChangeLogic", "Void", false, "Int32", "Boolean");
Contract("ItemBattery", "BatteryFullPercentChangeRPC", "Void", false, "Int32", "Boolean", "PhotonMessageInfo");
Contract("PlayerAvatar", "Spawn", "Void", true, "Vector3", "Quaternion");
Contract("PlayerAvatar", "SpawnRPC", "Void", false, "Vector3", "Quaternion", "PhotonMessageInfo");
Contract("PhysGrabber", "OverrideGrabRelease", "Void", true, "Int32", "Single");
Contract("PhysGrabObject", "OverrideMass", "Void", true, "Single", "Single");
Contract("PhysGrabObject", "OverrideIndestructible", "Void", true, "Single");

Console.WriteLine($"PASS: {checks} arithmetic, integration, resource, version and installed-game contract checks. Runtime multiplayer testing is separate.");

sealed class SignatureNames : ISignatureTypeProvider<string, int>
{
    public string GetArrayType(string elementType, ArrayShape shape) => elementType + "[]";
    public string GetByReferenceType(string elementType) => elementType + "&";
    public string GetFunctionPointerType(MethodSignature<string> signature) => "fn";
    public string GetGenericInstantiation(string genericType, ImmutableArray<string> typeArguments) => genericType;
    public string GetGenericMethodParameter(int genericContext, int index) => "!!" + index;
    public string GetGenericTypeParameter(int genericContext, int index) => "!" + index;
    public string GetModifiedType(string modifier, string unmodifiedType, bool isRequired) => unmodifiedType;
    public string GetPinnedType(string elementType) => elementType;
    public string GetPointerType(string elementType) => elementType + "*";
    public string GetPrimitiveType(PrimitiveTypeCode typeCode) => typeCode.ToString();
    public string GetSZArrayType(string elementType) => elementType + "[]";
    public string GetTypeFromDefinition(MetadataReader reader, TypeDefinitionHandle handle, byte rawTypeKind) => reader.GetString(reader.GetTypeDefinition(handle).Name);
    public string GetTypeFromReference(MetadataReader reader, TypeReferenceHandle handle, byte rawTypeKind) => reader.GetString(reader.GetTypeReference(handle).Name);
    public string GetTypeFromSpecification(MetadataReader reader, int genericContext, TypeSpecificationHandle handle, byte rawTypeKind) => reader.GetTypeSpecification(handle).DecodeSignature(this, genericContext);
}
