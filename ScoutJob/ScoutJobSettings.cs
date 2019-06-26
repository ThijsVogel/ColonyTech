﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Jobs;
using NPC;
using Pipliz;
using BlockTypes;

namespace Colonisation.ScoutJob
{
    class ScoutJobSettings : Jobs.IBlockJobSettings
    {
        public virtual string NPCTypeKey { get { return "colonisation.scout"; } }

        public virtual float CraftingCooldown { get; set; }

        public ItemTypes.ItemType[] BlockTypes { get; }

        public virtual NPCType NPCType { get; set; }

        public InventoryItem RecruitmentItem { get; }

        public bool ToSleep { get; }

        public float NPCShopGameHourMinimum => TimeCycle.Settings.SleepTimeEnd;

        public float NPCShopGameHourMaximum => TimeCycle.Settings.SleepTimeStart;

        public ScoutJobSettings(string Name, string jobName, InventoryItem inventoryItem)
        {
            ScoutJobHelper.WriteLog("Setting up settings for " + Name + " (" + jobName + ")");
            BlockTypes = new ItemTypes.ItemType[] { Classes.GeneralBlocks.ScoutRallyPoint };
            NPCType = NPCType.GetByKeyNameOrDefault(NPCTypeKey);
            CraftingCooldown = 5f;
        }

        public Vector3Int GetJobLocation(BlockJobInstance instance)
        {
            ScoutJobInstance scoutJobInstance = (ScoutJobInstance)instance;

            if (!scoutJobInstance.StockedUp)
            {
                ScoutJobHelper.WriteLog("Not stocked up");
                scoutJobInstance.SetActivity(ScoutJobGlobals.ScoutActivity.Restocking);
                return scoutJobInstance.Position;
            }

            //If it's almost sunset, don't move, NPC will start preparing base for the night
            if ((TimeCycle.TimeTillSunSet.GetRealTimeSpan().Seconds) < 10)
            {
                ScoutJobHelper.WriteLog("Set up Camp!");
                scoutJobInstance.SetActivity(ScoutJobGlobals.ScoutActivity.SetUpCamp);
                return scoutJobInstance.Position;
            }

            if (scoutJobInstance.Activity == ScoutJobGlobals.ScoutActivity.Walking)
            {
                bool canPath = false;
                Vector3Int newDestination;

                AI.PathingManager.TryGetClosestPositionWorldNotAt(scoutJobInstance.currentDestination, scoutJobInstance.Position, out canPath, out newDestination);

                if (canPath)
                {
                    return scoutJobInstance.currentDestination;
                }
                else
                {
                    //We can't reach this destination, register it as scouted for now
                    scoutJobInstance.getScoutChunkManager().RegisterPositionScouted(scoutJobInstance.currentDestination);
                }
            }

            if (findClosestUnscoutedChunk(scoutJobInstance, out Vector3Int targetLocation))
            {
                scoutJobInstance.Activity = ScoutJobGlobals.ScoutActivity.Walking;

                scoutJobInstance.currentDestination = targetLocation;

                return targetLocation;
            }
            else
            {
                ScoutJobHelper.WriteLog("Nothing to scout found.");
                return scoutJobInstance.Position;
            }
        }

