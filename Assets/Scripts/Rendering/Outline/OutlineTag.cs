using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class OutlineTag : MonoBehaviour
{
    private static readonly HashSet<OutlineTag> ActiveTags = new HashSet<OutlineTag>();

    [SerializeField] private OutlineCategory categories = OutlineCategory.Pokemon;
    [SerializeField] private bool includeChildren = true;
    [SerializeField] private Renderer[] renderers;

    public OutlineCategory Categories => categories;
    public IReadOnlyList<Renderer> Renderers => renderers;

    public static IEnumerable<OutlineTag> EnumerateActiveTags() => ActiveTags;

    private void Reset() => RefreshRenderers();

    private void OnValidate()
    {
        if (includeChildren && (renderers == null || renderers.Length == 0)) RefreshRenderers();
    }

    private void OnEnable() => ActiveTags.Add(this);

    private void OnDisable() => ActiveTags.Remove(this);

    [ContextMenu("Refresh Renderers")]
    public void RefreshRenderers() =>
        renderers = includeChildren ? GetComponentsInChildren<Renderer>(true) : GetComponents<Renderer>();

    public bool MatchesEnabledCategories(OutlineCategory enabled) => (categories & enabled) != 0;

    public void SetCategories(OutlineCategory value) => categories = value;
}
