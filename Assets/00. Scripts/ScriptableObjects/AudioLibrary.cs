using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[CreateAssetMenu(menuName = "Audio/Audio Library")]
public class AudioLibrary : ScriptableObject
{
    [System.Serializable]
    public class AudioEntry
    {
        public string key;
        public AudioClip clip;
    }

    public List<AudioEntry> entries;

    private Dictionary<string, AudioEntry> _dict;

    public AudioEntry Get(string key)
    {
        if (_dict == null)
            _dict = entries.ToDictionary(e => e.key);
        return _dict.TryGetValue(key, out var entry) ? entry : null;
    }
}