        public bool findClosestUnscoutedChunk(BlockJobInstance instance, out Vector3Int checkedPosition)
        {
            ScoutJobInstance scoutJobInstance = (ScoutJobInstance)instance;

            Vector3Int output = scoutJobInstance.NPC.Position;

            int y = 64;

            if (scoutJobInstance.PathState == ScoutJobInstance.PathingState.Started)
            {
                scoutJobInstance.pathingX = scoutJobInstance.GetScoutBanner().Position.x;
                scoutJobInstance.pathingZ = scoutJobInstance.GetScoutBanner().Position.z;
            }

            scoutJobInstance.xStart = scoutJobInstance.pathingX;
            scoutJobInstance.zStart = scoutJobInstance.pathingZ;

            checkedPosition = new Vector3Int(scoutJobInstance.pathingX, y, scoutJobInstance.pathingZ);

            bool canPath;

            if (!AI.PathingManager.TryGetClosestPositionWorldNotAt(scoutJobInstance.currentDestination, scoutJobInstance.Position, out canPath, out checkedPosition))
            {
                return false;
            }

            if (!scoutJobInstance.ChunkManagerHasChunkAt(scoutJobInstance.pathingX, y, scoutJobInstance.pathingZ, out checkedPosition) &&
                scoutJobInstance.IsOutsideMinimumRange(new Vector3Int(scoutJobInstance.pathingX, y, scoutJobInstance.pathingZ), scoutJobInstance.GetScoutBanner()))
            {
                return true;
            }

            bool foundChunk = false;

            while (scoutJobInstance.CoordWithinBounds(scoutJobInstance.pathingX, scoutJobInstance.pathingZ, scoutJobInstance.GetScoutBanner().Position.x, scoutJobInstance.GetScoutBanner().Position.z, scoutJobInstance.MaxChunkScoutRange * 16))
            {
                switch (scoutJobInstance.PathState)
                {
                    case ScoutJobInstance.PathingState.Started:
                        scoutJobInstance.stepAmount = 1;
                        scoutJobInstance.Direction = ScoutJobInstance.PathingDirection.NORTH;
                        scoutJobInstance.PathState = ScoutJobInstance.PathingState.Stepping;
                        break;
                    case ScoutJobInstance.PathingState.Stepping:
                        for (var steps = scoutJobInstance.steppingProgress; steps < scoutJobInstance.stepAmount; steps++)
                        {
                            if (!scoutJobInstance.ChunkManagerHasChunkAt(scoutJobInstance.pathingX, y, scoutJobInstance.pathingZ, out checkedPosition) &&
                               scoutJobInstance.IsOutsideMinimumRange(checkedPosition, scoutJobInstance.GetScoutBanner()))
                            {
                                foundChunk = true;
                                scoutJobInstance.steppingProgress++;

                                scoutJobInstance.PerformStep();

                                break;
                            }

                            scoutJobInstance.steppingProgress++;

                            scoutJobInstance.PerformStep();
                        }

                        scoutJobInstance.PathState = ScoutJobInstance.PathingState.Turning;
                        break;
                    case ScoutJobInstance.PathingState.Turning:
                        scoutJobInstance.TurnClockwise();
                        scoutJobInstance.StartMoving();
                        break;
                }

                if (!scoutJobInstance.IsOutsideMinimumRange(checkedPosition, scoutJobInstance.GetScoutBanner()))
                {
                    continue;
                }

                if (foundChunk)
                {
                    ScoutJobHelper.WriteLog(checkedPosition.ToString());
                    return true;
                }
            }

            output = Vector3Int.invalidPos;
            return false;
        }

        public void OnGoalChanged(BlockJobInstance instance, NPCBase.NPCGoal goalOld, NPCBase.NPCGoal goalNew)
        {
            // Nothing for now
        }

        public void OnNPCAtJob(BlockJobInstance instance, ref NPCBase.NPCState state)
        {
            ScoutJobInstance scoutJobInstance = (ScoutJobInstance)instance;
            state.SetCooldown(2);

            scoutJobInstance.getScoutChunkManager().RemoveDoublePositions();

            if (scoutJobInstance.Activity == ScoutJobGlobals.ScoutActivity.Scouting)
            {
                scoutJobInstance.getScoutChunkManager().RegisterPositionScouted(scoutJobInstance.currentDestination.ToChunk());
            }

            if (scoutJobInstance.Activity == ScoutJobGlobals.ScoutActivity.Walking)
            {
                scoutJobInstance.SetActivity(ScoutJobGlobals.ScoutActivity.Scouting);
                Vector3Int positionToScout = scoutJobInstance.Position;
                scoutJobInstance.getScoutChunkManager().RegisterPositionScouted(positionToScout);
                state.SetCooldown(2);
            }

            //WriteLog("OnNPCAtJob");

            if (!scoutJobInstance.StockedUp)
            {
                scoutJobInstance.StockedUp = true;
                scoutJobInstance.SetActivity(ScoutJobGlobals.ScoutActivity.Scouting);
            }

            var commenceBaseBuild = false;

            if (!scoutJobInstance.IsOutsideMinimumRange(scoutJobInstance.Position, scoutJobInstance.GetColony().Banners[0]))
            {
                return;
            }

            //Check the surrounding area and calculate its average flatness, to determine if it's suitable for a base
            var suitability = scoutJobInstance.calculateAreaSuitability();

            switch (suitability)
            {
                case ScoutJobInstance.Suitability.None:
                    break;
                case ScoutJobInstance.Suitability.Bad:
                    break;
                case ScoutJobInstance.Suitability.Decent:
                    break;
                case ScoutJobInstance.Suitability.Good:
                    ScoutJobHelper.WriteLog("Suitable location found for new base.");
                    commenceBaseBuild = true;
                    break;
                case ScoutJobInstance.Suitability.Excellent:
                    break;
                default:
                    Log.WriteWarning("Invalid Area Suitability received: {0}", suitability);
                    break;
            }

            //Only actually build base if the area is suitable enough
            // if (commenceBaseBuild) PrepareBase();
        }

        public void OnNPCAtStockpile(BlockJobInstance instance, ref NPCBase.NPCState state)
        {
            state.SetCooldown(0.5);
        }
    }
}
