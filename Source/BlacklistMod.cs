using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using Verse;

namespace GermlineMutatorBlacklist
{
    public class BlacklistSettings : ModSettings
    {
        // Only the player's departures from the default lists are stored, so a later change to a default list
        // still reaches them. defNames, so an entry survives its mod being temporarily unloaded.
        public HashSet<string> forcedOn = new HashSet<string>();
        public HashSet<string> forcedOff = new HashSet<string>();
        // Treat Alpha Genes' wretch blacklist as a second default list.
        public bool useWretchBlacklist = true;

        public override void ExposeData()
        {
            Scribe_Collections.Look(ref forcedOn, "forcedOn", LookMode.Value);
            Scribe_Collections.Look(ref forcedOff, "forcedOff", LookMode.Value);
            Scribe_Values.Look(ref useWretchBlacklist, "useWretchBlacklist", true);
            if (forcedOn == null)
            {
                forcedOn = new HashSet<string>();
            }
            if (forcedOff == null)
            {
                forcedOff = new HashSet<string>();
            }
        }

        public bool Blocked(string gene, bool inDefault)
        {
            return forcedOn.Contains(gene) || (inDefault && !forcedOff.Contains(gene));
        }

        public void Set(string gene, bool inDefault, bool blocked)
        {
            forcedOn.Remove(gene);
            forcedOff.Remove(gene);
            if (blocked && !inDefault)
            {
                forcedOn.Add(gene);
            }
            else if (!blocked && inDefault)
            {
                forcedOff.Add(gene);
            }
        }
    }

    public class BlacklistMod : Mod
    {
        public static BlacklistSettings settings;

        private const float RowHeight = 30f;

        private class Row
        {
            public GeneDef gene;
            public string label;
            public string modName;
            public string searchText;
            public string tip;
        }

        private enum Show
        {
            All,
            Blocked,
            Allowed
        }

        private List<Row> rows;
        private readonly List<Row> filtered = new List<Row>();
        private string search = "";
        private string appliedSearch;
        private Show show;
        private Show appliedShow;
        private Vector2 scroll;
        // Recounted only when a tick box changes.
        private bool countsDirty = true;
        private int blockedCount;

        public BlacklistMod(ModContentPack content) : base(content)
        {
            settings = GetSettings<BlacklistSettings>();
            GermlineFilter.Patch(new Harmony("DanZinagri.AlphaGenes.GermlineMutatorBlacklist"));
        }

        public override string SettingsCategory()
        {
            return "Alpha Genes - Germline Mutator Blacklist";
        }

        private static bool IsBlocked(GeneDef gene)
        {
            return settings.Blocked(gene.defName, GermlineFilter.InDefault(gene));
        }

        private void SetBlocked(GeneDef gene, bool blocked)
        {
            settings.Set(gene.defName, GermlineFilter.InDefault(gene), blocked);
            Changed();
        }

        // Takes effect the next time a mutator is used.
        private void Changed()
        {
            GermlineFilter.dirty = true;
            countsDirty = true;
        }

        // Every gene Alpha Genes' own filter accepts: the pool the mutator would roll from without this mod.
        private void BuildRows()
        {
            if (GermlineFilter.dirty)
            {
                GermlineFilter.Rebuild();
            }
            rows = new List<Row>();
            foreach (GeneDef gene in DefDatabase<GeneDef>.AllDefsListForReading)
            {
                if (!GermlineFilter.AlphaGenesAccepts(gene))
                {
                    continue;
                }
                string label = gene.LabelCap;
                string modName = gene.modContentPack?.Name ?? "?";
                rows.Add(new Row
                {
                    gene = gene,
                    label = label,
                    modName = modName,
                    searchText = (label + " " + gene.defName + " " + modName).ToLowerInvariant(),
                    tip = label + " (" + gene.defName + ")\n\n" + gene.description
                });
            }
            rows.Sort((a, b) => string.CompareOrdinal(a.searchText, b.searchText));
        }

        // The list is rebuilt when the search text or the Show mode changes, not on every tick box,
        // so a row does not vanish from under the cursor the moment it is toggled.
        private void ApplyFilter()
        {
            appliedSearch = search;
            appliedShow = show;
            filtered.Clear();
            string needle = search.ToLowerInvariant();
            foreach (Row row in rows)
            {
                if (needle.Length != 0 && !row.searchText.Contains(needle))
                {
                    continue;
                }
                if (show != Show.All && IsBlocked(row.gene) != (show == Show.Blocked))
                {
                    continue;
                }
                filtered.Add(row);
            }
        }

        private void Recount()
        {
            countsDirty = false;
            blockedCount = 0;
            foreach (Row row in rows)
            {
                if (IsBlocked(row.gene))
                {
                    blockedCount++;
                }
            }
        }

