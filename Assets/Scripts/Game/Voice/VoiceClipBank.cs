using System.Collections.Generic;
using UnityEngine;

namespace Dikdik.Game.Voice
{
    /// <summary>
    /// Loads the recorded lines and hands them out one at a time, in order.
    ///
    /// <para>Clips are named <c>sup_&lt;group&gt;_&lt;nn&gt;</c>, so the group is the
    /// middle token and the number sorts within it. Groups cycle in script order rather
    /// than shuffling, because random repeats itself in a way players read as the game not
    /// paying attention. Hearing the same line twice in a row when there were five to
    /// choose from reads as a bug even when it is chance.</para>
    /// </summary>
    public class VoiceClipBank
    {
        private readonly Dictionary<string, List<AudioClip>> _groups =
            new Dictionary<string, List<AudioClip>>();

        private readonly Dictionary<string, int> _cursor = new Dictionary<string, int>();

        public VoiceClipBank(string resourceFolder)
        {
            foreach (var clip in Resources.LoadAll<AudioClip>(resourceFolder))
            {
                var parts = clip.name.Split('_');
                if (parts.Length < 3)
                    continue;

                var group = parts[1];
                if (!_groups.TryGetValue(group, out var list))
                {
                    list = new List<AudioClip>();
                    _groups[group] = list;
                }

                list.Add(clip);
            }

            // Sort by name so cycling follows the order they were written and recorded,
            // not the order the filesystem happened to return them.
            foreach (var list in _groups.Values)
                list.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
        }

        public bool Has(string group) => _groups.ContainsKey(group) && _groups[group].Count > 0;

        public int Count(string group) => _groups.TryGetValue(group, out var l) ? l.Count : 0;

        /// <summary>The next clip in a group, wrapping at the end. Null if the group is empty.</summary>
        public AudioClip Next(string group)
        {
            if (!_groups.TryGetValue(group, out var list) || list.Count == 0)
                return null;

            var index = _cursor.TryGetValue(group, out var c) ? c : 0;
            _cursor[group] = index + 1;
            return list[index % list.Count];
        }

        /// <summary>Every clip in a group, in order. For sequences like the opening briefing.</summary>
        public IReadOnlyList<AudioClip> All(string group)
        {
            return _groups.TryGetValue(group, out var list)
                ? (IReadOnlyList<AudioClip>)list
                : System.Array.Empty<AudioClip>();
        }
    }
}
