using System.Globalization;
using System.Text;

namespace Waypoint.Api.Pagination;

public static class Cursor
{
    public static string Encode(DateTimeOffset sortValue, Guid id)
    {
        var raw = $"{sortValue.ToUnixTimeMilliseconds()}|{id}";
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(raw));
    }

    public static (DateTimeOffset SortValue, Guid Id) Decode(string cursor)
    {
        try
        {
            var raw = Encoding.UTF8.GetString(Convert.FromBase64String(cursor));
            var parts = raw.Split('|');
            if (parts.Length != 2) throw new FormatException("Cursor must have two segments.");
            var ts = DateTimeOffset.FromUnixTimeMilliseconds(long.Parse(parts[0], CultureInfo.InvariantCulture));
            var id = Guid.Parse(parts[1]);
            return (ts, id);
        }
        catch (Exception ex) when (ex is not FormatException)
        {
            throw new FormatException("Malformed cursor", ex);
        }
    }
}
