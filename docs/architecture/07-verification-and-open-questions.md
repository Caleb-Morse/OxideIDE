# Verification and open questions

Oxide must distinguish architectural decisions from claims about undocumented
game-engine behavior. Resolution rules become authoritative only after they are
backed by a fixture or a reliable versioned source.

## Fixture strategy

Create tiny synthetic mods that isolate one behavior at a time. Each fixture
contains base-like input, dependency input, active-mod input, a documented
playset, and the expected effective declarations and diagnostics. Where
possible, compare expectations with the game's error log and observable result.

Required fixture families:

- same virtual filename across content layers;
- distinct filenames declaring the same entity ID;
- filename ordering within a directory;
- `replace_path` at exact, parent, and nested paths;
- multiple dependency mods and dependency ordering;
- DLC versus base definitions;
- duplicate event, focus, state, sprite, localisation, and balance-of-power IDs;
- additive structures such as on-actions; and
- invalid syntax before and after a valid declaration.

## Open questions

1. How is the active launcher playset and dependency order obtained reliably on
   each supported platform?
2. Which DLC roots are active, and what is their precise position in load order?
3. Which directories replace files by virtual path, merge all files, or apply
   special loaders?
4. What comparison and filename ordering rules are platform-independent?
5. Which entity types merge properties rather than replacing whole definitions?
6. How do `replace_path` rules compose across several mods?
7. Which encodings occur in real mods beyond UTF-8 with or without a BOM?
8. Which scripted constructs can create declarations or references that static
   analysis cannot enumerate?
9. What is the smallest safe override for every editable entity kind?

## Confidence metadata

Each game profile rule records its source, HoI4 version range, confidence
(`Verified`, `Documented`, `Observed`, or `Assumed`), and fixture IDs. Assumed
rules produce visible uncertainty in developer diagnostics and cannot silently
drive destructive refactorings.

## Acceptance criteria for implementation

- Full and incremental rebuilds produce equivalent semantic snapshots.
- Every effective value links to source provenance and its resolution policy.
- Missing, ambiguous, hidden, and shadowed definitions remain inspectable.
- Unchanged parse/emit is lossless across the encoding fixture suite.
- Failed multi-file edits leave every source file unchanged.
- No UI component implements game load-order or identity rules.
