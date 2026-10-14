using System;
using System.Collections.Generic;
using System.Reflection;
using AlphaGenes;
using HarmonyLib;
using Verse;

namespace GermlineMutatorBlacklist
{
    // The default blacklist: genes the germline mutator will not give unless the player allows them.
    // Any number of these defs may be loaded (another mod can add its own, or patch this mod's); their lists are merged.
    public class GermlineBlacklistDef : Def
    {
        // defNames, not def references: an entry whose gene is not loaded is skipped without an error,
        // and MayRequire works on each entry.
        public List<string> genes = new List<string>();
        // A gene whose defName contains one of these is blocked too.
        public List<string> defNameContains = new List<string>();
    }

    // Alpha Genes picks the mutator's gene with one filter, a lambda inside CompTargetEffect_GermlineMutator.DoEffectOn
    // (no character-creation-only genes, no VRE Android genes, no prerequisite). This postfixes that lambda and turns
    // its answer to false for blocked genes, so the pick, the coma and everything else in DoEffectOn stay Alpha Genes'.
    // The lambda only runs when a mutator is used.
    public static class GermlineFilter
    {
        // Alpha Genes' filter lambda, or null when Alpha Genes changed and it could not be found.
        public static MethodInfo picker;

        private static object pickerInstance;
        // Set while the settings menu asks Alpha Genes which genes it would accept at all.
        private static bool bypass;

        // Rebuilt after any settings change, the next time it is needed.
        public static bool dirty = true;
        private static readonly HashSet<GeneDef> blocked = new HashSet<GeneDef>();
        private static readonly HashSet<string> defaultNames = new HashSet<string>();
        private static readonly List<string> defaultFragments = new List<string>();
        private static readonly HashSet<string> wretchNames = new HashSet<string>();
        private static readonly List<string> wretchFragments = new List<string>();

        public static void Patch(Harmony harmony)
        {
            picker = FindPicker();
            if (picker == null)
            {
                Log.Warning("[Germline Mutator Blacklist] Could not find Alpha Genes' germline mutator gene filter, so nothing is blocked. Alpha Genes may have been updated.");
                return;
            }
            if (!picker.IsStatic)
            {
                // A lambda without captures lives on a compiler-made singleton, kept in its "<>9" field.
                pickerInstance = AccessTools.Field(picker.DeclaringType, "<>9")?.GetValue(null);
            }
            harmony.Patch(picker, postfix: new HarmonyMethod(typeof(GermlineFilter), nameof(BlockListed)));
        }

        // The only bool(GeneDef) method the compiler generated for DoEffectOn.
        private static MethodInfo FindPicker()
        {
            List<Type> types = new List<Type> { typeof(CompTargetEffect_GermlineMutator) };
            types.AddRange(typeof(CompTargetEffect_GermlineMutator).GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic));
            foreach (Type type in types)
            {
                foreach (MethodInfo method in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
                {
                    if (method.ReturnType != typeof(bool) || !method.Name.Contains("DoEffectOn"))
                    {
                        continue;
                    }
                    ParameterInfo[] parameters = method.GetParameters();
                    if (parameters.Length == 1 && parameters[0].ParameterType == typeof(GeneDef))
                    {
                        return method;
                    }
                }
            }
            return null;
        }

        public static void BlockListed(GeneDef __0, ref bool __result)
        {
            if (__result && !bypass && IsBlocked(__0))
            {
                __result = false;
            }
        }

        public static bool IsBlocked(GeneDef gene)
        {
            if (dirty)
            {
                Rebuild();
            }
            return blocked.Contains(gene);
        }

        // Would Alpha Genes itself accept this gene? Asked through its own filter, with this mod's block lifted.
        public static bool AlphaGenesAccepts(GeneDef gene)
        {
            if (picker == null || (!picker.IsStatic && pickerInstance == null))
            {
                return true;
            }
            bypass = true;
            try
            {
                return (bool)picker.Invoke(pickerInstance, new object[] { gene });
            }
            finally
            {
                bypass = false;
            }
        }

        public static bool InListDefault(GeneDef gene)
        {
            return Matches(gene.defName, defaultNames, defaultFragments);
        }

        public static bool InWretchBlacklist(GeneDef gene)
        {
            return Matches(gene.defName, wretchNames, wretchFragments);
        }

        // Blocked by default: on one of this mod's lists, or (unless switched off) on Alpha Genes' wretch blacklist.
        public static bool InDefault(GeneDef gene)
        {
            return InListDefault(gene) || (BlacklistMod.settings.useWretchBlacklist && InWretchBlacklist(gene));
        }

        private static bool Matches(string defName, HashSet<string> names, List<string> fragments)
        {
            if (names.Contains(defName))
            {
                return true;
            }
            for (int i = 0; i < fragments.Count; i++)
            {
                if (defName.Contains(fragments[i]))
                {
                    return true;
                }
            }
            return false;
        }

        public static void Rebuild()
        {
            dirty = false;
            defaultNames.Clear();
            defaultFragments.Clear();
            wretchNames.Clear();
            wretchFragments.Clear();
            foreach (GermlineBlacklistDef def in DefDatabase<GermlineBlacklistDef>.AllDefsListForReading)
            {
                if (def.genes != null)
                {
                    defaultNames.UnionWith(def.genes);
                }
                if (def.defNameContains != null)
                {
                    defaultFragments.AddRange(def.defNameContains);
                }
            }
            foreach (WretchBlacklistDef def in DefDatabase<WretchBlacklistDef>.AllDefsListForReading)
            {
                if (def.blackListedGenes != null)
                {
                    foreach (GeneDef gene in def.blackListedGenes)
                    {
                        if (gene != null)
                        {
                            wretchNames.Add(gene.defName);
                        }
                    }
                }
                if (def.blackListedDefNameStrings != null)
                {
                    wretchFragments.AddRange(def.blackListedDefNameStrings);
                }
            }
            blocked.Clear();
            BlacklistSettings settings = BlacklistMod.settings;
            foreach (GeneDef gene in DefDatabase<GeneDef>.AllDefsListForReading)
            {
                if (settings.Blocked(gene.defName, InDefault(gene)))
                {
                    blocked.Add(gene);
                }
            }
        }
    }
}
