# Risks

## Accepted risks

### RISK-001: Vulnerable ImageSharp dependency in the Storefront

**Owner:** developer

The Storefront and its unit-test project suppress NuGet Audit findings for ImageSharp
3.1.12 from the Optimizely CMS 13.1.3 dependency graph:

- `GHSA-gwg2-r3hj-4w44`
- `GHSA-j3p4-wp97-rph4`
- `GHSA-j9gm-c75j-xc9q`
- `GHSA-jjfr-hcj7-qf5w`
- `GHSA-wmxv-xphr-5c9g`

The dependency supports CMS thumbnails and image-editor processing. Malformed image
content can cause excessive memory use, a non-progressing image decode, or memory-safety
failures. The dependency stays in the application while Optimizely CMS requires
ImageSharp versions from 3.1.12 up to, but not including, version 4.0.0.

The suppressions are specific to these five advisory URLs. NuGet Audit remains enabled,
and a new advisory still fails the build. ImageSharp 4.1.2 or later fixes these findings,
but the current Optimizely dependency constraint excludes it and the project does not
have the required Six Labors license.

Remove the suppressions when Optimizely provides a supported ImageSharp dependency path
that resolves the advisories, or when the team replaces the affected image-processing
integration with an approved alternative.
