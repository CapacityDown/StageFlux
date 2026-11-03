using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;

namespace REPOJP.StagePhysicsEvents;

// Profile-local selections only. No gameplay values, seeds, or host data are saved.
internal sealed class CustomEventPresets
{
    internal const int Capacity = 3;
    private readonly string _path;
    private string[]?[] _slots = new string[]?[Capacity];
    internal bool Available { get; private set; } = true;
    internal string Error { get; private set; } = "";
    internal int Revision { get; private set; }

    internal CustomEventPresets(string path)
    {
        _path = path;
        try
        {
            if (!File.Exists(path)) return;
            if (new FileInfo(path).Length > 65536) throw new InvalidDataException("Preset file too large.");
            using FileStream stream = File.OpenRead(path);
            var data = (PresetFile?)new DataContractJsonSerializer(typeof(PresetFile)).ReadObject(stream);
            if (data == null || data.Version != 1 || data.Slots == null || data.Slots.Length != Capacity)
                throw new InvalidDataException("Unsupported preset file.");
            foreach (string[]? slot in data.Slots) if (slot != null) Validate(slot);
            _slots = data.Slots;
        }
        catch (Exception exception)
        {
            Available = false; Error = exception.Message;
            // Preserve corrupt/future files rather than silently replacing them.
        }
    }

    internal string[]? Get(int slot) => Available && slot >= 0 && slot < Capacity && _slots[slot] != null
        ? (string[])_slots[slot]!.Clone() : null;

    internal bool TrySave(int slot, IEnumerable<string> enabledNames, bool overwrite)
    {
        if (!Available || slot < 0 || slot >= Capacity || (_slots[slot] != null && !overwrite)) return false;
        var names = new List<string>(new HashSet<string>(enabledNames, StringComparer.Ordinal));
        names.Sort(StringComparer.Ordinal); Validate(names.ToArray());
        var next = (string[]?[])_slots.Clone(); next[slot] = names.ToArray();
        var data = new PresetFile { Version = 1, Slots = next };
        string temporary = _path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(_path))!);
            using (FileStream stream = new(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                new DataContractJsonSerializer(typeof(PresetFile)).WriteObject(stream, data);
                stream.Flush(true);
            }
            if (File.Exists(_path)) File.Replace(temporary, _path, _path + ".bak");
            else File.Move(temporary, _path);
            _slots = next; Revision++; return true;
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    private static void Validate(string[] names)
    {
        if (names.Length > 512 || Array.Exists(names, n => string.IsNullOrWhiteSpace(n) || n.Length > 80))
            throw new InvalidDataException("Invalid preset event names.");
    }

    [DataContract]
    private sealed class PresetFile
    {
        [DataMember(Name = "version", IsRequired = true)] public int Version;
        [DataMember(Name = "slots", IsRequired = true)] public string[]?[] Slots = null!;
    }
}
