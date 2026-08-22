# Oxide — A Modding IDE for Hearts of Iron IV

**Oxide** is a custom Integrated Development Environment (IDE) designed specifically for modding *Hearts of Iron IV*, the grand strategy game by Paradox Interactive.

The goal of Oxide is to streamline and enhance the modding experience by making it easier to visualize, edit, and navigate the complex web of files and references that make up a HoI4 mod.

---

## ⚙️ Project Goals (Early Stage)

- Provide a unified workspace for HoI4 modding.
- Parse and visualize commonly referenced entities (states, focuses, events, decisions, etc.).
- Enable seamless navigation between related content across files.
- Offer syntax validation, reference checking, and editing tools.
- Lay the groundwork for visual editing of focus trees, events, and map elements.

---

## 🚧 Project Status

This project is in early development. The first read-only vertical slice is
working: Oxide can open a game installation and optional active mod, build an
immutable semantic workspace, browse and search states, inspect provenance, and
show workspace and semantic problems.

---

## 🛠 Built With

- [.NET 9](https://dotnet.microsoft.com/)
- [Avalonia UI](https://avaloniaui.net/)
- C#
- [GitHub Projects](https://github.com/features/project-management/) for planning and issue tracking

---

## Getting started

The repository pins the .NET 9 SDK in `global.json`. The canonical local
verification command is:

```sh
./scripts/verify.sh
```

It restores the solution, builds Debug and Release, runs all normal tests,
checks formatting and repository contents, and generates the synthetic corpus
summary. To run the individual development commands manually:

```sh
dotnet restore Oxide.sln
dotnet build Oxide.sln --no-restore
dotnet test Oxide.sln --no-build
dotnet run --project Oxide.App/Oxide.App.csproj
```

The first three commands form the local build-and-test workflow. The final
command launches the desktop application.

Tests requiring a local HOI4 installation are deliberately separate:

```sh
./scripts/verify-external-corpus.sh /path/to/hoi4 [optional-mod-root]
```

---

## 🧪 Planned Features

- **State browser** with cross-reference listing (events, decisions, focuses, etc.)
- **Focus tree visual editor**
- **Event scripting interface**
- **Mod validation and linting**
- **Custom mod templates and wizards**

---

## 📂 Solution structure

- `Oxide.Syntax`: source text, tokens, lossless syntax trees, and parsing.
- `Oxide.Core`: workspace, semantic, diagnostic, and application services.
- `Oxide.App`: Avalonia composition and presentation.
- `Oxide.Tests`: unit, integration, and architecture tests.
- `docs/architecture`: product and architecture decisions.

Dependencies point inward: `Oxide.App` depends on `Oxide.Core`, and
`Oxide.Core` depends on `Oxide.Syntax`. Neither core project may depend on
Avalonia.

---

## 📜 License

This project is licensed under the [MIT License](LICENSE).

---

## 💬 Contributing

Contributions are welcome once the core systems are established.  
If you're interested in helping build Oxide, feel free to open an issue or discussion.

---

## 🔗 Related Projects & Resources

- [Hearts of Iron IV Modding Wiki](https://hoi4.paradoxwikis.com/Modding)
- [CWTools](https://github.com/cwtools/cwtools) — Clausewitz script tooling (used by Paradox language extensions)
- [Paradox Interactive](https://www.paradoxinteractive.com/)

---

## 👤 Author

Caleb Morse  
*Developer, designer, and HoI4 modding enthusiast*

---

## 🧭 Vision

Modding *Hearts of Iron IV* shouldn't feel like navigating a minefield of scattered files.  
**Oxide** aims to bring clarity, cohesion, and creativity to the modding process — so you can focus on building your vision, not fighting the tools.