        private static string Info(Row row)
        {
            if (GermlineFilter.InListDefault(row.gene))
            {
                return "default  -  " + row.modName;
            }
            if (settings.useWretchBlacklist && GermlineFilter.InWretchBlacklist(row.gene))
            {
                return "wretch blacklist  -  " + row.modName;
            }
            return row.modName;
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            if (rows == null)
            {
                BuildRows();
            }
            if (appliedSearch != search || appliedShow != show)
            {
                ApplyFilter();
            }
            if (countsDirty)
            {
                Recount();
            }

            Text.Font = GameFont.Small;
            Rect header = new Rect(inRect.x, inRect.y, inRect.width, 70f);
            Widgets.Label(new Rect(header.x, header.y, header.width, 22f), "Ticked genes are never given by Alpha Genes' germline mutator. Changes apply straight away.");
            if (GermlineFilter.picker == null)
            {
                GUI.color = Color.red;
                Widgets.Label(new Rect(header.x, header.y + 22f, header.width, 22f), "Alpha Genes' gene filter was not found (it may have been updated), so nothing is being blocked.");
            }
            else if (blockedCount >= rows.Count)
            {
                GUI.color = Color.red;
                Widgets.Label(new Rect(header.x, header.y + 22f, header.width, 22f), "Every possible gene is blocked: the mutator has nothing left to give. Allow at least one.");
            }
            else
            {
                Widgets.Label(new Rect(header.x, header.y + 22f, header.width, 22f), blockedCount + " of " + rows.Count + " possible genes blocked.");
            }
            GUI.color = Color.white;
            bool useWretch = settings.useWretchBlacklist;
            Widgets.CheckboxLabeled(new Rect(header.x, header.y + 44f, 520f, 24f), "Also block everything on Alpha Genes' wretch blacklist by default", ref useWretch);
            if (useWretch != settings.useWretchBlacklist)
            {
                settings.useWretchBlacklist = useWretch;
                Changed();
                appliedSearch = null;
            }

            Rect bar = new Rect(inRect.x, header.yMax + 4f, inRect.width, 28f);
            search = Widgets.TextField(new Rect(bar.x, bar.y, 220f, bar.height), search);
            if (Widgets.ButtonText(new Rect(bar.x + 226f, bar.y, 140f, bar.height), "Show: " + show.ToString().ToLowerInvariant()))
            {
                show = (Show)(((int)show + 1) % 3);
            }
            if (Widgets.ButtonText(new Rect(bar.xMax - 370f, bar.y, 110f, bar.height), "Block shown"))
            {
                foreach (Row row in filtered)
                {
                    SetBlocked(row.gene, true);
                }
            }
            if (Widgets.ButtonText(new Rect(bar.xMax - 256f, bar.y, 110f, bar.height), "Allow shown"))
            {
                foreach (Row row in filtered)
                {
                    SetBlocked(row.gene, false);
                }
            }
            if (Widgets.ButtonText(new Rect(bar.xMax - 142f, bar.y, 142f, bar.height), "Reset to defaults"))
            {
                settings.forcedOn.Clear();
                settings.forcedOff.Clear();
                settings.useWretchBlacklist = true;
                Changed();
                appliedSearch = null;
            }

            Rect outRect = new Rect(inRect.x, bar.yMax + 6f, inRect.width, inRect.yMax - bar.yMax - 6f);
            Rect viewRect = new Rect(0f, 0f, outRect.width - 16f, filtered.Count * RowHeight);
            Widgets.BeginScrollView(outRect, ref scroll, viewRect);
            // Only the rows on screen are drawn.
            int first = Mathf.Max(0, Mathf.FloorToInt(scroll.y / RowHeight));
            int last = Mathf.Min(filtered.Count - 1, Mathf.CeilToInt((scroll.y + outRect.height) / RowHeight));
            for (int i = first; i <= last; i++)
            {
                Row row = filtered[i];
                Rect rect = new Rect(0f, i * RowHeight, viewRect.width, RowHeight);
                if (i % 2 == 1)
                {
                    Widgets.DrawLightHighlight(rect);
                }
                Widgets.DrawHighlightIfMouseover(rect);

                bool blocked = IsBlocked(row.gene);
                Widgets.CheckboxDraw(rect.x + 4f, rect.y + 3f, blocked, false);
                Widgets.DefIcon(new Rect(rect.x + 34f, rect.y + 1f, RowHeight - 2f, RowHeight - 2f), row.gene);
                GUI.color = Color.white;

                Text.Anchor = TextAnchor.MiddleLeft;
                Widgets.Label(new Rect(rect.x + 70f, rect.y, 250f, RowHeight), row.label);
                GUI.color = Color.gray;
                Widgets.Label(new Rect(rect.x + 326f, rect.y, rect.width - 330f, RowHeight), Info(row));
                GUI.color = Color.white;
                Text.Anchor = TextAnchor.UpperLeft;

                if (Mouse.IsOver(rect))
                {
                    TooltipHandler.TipRegion(rect, row.tip);
                }
                // The whole row is the tick box.
                if (Widgets.ButtonInvisible(rect))
                {
                    SetBlocked(row.gene, !blocked);
                }
            }
            Widgets.EndScrollView();
        }
    }
}
