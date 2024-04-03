<p align="center">
	<img src="https://raw.githubusercontent.com/4Players/odin-sdk-unity/master/Editor/Editor%20Default%20Resources/odinbanner.png" alt="OdinWrapper" width="200" /><br>
	<b>Version 1</b><br>
	C# wrapper for ODIN<br>
	cross-platform client library aki
</p>

<p align="center">
	<a href="https://developers.4players.io/odin">
		<img src="https://img.shields.io/badge/documentation-%F0%9F%94%8D-blue?style=flat" />
	</a>
		<a href="https://4np.de/discord">
		<img src="https://img.shields.io/discord/803630432150224957.svg?style=flat&logo=discord&label=discord" />
	</a>
	<a href="mailto:odin@4players.io">
		<img src="https://img.shields.io/badge/email-odin@4players.io-blue.svg?style=flat" />
	</a>
	<a href="https://twitter.com/4PlayersBiz">
		<img src="https://img.shields.io/badge/twitter-@4PlayersBiz-blue.svg?style=flat&logo=twitter" />
	</a>
	<a href="../../blob/master/LICENSE">
		<img src="https://img.shields.io/github/license/4Players/odin-wrapper-csharp.svg?style=flat" />
	</a>
</p>

# 4Players ODIN SDK C# Wrapper

ODIN SDK Wrapper is for the dotnet framework based on native ODIN.

ODIN is a cross-platform software development kit (SDK) that enables developers
to integrate real-time chat technology into multiplayer games, apps and
websites.

## Getting Started

The current release of ODIN C# Wrapper is a
[Shared Project](https://docs.microsoft.com/en-us/xamarin/cross-platform/app-fundamentals/shared-projects).

This project depends on the release of
[ODIN SDK](https://github.com/4Players/odin-sdk#getting-started) which is
shipped with native pre-compiled binaries.

To check out the SDK for development, head over to
[4Players ODIN SDK](https://github.com/4Players/odin-sdk).

## How to use

1. Clone the repository with
   `git clone https://github.com/4Players/odin-wrapper-csharp.git`

2. and use the project as a Shared Project with
   `Add Reference.. -> Shared Projects`

3. Add the native Odin library for your platform from
   [ODIN SDK](https://github.com/4Players/odin-sdk#getting-started) to the
   project

The `OdinClient`-Class is the convenient client wrapper for ODIN ffi. Available
with the pre-compiled managed library of your target framework or Shared
Project.

For your own Client use the internal function `OdinLibrary.Api` which is
currently only in a Shared Project reference available.

_The Project includes meta files for Unity Assets (2019.4+) as well as
LogException, LogAssertions, ... support. Until - "Future versions of Unity are
expected to always throw exceptions"._

Check out the [Online Documentation](https://developers.4players.io/odin).

## Troubleshooting

Maybe checkout our
[Odin Unity integration](https://github.com/4Players/odin-sdk-unity) that uses
the wrapper as a shared project
[here](https://github.com/4Players/odin-sdk-unity/tree/master/Runtime/OdinWrapper).

Contact us through the listed methods below to receive answers to your questions
and learn more about ODIN.

### Discord

Join our official Discord server to chat with us directly and become a part of
the 4Players ODIN community.

[![Join us on Discord](https://developers.4players.io/images/join_discord.png)](https://4np.de/discord)

### Twitter

Have a quick question? Tweet us at
[@4PlayersBiz](https://twitter.com/4PlayersBiz) and we’ll help you resolve any
issues.

### Email

Don’t use Discord or Twitter? Send us an [email](mailto:odin@4players.io) and
we’ll get back to you as soon as possible.
