namespace ProjectTemplate.Web.Extensions;

/// <summary>
/// Gives list options bound from configuration replace semantics instead of append semantics.
/// </summary>
/// <remarks>
/// The configuration binder adds configured entries to a list that already holds code defaults, so a configured list
/// could only ever extend the defaults and never remove one. A configured list replaces the defaults here instead.
/// Blank entries are ignored, which lets a higher-precedence configuration source remove an inherited entry by
/// overriding its index with an empty value. When the section has no entries, the code defaults are kept.
/// </remarks>
internal static class ConfigurationListBinding
{
    /// <summary>
    /// Replaces the contents of <paramref name="target"/> with the non-blank values configured in
    /// <paramref name="section"/>, when the section has any entries.
    /// </summary>
    /// <param name="target">The bound list to replace.</param>
    /// <param name="section">The configuration section that holds the list entries.</param>
    internal static void ReplaceWithConfiguredValues(List<string> target, IConfigurationSection section)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(section);

        List<string> configuredValues = [];
        bool hasConfiguredEntries = false;

        foreach (IConfigurationSection child in section.GetChildren())
        {
            hasConfiguredEntries = true;

            if (!string.IsNullOrWhiteSpace(child.Value))
            {
                configuredValues.Add(child.Value);
            }
        }

        if (!hasConfiguredEntries)
        {
            return;
        }

        target.Clear();
        target.AddRange(configuredValues);
    }
}
