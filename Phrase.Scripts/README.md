# Phrase

This is an initial draft of Proof of Concept.

## Prerequisites

- [.NET Core SDK](https://dotnet.microsoft.com/) to work with F# files and dependencies;
- [Paket](https://fsprojects.github.io/Paket/) to manage dependencies.

## How to use

### Configuration

- `touch "../.env"` — create an environment variables configuration file;
-  add your CSE ID and API key to the configuration, use `cat "../.env.example` for example.

### Paket

- `paket init` — initialize paket bootstraper;
- `paket restore` — install dependencies;
- `paket generate-load-scripts --type fsx` — generate include scripts that reference installed packages in the interactive environment.
