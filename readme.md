# SqlClientSlim

This is a fork of the [Official System.Data.SqlClient](https://github.com/dotnet/corefx) optimized for speed, application size and portability over backwards compatibility.

## Why would I use this?

If you're building a new application and want to use basic scenarios (basic queries, an ORM like EF, etc.) and don't rely on the legacy behavior of ADO.NET (e.g. instance name lookups using SQL Browser), then this library is perfect for you.

Please be aware of the [breaking changes](docs/breaking.md) before you get started, and note that this is NOT an officially supported Microsoft project - it is provided "as is".

## Why wouldn't I want to use this?

* You rely on legacy behavior, or something that was [broken](docs/breaking.md).
* You need official Microsoft support for your application.
* Your application needs to work with both the desktop version of System.Data.dll and a portable version of System.Data.SqlClient.dll.
* You need a stable API surface area.

## What else should I know?

Make sure that you read the [Known Issues](docs/issues.md) page, and browse the [Release Notes](docs/releasenotes.md) to see which version is best for you.