using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

// 씬의 토글을 EffectToggleSettings_m에 연결합니다.
[DisallowMultipleComponent]
public sealed class EffectToggleUI_m : MonoBehaviour
{
    [Serializable]
    private struct Entry
    {
        public EffectToggleType_m Type;
        public Toggle Toggle;
    }

    [SerializeField] private List<Entry> _entries = new();

    private readonly List<UnityAction<bool>> _listeners = new();

    private void Awake()
    {
        foreach (Entry entry in _entries)
        {
            if (entry.Toggle == null)
            {
                _listeners.Add(null);
                continue;
            }

            EffectToggleType_m type = entry.Type;
            UnityAction<bool> listener = isOn => EffectToggleSettings_m.Set(type, isOn);
            entry.Toggle.SetIsOnWithoutNotify(EffectToggleSettings_m.IsOn(type));
            entry.Toggle.onValueChanged.AddListener(listener);
            _listeners.Add(listener);
        }
    }

    private void OnDestroy()
    {
        for (int i = 0; i < _entries.Count && i < _listeners.Count; i++)
        {
            if (_entries[i].Toggle != null && _listeners[i] != null)
            {
                _entries[i].Toggle.onValueChanged.RemoveListener(_listeners[i]);
            }
        }
        _listeners.Clear();
    }
}
