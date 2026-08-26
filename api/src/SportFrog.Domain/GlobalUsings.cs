// The domain speaks one language across its own folders: a standings row uses
// a ruleset, a ruleset names a score mode, a fixture is placed under schedule
// settings. Splitting that vocabulary into per-file imports would suggest the
// folders are separate libraries, and they are not.

global using SportFrog.Domain.Competitions;
global using SportFrog.Domain.Documents;
global using SportFrog.Domain.Matches;
global using SportFrog.Domain.Rules;
global using SportFrog.Domain.Scheduling;
global using SportFrog.Domain.Serialization;
global using SportFrog.Domain.Standings;
global using SportFrog.Domain.Statistics;
global using SportFrog.Domain.ValueObjects;
