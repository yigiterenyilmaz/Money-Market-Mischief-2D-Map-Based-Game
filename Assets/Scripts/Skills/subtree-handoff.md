# Skill Tree — Subtree Implementation Handoff

Procedure for wiring a stale subtree of the skill tree. Written from doing the
Finance/RealEstate subtree (`a33`); follow the same route for the rest.

---

## 1. Current state

74 skills, 71 in subtrees under three category roots (`a1` Finance, `b1` Politics,
`c1` Media). A "subtree" is the tree rooted at a **direct child of a category root**.

| Subtree | Branch | Size | Wired | Stale |
|---|---|---:|---:|---:|
| `a2` You Could Be Mine | Finance / Mine | 11 | 7 | 4 |
| `a11` Welcome To The Jungle | Finance / Crypto | 7 | 7 | 0 |
| `a20` *(unnamed)* | Finance / Market | 6 | 0 | 6 |
| `a26` *(unnamed)* | Finance / Farming | 7 | 7 | 0 |
| `a33` *(unnamed)* | Finance / RealEstate | 8 | 7 | 1 |
| `b2` / `b3` / `b11` | **Media** (klasör yanlış) | 6 / 4 / 4 | 6 / 4 / 4 | 0 |
| `c2` / `c3` / `c4` | **Politics** (klasör yanlış) | 9 / 6 / 3 | 4 / 6 / 0 | 5 / 0 / 3 |

**Total wired: 58 / 71** (counted from the assets on 2026-08-12: a 35/40, b 15/15, c 11/19,
minus the three roots). Every remaining stale skill in the b/c trees is bribery or mafia —
`c4`, `c6`, `c10`, `c11`, `c12`, `c17`, `c18`, `c20` — and is blocked on the unwritten bribery
mechanic, not on design. See [../Media/media-politics-readme.md](../Media/media-politics-readme.md).

Two cautions on that table:

- The single "wired" skill in each of `b2`/`b3`/`b11` and in `c2`/`c4` is a **demo
  stub**, not real design: `b4`, `b8`, `b13`, `c6`, `c12`, `c17` carry invented
  `StatModifierEffect` active abilities that exist only to exercise the cooldown-ring
  UI. Treat them as stale.
- **The 34 Politics + Media skills are no longer specless.** They shared byte-identical
  `PLACEHOLDER —` descriptions until 2026-08-12, when the user supplied the design;
  every asset now carries a real name and note. Read
  [politics-media-design.md](politics-media-design.md) **before touching either tree** —
  it records the cross-cutting mechanics (bribery, reputation↔suspicion, feed
  manipulation) and three open decisions, the loudest being that **`b*` is Media and
  `c*` is Politics**, the opposite of what the folders and `SkillBranch` flags say.

`a20` and the two trees above are the remaining work. Each skill's only spec is a
one-line Turkish note in its asset `description`.

`a26` (Farming) is done — see `Assets/Scripts/Map/crop-depot-readme.md` for what that
subtree built, which numbers were invented, and the one note (`a32` "yumaklı files")
that had to be implemented from an interpretation rather than a readable spec.

`a11` (Crypto) is done — see `Assets/Scripts/Trading/trading-readme.md`. It turned the
previously decorative `CandlestickChart` into a real trade screen and gated it behind
the skill. Note for `a20`: that subtree's own readme flagged "no screen where the player
trades by hand" as a gap — `a11` fills it, so build on `TradingSystem` rather than a
second buy/sell path. `a15` ("sidik yarışı") was specified verbally by the user
mid-session, not from the asset note; the spec is recorded in the readme.

---

## 2. How the skill system works

Nothing about the new skills is bespoke — they ride the existing machinery.

**Purchase flow.** Node hold-to-buy → `SkillEvents.OnSkillUnlockRequested` →
`SkillTreeManager.TryUnlock` (checks prerequisites, cost via `GameStatManager`,
blocked list) → `ApplyUnlock` iterates `skill.effects` and calls `Apply()` on each,
each wrapped in try/catch so a broken effect can't block the unlock.

**Effects.** Every effect subclasses `SkillEffect` and implements `Apply()`.
Serialized with `[SerializeReference]`, so the asset stores a class name, not a GUID.

