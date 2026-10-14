# Alpha Genes - Germline Mutator Blacklist

Choose which genes Alpha Genes' germline mutator is never allowed to give. RimWorld 1.7 only.

**Upstream behaviour.** `AlphaGenes.CompTargetEffect_GermlineMutator.DoEffectOn` adds one random gene to the target pawn.
It picks from every loaded gene that has exclusion tags, has no prerequisite, is not tagged `AG_OnlyOnCharacterCreation`
and whose defName does not contain `VREA_`. With a large gene-mod list that pool includes android parts, astrogenes,
sleeve quality genes and other genes that make no sense as a random mutation.

**What this does.** A Harmony postfix on Alpha Genes' own gene filter (the lambda inside `DoEffectOn`) turns its answer
to false for blocked genes. The pick, the item being used up and the gene removal coma all stay Alpha Genes' code;
nothing of it is reimplemented here. A gene is blocked when it is on a default list or the player ticked it, and the
player has not unticked it. The filter only runs when a mutator is used.

**Default lists.**
- `1.7/Defs/GermlineMutatorBlacklist.xml` holds a `GermlineMutatorBlacklist.GermlineBlacklistDef` with `genes` (gene
  defNames) and `defNameContains` (defName fragments). All loaded defs of that type are merged, so another mod can add
  its own list; patch the def to change or replace it. Genes that are not loaded are skipped.
- Alpha Genes' own wretch blacklist (`AlphaGenes.WretchBlacklistDef`) counts as a second default list. A tick box in
  the settings switches that off.

**Settings.** Options > Mod settings lists every gene Alpha Genes' filter accepts, with its icon, a search box (name,
defName or mod), a Show all/blocked/allowed switch, and Block shown / Allow shown / Reset to defaults. Only departures
from the defaults are saved, so later changes to a default list still apply. Changes take effect the next time a mutator
is used; no restart. If every gene ends up blocked the menu says so, because the mutator would then have nothing to give.

| Path | Contents |
|---|---|
| `1.7/Assemblies/GermlineMutatorBlacklist.dll` | net10.0 build |
| `1.7/Defs/GermlineMutatorBlacklist.xml` | the default list (Too Many Mods' original 9 genes and 4 name fragments; not yet checked against current gene mods) |
| `Source/` | `GermlineFilter.cs` (def, filter patch, blocked-gene cache), `BlacklistMod.cs` (settings and menu). Not deployed. |

**Build.** `dotnet build -c Release` in `Source`. Alpha Genes has no 1.7 build yet, so the csproj references its 1.6 DLL;
repoint and rebuild once it ships. If Alpha Genes changes `DoEffectOn` so the filter lambda can no longer be found, the
mod logs a warning, blocks nothing, and says so at the top of its settings menu.

**Origin.** Split out of Too Many Mods - Compats and Rebalances by Warachia (`AlphaGenes_BlacklistForGermlineMutator.dll`),
where a Harmony prefix replaced the whole of `DoEffectOn` and used Alpha Genes' wretch blacklist, extended by XML patches,
as its only list. Rewritten 2026-10-13 as a filter with its own default list and a settings menu. The XML additions to the
wretch blacklist themselves (which also affect wretch generation) stayed in MMMM Rebalances - Genetics.
Untested in-game.
