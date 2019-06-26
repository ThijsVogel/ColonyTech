﻿namespace Colonisation.Classes
{
    [ModLoader.ModManager]
    public static class TechBlocks
    {
        public static ushort OreProcessor;

        public static void ResolveIndices()
        {
            TechBlocks.OreProcessor = TechBlocks.ToIndex("oreprocessor");
        }

        private static ushort ToIndex(string name)
        {
            ushort index;
            if (ItemTypes.IndexLookup.TryGetIndex(name, out index))
                return index;
            Pipliz.Log.WriteWarning<string>("Could not find TechBlock type {0}", name);
            return 0x539;
        }
    }

    [ModLoader.ModManager]
    public static class GeneralBlocks
    {
        public static ItemTypes.ItemType ScoutRallyPoint;
        public static ItemTypes.ItemType AIColonyBanner;

        public static void ResolveIndices()
        {
            GeneralBlocks.ScoutRallyPoint = GeneralBlocks.ToIndex("scoutrallypoint");
            GeneralBlocks.AIColonyBanner = GeneralBlocks.ToIndex("aicolonybanner");
        }

        private static ItemTypes.ItemType ToIndex(string name)
        {
            ItemTypes.ItemType index;
            index = ItemTypes.GetType(name);

            if (index != null)
                return index;
            Pipliz.Log.WriteWarning<string>("Could not find GeneralBlock type {0}", name);
            return null;
        }
    }
}