**Active abilities.** `Skill.activeAbility` (`enabled`, `abilityName`, `description`,
`cooldownSeconds`, `onActivate` list). Clicking an owned node calls
`SkillTreeManager.TryActivate`, which runs `onActivate` and starts the cooldown.
Use this for "opens a tool" skills. `cooldownSeconds: 0` is legal and renders a
static ring; a non-zero cooldown renders a filling, pulsing ring.

**Tree rendering.** Nodes come from `SkillTreeLayout.asset` + `SkillDatabase.asset`.
Both already contain every skill — **you do not add nodes**, only wire effects.

**The house pattern for anything substantial:** the effect flips a switch on a
separate MonoBehaviour that owns the behaviour. Precedent: `UnlockMinigameEffect` →
`MinigameManager`, feed effects → `SocialMediaManager`, `UnlockRealEstateEffect` →
`RealEstateSystem`. Don't put gameplay in the effect class.

---

## 3. Procedure

**Step 1 — Map the subtree.** Resolve prerequisite GUIDs to ids and print the tree
with each skill's note. Don't work from the folder layout alone; it agrees with the
graph today but the graph is the truth.

**Step 2 — Read every note first, then classify.** For each stale skill decide which
bucket it lands in:

- *decision-free wiring* — an existing effect type maps 1:1 onto the note
- *needs balance numbers* — effect exists, values are the user's call
- *needs new content assets* — e.g. a new `PassiveIncomeProduct`
- *needs a new system* — a new effect class plus a MonoBehaviour
- *nothing to implement from* — the note is too vague

**Step 3 — Ask before inventing.** The user asked to be consulted at each decision
point. Keep questions few and offer a recommendation; they pushed back on long option
lists. Genuine decisions: what a vague note means, cost/income magnitudes, whether a
mechanic is one-off or recurring. Not decisions: which class to subclass, file layout.

**Step 4 — Wire, compile, report.** Wire one skill at a time, compile (§5), and say
plainly which numbers you invented.

**Step 5 — Document anything half-built.** If a skill depends on a system that
doesn't exist yet, write a readme next to the code and state the gap in the class
docstring *and* the inspector tooltip. Precedent: `Assets/Scripts/Stats/trust-system-readme.md`.
A skill that silently does nothing is worse than one that's obviously unfinished.

---

## 4. Wiring an asset

Skill assets are Unity YAML. `[SerializeReference]` effects live in a `references`
block keyed by `rid`. To add an effect, append to `effects` **and** to `RefIds`:

```yaml
  effects:
  - rid: 7300000000000000021
  otherPrerequisites: 0
  blocksSkills: []
  references:
    version: 2
    RefIds:
    - rid: 7300000000000000021
      type: {class: PermanentPassiveIncomeEffect, ns: , asm: Assembly-CSharp}
      data:
        incomePerSecond: 30
```

An active ability sits between `effects` and `otherPrerequisites`, with its own rid
in the same `RefIds` block:

```yaml
  activeAbility:
    enabled: 1
    abilityName: Şehir Kur
    description: Haritada boş araziye sınır çizip yeni bir şehir bölgesi kurar.
    cooldownSeconds: 0
    onActivate:
    - rid: 7300000000000000027
```

Rules that matter:

- **`rid` values only need to be unique within one file.** Hand-authored rids are an
  established convention here (`73000000000000000xx`). Reusing the same numbers in a
  different asset is fine.
- **`ns` is empty and `asm` is `Assembly-CSharp`** — all effect classes are global
  namespace in the main assembly.
- **`data:` holds the serialized fields.** A field-less effect serializes as
  `data: {}`; that form had no precedent in this project, so eyeball the asset in the
  Inspector after the first import.
- **Enum fields serialize as integers.** `statType: 4` is Trust. Check the enum order
  before writing a number.

---

## 5. Verifying — you can actually compile

Unity 6000.3.6f1 and VS2022 MSBuild are installed, so don't guess at correctness.
This compiles the real `Assembly-CSharp` against the real Unity assemblies:

```powershell
$out = "<a scratch dir>"
& "C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe" `
  "C:\Users\enesk\Map design\Assembly-CSharp.csproj" /t:Build /p:Configuration=Debug `
  /p:OutputPath="$out\" /p:IntermediateOutputPath="$out\obj\\" /nologo /v:minimal /clp:NoSummary `
  2>&1 | Select-String -Pattern ": error|: warning CS"
