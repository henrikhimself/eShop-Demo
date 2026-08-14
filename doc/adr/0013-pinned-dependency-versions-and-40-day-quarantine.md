# 0013 - Pinned dependency versions, updated only with a 40-day quarantine

## Status

Accepted

## Context

The team depends on many external packages: NuGet packages for the .NET projects,
npm packages for the Seller Portal Web frontend. A compromised version of a
dependency is a supply chain attack. An automatic version update can pull a
compromised version into the system before anyone reviews it. The community
usually finds and reports a compromised version within its first weeks of release.

## Decision

The team pins every dependency to one exact version. No tool updates a dependency
version automatically. A team member updates a dependency version only through a
deliberate, reviewed action.

By default, the team does not update a dependency to a version less than 40 days
old. The 40-day wait gives the community time to find and report a supply chain
attack in a new version before the team adopts it.

The team makes an exception to the 40-day wait for a very high priority update,
for example a fix for a vulnerability under active exploitation. The team weighs
the risk of the known, exploited vulnerability against the risk of an unvetted new
version, case by case, for this kind of exception.

## Consequences

- Every dependency file pins an exact version: `Version="..."` entries in
  `Directory.Packages.props` for .NET packages, exact (no `^`/`~`) entries in
  `package.json` for the Seller Portal Web frontend.
- `.npmrc` sets `save-exact=true`, so `pnpm add`/`pnpm update` write an exact
  pin instead of a range.
- The team does not run a dependency update tool (for example Dependabot or
  Renovate) in an auto-merge configuration. Such a tool may still open a proposal
  for a team member to review; the team never lets it merge on its own.
- Before an update, the team checks the new version's release date against the
  40-day default wait, unless the update is a very high priority exception.
- The team has not yet automated the 40-day check. The team performs this check by
  hand until the team builds tooling for it.
