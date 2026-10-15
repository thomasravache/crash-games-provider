using System.Runtime.Versioning;

// The deployment runs on Linux (the VPS, ADR-0029): the tests drive bash scripts, Docker and Unix file modes.
[assembly: SupportedOSPlatform("linux")]
[assembly: SupportedOSPlatform("macos")]