Write-Output "EXIT: $LASTEXITCODE"
```

If new `.cs` files aren't in the `.csproj` yet, Unity regenerates it on focus — or
add `<Compile Include="..." />` entries yourself. The `.csproj` is **not** tracked in git
(`.gitignore:55` ignores `*.csproj`), so leaving correct entries in costs nothing and keeps
CLI builds working until Unity next regenerates it. An earlier version of this doc claimed it
was tracked and had to be restored — it isn't.

---

## 6. Hazards

**`StatType` — coordinate before touching.** Skill and event assets store `statType`
as a raw **integer index**. Inserting a value anywhere but the end silently re-points
every existing effect to the wrong stat, with no error and no compile failure.
Current order: `Wealth=0, Suspicion=1, Reputation=2, PoliticalInfluence=3, Trust=4`.
Only ever append. If two agents append independently you get both a merge conflict
and an index collision — agree on ownership first.

**Singletons on the shared `Managers` object.** The house pattern is
`if (Instance != null && Instance != this) { Destroy(gameObject); return; }`. On a
shared object that destroys **every manager on it** — this already caused a bug where
a duplicate component silently killed `SkillTreeManager` and no skill could be bought.
New singletons on `Managers` must `Destroy(this)`, not `Destroy(gameObject)`.
`SkillTreeManager` and `GameStatManager` still have the unsafe form.

**Shared files.** `SkillTreeManager.cs` (its Update loop now also ticks Trust
programs), `SkillNodeView.cs`, `SkillTreeTooltip.cs`, `GameStatManager.cs`,
`UImanager.cs`, and the `MapDecorPlacer` partials. New effect classes and skill
assets never conflict.

**Don't use emoji in TMP text.** LiberationSans lacks most of them; `⚡` produced a
console warning on every tooltip rebuild.

**`Time.deltaTime` vs `unscaledDeltaTime`.** Some flows pause the game
(`GameManager.PauseGame` sets `timeScale = 0`). Anything that must keep moving while
paused — camera smoothing, UI animation — needs `unscaledDeltaTime`.

---

## 7. Known gaps

**No UI consumes the economy systems.** `OnProductsUnlocked`, `OnInvestmentsUnlocked`,
`OnJewelerUnlocked`, `OnTrainingUnlocked`, `BuyProduct`, `BuyInvestment`, `BuyJeweler`,
`StartScientistTraining` have **zero call sites** outside `SkillTreeManager`. So
`a2`/`a3`/`a12`/`a13` unlock storefronts the player cannot open. Any new skill wired to
`PassiveIncomeEffect` or `InvestmentEffect` lands invisible for the same reason.
This matters most for `a20` (Market) and `a11` (Crypto).

**No `ScientistData` assets exist**, so training can never produce a scientist and
`OtherPrerequisite.ThreeScientistsTrained` is permanently unsatisfiable.

**Trust does nothing yet.** The stat accumulates and fires events, but the
risk-buffering behaviour is unwritten — see `trust-system-readme.md`. `a34`/`a36`/`a39`
spend real money for it.

**`WarForOilManager` and `IllegalScientistProviderManager` share one `MiniGameData`**
(`WarForOilMG.asset`); `WarForOilData.asset` is orphaned. Unlocking either unlocks
both and their cooldowns collide. This blocks `a8` and `a10`. The user chose to leave
it — **don't wire a skill to that asset** without raising it again.

---

## 8. Conventions

- Comments in Turkish, `///` summaries on public members. Explain **why**, not what.
- Runtime-built UI (no prefabs). Reuse `SkillTreeUI` + `UISpriteFactory` so screens
  share a visual language.
- Map sprites are pixel art: generate overlay textures with `FilterMode.Point` and
  hard alpha thresholds. `UISpriteFactory` is `Bilinear` + anti-aliased — right for
  the skill tree, wrong on the map.
- Isometric buildings meet the ground at the sprite's **bottom edge**, not its centre.
- Costs are currently left at `0` across the tree by the user's decision; don't invent
  them unless asked.
- New MonoBehaviours need manual scene wiring — say so explicitly when you finish,
  since nothing works until the user adds them.
