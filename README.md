# MiNET

## Overview

MiNET is a Minecraft Bedrock server project written in C#.

The project is currently experimental. It is not designed for production use, and there is no guarantee of stability, compatibility, or long-term maintenance at this stage.

The current goal is to progressively rework the architecture, make the code easier to understand, and prepare a cleaner foundation for future Bedrock protocol and plugin API work.

## Current Changes

Compared to the original fork, the main changes so far are:

- reworked and split several large classes to make the code easier to understand;
- progressively separated responsibilities in `Player` and `Level`;
- introduced an initial event foundation for `Player`, `Entity`, and `Level`;
- migrated the project to .NET 10;
- added support for Minecraft Bedrock 1.26.10 (protocol v944).

## Goals

The main goals of the project are:

- implement all packets required by the targeted Minecraft Bedrock protocol;
- build a complete event system for players, entities, worlds, server lifecycle, inventories, and plugins;
- clean up the project and progressively remove old or unused secondary projects such as `TestPlugin`, `Plotter`, `BuilderBase`, and similar legacy folders;
- properly rework the Bedrock protocol implementation;
- make the architecture clearer and easier to maintain.

## Contribution

Contributions are welcome.

AI usage is not forbidden. AI has already been used on this project to help with refactoring, documentation, and code exploration. Contributions are still expected to be reviewed, understandable, and testable.

Before proposing a large change, keep in mind that the project is actively being reworked and that some areas, especially the generated protocol code, may be sensitive.

## Credits

This project is based on the original work of Niclas Olofsson and the MiNET project.

Credits also go to CRPE-Team, from whose project this repository was forked.
