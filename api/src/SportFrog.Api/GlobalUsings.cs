// The vocabulary of the domain, available everywhere in the API.
//
// Global rather than per-file on purpose. These namespaces are not a library
// this project happens to use: they are the words this system is written in —
// a match state, a ruleset, a standings row. Importing them by hand in ninety
// files would say the opposite, that each one made a local choice.
//
// Anything that is not the domain's vocabulary is imported where it is used.

global using SportFrog.Domain.Accreditation;
global using SportFrog.Domain.Competitions;
global using SportFrog.Domain.Documents;
global using SportFrog.Domain.Matches;
global using SportFrog.Domain.Performances;
global using SportFrog.Domain.Rules;
global using SportFrog.Domain.Scheduling;
global using SportFrog.Domain.Serialization;
global using SportFrog.Domain.Standings;
global using SportFrog.Domain.Statistics;
global using SportFrog.Domain.ValueObjects;
