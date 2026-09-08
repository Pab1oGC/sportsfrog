namespace SportFrog.Api.Features.Draw;

/// <summary>Resolves the draw for a competition's declared format.</summary>
internal interface ICalendarDrawRegistry
{
    ICalendarDraw For(string format);
}
