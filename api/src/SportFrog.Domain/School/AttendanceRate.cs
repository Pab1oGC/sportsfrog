using System.Text.Json.Serialization;

namespace SportFrog.Domain.School;

/// <summary>What a student's mark on one dictated session was.</summary>
/// <remarks>
/// Four states rather than a boolean, for the same reason <see
/// cref="Matches.MatchState"/> is not one: "late" is not the same fact as
/// "present", and an absence excused in advance is not the same fact as one
/// that was not. Collapsing them would make the rate right but the record
/// wrong — a parent asking why their child was marked absent on a day they
/// had already asked for leave deserves a different answer than "the
/// attendance dropped".
/// </remarks>
[JsonConverter(typeof(SnakeCaseEnumConverter<AttendanceState>))]
public enum AttendanceState
{
    /// <summary>Attended, on time.</summary>
    Present,

    /// <summary>Did not attend, with no excuse on record.</summary>
    Absent,

    /// <summary>Attended, after the session had started.</summary>
    Late,

    /// <summary>Did not attend, with an excuse on record.</summary>
    Excused,
}

/// <summary>
/// One dictated session, and what this student's mark on it was — or that
/// nobody recorded one.
/// </summary>
/// <param name="Held">
/// Whether the session actually ran. A session still scheduled, or cancelled
/// outright, asks nothing of a student and so is not a fact about their
/// attendance at all — see <see cref="AttendanceRate"/>'s own remarks.
/// </param>
/// <param name="Mark">
/// Null when the session ran but nobody took attendance for this student.
/// That is a gap in the record, not a fourth kind of absence, and the two
/// must not be told apart by guesswork later.
/// </param>
public sealed record AttendanceEntry(bool Held, AttendanceState? Mark);

/// <summary>
/// How a student did across a set of sessions, reduced to the counts a
/// report prints and the one rate everyone asks for.
/// </summary>
/// <param name="Recorded">
/// How many sessions actually counted toward <see cref="Rate"/> — see <see
/// cref="AttendanceRate"/>'s remarks for which ones that excludes.
/// </param>
public sealed record AttendanceTally(int Recorded, int Present, int Late, int Absent, int Excused)
{
    /// <summary>
    /// Attended (on time or late) over <see cref="Recorded"/>. Null rather
    /// than zero when nothing was recorded — a student with no sessions yet
    /// has no rate, not a zero percent one.
    /// </summary>
    public decimal? Rate => Recorded == 0 ? null : (decimal)(Present + Late) / Recorded;
}

/// <summary>
/// Counts a student's attendance across a course.
/// </summary>
/// <remarks>
/// Two kinds of session never reach the count a rate is built from, and
/// both are deliberate. A cancelled class, or one still in the future,
/// asked nothing of anybody — counting it as an absence would punish a
/// student for a class that did not happen. And a class that <em>was</em>
/// held but whose attendance nobody took is a gap in the record, not
/// evidence the student skipped it — counting that as an absence would
/// turn an operator's missed afternoon into a mark against a child.
///
/// That is also why this reads an explicit <see cref="AttendanceEntry.Held"/>
/// per session rather than being handed only the ones somebody marked:
/// the first exclusion needs to see the sessions that never got a mark at
/// all, to tell "nobody has taken attendance for this course yet" apart from
/// "this student missed every class that ran".
/// </remarks>
public static class AttendanceRate
{
    public static AttendanceTally Resolve(IReadOnlyList<AttendanceEntry> entries)
    {
        var present = 0;
        var late = 0;
        var absent = 0;
        var excused = 0;
        var recorded = 0;

        foreach (var entry in entries)
        {
            if (!entry.Held || entry.Mark is not { } mark)
            {
                continue;
            }

            recorded++;

            switch (mark)
            {
                case AttendanceState.Present:
                    present++;
                    break;
                case AttendanceState.Late:
                    late++;
                    break;
                case AttendanceState.Absent:
                    absent++;
                    break;
                case AttendanceState.Excused:
                    excused++;
                    break;
            }
        }

        return new AttendanceTally(recorded, present, late, absent, excused);
    }
}
