using Avalonia;
using Avalonia.Controls;
using Lucia.Companion.Core;

namespace Lucia.Companion.App.Controls;

/// <summary>Retains named attention buttons by place identity so reports do not interrupt focus or a press.</summary>
public sealed class AttentionList : UserControl
{
    private readonly List<Button> rows = [];
    private readonly TextBlock heading = new() { Classes = { "section" }, TextWrapping = Avalonia.Media.TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 6), Focusable = true };
    private readonly StackPanel stack = new() { Spacing = 6 };
    public event Action<string>? PlaceInvoked;
    public string Heading { get; private set; } = string.Empty;
    public IReadOnlyList<Button> Rows => rows;

    public AttentionList() { stack.Children.Add(heading); Content = stack; }

    public void SetPlaces(IReadOnlyList<Place> places, bool isDemo = true, DateTimeOffset? reportTime = null)
    {
        ArgumentNullException.ThrowIfNull(places);
        Heading = places.Count > 0 ? "Needs you" : isDemo ? "Nothing needs you in this sample" : "No configured reports flagged";
        heading.Text = Heading;
        var previous = rows.OfType<AttentionPlaceRow>().GroupBy(row => (string)row.Tag!)
            .Where(group => group.Count() == 1).ToDictionary(group => group.Key, group => group.Single());
        var counts = places.GroupBy(place => place.Id).ToDictionary(group => group.Key, group => group.Count());
        var next = places.Select((place, index) =>
        {
            var row = counts[place.Id] == 1 && previous.TryGetValue(place.Id, out var retained)
                ? retained : new AttentionPlaceRow(id => PlaceInvoked?.Invoke(id));
            row.Update(place, reportTime, lead: index == 0);
            return row;
        }).ToArray();
        var focused = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement();
        var removedFocus = rows.Any(row => ReferenceEquals(row, focused) && !next.Contains(row));
        foreach (var obsolete in rows.Where(row => !next.Contains(row)).ToArray()) { stack.Children.Remove(obsolete); }
        for (var index = 0; index < next.Length; index++)
        {
            var current = stack.Children.IndexOf(next[index]);
            if (current < 0) { stack.Children.Insert(index + 1, next[index]); }
            else if (current != index + 1) { stack.Children.Move(current, index + 1); }
        }
        rows.Clear(); rows.AddRange(next);
        if (removedFocus) { if (rows.Count > 0) { rows[0].Focus(); } else { heading.Focus(); } }
    }
}
